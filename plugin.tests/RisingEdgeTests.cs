using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class RisingEdgeTests
{
    [Fact]
    public void ItIsTrueOnlyForTheCallInWhichTheStateGoesFromNotHeldToHeld()
    {
        var edge = new RisingEdge();
        Assert.False(edge.Update(false));
        Assert.True(edge.Update(true));   // pressed
        Assert.False(edge.Update(true));  // still held
        Assert.False(edge.Update(true));
        Assert.False(edge.Update(false)); // released
        Assert.True(edge.Update(true));   // pressed again
    }

    [Fact]
    public void AStateThatStartsHeldCountsAsAPress()
    {
        Assert.True(new RisingEdge().Update(true));
    }
}
