using System.Collections;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Surity;

namespace HomuraHimeAP.GameTests;

[TestClass]
public class ConnectionTests
{
    [Test]
    public IEnumerator ConnectsToTheServerAndLoadsTheSlot() => Harness.Guard(nameof(ConnectsToTheServerAndLoadsTheSlot), Connects());

    private static IEnumerator Connects()
    {
        yield return Harness.Connect();

        Assert.IsTrue(ApClient.IsConnected, "not connected");
        Assert.IsTrue(!string.IsNullOrEmpty(ApClient.Slot.WorldVersion), "no slot data");
        Assert.IsTrue(ApClient.State != null, "no session state");
    }

    [Test]
    public IEnumerator EveryLocationOfTheSlotIsScouted() => Harness.Guard(nameof(EveryLocationOfTheSlotIsScouted), Scouted());

    private static IEnumerator Scouted()
    {
        yield return Harness.Connect();

        var missing = 0;
        foreach (var loc in Catalog.AllLocations)
            if (ScoutCache.Get(loc.Id) == null) missing++;
        Assert.Equal(0, missing, "scouted locations missing");
    }

    [Test]
    public IEnumerator SlotLocationsFilterTheCatalog() => Harness.Guard(nameof(SlotLocationsFilterTheCatalog), Filtered());

    private static IEnumerator Filtered()
    {
        yield return Harness.Connect();

        // encounter clears are on in the test seed, the ambush rewards are on too: nothing is filtered away
        Assert.NotNull(Catalog.Location("shop/ComboB"), "shop slot");
        Assert.NotNull(Catalog.Location("tutorial/clear"), "tutorial reward");
    }
}
