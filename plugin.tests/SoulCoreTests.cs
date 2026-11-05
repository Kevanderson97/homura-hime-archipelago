using System.Linq;
using HomuraHimeAP.Data;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class SoulCoreTests : IClassFixture<CatalogTests.Fixture>
{
    private static readonly string[] Cores =
    {
        "Codex_Important_Level1_SoulCore", "Codex_Important_Level2_SoulCore", "Codex_Important_Level3_SoulCore",
        "Codex_Important_Level4_SoulCore", "Codex_Important_Level5_SoulCore",
    };

    [Theory]
    [InlineData("LevelFinalP1_L2_Boss", "Codex_Important_Level2_SoulCore")] // Ling Ling and the Corpse Princess
    [InlineData("LevelFinalP2_L3_Boss", "Codex_Important_Level3_SoulCore")] // Nozomi
    [InlineData("LevelFinalP2_L4_Ikai_Entrance", "Codex_Important_Level4_SoulCore")] // the door to Yumiko & Kumiko's Ikai
    [InlineData("LevelFinalP2_L4_Boss", "Codex_Important_Level4_SoulCore")] // Yumiko & Kumiko
    [InlineData("LevelFinalP3_L5_A2_Entrance", "Codex_Important_Level1_SoulCore")] // Agasa, the first area of Chapter 8
    [InlineData("LevelFinalP3_L5_Boss", "Codex_Important_Level5_SoulCore")] // Mamiya
    public void EveryBossFightNeedsTheCoreOfThatBoss(string destination, string definition) =>
        Assert.Equal(definition, SoulCores.NeededForDestination(destination));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LevelFinalP1_L2_A5_Entrance")]
    [InlineData("LevelFinalP1_L1_Ikai_Entrance")]
    [InlineData("Level2_BossRoom")]
    [InlineData("LevelFinalP4_Passage_Entrance")]
    [InlineData("HubWorld_Main")]
    public void OtherDestinationsNeedNothing(string destination) => Assert.Null(SoulCores.NeededForDestination(destination));

    [Fact]
    public void AllFiveCoresAreNeededOnce() => Assert.Equal(Cores.OrderBy(c => c), SoulCores.Definitions.OrderBy(c => c));

    [Fact]
    public void ADestinationIsOpenOnlyWithItsOwnCore()
    {
        const string destination = "LevelFinalP2_L3_Boss";
        Assert.Equal("Codex_Important_Level3_SoulCore", SoulCores.MissingFor(destination, _ => false));
        Assert.Null(SoulCores.MissingFor(destination, definition => definition == "Codex_Important_Level3_SoulCore"));
        Assert.Equal("Codex_Important_Level3_SoulCore",
            SoulCores.MissingFor(destination, definition => definition != "Codex_Important_Level3_SoulCore"));
        Assert.Null(SoulCores.MissingFor("HubWorld_Main", _ => false));
    }

    [Fact]
    public void TheMessageNamesTheCore() =>
        Assert.Equal("Ling Ling's Soul Core required to continue", SoulCores.Message("Codex_Important_Level2_SoulCore"));

    [Fact]
    public void TheNamesAreTheNamesOfTheItemsInTheMultiworld()
    {
        foreach (var definition in Cores)
        {
            var item = Catalog.AllItems.Single(i => i.Game == definition);
            Assert.Equal("soul_core", item.Type);
            Assert.Equal(item.Name, SoulCores.NameOf(definition));
        }
    }

    [Fact]
    public void EveryDestinationButTheIkaiIsASubChapterOfTheChapterSelect()
    {
        // the Ikai before Yumiko & Kumiko is a scene the story passes through; the Chapter Select does not list it
        foreach (var destination in SoulCores.Destinations.Where(d => d != "LevelFinalP2_L4_Ikai_Entrance"))
            Assert.True(ChapterData.TryGetSubChapter(destination, out _, out _), destination);
    }

    [Fact]
    public void TheHubHandsEachCoreOutAtALocationOfItsOwn()
    {
        foreach (var definition in Cores)
        {
            var location = Catalog.ByVanillaItem("HubWorld_Main", definition);
            Assert.NotNull(location);
            Assert.Equal("soul_core", location.Category);
        }
    }
}
