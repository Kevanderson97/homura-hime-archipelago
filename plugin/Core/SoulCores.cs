using System;
using System.Collections.Generic;

namespace HomuraHimeAP.Core;

/// <summary>
/// Chapters 6-8 fight the bosses of Levels 1-5 again, and the Soul Core of each boss (an item of the multiworld) is needed to
/// get to that fight: Ling Ling and the Corpse Princess, Nozomi, Yumiko &amp; Kumiko, Agasa and Mamiya. The way to a fight is
/// named by the key of the sub-chapter it leads to (the same keys the portals and the Chapter Select use, see ChapterData).
/// </summary>
internal static class SoulCores
{
    /// <summary>The Soul Core (item definition) and its name for each destination.</summary>
    private static readonly Dictionary<string, (string Definition, string Name)> NeededFor = new()
    {
        ["LevelFinalP1_L2_Boss"] = ("Codex_Important_Level2_SoulCore", "Ling Ling's Soul Core"),
        ["LevelFinalP2_L3_Boss"] = ("Codex_Important_Level3_SoulCore", "Chirakuin's Soul Core"),
        // the door at the end of Area 2 leads into the Ikai (the Dark Homura Hime fight), which moves on to the rematch by itself
        ["LevelFinalP2_L4_Ikai_Entrance"] = ("Codex_Important_Level4_SoulCore", "Yumiko & Kumiko's Soul Core"),
        ["LevelFinalP2_L4_Boss"] = ("Codex_Important_Level4_SoulCore", "Yumiko & Kumiko's Soul Core"),
        [AgasaFight] = ("Codex_Important_Level1_SoulCore", "Agasa's Soul Core"),
        ["LevelFinalP3_L5_Boss"] = ("Codex_Important_Level5_SoulCore", "Mamiya's Soul Core"),
    };

    /// <summary>Agasa is fought in the first area of Chapter 8, which the walk to the memory tree in the Ikai before it starts.</summary>
    internal const string AgasaFight = "LevelFinalP3_L5_A2_Entrance";

    /// <summary>The gameplay round of that walk: reaching the tree plays the dialogue and then moves the player to the fight.</summary>
    internal const string AgasaTreeRound = "GameplayRound_最後靠近記憶樹";

    /// <summary>The destinations (sub-chapter keys) that need a Soul Core.</summary>
    internal static IEnumerable<string> Destinations => NeededFor.Keys;

    internal static IEnumerable<string> Definitions
    {
        get
        {
            var seen = new HashSet<string>();
            foreach (var (definition, _) in NeededFor.Values)
                if (seen.Add(definition)) yield return definition;
        }
    }

    /// <summary>The Soul Core item definition the way to a fight needs (null when the destination is no boss fight of these).</summary>
    internal static string NeededForDestination(string portalKey) =>
        portalKey != null && NeededFor.TryGetValue(portalKey, out var needed) ? needed.Definition : null;

    /// <summary>The Soul Core the destination needs and the player does not have; null when the way is open.</summary>
    internal static string MissingFor(string portalKey, Func<string, bool> has)
    {
        var definition = NeededForDestination(portalKey);
        return definition != null && !has(definition) ? definition : null;
    }

    /// <summary>The name of a Soul Core (the name of its item in the multiworld).</summary>
    internal static string NameOf(string definition)
    {
        foreach (var (candidate, name) in NeededFor.Values)
            if (candidate == definition) return name;
        return definition;
    }

    internal static string Message(string definition) => NameOf(definition) + " required to continue";
}
