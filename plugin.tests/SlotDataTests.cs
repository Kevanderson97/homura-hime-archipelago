using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Archipelago;
using Newtonsoft.Json.Linq;
using Xunit;

namespace HomuraHimeAP.Tests;

public class SlotDataTests
{
    public SlotDataTests() => TestEnvironment.Ensure();

    private static readonly string[] Shuffled = HomuraHimeAP.Core.WorldRules.VanillaOrder.Reverse().ToArray();

    private static Dictionary<string, object> Raw(string version = "0.4.0") => new() { ["world_version"] = version };

    [Fact]
    public void ParsesWhatTheWorldSends()
    {
        var raw = new Dictionary<string, object>
        {
            ["world_version"] = "0.4.1",
            ["level_order"] = JArray.FromObject(Shuffled),
            ["level_order_mode"] = "randomized",
            ["level_unlock_items"] = "linear_progression",
            ["dango_count"] = 10L,
            ["puffer_swarm_traps"] = 7L,
            ["hexed_doll_trio_traps"] = 3L,
            ["include_cut_content"] = false,
            ["encounter_rewards"] = 1L,
            ["advanced_movement_required"] = true,
            ["death_link"] = true,
            ["death_link_skip_revives"] = false,
        };

        var data = SlotData.Parse(raw);

        Assert.Equal("0.4.1", data.WorldVersion);
        Assert.Equal(Shuffled, data.LevelOrder);
        Assert.True(data.IsRandomized);
        Assert.True(data.LevelUnlockItems);
        Assert.Equal(HomuraHimeAP.Core.UnlockMode.Linear, data.UnlockMode);
        Assert.Equal(10, data.DangoCount);
        Assert.Equal(7, data.PufferSwarmTraps);
        Assert.Equal(3, data.HexedDollTrioTraps);
        Assert.False(data.IncludeCutContent);
        Assert.True(data.EncounterRewards);
        Assert.True(data.AdvancedMovementRequired);
        Assert.True(data.DeathLink);
        Assert.False(data.DeathLinkSkipRevives);
    }

    [Fact]
    public void MissingValuesFallBackToTheVanillaSetup()
    {
        var data = SlotData.Parse(Raw());
        Assert.Empty(data.LevelOrder);
        Assert.Equal("vanilla", data.LevelOrderMode);
        Assert.False(data.IsRandomized);
        Assert.False(data.AdvancedMovementRequired);
        Assert.False(data.LevelUnlockItems);
        Assert.False(data.DeathLink);
    }

    [Fact]
    public void NullSlotDataIsTolerated()
    {
        Assert.Empty(SlotData.Parse(null).LevelOrder);
    }

    [Theory]
    [InlineData("0.4.0", true)]
    [InlineData("0.4.3", true)]
    [InlineData("0.4.7", true)]
    [InlineData("0.2.1", false)] // no Konpeito items in the pool yet
    [InlineData("0.1.0", false)]
    [InlineData("0.3.4", false)] // eight worlds in the order, the climax was part of Chapter 6
    [InlineData("0.0.9", false)]
    [InlineData("0.5.0", false)]
    [InlineData("1.0.0", false)]
    [InlineData("garbage", false)]
    public void OnlySupportedWorldVersionsCanBePlayed(string version, bool supported)
    {
        var data = SlotData.Parse(Raw(version));
        Assert.Equal(supported, data.Problem == null);
        if (!supported) Assert.False(string.IsNullOrEmpty(data.Problem));
    }

    [Fact]
    public void ASeedWithoutAVersionOrWithoutSlotDataIsRefused()
    {
        Assert.NotNull(SlotData.Parse(new Dictionary<string, object>()).Problem);
        Assert.NotNull(SlotData.Parse(null).Problem);
    }

    [Fact]
    public void ALevelOrderThatIsNotTheNineWorldsFallsBackToTheStoryOrder()
    {
        foreach (var order in new[] { new[] { "Level1", "Level2" }, Shuffled.Take(8).Append(Shuffled[0]).ToArray(), Shuffled.Take(8).Append("Nowhere").ToArray() })
        {
            var raw = Raw();
            raw["level_order"] = JArray.FromObject(order);
            raw["level_order_mode"] = "randomized";

            var data = SlotData.Parse(raw);

            Assert.Empty(data.LevelOrder);
            Assert.False(data.IsRandomized);
            Assert.Single(data.Warnings);
        }
    }

    [Fact]
    public void OddValuesGetDefaultsAndAWarningInsteadOfAnException()
    {
        var raw = Raw();
        raw["dango_count"] = "many";
        raw["death_link"] = new[] { 1, 2 };

        var data = SlotData.Parse(raw);

        Assert.Equal(0, data.DangoCount);
        Assert.False(data.DeathLink);
        Assert.Equal(2, data.Warnings.Count);
    }

    [Theory]
    [InlineData("off", "Off", 0)]
    [InlineData("linear_progression", "Linear", 0)]
    [InlineData("freeform_progression", "Freeform", 0)]
    [InlineData("sideways", "Off", 1)] // unknown: off, with a warning
    public void TheLevelKeyModeIsReadFromTheSlotData(string value, string expected, int warnings)
    {
        var raw = Raw();
        raw["level_unlock_items"] = value;
        var data = SlotData.Parse(raw);
        Assert.Equal(expected, data.UnlockMode.ToString());
        Assert.Equal(warnings, data.Warnings.Count);
    }

    [Theory]
    [InlineData("off", false)]
    [InlineData("vanilla", false)]
    [InlineData("balanced", true)]
    [InlineData("full", true)]
    [InlineData("randomized", true)]
    public void TheOrderModeTellsWhetherTheWorldsAreShuffled(string mode, bool shuffled)
    {
        var raw = Raw();
        raw["level_order_mode"] = mode;
        Assert.Equal(shuffled, SlotData.Parse(raw).IsRandomized);
    }

    [Fact]
    public void ShopHintsAreOffForWorldsThatDoNotSendTheOption()
    {
        Assert.False(SlotData.Parse(Raw()).ShopHints);
        var raw = Raw();
        raw["shop_hints"] = true;
        Assert.True(SlotData.Parse(raw).ShopHints);
    }
}
