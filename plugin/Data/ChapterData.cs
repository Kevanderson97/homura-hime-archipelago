using System.Collections.Generic;

namespace HomuraHimeAP.Data;

/// <summary>
/// One chapter of the Chapter Select (see data/chapter_select.md). Sub-chapters are the named portal entries, in story
/// order, identified by the item-definition name used in the player's CharPortal collection
/// ("&lt;Category&gt;_&lt;locationName&gt;", e.g. "Level1_Area_1_Entrance").
/// </summary>
public sealed class Chapter
{
    /// <summary>Internal category, e.g. "Level1", "LevelFinalP2".</summary>
    public readonly string Id;

    /// <summary>1-5 and 6-8 for the eight chapters, 0 for the Climax prelude, 9 for the finale.</summary>
    public readonly int Number;

    public readonly string Name;

    /// <summary>Hub progress value at which this chapter is available in the vanilla story order.</summary>
    public readonly int RequiredProgress;

    /// <summary>Sub-chapter keys in story order. The last one is the boss (or ending) sub-chapter.</summary>
    public readonly string[] SubChapters;

    public string HeaderItem => "PortalChapter_" + Id;

    /// <summary>Hub progress value that means "this chapter has been cleared" in the vanilla order.</summary>
    public int ClearedProgress => RequiredProgress + 2;

    public Chapter(string id, int number, string name, int requiredProgress, params string[] subChapters)
    {
        Id = id;
        Number = number;
        Name = name;
        RequiredProgress = requiredProgress;
        SubChapters = subChapters;
    }
}

public static class ChapterData
{
    public static readonly Chapter[] All =
    {
        new("Level1", 1, "Chapter 1: The Skeleton Girl", 0,
            "Level1_Area_1_Entrance", "Level1_Ikai_Part1_Entrance", "Level1_BossRoom_Pass1"),
        new("Level2", 2, "Chapter 2: The Flower and Corpse Princess", 2,
            "Level2_Area_1_Entrance", "Level2_Area_3_Entrance", "Level2_Area_5_Entrance", "Level2_Area_6_Entrance",
            "Level2_BossRoom"),
        new("Level3", 3, "Chapter 3: The Holy Maiden of the Full Moon Order", 4,
            "Level3_Area_1_Entrance", "Level3_Area_3_Entrance", "Level3_Area_5_Entrance", "Level3_Ikai_Part2_Entrance",
            "Level3_BossRoom"),
        // Order follows the game's own portal list (Ikai Part 1 sits between Area 2 and Area 3). Verify with a playthrough.
        new("Level4", 4, "Chapter 4: The Twin Watchdogs", 6,
            "Level4_Area_1_Entrance", "Level4_Ikai_Part1_Entrance", "Level4_Area_3_Entrance", "Level4_Area_4_Entrance",
            "Level4_Ikai_Part2_Entrance", "Level4_BossRoom"),
        new("Level5", 5, "Chapter 5: The Fallen Samurai", 8,
            "Level5_Area_1_Entrance", "Level5_Area_4_Entrance", "Level5_Area_5_Entrance", "Level5_BossRoom"),
        new("LevelFinalP0", 0, "Climax: The Choice", 10,
            "LevelFinalP0_Jinguu"),
        new("LevelFinalP1", 6, "Chapter 6: Where it All Began", 12,
            "LevelFinalP1_L1_Ikai_Entrance", "LevelFinalP1_L2_A5_Entrance", "LevelFinalP1_L2_Boss"),
        new("LevelFinalP2", 7, "Chapter 7: The Brambled Path of Rebellion", 14,
            "LevelFinalP2_L3_A5_Entrance", "LevelFinalP2_L3_Boss", "LevelFinalP2_L4_A2_Entrance",
            "LevelFinalP2_L4_Boss"),
        new("LevelFinalP3", 8, "Chapter 8: The Future of Reunion", 16,
            "LevelFinalP3_L5_Ikai_Entrance", "LevelFinalP3_L5_A2_Entrance", "LevelFinalP3_L5_A5_Entrance",
            "LevelFinalP3_L5_Boss"),
        new("LevelFinalP4", 9, "Finale: Desire", 18,
            "LevelFinalP4_Passage_Entrance", "LevelFinalP4_Jinguu_Phase1", "LevelFinalP4_Jinguu_Ending"),
    };

    /// <summary>Hub progress value of the finished game (post-game hub).</summary>
    public const int GameCompleteProgress = 20;

    private static readonly Dictionary<string, (Chapter chapter, int index)> BySubChapter = new();
    private static readonly Dictionary<string, Chapter> ByHeader = new();
    private static readonly Dictionary<string, Chapter> ById = new();

    static ChapterData()
    {
        foreach (var chapter in All)
        {
            ByHeader[chapter.HeaderItem] = chapter;
            ById[chapter.Id] = chapter;
            for (var i = 0; i < chapter.SubChapters.Length; i++)
                BySubChapter[chapter.SubChapters[i]] = (chapter, i);
        }
    }

    public static bool TryGetSubChapter(string key, out Chapter chapter, out int index)
    {
        if (key != null && BySubChapter.TryGetValue(key, out var hit))
        {
            chapter = hit.chapter;
            index = hit.index;
            return true;
        }

        chapter = null;
        index = -1;
        return false;
    }

    public static bool TryGetHeader(string itemName, out Chapter chapter)
    {
        chapter = null;
        return itemName != null && ByHeader.TryGetValue(itemName, out chapter);
    }

    public static bool TryGetChapter(string id, out Chapter chapter)
    {
        chapter = null;
        return id != null && ById.TryGetValue(id, out chapter);
    }
}
