using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class PostGameHubTests
{
    [Theory]
    [InlineData("HubWorld_PostGame_Main", 0, true)]
    [InlineData("HubWorld_PostGame_Main", 12, true)]
    [InlineData("HubWorld_PostGame_Main", 19, true)]
    [InlineData("HubWorld_PostGame_Main", 20, false)] // the post-game is open
    [InlineData("HubWorld_PostGame_Main", -1, false)] // the progress could not be read: leave the game's choice alone
    [InlineData("HubWorld_HubWorld", 0, false)]
    [InlineData("Level1_Level1", 0, false)]
    [InlineData(null, 0, false)]
    public void ThePostGameHubIsRedirectedUntilThePostGameIsReached(string key, int progress, bool redirected)
    {
        Assert.Equal(redirected, PostGameHub.IsTooEarly(key, progress));
    }
}
