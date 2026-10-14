using System.Collections;
using System.Linq;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.Patches;
using Surity;

namespace HomuraHimeAP.GameTests;

/// <summary>Items travelling between Homura-Hime and another game (see <see cref="OtherPlayer"/>).</summary>
[TestClass]
public class MultiworldTests
{
    private static OtherPlayer _other;

    private static IEnumerator ConnectOther()
    {
        if (_other == null) yield return OtherPlayer.Connect(player => _other = player);
    }

    [Test]
    public IEnumerator SomeOfOurLocationsHoldItemsOfTheOtherGame() => Harness.Guard(nameof(SomeOfOurLocationsHoldItemsOfTheOtherGame), ForeignItems());

    private static IEnumerator ForeignItems()
    {
        yield return Harness.Connect();

        var foreign = Catalog.AllLocations.Select(l => ScoutCache.Get(l.Id)).Where(s => s != null && !s.IsOwn).ToList();
        Assert.IsTrue(foreign.Count > 0, "no location holds an item of another player");
        Assert.IsTrue(foreign.All(s => s.PlayerName == OtherPlayer.SlotName), "unexpected owner");
    }

    [Test]
    public IEnumerator OtherPlayersItemsAreNamedAfterTheirOwner() => Harness.Guard(nameof(OtherPlayersItemsAreNamedAfterTheirOwner), Naming());

    private static IEnumerator Naming()
    {
        yield return Harness.Connect();

        var scouted = Catalog.AllLocations.Select(l => ScoutCache.Get(l.Id)).First(s => s != null && !s.IsOwn);
        Assert.Equal(OtherPlayer.SlotName + "'s " + scouted.Name, ShopDisplay.Name(scouted), "shop name");
        Assert.IsTrue(ShopDisplay.Description(scouted).Contains(OtherPlayer.SlotName), "description names the owner");
    }

    [Test]
    public IEnumerator CheckingALocationSendsTheItemToTheOtherPlayer() => Harness.Guard(nameof(CheckingALocationSendsTheItemToTheOtherPlayer), Sending());

    private static IEnumerator Sending()
    {
        yield return Harness.Connect();
        yield return ConnectOther();
        Harness.FreshSession();

        // a location nobody has checked yet: the server ignores a second check of the same one
        var target = Catalog.AllLocations.First(l => ScoutCache.Get(l.Id) is { IsOwn: false } && !ApClient.Session.Locations.AllLocationsChecked.Contains(l.Id));
        var item = ScoutCache.Get(target.Id);
        var before = _other.ReceivedFrom(Harness.Slot, item.ItemId);

        ApClient.CheckLocation(target.Id);

        yield return OtherPlayer.WaitUntil(() => _other.ReceivedFrom(Harness.Slot, item.ItemId) > before,
            item.Name + " to arrive at " + OtherPlayer.SlotName);
    }

    [Test]
    public IEnumerator ItemsFoundByTheOtherPlayerReachUs() => Harness.Guard(nameof(ItemsFoundByTheOtherPlayerReachUs), Receiving());

    private static IEnumerator Receiving()
    {
        yield return Harness.Connect();
        yield return ConnectOther();

        // the library hands out a new collection on every call: a reference kept from before never sees new items
        int FromOther() => ApClient.Session.Items.AllItemsReceived.Count(i => i.Player.Name == OtherPlayer.SlotName);
        var before = FromOther();

        _other.CheckEverything();

        yield return OtherPlayer.WaitUntil(() => FromOther() > before, "items from " + OtherPlayer.SlotName);

        // every item that reaches us must be one the plugin knows how to grant
        foreach (var info in ApClient.Session.Items.AllItemsReceived)
            Assert.NotNull(Catalog.Item(info.ItemId), "unknown item id " + info.ItemId);
    }
    [AfterAll]
    public void Disconnect()
    {
        _other?.Disconnect();
        _other = null;
    }
}
