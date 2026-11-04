using System;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class TrapPlacementTests
{
    private static double Length((float X, float Y, float Z) o) => Math.Sqrt(o.X * o.X + o.Y * o.Y + o.Z * o.Z);

    [Fact]
    public void EveryPointIsBetweenSixAndTwentyMetresAway()
    {
        var random = new Random(1);
        for (var i = 0; i < 5000; i++)
        {
            var distance = Length(TrapPlacement.Offset(random));
            Assert.InRange(distance, 6 - 0.001, 20 + 0.001);
        }
    }

    [Fact]
    public void NothingIsPlacedBelowThePlayer()
    {
        var random = new Random(2);
        for (var i = 0; i < 5000; i++) Assert.True(TrapPlacement.Offset(random).Y >= 0);
    }

    [Fact]
    public void PointsSpreadAroundAndOverTheWholeRange()
    {
        var random = new Random(3);
        double near = double.MaxValue, far = 0, minX = 0, maxX = 0, minZ = 0, maxZ = 0;
        for (var i = 0; i < 5000; i++)
        {
            var o = TrapPlacement.Offset(random);
            var d = Length(o);
            (near, far) = (Math.Min(near, d), Math.Max(far, d));
            (minX, maxX, minZ, maxZ) = (Math.Min(minX, o.X), Math.Max(maxX, o.X), Math.Min(minZ, o.Z), Math.Max(maxZ, o.Z));
        }

        Assert.True(near < 6.5 && far > 19.5);
        Assert.True(minX < -15 && maxX > 15 && minZ < -15 && maxZ > 15);
    }

    [Fact]
    public void AGroupStaysTogetherAndEveryMemberIsBetweenFourAndEightMetresAway()
    {
        var random = new Random(4);
        for (var i = 0; i < 5000; i++)
        {
            var group = TrapPlacement.Group(random, 3);
            Assert.Equal(3, group.Length);
            foreach (var (x, z) in group) Assert.InRange(Math.Sqrt(x * x + z * z), 4 - 0.001, 8 + 0.001);

            for (var a = 0; a < 3; a++)
                for (var b = a + 1; b < 3; b++)
                {
                    var apart = Math.Sqrt(Math.Pow(group[a].X - group[b].X, 2) + Math.Pow(group[a].Z - group[b].Z, 2));
                    Assert.InRange(apart, 1, 3.1); // a trio on a ring of 1.5 m: about 2.6 m apart
                }
        }
    }

    [Fact]
    public void LevelOffsetsStayAtThePlayersHeight()
    {
        var random = new Random(5);
        for (var i = 0; i < 1000; i++) Assert.Equal(0f, TrapPlacement.Offset(random, 0f).Y, 3);
    }

    [Theory]
    [InlineData("Level_1_Area_1_Main", true)]
    [InlineData("Level_3_Area_5_Boss_Main", true)]
    [InlineData("Level_FinalP2_L3_A5_Main", true)]
    [InlineData("Level3_SecretZone_Main", true)]
    [InlineData("HubWorld_Main", false)]
    [InlineData("HubWorld_PostGame_Main", false)]
    [InlineData("HubWorld_TrainingGround_Main", false)]
    [InlineData("StartScreen", false)]
    [InlineData("LoadingScene", false)]
    [InlineData("MainCore", false)]
    [InlineData(null, false)]
    public void TrapsOnlyGoOffInLevels(string scene, bool allowed)
    {
        Assert.Equal(allowed, TrapPlacement.IsLevel(scene));
    }
}
