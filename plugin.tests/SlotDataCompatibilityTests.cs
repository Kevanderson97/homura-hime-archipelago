using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

/// <summary>
/// The slot data of real seeds, as the worlds 0.4.1 and 0.4.2 sent them (generated with the same options and seed: the worlds place
/// every item alike, 0.4.2 only adds the four currency scales for Universal Tracker). The plugin has to read both without a problem
/// or a warning.
/// </summary>
public class SlotDataCompatibilityTests
{
    // seed 2: full order, freeform level keys, Death Link
    private const string World041Freeform = """{"advanced_movement_required": false, "dango_count": 15, "death_link": true, "death_link_skip_revives": false, "goal": "all_worlds", "hexed_doll_trio_traps": 5, "include_cut_content": false, "level_order": ["Level2", "Level4", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3", "Level3", "Level5", "Level1", "LevelFinalP0"], "level_order_mode": "full", "level_unlock_items": "freeform_progression", "optional_locations": true, "puffer_swarm_traps": 20, "shop_hints": true, "world_version": "0.4.1"}""";
    private const string World042Freeform = """{"advanced_movement_required": false, "dango_count": 15, "death_link": true, "death_link_skip_revives": false, "goal": "all_worlds", "hexed_doll_trio_traps": 5, "include_cut_content": false, "level_order": ["Level2", "Level4", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3", "Level3", "Level5", "Level1", "LevelFinalP0"], "level_order_mode": "full", "level_unlock_items": "freeform_progression", "money_scale": 1.1241062308478038, "optional_locations": true, "petal_scale": 1.3823529411764706, "puffer_swarm_traps": 20, "senbei_scale": 1.8333333333333333, "senbei_unit": "2 Senbei", "shop_hints": true, "world_version": "0.4.2"}""";

    // seed 4: no puffer traps, 3 doll traps, Death Link with skip revives, the highest Konpeito target
    private const string World041Plain = """{"advanced_movement_required": false, "dango_count": 15, "death_link": true, "death_link_skip_revives": true, "goal": "all_worlds", "hexed_doll_trio_traps": 3, "include_cut_content": false, "level_order": ["Level1", "Level2", "Level3", "Level4", "Level5", "LevelFinalP0", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3"], "level_order_mode": "off", "level_unlock_items": "off", "optional_locations": true, "puffer_swarm_traps": 0, "shop_hints": true, "world_version": "0.4.1"}""";
    private const string World042Plain = """{"advanced_movement_required": false, "dango_count": 15, "death_link": true, "death_link_skip_revives": true, "goal": "all_worlds", "hexed_doll_trio_traps": 3, "include_cut_content": false, "level_order": ["Level1", "Level2", "Level3", "Level4", "Level5", "LevelFinalP0", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3"], "level_order_mode": "off", "level_unlock_items": "off", "money_scale": 1.0, "optional_locations": true, "petal_scale": 2.0, "puffer_swarm_traps": 0, "senbei_scale": 1.0, "senbei_unit": "2 Senbei", "shop_hints": true, "world_version": "0.4.2"}""";

    [Theory]
    [InlineData(World041Freeform, "0.4.1")]
    [InlineData(World042Freeform, "0.4.2")]
    public void ASeedWithFreeformKeysIsReadFromEitherWorld(string json, string version)
    {
        var slot = SlotData.FromJson(json);
        Assert.Null(slot.Problem);
        Assert.Empty(slot.Warnings);
        Assert.Equal(version, slot.WorldVersion);
        Assert.Equal(UnlockMode.Freeform, slot.UnlockMode);
        Assert.Equal(GoalMode.AllWorlds, slot.Goal);
        Assert.Equal(9, slot.LevelOrder.Count);
        Assert.True(slot.SoulCores);
        Assert.True(slot.DeathLink);
        Assert.False(slot.DeathLinkSkipRevives);
    }

    [Theory]
    [InlineData(World041Plain, "0.4.1")]
    [InlineData(World042Plain, "0.4.2")]
    public void ASeedWithoutKeysIsReadFromEitherWorld(string json, string version)
    {
        var slot = SlotData.FromJson(json);
        Assert.Null(slot.Problem);
        Assert.Empty(slot.Warnings);
        Assert.Equal(version, slot.WorldVersion);
        Assert.Equal(UnlockMode.Off, slot.UnlockMode);
        Assert.Equal(0, slot.PufferSwarmTraps);
        Assert.Equal(3, slot.HexedDollTrioTraps);
        Assert.True(slot.DeathLink);
        Assert.True(slot.DeathLinkSkipRevives);
    }

    [Fact]
    public void TheTwoWorldsSendTheSameThingsTheClientReads()
    {
        foreach (var (older, newer) in new[] { (World041Freeform, World042Freeform), (World041Plain, World042Plain) })
        {
            var a = SlotData.FromJson(older);
            var b = SlotData.FromJson(newer);
            Assert.Equal(a.LevelOrder, b.LevelOrder);
            Assert.Equal(a.LevelOrderMode, b.LevelOrderMode);
            Assert.Equal(a.UnlockMode, b.UnlockMode);
            Assert.Equal(a.Goal, b.Goal);
            Assert.Equal(a.DangoCount, b.DangoCount);
            Assert.Equal(a.PufferSwarmTraps, b.PufferSwarmTraps);
            Assert.Equal(a.HexedDollTrioTraps, b.HexedDollTrioTraps);
            Assert.Equal(a.IncludeCutContent, b.IncludeCutContent);
            Assert.Equal(a.OptionalLocations, b.OptionalLocations);
            Assert.Equal(a.AdvancedMovementRequired, b.AdvancedMovementRequired);
            Assert.Equal(a.DeathLink, b.DeathLink);
            Assert.Equal(a.ShopHints, b.ShopHints);
        }
    }
}
