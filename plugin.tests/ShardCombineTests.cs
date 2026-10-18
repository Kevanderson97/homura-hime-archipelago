using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ShardCombineTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)] // not a full set
    [InlineData(3, true)]  // a full set was left behind
    [InlineData(7, true)]  // several sets were received at once
    public void AnotherCrystalIsLeftWhenAFullSetRemains(int shards, bool left)
    {
        Assert.Equal(left, ShardCombinePatch.CrystalLeft(shards));
    }
}
