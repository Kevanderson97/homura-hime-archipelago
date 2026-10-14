using System.Collections.Generic;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class TickGuardTests
{
    private readonly List<string> _log = new();

    public TickGuardTests()
    {
        TickGuard.Reset();
        TickGuard.Log = _log.Add;
    }

    [Fact]
    public void AFailingSubsystemDoesNotStopTheOthers()
    {
        var ran = new List<string>();
        TickGuard.Run("broken", 0f, () => throw new System.InvalidOperationException("boom"));
        TickGuard.Run("fine", 0f, () => ran.Add("fine"));

        Assert.Equal(new[] { "fine" }, ran);
    }

    [Fact]
    public void TheSameFailureIsReportedOnce()
    {
        for (var i = 0; i < 2; i++) TickGuard.Run("broken", i, () => throw new System.InvalidOperationException("boom"));
        Assert.Single(_log);
    }

    [Fact]
    public void ARepeatedlyFailingSubsystemIsPausedThenTriedAgain()
    {
        var calls = 0;
        void Fails() { calls++; throw new System.InvalidOperationException("boom"); }

        for (var i = 0; i < TickGuard.FailuresBeforePause; i++) TickGuard.Run("broken", 0f, Fails);
        TickGuard.Run("broken", 1f, Fails); // paused
        Assert.Equal(TickGuard.FailuresBeforePause, calls);

        TickGuard.Run("broken", TickGuard.PauseSeconds + 1f, Fails);
        Assert.Equal(TickGuard.FailuresBeforePause + 1, calls);
    }

    [Fact]
    public void ASuccessResetsTheFailureCount()
    {
        var fail = true;
        void Sometimes() { if (fail) throw new System.InvalidOperationException("boom"); }

        for (var i = 0; i < TickGuard.FailuresBeforePause - 1; i++) TickGuard.Run("flaky", 0f, Sometimes);
        fail = false;
        TickGuard.Run("flaky", 0f, Sometimes);
        fail = true;
        TickGuard.Run("flaky", 0f, Sometimes);

        Assert.DoesNotContain(_log, line => line.Contains("paused"));
    }
}
