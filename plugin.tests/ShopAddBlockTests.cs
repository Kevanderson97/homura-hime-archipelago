using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ShopAddBlockTests
{
    [Theory]
    [InlineData(true, true, true)]    // the slot's item going into the player's inventory: skipped
    [InlineData(true, false, false)]  // the shop moving its own stock (store, sold out) is untouched
    [InlineData(false, true, false)]  // anything else the player receives
    public void OnlyThePurchasedItemIsKeptOutOfThePlayersInventory(bool purchase, bool toPlayer, bool blocked)
    {
        Assert.Equal(blocked, ShopAddBlockPatch.Blocks(purchase, toPlayer));
    }

    [Theory]
    [InlineData(0, 0, true)]    // gone from the store and not listed: it would vanish from the shop
    [InlineData(0, 1, false)]   // sold out
    [InlineData(2, 0, false)]   // still on sale (stock left, or a shard at a lower price stage)
    public void ASlotThatLeftTheStoreIsListedAsSoldOut(int inStore, int inSoldOut, bool added)
    {
        Assert.Equal(added, ShopBuyPatch.NeedsSoldOutEntry(inStore, inSoldOut));
    }
}
