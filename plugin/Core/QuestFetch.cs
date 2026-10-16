using System.Collections.Generic;

namespace HomuraHimeAP.Core;

/// <summary>
/// The story's item quests (an NPC asks for something, the player fetches it, the NPC takes it) wait for the fetch to happen at
/// its original place, even when the player already owns the item because the multiworld sent it. In a randomizer game the
/// fetch step is finished at once when the item is owned (see QuestFetchPatch); the locations that step would have paid out
/// are reported as it finishes.
/// </summary>
internal static class QuestFetch
{
    internal sealed class Step
    {
        internal string Scene;
        /// <summary>The name of the story node (<c>Gameplay</c>) that waits for the fetch.</summary>
        internal string Node;
        /// <summary>The game item the quest asks for.</summary>
        internal string Item;
        /// <summary>The locations the step pays out: the hand-over of the item and the fight that carries it, if any.</summary>
        internal string[] Locations;
    }

    private static readonly Step[] Steps =
    {
        // Level 2 Area 1: the infantry NPC wants the lever of the elevator, which the Murmur-flower event hands over
        new() { Scene = "Level_2_Area_1_Main", Node = "Gameplay_等待獲取任務道具", Item = "Codex_Important_Level2_Launcher",
                Locations = new[] { "handover/launcher" } },
        // Level 2 Area 4: Nurikabe wants his shoe, the reward of battle B04
        new() { Scene = "Level_2_Area_4_Main", Node = "Gameplay_B04", Item = "Codex_Important_Level2_Boot",
                Locations = new[] { "handover/boot", "encounter/Level_2_Area_4_Main/B04" } },
        // Level 3 Area 5: Nurikabe wants his hat, the reward of battle B05
        new() { Scene = "Level_3_Area_5_Main", Node = "Gameplay_B05", Item = "Codex_Important_Level3_Nurikabe'sHat",
                Locations = new[] { "handover/hat", "encounter/Level_3_Area_5_Main/B05" } },
    };

    /// <summary>The fetch step that is this story node, if it is one.</summary>
    internal static Step Find(string scene, string node)
    {
        foreach (var step in Steps)
            if (step.Scene == scene && step.Node == node) return step;
        return null;
    }

    /// <summary>The step is finished at once when the randomizer is active and the player owns what it asks for.</summary>
    internal static bool ShouldSkip(bool randomizerActive, bool ownsItem) => randomizerActive && ownsItem;

    internal static IEnumerable<Step> All => Steps;
}
