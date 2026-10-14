using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HomuraHimeAP.Core;
using UnityEngine;

namespace HomuraHimeAP;

/// <summary>
/// The BepInEx entry point: binds the config, loads the data catalog, applies every Harmony patch and runs the per-frame
/// work of the plugin. The rest of the plugin lives in the Archipelago (network), Core (state), Items, Patches (game
/// hooks) and UI folders; see plugin/README.md.
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.homurahime.archipelago";
    public const string PluginName = "Homura-Hime Archipelago";
    public const string PluginVersion = "0.3.2";

    /// <summary>The plugin's log source (BepInEx/LogOutput.log); set in <see cref="Awake"/>.</summary>
    internal static ManualLogSource Log;

    // Config entries (BepInEx/config/com.homurahime.archipelago.cfg), bound in Awake.
    internal static ConfigEntry<bool> CfgMonotonicProgress;
    internal static ConfigEntry<bool> CfgChapterSelectFromStart;
    internal static ConfigEntry<bool> CfgLockFutureChapters;
    internal static ConfigEntry<bool> CfgOrderedSubChapters;
    internal static ConfigEntry<bool> CfgVerbose;
    internal static ConfigEntry<bool> CfgShowLabel;
    internal static ConfigEntry<string> CfgHost, CfgSlot, CfgPassword;
    internal static ConfigEntry<bool> CfgAutoConnect, CfgRememberPassword;
    internal static ConfigEntry<UI.ToastVerbosity> CfgItemNotifications;
    internal static ConfigEntry<bool> CfgShopHints, CfgTextAudit;
    internal static ConfigEntry<string> CfgDisabledPatches;

    private Harmony _harmony;

    /// <summary>Runs once when BepInEx loads the plugin: config, data, patches, optional auto-connect.</summary>
    private void Awake()
    {
        Log = Logger;

        CfgMonotonicProgress = Config.Bind("Progress", "MonotonicProgress", true,
            "Hub progress can only increase, so replaying earlier chapters never re-locks shop tiers or hub objects.");
        CfgChapterSelectFromStart = Config.Bind("ChapterSelect", "AvailableFromStart", true,
            "Make Chapter Select (pause menu > Char Travel) available from the very beginning.");
        CfgLockFutureChapters = Config.Bind("ChapterSelect", "HideFutureChapters", true,
            "Hide chapters whose previous chapter has not been cleared yet.");
        CfgOrderedSubChapters = Config.Bind("ChapterSelect", "OrderedSubChapters", true,
            "Inside a chapter, list a sub-chapter only after the previous sub-chapter is completed.");
        CfgVerbose = Config.Bind("Debug", "VerboseLog", false, "Log teleports and ignored progress writes.");
        CfgShowLabel = Config.Bind("Debug", "ShowVersionLabel", true, "Show a small version label on screen.");

        CfgHost = Config.Bind("Archipelago", "Server", "archipelago.gg:38281", "Server address (host:port).");
        CfgSlot = Config.Bind("Archipelago", "Slot", "", "Your slot (player) name.");
        CfgPassword = Config.Bind("Archipelago", "Password", "", "Room password, if any.");
        CfgRememberPassword = Config.Bind("Archipelago", "RememberPassword", false,
            "Keep the room password in this config file (as plain text). Off: it is asked for again each session.");
        CfgAutoConnect = Config.Bind("Archipelago", "AutoConnect", false, "Connect automatically when the game starts.");

        CfgTextAudit = Config.Bind("Debug", "TextAudit", true,
            "Once, write journal_names.tsv and text_audit.txt (texts the game lacks) next to the session files.");
        CfgShopHints = Config.Bind("Archipelago", "ShopHints", true,
            "Send a hint for every Archipelago item of another player that appears in the shop (needs the world option shop_hints; this setting can only turn it off).");
        CfgItemNotifications = Config.Bind("Notifications", "ItemNotifications", UI.ToastVerbosity.All,
            "Which item notifications are shown: All, Important (progression, useful, traps and items from other players) or Off.");
        UI.Toasts.Verbosity = CfgItemNotifications.Value;
        CfgItemNotifications.SettingChanged += (_, _) => UI.Toasts.Verbosity = CfgItemNotifications.Value;

        CfgDisabledPatches = Config.Bind("Debug", "DisabledPatches", "",
            "Comma-separated patch class names to skip (for troubleshooting), or ALL. Example: ProgressGuardPatch,TeleportPatch. Use NOTRACKER to skip the tutorial tracker.");

        // Order matters: the catalog must be loaded before anything that looks items or locations up, and the patches
        // come last so no hook can fire before the state it reads exists.
        Catalog.Load();
        SceneClock.Init();
        if (!IsDisabled("NOTRACKER")) TutorialTracker.Init();
        WorldProgress.Init();
        SaveState.Load();
        ApplyPatches();
        if (!Patches.SaveSlots.Subscribe()) Log.LogWarning("Save events not found: saves are not bound to Archipelago slots");
        if (CfgAutoConnect.Value && CfgSlot.Value != "")
            Archipelago.ApClient.Connect(CfgHost.Value, CfgSlot.Value, CfgPassword.Value);
        Log.LogInfo($"{PluginName} {PluginVersion} loaded");
    }

    /// <summary>
    /// Each patch class is applied on its own, so one broken target (for example after a game update) only disables
    /// that feature and is reported in the log.
    /// </summary>
    private void ApplyPatches()
    {
        _harmony = new Harmony(PluginGuid);
        foreach (var type in typeof(Plugin).Assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
            if (IsDisabled(type.Name)) { Log.LogWarning("Patch disabled by config: " + type.Name); continue; }
            try
            {
                new PatchClassProcessor(_harmony, type).Patch();
                Log.LogInfo("Patched: " + type.Name);
            }
            catch (Exception e)
            {
                Log.LogError($"Patch {type.Name} failed: {e.Message}");
            }
        }
    }

    /// <summary>Whether the DisabledPatches config entry turns off the named patch class (or everything with "ALL").</summary>
    private static bool IsDisabled(string name)
    {
        var list = CfgDisabledPatches.Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in list)
            if (entry.Equals(name, StringComparison.OrdinalIgnoreCase) || (entry.Equals("ALL", StringComparison.OrdinalIgnoreCase) && name != "NOTRACKER"))
                return true;
        return false;
    }

    /// <summary>
    /// Per-frame work. Every job runs inside <see cref="TickGuard"/>, so an exception in one job is logged (rate limited)
    /// instead of stopping the others or the game.
    /// </summary>
    private void Update()
    {
        var now = Time.unscaledTime;
        // method groups and lambdas without captures are cached by the compiler: nothing is allocated per frame
        TickGuard.Run("main-thread queue", now, MainThread.Drain);
        TickGuard.Run("connection panel", now, () => { if (Input.GetKeyDown(KeyCode.F10)) UI.ConnectionPanel.Toggle(); });
        TickGuard.Run("item granter", now, Items.ItemGranter.Tick);
        TickGuard.Run("session saving", now, SessionState.Tick);
        TickGuard.Run("save record", now, ApSaver.Tick);
        TickGuard.Run("text audit", now, TextAudit.Tick);
        TickGuard.Run("text overrides", now, CustomTexts.ApplyOverrides);
        TickGuard.Run("skip tutorial button", now, SkipTutorialOption.Tick);
        TickGuard.Run("shop hints", now, Archipelago.HintSender.Tick);
        TickGuard.Run("notifications", now, UI.Toasts.Update);
        TickGuard.Run("deathlink", now, Archipelago.DeathLinkHandler.Tick);
    }

    /// <summary>Runs after the game's own Update, where the connection form can safely read the keyboard.</summary>
    private void LateUpdate() => UI.ConnectionPanel.Update();

    /// <summary>Draws the version label, the connection form and the item notifications (IMGUI, on top of the game).</summary>
    private void OnGUI()
    {
        if (CfgShowLabel.Value) GUI.Label(new Rect(8, 4, 360, 20), $"{PluginName} v{PluginVersion}");
        UI.ConnectionPanel.Draw();
        UI.Toasts.Draw();
    }

    /// <summary>Writes any session data still waiting for its deferred save.</summary>
    private void OnApplicationQuit() => SessionState.FlushAll();

    /// <summary>Closes the Archipelago connection and removes the Harmony patches.</summary>
    private void OnDestroy()
    {
        Archipelago.ApClient.Disconnect();
        _harmony?.UnpatchSelf();
    }
}
