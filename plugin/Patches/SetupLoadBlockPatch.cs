using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Items;
using HomuraHimeAP.UI;

namespace HomuraHimeAP.Patches;

/// <summary>
/// While the trap setup is running (the screen is black and the plugin loads and unloads scenes), nothing that starts a game or
/// loads a save may run, whatever input reaches it: a scene change in the middle of the setup would break it. The menu's input
/// is switched off for the duration as well (the event system), but the game also reads its own input in several
/// <c>Update</c> methods, so the entry points are blocked here: the slot buttons of the save panel (load, new game, difficulty
/// choice) and the start screen's direct starts. The first moments after connecting, before the screen has faded, count too.
/// Leaving the game, the options and closing panels stay possible.
/// </summary>
[HarmonyPatch]
internal static class SetupLoadBlockPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var panel = AccessTools.TypeByName("SavePanelHandler");
        var start = AccessTools.TypeByName("StartScreenManager");
        var targets = new (Type Type, string Name, Type[] Args)[]
        {
            (panel, "Continue", new[] { typeof(int) }),
            (panel, "NewGame", new[] { typeof(int) }),
            (panel, "StartNewGameWithDifficulty", new[] { typeof(int) }),
            (start, "NewGame", Type.EmptyTypes),
            (start, "NewGameDirect", Type.EmptyTypes),
            (start, "ContinueDirect", Type.EmptyTypes),
            (start, "StartLevel", new[] { typeof(int) }),
        };

        foreach (var (type, name, args) in targets)
            // only plain void methods: skipping the original of one that returns something (a coroutine) would hand its caller null
            if (type != null && AccessTools.Method(type, name, args) is { } method && method.ReturnType == typeof(void)) yield return method;
    }

    private static bool Prefix()
    {
        if (!TrapSpawner.BlocksLoading) return true;

        Plugin.Log.LogInfo("A save load or new game was blocked while the trap setup is pending or running");
        if (!SetupOverlay.Visible) MenuNotice.Show("Preparing AP World Settings, please try again in a moment");
        return false;
    }
}
