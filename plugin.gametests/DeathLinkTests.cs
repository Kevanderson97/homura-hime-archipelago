using System.Collections;
using HomuraHimeAP.Archipelago;
using Surity;

namespace HomuraHimeAP.GameTests;

/// <summary>DeathLink between this slot and the second player of the test seed.</summary>
[TestClass]
public class DeathLinkTests
{
    private static OtherPlayer _other;

    private static IEnumerator ConnectBoth()
    {
        yield return Harness.Connect();
        if (_other == null)
        {
            yield return OtherPlayer.Connect(player => _other = player);
            _other.EnableDeathLink();
        }
    }

    [Test]
    public IEnumerator ADeathOfTheOtherPlayerIsReceived() => Harness.Guard(nameof(ADeathOfTheOtherPlayerIsReceived), Receiving());

    private static IEnumerator Receiving()
    {
        yield return ConnectBoth();
        Assert.IsTrue(DeathLinkHandler.Enabled, "DeathLink is not enabled for this slot");

        _other.SendDeath();

        yield return Harness.WaitFor(() => DeathLinkHandler.DeathWaiting, "the death to arrive");
    }

    [Test]
    public IEnumerator OurDeathIsSentToTheOtherPlayer() => Harness.Guard(nameof(OurDeathIsSentToTheOtherPlayer), Sending());

    private static IEnumerator Sending()
    {
        yield return ConnectBoth();
        var before = _other.DeathsReceived;

        DeathLinkHandler.OnPlayerDied(); // what the game's death flow reports

        yield return OtherPlayer.WaitUntil(() => _other.DeathsReceived > before, "our death to reach the other player");
    }
}
