using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HomuraHimeAP.Core;

/// <summary>
/// Mihare Shrine exists twice: the hub of the story and the post-game one (after the credits, with Harumi's last rewards). The
/// game only offers the post-game hub in its Chapter Select once the game is beaten; with Chapter Select available from the
/// start the entry is there from the beginning, and it would take the player to the post-game hub whatever the progress.
/// Until the post-game is reached, that entry leads to the hub of the current progress instead.
/// </summary>
internal static class PostGameHub
{
    private const string PostGameKey = "HubWorld_PostGame_Main";

    /// <summary>The hub progress from which the game itself treats the post-game as open (the Char Travel threshold).</summary>
    internal const int PostGameProgress = 20;

    /// <summary>
    /// The player arrives in the story hub at the spot the post-game hub uses, which is inside the floor there (the main plane
    /// of the hub reaches 2.1 above the spot): the player stands below the plane until jumping. Measured at 2.1.
    /// </summary>
    internal const float ArrivalLift = 2.2f;

    /// <summary>True when a teleport to this portal is to the post-game hub before the post-game has been reached.</summary>
    internal static bool IsTooEarly(string key, int hubProgress) =>
        key == PostGameKey && hubProgress >= 0 && hubProgress < PostGameProgress;

    /// <summary>
    /// For a teleport to <paramref name="key"/>: when it is too early for the post-game hub, goes to the regular hub and
    /// returns true (the original teleport must then be skipped).
    /// </summary>
    internal static bool Redirect(string key, Action onLoad)
    {
        if (!IsTooEarly(key, GameApi.HubProgress)) return false;

        var portal = GameApi.FirstPortalOf("HubWorld"); // the first hub portal is the story's hub
        var manager = UnityEngine.Object.FindAnyObjectByType(GameApi.PortalManagerType);
        var teleport = AccessTools.Method(GameApi.PortalManagerType, "Teleport", new[] { GameApi.PortalDataType, typeof(Action) });
        if (portal == null || manager == null || teleport == null) return false;

        Plugin.Log.LogInfo("The post-game Mihare Shrine is not open yet; going to the Mihare Shrine of the story");
        teleport.Invoke(manager, new[] { portal, onLoad });
        LiftOnArrival();
        return true;
    }

    /// <summary>Lifts the player by <see cref="ArrivalLift"/> once the hub being loaded has placed the player and started.</summary>
    private static void LiftOnArrival()
    {
        _started = AccessTools.TypeByName("MainPipelineManager")?.GetEvent("OnMainPipelienStartGameplay");
        if (_started == null) return;

        // the game's event has a delegate type of its own (no arguments)
        _lift = Delegate.CreateDelegate(_started.EventHandlerType, typeof(PostGameHub).GetMethod(nameof(Lift), BindingFlags.Static | BindingFlags.NonPublic));
        _started.AddEventHandler(null, _lift);
    }

    private static EventInfo _started;
    private static Delegate _lift;

    private static void Lift()
    {
        _started.RemoveEventHandler(null, _lift);
        if (Items.PlayerApi.Position is { } at && Items.PlayerApi.MoveTo(at + Vector3.up * ArrivalLift))
            Plugin.Log.LogInfo($"Arrived in the hub below its floor; moved up to {at.y + ArrivalLift:0.00}");
    }
}
