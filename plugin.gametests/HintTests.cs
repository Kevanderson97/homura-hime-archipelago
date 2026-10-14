using System.Collections;
using System.Linq;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Surity;

namespace HomuraHimeAP.GameTests;

/// <summary>Shop hints (plan section 7): a visible shop entry holding another player's item becomes a hint for that player.</summary>
[TestClass]
public class HintTests
{
    private static OtherPlayer _other;

    private static IEnumerator ConnectBoth()
    {
        yield return Harness.Connect();
        if (_other == null) yield return OtherPlayer.Connect(player => _other = player);
        Harness.FreshSession();
    }

    /// <summary>
    /// A location of the wanted category. The sender does not care what category of location it is told about (the shop display only
    /// calls it for shop entries), and the test seed may hold no other player's item in a shop at all.
    /// </summary>
    private static LocationRecord Location(bool own) =>
        Catalog.AllLocations.First(l => (!own || l.Category == "shop") && ScoutCache.Get(l.Id) is { } s && s.IsOwn == own
                                        && !ApClient.Session.Locations.AllLocationsChecked.Contains(l.Id));

    [Test]
    public IEnumerator ASeenShopEntryWithAnItemForAnotherPlayerBecomesAHint() => Harness.Guard(nameof(ASeenShopEntryWithAnItemForAnotherPlayerBecomesAHint), Foreign());

    private static IEnumerator Foreign()
    {
        yield return ConnectBoth();
        Assert.IsTrue(ApClient.Slot.ShopHints, "the test seed must have shop hints on");
        var location = Location(own: false);

        HintSender.Seen(location);
        yield return Harness.WaitFor(() => ApClient.State.HintedLocations.Contains(location.Id), "the hint to be sent");
        yield return OtherPlayer.WaitUntil(() => _other.HasHintAt(location.Id), "the hint to exist on the server");

        HintSender.Seen(location); // seeing it again must not send it again
        Assert.IsTrue(!ApClient.State.PendingHints.Contains(location.Id), "a hinted entry was queued again");
    }

    [Test]
    public IEnumerator AShopEntryWithOneOfOurOwnItemsIsNotHinted() => Harness.Guard(nameof(AShopEntryWithOneOfOurOwnItemsIsNotHinted), Own());

    private static IEnumerator Own()
    {
        yield return ConnectBoth();
        var location = Location(own: true);

        HintSender.Seen(location);

        Assert.IsTrue(!ApClient.State.PendingHints.Contains(location.Id) && !ApClient.State.HintedLocations.Contains(location.Id), "an own item was hinted");
    }
}
