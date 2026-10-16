using System;
using HarmonyLib;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>
/// A reward that a location pays in full leaves the NPC's reward collection empty, so nothing reaches the player's reward
/// collection and the game's reward screen never opens. Whatever waits for the screen to close (the end of a trial, a
/// dialogue) then waits for good and the player is stuck. The screen's close event is sent instead.
/// </summary>
internal static class RewardWaiters
{
    private const string CloseEvent = "EVENT_REWARDMONITER_ON_CLOSE";

    /// <summary>Only rewards (not hand-overs) that were emptied have a screen that is waited for.</summary>
    internal static bool NeedsRelease(bool collectionIsReward) => collectionIsReward;

    /// <summary>Sent on the next frame, after the game has finished the call that started the reward.</summary>
    internal static void ReleaseNextFrame() => MainThread.Run(Release);

    private static void Release()
    {
        try
        {
            AccessTools.Method(AccessTools.TypeByName("HHSimpleInventoryMoniter"), "SetExternalClose")?.Invoke(null, new object[] { false });
            AccessTools.Method(AccessTools.TypeByName("Opsive.Shared.Events.EventHandler"), "ExecuteEvent", new[] { typeof(string) })
                ?.Invoke(null, new object[] { CloseEvent });
            Plugin.Log.LogInfo("A reward paid by a location has no reward screen: sent its close event");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not release what waits for the reward screen: " + e.Message);
        }
    }
}
