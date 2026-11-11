using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class DeathCauseTests
{
    [Theory]
    [InlineData("Enemy_CursedDoll_A_Default(Clone)", "Hexed Doll A")]
    [InlineData("Enemy_CursedDoll_C_Default", "Hexed Doll C")]
    [InlineData("Enemy_KasaObake_01_Default(Clone)", "Kasa Obake 01")]
    [InlineData("Enemy_Oni", "Oni")]
    [InlineData("BossNozomi", "Boss Nozomi")]
    [InlineData("SpikyPuffer(Clone)", "Spiky Puffer")]
    [InlineData("HPDropper", "HP Dropper")] // an acronym stays together
    [InlineData("小鬼(Clone)", "小鬼")] // names in the game's own language are kept as they are
    public void AnObjectNameBecomesAReadableName(string objectName, string expected) =>
        Assert.Equal(expected, DeathCause.NameOf(objectName));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Enemy_Default(Clone)")] // nothing is left once the noise is gone
    public void NothingReadableGivesNoName(string objectName) => Assert.Null(DeathCause.NameOf(objectName));

    [Fact]
    public void ThereAreTenGenericCausesAndEveryOneNamesThePlayer()
    {
        Assert.Equal(10, DeathCause.Generic.Length);
        for (var i = 0; i < DeathCause.Generic.Length; i++)
        {
            var message = DeathCause.GenericMessage("HimeTest", i);
            Assert.StartsWith("HimeTest ", message);
            Assert.EndsWith(".", message);
        }

        Assert.Equal("HimeTest should have parried.", DeathCause.GenericMessage("HimeTest", 0));
        Assert.Equal("HimeTest couldn't defeat the Will of the World.", DeathCause.GenericMessage("HimeTest", 9));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(23, 3)]
    [InlineData(-1, 9)]
    public void AnyNumberPicksOneOfTheGenericCauses(int pick, int index) =>
        Assert.Equal(DeathCause.GenericMessage("P", index), DeathCause.GenericMessage("P", pick));

    [Fact]
    public void TheMessageNamesTheKillerOrElseThePlace()
    {
        Assert.Equal("HimeTest was killed by Oni", DeathCause.Message("HimeTest", "Oni", "Level_2_Area_3_Main"));
        Assert.Equal("HimeTest died in Level_2_Area_3_Main", DeathCause.Message("HimeTest", null, "Level_2_Area_3_Main"));
        Assert.Equal("HimeTest died in Level_2_Area_3_Main", DeathCause.Message("HimeTest", " ", "Level_2_Area_3_Main"));
    }

    [Theory]
    [InlineData("Enemy_Oni", 100f, 105f, 10f, "Enemy_Oni")] // a few seconds ago: the death animation
    [InlineData("Enemy_Oni", 100f, 110f, 10f, "Enemy_Oni")] // at the edge of the window
    [InlineData("Enemy_Oni", 100f, 111f, 10f, null)] // too long ago: a fall or a script killed the player
    [InlineData(null, 0f, 5f, 10f, null)] // nothing ever hit the player
    public void OnlyARecentHitCountsAsTheKiller(string source, float at, float now, float window, string expected) =>
        Assert.Equal(expected, DeathCause.Killer(source, at, now, window));

    [Theory]
    [InlineData(new[] { 400f, 9f, 100f }, 25f, 1)] // the nearest enemy within reach
    [InlineData(new[] { 900f, 2500f }, 25f, -1)] // nobody within 25 m
    [InlineData(new[] { 625f }, 25f, 0)] // exactly at the edge
    [InlineData(new float[0], 25f, -1)] // no enemies at all
    [InlineData(new[] { 4f, 4f }, 25f, 0)] // a tie goes to the first
    public void TheShooterOfALaserIsTheNearestEnemyInReach(float[] squared, float max, int expected) =>
        Assert.Equal(expected, DeathCause.Nearest(squared, max));
}
