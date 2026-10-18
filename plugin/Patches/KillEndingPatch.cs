using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HutongGames.PlayMaker;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The goal of the randomizer is beating the game. The game only writes its final hub progress once the credits have played,
/// but the story ends with a choice (Kill Ann, or spare her), and choosing Kill is the end of the fight: the goal is reported
/// as soon as the kill goes through. The choice is a PlayMaker FSM ("EndingInteracterFSM", under the ending's gameplay round)
/// that waits in the state "WaitForChoise" for the event "Kill" or "NotKill". Sparing Ann does not end the game, so nothing
/// else reports the goal.
/// </summary>
internal static class KillEnding
{
    private const string Fsm = "EndingInteracterFSM";
    private const string WaitingState = "WaitForChoise";
    private const string KillEvent = "Kill";

    /// <summary>True for the event that kills Ann: "Kill" sent to the ending's choice while it waits for the choice.</summary>
    internal static bool IsKill(string eventName, string fsmObject, string activeState) =>
        eventName == KillEvent && fsmObject == Fsm && activeState == WaitingState;
}

[HarmonyPatch]
internal static class KillEndingPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(Fsm), "ProcessEvent", new[] { typeof(FsmEvent), typeof(FsmEventData) });

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(Fsm __instance, FsmEvent fsmEvent)
    {
        // every PlayMaker event passes here: the cheap test comes first
        if (fsmEvent == null || fsmEvent.Name != "Kill" || !ApClient.Active) return;
        if (!KillEnding.IsKill(fsmEvent.Name, __instance.GameObjectName, __instance.ActiveStateName)) return;

        ApClient.CompleteGoal();
    }
}
