using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class SenbeiVisualTests
{
    [Theory]
    [InlineData(true, false, true)]  // an item of another world: the logo
    [InlineData(true, true, false)]  // an item of this game: the Senbei stays
    [InlineData(false, false, false)] // not known yet: nothing is claimed
    public void OnlyItemsOfOtherWorldsReplaceTheSenbei(bool scouted, bool own, bool logo)
    {
        Assert.Equal(logo, SenbeiVisuals.ShowsLogo(scouted, own));
    }
}
