using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using HomuraHimeAP.Data;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Core;

/// <summary>
/// Sends the hub's mission boards to the world the player is due to play next. A board starts the chapter the story expects at
/// that point; with another world order (or one the unlock items changed) that is the wrong world. Departures from the Chapter
/// Select are left alone: the player chose those.
/// </summary>
internal static class HubBoards
{
    private const string HubScene = "HubWorld_Main";

    /// <summary>The game's Chapter Select code; a teleport that has one of these on its call stack was chosen from the menu.</summary>
    private static readonly string[] ChapterSelectTypes = { "PortalDataItemAction", "ChapterPortalDataItemAction", "HHCharPortalOpener", "ChapterPortalSelectItemView" };

    /// <summary>
    /// For a teleport to <paramref name="key"/>: if it is a hub board's departure to the wrong chapter, starts the right one and
    /// returns true (the original teleport must then be skipped).
    /// </summary>
    internal static bool Redirect(string key, Action onLoad)
    {
        if (!WorldProgress.Active || key == null || SceneManager.GetActiveScene().name != HubScene) return false;
        if (!ChapterData.TryGetSubChapter(key, out var chapter, out var index) || index != 0) return false;
        if (FromChapterSelect()) return false;

        var target = WorldProgress.BoardTarget(chapter);
        if (target == null) return false;

        var portal = GameApi.FirstPortalOf(target);
        var manager = UnityEngine.Object.FindAnyObjectByType(GameApi.PortalManagerType);
        var teleport = AccessTools.Method(GameApi.PortalManagerType, "Teleport", new[] { GameApi.PortalDataType, typeof(Action) });
        if (portal == null || manager == null || teleport == null) return false;

        Plugin.Log.LogInfo($"Hub board for {chapter.Name} leads to {target} instead (next world in this seed)");
        teleport.Invoke(manager, new[] { portal, onLoad });
        return true;
    }

    private static bool FromChapterSelect()
    {
        try
        {
            return new StackTrace(false).GetFrames()?.Any(frame => ChapterSelectTypes.Contains(frame.GetMethod()?.DeclaringType?.Name)) == true;
        }
        catch (Exception)
        {
            return true; // when in doubt, leave the game''s own choice alone
        }
    }
}
