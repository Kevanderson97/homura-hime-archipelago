namespace HomuraHimeAP.Core;

/// <summary>
/// The hub shows the post-mission report and pays the clear reward of the chapter that its story progress points at. The plugin
/// stores the hub progress as "number of cleared worlds" (see <see cref="WorldRules"/>), so after clearing Chapter 3 first
/// the hub asks for the report of Chapter 1. The report and the reward are shown for the world that was just cleared instead.
/// </summary>
internal static class StageReport
{
    /// <summary>Stage categories that are not a chapter (the tutorial, the hub and the like): never replaced.</summary>
    internal static bool IsChapter(string category) =>
        category != null && category.StartsWith("Level") && category != "Level0";

    /// <summary>
    /// The category to show instead of <paramref name="shown"/> (what the hub asked for), or null to leave it. Nothing is
    /// replaced without a world that was just cleared, or for a report that is not a chapter's.
    /// </summary>
    internal static string Replacement(string shown, string justCleared) =>
        IsChapter(shown) && IsChapter(justCleared) && shown != justCleared ? justCleared : null;
}
