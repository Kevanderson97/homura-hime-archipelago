using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class HudVisibilityTests
{
    [Fact]
    public void WithoutTheGamesHudManagerTheHudCountsAsShown()
    {
        // the unit tests run without the game: nothing may be held back because the check cannot be made
        TestEnvironment.Ensure();
        Assert.False(GameApi.HudHidden);
    }
}
