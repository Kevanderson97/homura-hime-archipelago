using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ConsoleLogTests
{
    [Fact]
    public void TheNewestLinesAreShownOldestFirst()
    {
        var log = new ConsoleLog();
        for (var i = 1; i <= 5; i++) log.Add("line " + i);
        Assert.Equal("line 3\nline 4\nline 5", log.Tail(3));
        Assert.Equal("line 1\nline 2\nline 3\nline 4\nline 5", log.Tail(50));
    }

    [Fact]
    public void OnlyTheNewestTwoHundredLinesAreKept()
    {
        var log = new ConsoleLog();
        for (var i = 1; i <= ConsoleLog.Capacity + 25; i++) log.Add("line " + i);
        Assert.Equal(ConsoleLog.Capacity, log.Count);
        Assert.StartsWith("line 26\n", log.Tail(1000));
        Assert.EndsWith("line " + (ConsoleLog.Capacity + 25), log.Tail(1000));
    }

    [Fact]
    public void AMessageWithSeveralLinesBecomesSeveralLinesAndEmptyOnesAreIgnored()
    {
        var log = new ConsoleLog();
        log.Add("first\r\nsecond\n\n  third  ");
        log.Add("");
        log.Add(null);
        log.Add("   ");
        Assert.Equal(3, log.Count);
        Assert.Equal("first\nsecond\nthird", log.Tail(10));
    }

    [Fact]
    public void TheVersionChangesWhenTheContentDoes()
    {
        var log = new ConsoleLog();
        var empty = log.Version;
        log.Add("a");
        Assert.NotEqual(empty, log.Version);
        var one = log.Version;
        log.Add("");
        Assert.Equal(one, log.Version); // nothing was added
        log.Clear();
        Assert.NotEqual(one, log.Version);
        Assert.Equal("", log.Tail(5));
    }
}
