using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Surity;

namespace HomuraHimeAP.GameTests;

/// <summary>Connection loss and reconciliation with the server (audit steps 2.2 and 2.3).</summary>
[TestClass]
public class ResilienceTests
{
    /// <summary>Closes our socket the way a network failure would (from the pool: the call can wait on the socket).</summary>
    private static void DropConnection()
    {
        var session = ApClient.Session;
        Task.Run(() => session.Socket.DisconnectAsync());
    }

    [Test]
    public IEnumerator ReconnectsByItselfAfterTheSocketCloses() => Harness.Guard(nameof(ReconnectsByItselfAfterTheSocketCloses), Reconnects());

    private static IEnumerator Reconnects()
    {
        yield return Harness.Connect();
        var state = ApClient.State;

        DropConnection();
        yield return Harness.WaitFor(() => ApClient.Status == ConnectionStatus.Reconnecting, "the connection loss to be noticed");
        Assert.IsTrue(ApClient.Active, "the randomizer must stay active while reconnecting");

        yield return Harness.WaitFor(() => ApClient.Status == ConnectionStatus.Connected, "the reconnect");
        Assert.IsTrue(ApClient.State == state, "the session state must survive a reconnect");
    }

    [Test]
    public IEnumerator ChecksMadeWhileDisconnectedReachTheServerAfterwards() => Harness.Guard(nameof(ChecksMadeWhileDisconnectedReachTheServerAfterwards), OfflineCheck());

    private static IEnumerator OfflineCheck()
    {
        yield return Harness.Connect();
        Harness.FreshSession();

        var target = Catalog.AllLocations.First(l => ScoutCache.Get(l.Id) is { IsOwn: true } && l.Category == "senbei"
                                                     && !ApClient.Session.Locations.AllLocationsChecked.Contains(l.Id));

        DropConnection();
        yield return Harness.WaitFor(() => ApClient.Status == ConnectionStatus.Reconnecting, "the connection loss to be noticed");
        ApClient.CheckLocation(target.Id);
        Assert.IsTrue(ApClient.State.CheckedLocations.Contains(target.Id), "recorded locally while disconnected");

        yield return Harness.WaitFor(() => ApClient.Status == ConnectionStatus.Connected, "the reconnect");
        yield return OtherPlayer.WaitUntil(() => ApClient.Session.Locations.AllLocationsChecked.Contains(target.Id), "the server to learn the check");
    }

    [Test]
    public IEnumerator LocationsCheckedElsewhereBecomeCheckedHere() => Harness.Guard(nameof(LocationsCheckedElsewhereBecomeCheckedHere), CheckedElsewhere());

    private static IEnumerator CheckedElsewhere()
    {
        yield return Harness.Connect();
        Harness.FreshSession();

        // a second connection of our own slot stands in for another client or `!collect`
        OtherPlayer second = null;
        yield return OtherPlayer.Connect("Homura-Hime", Harness.Slot, player => second = player);
        try
        {
            var target = Catalog.AllLocations.First(l => l.Category == "journal" && !ApClient.State.CheckedLocations.Contains(l.Id)
                                                         && !ApClient.Session.Locations.AllLocationsChecked.Contains(l.Id));
            second.Check(target.Id);
            yield return Harness.WaitFor(() => ApClient.State.CheckedLocations.Contains(target.Id), "the server's check to be adopted");
        }
        finally
        {
            second.Disconnect();
        }
    }
}
