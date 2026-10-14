using HomuraHimeAP.Archipelago;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Xunit;

namespace HomuraHimeAP.Tests;

public class DeathLinkMachineTests
{
    private static DeathLink Death() => new("Someone", "ran into a wall");

    private readonly DeathLinkMachine _machine = new();

    [Fact]
    public void AReceivedDeathWaitsForASafeMoment()
    {
        _machine.Receive(Death(), 0f);

        Assert.Equal(DeathLinkAction.None, _machine.Tick(1f, canDie: false));
        Assert.Equal(DeathLinkAction.Kill, _machine.Tick(2f, canDie: true));
    }

    [Fact]
    public void AReceivedDeathIsDroppedWhenNoSafeMomentComes()
    {
        _machine.Receive(Death(), 0f);

        Assert.Equal(DeathLinkAction.None, _machine.Tick(DeathLinkMachine.ReceiveWindow + 1f, canDie: true));
        Assert.Equal(DeathLinkMachine.Phase.Idle, _machine.Current);
    }

    [Fact]
    public void ADeathCausedByDeathLinkIsNotSentBack()
    {
        _machine.Receive(Death(), 0f);
        _machine.Tick(1f, canDie: true);
        _machine.Killed(true, 1f);

        Assert.Equal(DeathLinkAction.None, _machine.PlayerDied());
        Assert.Equal(DeathLinkAction.None, _machine.PlayerGaveUp());
    }

    [Fact]
    public void AKillThatDidNotKillDoesNotSwallowTheNextRealDeath()
    {
        _machine.Receive(Death(), 0f);
        _machine.Tick(1f, canDie: true);
        _machine.Killed(true, 1f);

        _machine.Tick(1f + DeathLinkMachine.EchoWindow + 1f, canDie: true);

        Assert.Equal(DeathLinkAction.Send, _machine.PlayerDied());
    }

    [Fact]
    public void AFailedKillIsForgotten()
    {
        _machine.Receive(Death(), 0f);
        _machine.Tick(1f, canDie: true);
        _machine.Killed(false, 1f);

        Assert.Equal(DeathLinkMachine.Phase.Idle, _machine.Current);
        Assert.Equal(DeathLinkAction.Send, _machine.PlayerDied());
    }

    [Fact]
    public void ADeathReceivedWhileAnotherTakesEffectIsIgnored()
    {
        _machine.Receive(Death(), 0f);
        _machine.Tick(1f, canDie: true);
        _machine.Receive(Death(), 1.5f);

        Assert.Equal(DeathLinkMachine.Phase.Applying, _machine.Current);
    }

    [Fact]
    public void AnOwnDeathIsSentOnceWhenRevivesCount()
    {
        Assert.Equal(DeathLinkAction.Send, _machine.PlayerDied());
        Assert.Equal(DeathLinkAction.None, _machine.PlayerGaveUp());
    }

    [Fact]
    public void WithSkipRevivesTheDeathIsSentWhenThePlayerGivesUp()
    {
        _machine.SkipRevives = true;

        Assert.Equal(DeathLinkAction.None, _machine.PlayerDied()); // may still revive
        Assert.Equal(DeathLinkAction.Send, _machine.PlayerGaveUp());
        Assert.Equal(DeathLinkAction.None, _machine.PlayerGaveUp()); // only once
    }

    [Fact]
    public void ANewDeathCanBeSentAfterTheLastOne()
    {
        Assert.Equal(DeathLinkAction.Send, _machine.PlayerDied());
        Assert.Equal(DeathLinkAction.Send, _machine.PlayerDied());
    }
}
