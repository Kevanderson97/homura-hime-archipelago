using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class RewardWaitersTests
{
    [Theory]
    [InlineData(true, true)]    // an emptied reward: the reward screen never opens
    [InlineData(false, false)]  // a hand-over has no reward screen to wait for
    public void OnlyEmptiedRewardsReleaseWhatWaitsForTheScreen(bool isReward, bool release)
    {
        Assert.Equal(release, RewardWaiters.NeedsRelease(isReward));
    }
}
