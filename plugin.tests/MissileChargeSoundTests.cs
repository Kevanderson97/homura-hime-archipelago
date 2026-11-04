using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class MissileChargeSoundTests
{
    [Fact]
    public void TheMissileChargeIsTheShotgunSampleAtTwentyPercent()
    {
        Assert.Equal(0.2f, CutGunSounds.MissileChargeVolume);
    }

    [Fact]
    public void TheSamplesOfTheOtherGunsPlayAtFiftyPercent()
    {
        Assert.Equal(0.5f, CutGunSounds.SampleVolume);
    }
}
