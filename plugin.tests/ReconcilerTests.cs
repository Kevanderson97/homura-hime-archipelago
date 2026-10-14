using System.Collections.Generic;
using HomuraHimeAP.Archipelago;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ReconcilerTests
{
    [Fact]
    public void WhatTheServerHasAndWeLackIsAdopted()
    {
        var difference = Reconciler.Compare(new HashSet<long> { 1, 2 }, new List<long> { 2, 3, 4 });
        Assert.Equal(new long[] { 3, 4 }, difference.ToAdopt);
    }

    [Fact]
    public void WhatWeHaveAndTheServerLacksIsSent()
    {
        var difference = Reconciler.Compare(new HashSet<long> { 1, 2, 5 }, new List<long> { 2 });
        Assert.Equal(new long[] { 1, 5 }, difference.ToSend);
    }

    [Fact]
    public void IdenticalRecordsNeedNothing()
    {
        var difference = Reconciler.Compare(new HashSet<long> { 1, 2 }, new List<long> { 2, 1 });
        Assert.Empty(difference.ToAdopt);
        Assert.Empty(difference.ToSend);
    }

    [Fact]
    public void EmptySidesAreFine()
    {
        Assert.Empty(Reconciler.Compare(new HashSet<long>(), new List<long>()).ToSend);
        Assert.Equal(new long[] { 7 }, Reconciler.Compare(new HashSet<long>(), new List<long> { 7 }).ToAdopt);
        Assert.Equal(new long[] { 7 }, Reconciler.Compare(new HashSet<long> { 7 }, new List<long>()).ToSend);
    }
}
