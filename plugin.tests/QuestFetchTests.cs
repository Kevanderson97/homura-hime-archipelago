using System.Linq;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class QuestFetchTests : IClassFixture<QuestFetchTests.Fixture>
{
    public sealed class Fixture
    {
        public Fixture() => TestEnvironment.Ensure();
    }

    [Theory]
    [InlineData("Level_2_Area_1_Main", "Gameplay_等待獲取任務道具", true)]
    [InlineData("Level_2_Area_4_Main", "Gameplay_B04", true)]
    [InlineData("Level_3_Area_5_Main", "Gameplay_B05", true)]
    [InlineData("Level_2_Area_4_Main", "Gameplay_B03", false)]       // the other fight of the area
    [InlineData("Level_5_Area_5_Main", "Gameplay_B04", false)]       // same node name, another scene
    [InlineData(null, null, false)]
    public void OnlyTheFetchNodesOfTheQuestsAreFound(string scene, string node, bool found)
    {
        Assert.Equal(found, QuestFetch.Find(scene, node) != null);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]  // the item is not owned yet: the quest runs as it is
    [InlineData(false, true, false)]  // the vanilla game
    public void TheStepIsSkippedOnlyForAnOwnedItemInARandomizerGame(bool randomizer, bool owns, bool skip)
    {
        Assert.Equal(skip, QuestFetch.ShouldSkip(randomizer, owns));
    }

    [Fact]
    public void EveryLocationOfAStepIsInTheCatalogue()
    {
        TestEnvironment.Ensure();
        foreach (var step in QuestFetch.All)
            foreach (var key in step.Locations)
                Assert.True(Catalog.Location(key) != null, key);
    }

    [Fact]
    public void EveryItemOfAStepIsAKeyItemOfTheWorld()
    {
        TestEnvironment.Ensure();
        Assert.All(QuestFetch.All, step => Assert.Contains(Catalog.AllItems, item => item.Game == step.Item));
    }
}
