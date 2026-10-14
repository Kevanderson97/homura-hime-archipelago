using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Events;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Switches the game's tutorials after the first one off (see <see cref="TutorialSkip"/>). The story's tutorials are nodes of
/// its gameplay rounds (<c>Gameplay</c>) whose whole script is the tutorial: locking the player, showing pop-ups, waiting for
/// the menu steps. Such a node is finished the moment it starts, which is all the round needs to go on.
/// </summary>
[HarmonyPatch]
internal static class TutorialNodePatch
{
    private static readonly Dictionary<int, bool> Known = new();

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Gameplay"), "Use");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(MonoBehaviour __instance)
    {
        if (!ApClient.Active || !TutorialSkip.Applies(true, __instance.gameObject.scene.name) || !IsTutorialNode(__instance)) return true;

        try
        {
            var node = Traverse.Create(__instance);
            node.Field("OnUseEvent").GetValue<UnityEvent>()?.Invoke();
            node.Method("StartGameplay").GetValue();
            node.Method("Finish").GetValue();
            Plugin.Log.LogInfo($"Tutorial skipped: {__instance.name}");
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not skip the tutorial {__instance.name}: {e.Message}");
            return true;
        }
    }

    private static bool IsTutorialNode(MonoBehaviour node)
    {
        var id = node.GetInstanceID();
        if (Known.TryGetValue(id, out var known)) return known;

        var fsm = Traverse.Create(node).Field("fsm").GetValue<PlayMakerFSM>();
        var states = fsm?.Fsm?.States;
        if (states == null) return Known[id] = false;
        var names = states.Where(state => state.Actions != null).SelectMany(state => state.Actions)
            .Where(action => action != null).Select(action => action.GetType().Name).ToList();
        return Known[id] = names.Any(TutorialSkip.IsTutorialAction) && !names.Any(TutorialSkip.IsContentAction);
    }
}

/// <summary>
/// The tutorial pop-ups that are not part of a story node (the first-time pop-ups for shards, extra battles, ...): the script
/// action that shows the pop-up and waits for it to be closed ends at once.
/// </summary>
[HarmonyPatch]
internal static class TutorialPopupPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UseTutorialAction"), "OnEnter");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(FsmStateAction __instance)
    {
        if (!TutorialSkip.Applies()) return true;
        TutorialEvents.Fire(__instance, "endEvent");
        return false;
    }
}

/// <summary>
/// The guided steps of the equip tutorials wait for a menu panel to open or for an Omamori or gun to be equipped. Whatever they
/// wait for counts as done.
/// </summary>
[HarmonyPatch]
internal static class TutorialStepPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("MainMenuHandler_TutorialEventAction"), "OnEnter");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(FsmStateAction __instance)
    {
        if (!TutorialSkip.Applies()) return true;
        TutorialEvents.Fire(__instance, "triggerEvent");
        return false;
    }
}

/// <summary>The equip tutorials lock the pause menu and the input until the last step; that lock is not taken.</summary>
[HarmonyPatch]
internal static class TutorialInputLockPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("MainMenuAndDisplayPanelHandler"), "OmamoriTutorialInputLock");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(bool __0) => !(__0 && TutorialSkip.Applies());
}

/// <summary>Tutorials that stop the game until a button is pressed.</summary>
[HarmonyPatch]
internal static class PauseTutorialPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("PauseTutorial"), "Use");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix() => !TutorialSkip.Applies();
}

internal static class TutorialEvents
{
    /// <summary>Sends the script the event its action was waiting for (or ends the action when it has none).</summary>
    internal static void Fire(FsmStateAction action, string eventField)
    {
        var wanted = Traverse.Create(action).Field(eventField).GetValue<FsmEvent>();
        if (wanted == null) action.Finish();
        else action.Fsm.Event(wanted);
    }
}
