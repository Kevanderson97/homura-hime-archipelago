using HomuraHimeAP.Data;

namespace HomuraHimeAP.Core;

/// <summary>
/// The Chapter Select rules for the vanilla level order (mode a in the plan): a chapter is listed once the previous
/// chapter's boss is beaten, and inside a chapter a sub-chapter is listed once the previous sub-chapter is completed.
/// The Archipelago level-order and unlock-item modes extend this class later.
/// </summary>
internal static class ChapterAccess
{
    /// <summary>True when the whole game is cleared (post-game): everything is selectable.</summary>
    private static bool GameComplete => GameApi.HubProgress >= ChapterData.GameCompleteProgress;

    internal static bool IsChapterVisible(Chapter chapter)
    {
        if (WorldProgress.Active) return WorldProgress.IsAvailable(chapter);
        if (!Plugin.CfgLockFutureChapters.Value) return true;
        var progress = GameApi.HubProgress;
        if (progress < 0) return true; // unknown state: never hide things we cannot judge
        return GameComplete || progress >= chapter.RequiredProgress;
    }

    internal static bool IsChapterCleared(Chapter chapter)
    {
        if (WorldProgress.Active) return WorldProgress.IsCleared(chapter);
        var progress = GameApi.HubProgress;
        return GameComplete || (progress >= 0 && progress >= chapter.ClearedProgress);
    }

    internal static bool IsSubChapterVisible(Chapter chapter, int index)
    {
        if (!IsChapterVisible(chapter)) return false;
        if (!Plugin.CfgOrderedSubChapters.Value || index <= 0) return true;
        if (IsChapterCleared(chapter)) return true;
        return PlayerProgress.CompletedSubChapters.Contains(chapter.SubChapters[index - 1]);
    }

    /// <summary>True when a randomizer game does not allow entering the chapter this portal leads to (yet).</summary>
    internal static bool IsLockedDestination(string key)
    {
        if (!WorldProgress.Active || !ChapterData.TryGetSubChapter(key, out var chapter, out _)) return false;
        return !WorldProgress.IsAvailable(chapter);
    }

    /// <summary>
    /// Called for every scene change that goes through PortalManager.Teleport (hub departures, area transitions and the
    /// Chapter Select alike). Entering a named sub-chapter means the previous one has been left behind.
    /// </summary>
    internal static void OnPortalUsed(string key)
    {
        if (!ChapterData.TryGetSubChapter(key, out var chapter, out var index)) return;
        if (index > 0 && PlayerProgress.MarkSubChapterCompleted(chapter.SubChapters[index - 1]))
            Plugin.Log.LogInfo($"Sub-chapter completed: {chapter.SubChapters[index - 1]} (entered {key})");
    }

    /// <summary>Hub progress changed: an even value means a chapter was cleared, so its last sub-chapter is done.</summary>
    internal static void OnHubProgressWritten(int value)
    {
        if (value <= 0 || value % 2 != 0) return;
        foreach (var chapter in ChapterData.All)
        {
            if (chapter.ClearedProgress != value) continue;
            var last = chapter.SubChapters[chapter.SubChapters.Length - 1];
            if (PlayerProgress.MarkSubChapterCompleted(last))
                Plugin.Log.LogInfo($"Chapter cleared: {chapter.Name} (progress {value})");
        }
    }
}
