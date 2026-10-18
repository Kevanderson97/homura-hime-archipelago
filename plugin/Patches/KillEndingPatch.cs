using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
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

    /// <summary>
    /// The main menu offers the ending's choice again once a save has beaten the game (and Spare leads back to it), and starts it
    /// without loading any save, so the choice has to be matched with a save of the connected game that is itself at the ending.
    /// That is the save played in this session, which is still the case after the return to the title screen, or else one of the
    /// save files made for the connected slot and seed. A save of another seed, or a save of this game that is still early in the
    /// story, never completes the goal, whatever other saves have done.
    /// </summary>
    internal static bool CountsForGoal(bool playedSaveIsAtFinale, Func<bool> aSaveFileIsAtFinale) =>
        playedSaveIsAtFinale || aSaveFileIsAtFinale();

    /// <summary>The location paid by clearing the second phase of the final boss, the fight that leads to the choice.</summary>
    internal const string FinalBossLocation = "encounter/Level_FinalP4_Jinguu_Phase2_Main/main";

    /// <summary>
    /// The server is the one record of a seed that no save file can fake: it has to hold the clear of the final boss. A seed that
    /// switched the encounter clears off has no such location (<paramref name="finalBoss"/> is then null) and has nothing to compare.
    /// </summary>
    internal static bool ServerHasFinalBoss(long? finalBoss, ICollection<long> checkedOnServer) =>
        finalBoss == null || (checkedOnServer != null && checkedOnServer.Contains(finalBoss.Value));
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

        var state = ApClient.State;
        var worlds = WorldProgress.WorldCount;
        var played = SaveBinding.CurrentSaveSlot.HasValue && state.CapturePlaythrough().ReachedFinale(worlds);
        if (!KillEnding.CountsForGoal(played, () => SaveFiles.AnySaveReachedFinale(state.Slot, state.Seed, worlds)))
        {
            Plugin.Log.LogInfo($"The goal was not reported: no save of this game (slot {state.Slot}, seed {state.Seed}) has reached the ending");
            UI.Toasts.Show("Goal not reported: none of your saves of this Archipelago game has reached the ending", UI.Toasts.Error);
            return;
        }

        // the server's own list of checked locations, or the stored copy of it while offline
        ICollection<long> onServer = ApClient.IsConnected ? ApClient.Session.Locations.AllLocationsChecked : state.CheckedLocations;
        if (!KillEnding.ServerHasFinalBoss(Catalog.Location(KillEnding.FinalBossLocation)?.Id, onServer))
        {
            Plugin.Log.LogInfo("The goal was not reported: the server has no clear of the final boss for this game");
            UI.Toasts.Show("Goal not reported: the final boss has not been cleared in this Archipelago game", UI.Toasts.Error);
            return;
        }

        ApClient.CompleteGoal();
    }
}
