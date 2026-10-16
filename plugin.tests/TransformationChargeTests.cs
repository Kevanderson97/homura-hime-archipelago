using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class TransformationChargeTests
{
    [Theory]
    [InlineData(true, false, true, true)]    // the ultimate always charged the meter
    [InlineData(false, true, true, true)]    // Transformation alone does now, in a randomizer game
    [InlineData(false, true, false, false)]  // the vanilla game is left as it is
    [InlineData(false, false, true, false)]  // neither ability: no charge
    [InlineData(true, true, false, true)]
    public void EitherAbilityChargesTheMeterInARandomizerGame(bool ultimate, bool transformation, bool randomizer, bool charges)
    {
        Assert.Equal(charges, TransformationCharge.Decide(ultimate, transformation, randomizer));
    }
}
