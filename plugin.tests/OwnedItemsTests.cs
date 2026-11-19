using System.Linq;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class OwnedItemsTests
{
    public OwnedItemsTests() => TestEnvironment.Ensure();

    [Theory]
    [InlineData("key")]
    [InlineData("soul_core")]
    [InlineData("ability")]
    [InlineData("dash_skill")]
    [InlineData("skill")]
    [InlineData("combo")]
    [InlineData("omamori")]
    [InlineData("gun")]
    public void ItemsThatOpenTheWayAndEquipmentAreKept(string type) => Assert.True(OwnedItems.IsPermanent(type));

    [Theory]
    [InlineData("currency")]
    [InlineData("shard")]
    [InlineData("crystal")]
    [InlineData("consumable")]
    [InlineData("upgrade")]
    [InlineData("utility")]
    [InlineData("trap")]
    [InlineData("unlock")]
    [InlineData("unlock_progressive")]
    public void ItemsThatAreUsedUpOrLiveInThePluginAreNotRepaired(string type) => Assert.False(OwnedItems.IsPermanent(type));

    [Fact]
    public void EveryKeyItemOfTheCatalogIsCovered()
    {
        // the seven key items (the Flower, the Lever, the Nurikabe items, the Fish, the Certificate) and the five Soul Cores
        var kept = Catalog.AllItems.Where(item => OwnedItems.IsPermanent(item.Type)).Select(item => item.Name).ToList();
        Assert.Contains("Pretty Little Flower", kept);
        Assert.Contains("Elevator Lever", kept);
        Assert.Contains("Inugami Certificate", kept);
        Assert.Contains("Agasa's Soul Core", kept);
        Assert.Equal(7, Catalog.AllItems.Count(item => item.Type == "key"));
        Assert.Equal(5, Catalog.AllItems.Count(item => item.Type == "soul_core"));
    }

    // an NPC that asks for an item also counts what the multiworld has sent
    [Fact]
    public void AnNpcGetsTheReceivedItemItAsksForWhenThePlayerLacksIt()
    {
        var asked = new[] { "Codex_Important_Level1_Flower" };

        Assert.Equal(asked, OwnedItems.ReceivedButNotOwned(asked, _ => false, _ => true));
        Assert.Empty(OwnedItems.ReceivedButNotOwned(asked, _ => true, _ => true)); // already owned
        Assert.Empty(OwnedItems.ReceivedButNotOwned(asked, _ => false, _ => false)); // never received: the quest waits for it
    }

    [Fact]
    public void OnlyTheMissingItemsOfTheListAreGivenAndEachOnce()
    {
        var asked = new[] { "A", "B", "A", "C" };

        var given = OwnedItems.ReceivedButNotOwned(asked, definition => definition == "B", definition => definition != "C");

        Assert.Equal(new[] { "A" }, given);
    }
}
