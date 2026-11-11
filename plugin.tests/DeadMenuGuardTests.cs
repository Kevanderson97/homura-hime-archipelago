using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class DeadMenuGuardTests
{
    [Theory]
    [InlineData(true, false, true, 8.0, true)]    // parked at the end of the Enter part, menu off: bring it back
    [InlineData(true, false, true, 8.005, true)]  // a hair off the pause time still counts
    [InlineData(true, true, true, 8.0, false)]    // the menu is on: nothing to do
    [InlineData(false, false, true, 8.0, false)]  // the death screen is not shown
    [InlineData(true, false, false, 8.0, false)]  // still playing (the controller has not parked it yet)
    [InlineData(true, false, true, 15.0, false)]  // parked at the end of the Reborn part: the menu is meant to be off
    [InlineData(true, false, true, 3.0, false)]   // the Enter part has not reached the menu yet
    public void OnlyAMenuLostAtTheEndOfTheEnterPartIsBroughtBack(bool shown, bool menuActive, bool paused, double time, bool expected) =>
        Assert.Equal(expected, DeadMenuGuard.NeedsRestore(shown, menuActive, paused, time, 8.0));

    [Fact]
    public void WithoutAnEnterPartNothingIsDone() =>
        Assert.False(DeadMenuGuard.NeedsRestore(true, false, true, 8.0, double.NaN));
}
