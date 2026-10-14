using System;
using HarmonyLib;

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
        return true;
    }
}
