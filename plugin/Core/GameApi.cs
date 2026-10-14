using System;
using System.Reflection;
using HarmonyLib;

namespace HomuraHimeAP.Core;

/// <summary>
/// Reflection access to the game's own types (Assembly-CSharp). Everything the plugin needs from the game goes through
/// here so that a changed name produces one clear log line instead of a build break or a crash.
/// </summary>
internal static class GameApi
{
    private static Func<int> _getHubProgress;
    private static Action<int> _setHubProgress;

    internal static Type HubProgressManagerType => AccessTools.TypeByName("Hub_GameplayRoundManager");
    internal static Type PortalManagerType => AccessTools.TypeByName("PortalManager");
    internal static Type PortalDataType => AccessTools.TypeByName("PortalData");

    private static Func<bool> _isHudHidden;

    /// <summary>
    /// True while the game has hidden its HUD (cutscenes, scripted scenes and the like). Without that game code (or when
    /// the call fails) the HUD counts as shown.
    /// </summary>
    internal static bool HudHidden
    {
        get
        {
            try
            {
                if (_isHudHidden == null)
                {
                    var method = AccessTools.Method(AccessTools.TypeByName("HUDManager"), "IsHide");
                    _isHudHidden = method == null ? () => false : (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), method);
                }

                return _isHudHidden();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("HUDManager.IsHide failed: " + e.Message);
                _isHudHidden = () => false;
                return false;
            }
        }
    }

    /// <summary>The hub progress variable (0-20; even = chapter cleared, odd = chapter entered).</summary>
    internal static int HubProgress
    {
        get
        {
            try
            {
                if (_getHubProgress == null)
                {
                    var m = AccessTools.Method(HubProgressManagerType, "GetDataValue");
                    if (m == null) return -1;
                    _getHubProgress = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), m);
                }

                return _getHubProgress();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("HubProgress read failed: " + e.Message);
                return -1;
            }
        }
    }

    /// <summary>Calls the game's SetDataValue directly. The progress guard is bypassed for this call.</summary>
    internal static void SetHubProgressUnchecked(int value)
    {
        var old = Patches.ProgressGuardPatch.Bypass;
        Patches.ProgressGuardPatch.Bypass = true;
        try
        {
            if (_setHubProgress == null)
            {
                var m = AccessTools.Method(HubProgressManagerType, "SetDataValue", new[] { typeof(int) });
                if (m == null) return;
                _setHubProgress = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), m);
            }

            _setHubProgress(value);
        }
        finally
        {
            Patches.ProgressGuardPatch.Bypass = old;
        }
    }

    /// <summary>
    /// Sends the player to the first scene of a portal category ("HubWorld", "Level1", ...), the same call the game uses
    /// to start a chapter. Returns false when the game does not offer it.
    /// </summary>
    internal static bool TeleportToCategory(string category)
    {
        try
        {
            var categoryType = AccessTools.Field(PortalDataType, "category")?.FieldType;
            var teleport = AccessTools.Method(PortalManagerType, "Teleport", new[] { categoryType, typeof(Action) });
            var manager = UnityEngine.Object.FindAnyObjectByType(PortalManagerType);
            if (teleport == null || manager == null) return false;
            teleport.Invoke(manager, new[] { Enum.Parse(categoryType, category), null });
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Teleport to {category} failed: {e.Message}");
            return false;
        }
    }

    /// <summary>The first area (portal) of a chapter, by its category name, or null when the game does not offer it.</summary>
    internal static object FirstPortalOf(string category)
    {
        try
        {
            var categoryType = AccessTools.Field(PortalDataType, "category")?.FieldType;
            var get = AccessTools.Method(PortalManagerType, "GetCategoryFirstPortalData", new[] { categoryType });
            var manager = UnityEngine.Object.FindAnyObjectByType(PortalManagerType);
            return get == null || manager == null ? null : get.Invoke(manager, new[] { Enum.Parse(categoryType, category) });
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"First portal of {category} not found: {e.Message}");
            return null;
        }
    }

    /// <summary>Builds the "&lt;Category&gt;_&lt;locationName&gt;" key (same as the CharPortal item definition name).</summary>
    internal static string PortalKey(object portalData)
    {
        if (portalData == null) return null;
        try
        {
            var t = portalData.GetType();
            var category = t.GetField("category", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(portalData);
            var location = t.GetField("locationName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(portalData);
            return category == null || location == null ? null : category + "_" + location;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("PortalKey failed: " + e.Message);
            return null;
        }
    }
}
