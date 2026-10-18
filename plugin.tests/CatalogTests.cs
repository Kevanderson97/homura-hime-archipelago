using System.Linq;
using HomuraHimeAP.Core;
using UnityEngine;
using Xunit;

namespace HomuraHimeAP.Tests;

public class CatalogTests : IClassFixture<CatalogTests.Fixture>
{
    public sealed class Fixture
    {
        public Fixture() => TestEnvironment.Ensure();
    }

    [Fact]
    public void ItemsAreFoundByNameAndId()
    {
        var item = Catalog.Item("Skill Phantom Blade");
        Assert.NotNull(item);
        Assert.Equal("Skill_Blade Warp", item.Game);
        Assert.Same(item, Catalog.Item(item.Id));
        Assert.Null(Catalog.Item("Phantom Blade"));
    }

    [Theory]
    [InlineData("500 Konpeito", 500)]
    [InlineData("1500 Konpeito", 1500)]
    [InlineData("20 Sakura Petals", 20)]
    [InlineData("Senbei", 1)]
    public void CurrencyAmountComesFromTheName(string name, int amount) => Assert.Equal(amount, Catalog.Item(name).Amount);

    [Fact]
    public void EveryItemTypeIsOneTheGranterKnows()
    {
        var known = new[]
        {
            "key", "ability", "dash_skill", "skill", "combo", "omamori", "gun", "upgrade", "shard", "crystal",
            "consumable", "currency", "utility", "unlock",
        };
        foreach (var type in AllItems().Select(i => i.Type).Distinct()) Assert.Contains(type, known);
    }

    [Fact]
    public void ItemAndLocationNamesAndIdsAreUnique()
    {
        var items = AllItems().ToList();
        Assert.Equal(items.Count, items.Select(i => i.Name).Distinct().Count());
        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());

        var locations = AllLocations().ToList();
        Assert.Equal(locations.Count, locations.Select(l => l.Name).Distinct().Count());
        Assert.Equal(locations.Count, locations.Select(l => l.Key).Distinct().Count());
        Assert.Equal(locations.Count, locations.Select(l => l.Id).Distinct().Count());
    }

    [Fact]
    public void UnlockItemsExistForEveryWorld()
    {
        var worlds = new[] { "Level1", "Level2", "Level3", "Level4", "Level5", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" };
        foreach (var world in worlds)
            Assert.Contains(AllItems(), i => i.Type == "unlock" && i.Game == world);
    }

    // ---------------------------------------------------------------- locations

    [Fact]
    public void ChestIsFoundByPositionWithinToleranceAndSceneOnly()
    {
        var chest = Catalog.Location("chest/Level_1_Area_1_Main/1");
        Assert.NotNull(chest);
        var position = new Vector3(chest.Pos[0] + 1f, chest.Pos[1], chest.Pos[2]);

        Assert.Same(chest, Catalog.Nearest("Level_1_Area_1_Main", position, 2f, "chest"));
        Assert.Null(Catalog.Nearest("Level_1_Area_1_Main", position, 0.5f, "chest"));
        Assert.Null(Catalog.Nearest("Level_1_Area_2_Main", position, 2f, "chest"));
        Assert.Null(Catalog.Nearest("Level_1_Area_1_Main", position, 2f, "senbei"));
    }

    [Fact]
    public void RewardsAreFoundByTheItemTheyNormallyGive()
    {
        Assert.Equal("handover/flower", Catalog.ByVanillaItem("Level_1_Area_1_Main", "Codex_Important_Level1_Flower").Key);
        Assert.Equal("handover/flower", Catalog.ByVanillaItem("SomewhereElse", "Codex_Important_Level1_Flower").Key);
        Assert.Equal("handover/boot", Catalog.ByVanillaItem("Level_2_Area_4_Main", "Codex_Important_Level2_Boot").Key);
        // Special Hanami Dango is the reward of many locations: without the right scene it is ambiguous
        Assert.Null(Catalog.ByVanillaItem("HubWorld_Main", "Kushinada_Dango"));
    }

    [Fact]
    public void AnyFestivalStallItemIsTheSameLocation()
    {
        foreach (var treat in new[] { "AppleCandy", "ChocolBanana", "Takoyaki", "WaterBall" })
            Assert.Equal("stall/festival", Catalog.ByVanillaItem("Level_4_Area_4_Main", "Codex_Important_Level4_" + treat).Key);
    }

    [Fact]
    public void RewardsThatSpawnAfterAnEncounterAreFoundByScene()
    {
        var spawned = Catalog.Unplaced("Level_1_Ikai_Part_1_Main", "head_chase", "ambush_chest").ToList();
        Assert.Single(spawned);
        Assert.Equal("head/Level1", spawned[0].Key);

        Assert.Equal(2, Catalog.Settlement("Level_FinalP2_L3_A5_Main").Count());
        Assert.Empty(Catalog.Settlement("Level_1_Area_1_Main"));
    }

    [Fact]
    public void EncounterIsFoundBySceneAndHierarchyChain()
    {
        var encounter = AllLocations().First(l => l.Category == "encounter" && l.Scene == "Level_1_Area_2_Main" && l.Chain.StartsWith("BattleRound_C01"));
        Assert.Same(encounter, Catalog.Encounter("Level_1_Area_2_Main", encounter.Chain));
        Assert.Null(Catalog.Encounter("Level_1_Area_1_Main", encounter.Chain));
        Assert.Null(Catalog.Encounter("Level_1_Area_2_Main", "BattleRound_Nope"));
    }

    [Fact]
    public void SearchesOnlyConsiderTheLocationsOfTheConnectedSlot()
    {
        var chest = Catalog.Location("chest/Level_1_Area_1_Main/1");
        try
        {
            Catalog.SetSlotLocations(new[] { 1L, 2L });
            Assert.Null(Catalog.Location("chest/Level_1_Area_1_Main/1"));
            Assert.Null(Catalog.Nearest("Level_1_Area_1_Main", new Vector3(chest.Pos[0], chest.Pos[1], chest.Pos[2]), 2f, "chest"));
            Assert.Null(Catalog.ByVanillaItem("Level_1_Area_1_Main", "Codex_Important_Level1_Flower"));

            Catalog.SetSlotLocations(new[] { chest.Id });
            Assert.NotNull(Catalog.Location("chest/Level_1_Area_1_Main/1"));
        }
        finally
        {
            Catalog.SetSlotLocations(null);
        }
    }

    [Fact]
    public void LocationsHaveTheFieldsTheirHooksNeed()
    {
        foreach (var loc in AllLocations())
        {
            Assert.False(string.IsNullOrEmpty(loc.Name), loc.Key);
            switch (loc.Category)
            {
                case "chest":
                case "senbei" when loc.SenbeiType == "map":
                    Assert.NotNull(loc.Pos);
                    Assert.False(string.IsNullOrEmpty(loc.Scene), loc.Key);
                    break;
                case "encounter":
                    Assert.False(string.IsNullOrEmpty(loc.Chain), loc.Key);
                    Assert.False(string.IsNullOrEmpty(loc.Scene), loc.Key);
                    break;
                case "shop":
                case "harumi":
                case "stage_clear":
                    Assert.Equal("Hub", loc.Region);
                    break;
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<ItemRecord> AllItems() => Catalog.AllItems;

    private static System.Collections.Generic.IEnumerable<LocationRecord> AllLocations() => Catalog.AllLocations;

    [Theory]
    [InlineData("trial/FC01", 2)]            // the currency and the note
    [InlineData("trial/FC01#2", 2)]          // asked for by the other part
    [InlineData("tournament/reward#3", 3)]
    [InlineData("stage/Level1", 2)]
    [InlineData("stage/Level2", 1)]          // a board that pays one thing is alone
    public void TheLocationsOfOneRewardScreenAreFoundTogether(string key, int parts)
    {
        var loc = Catalog.Location(key);
        Assert.NotNull(loc);
        Assert.Equal(parts, Catalog.Bundle(loc).Count());
        Assert.All(Catalog.Bundle(loc), part => Assert.Equal(Catalog.BundleKey(key), Catalog.BundleKey(part.Key)));
    }
}
