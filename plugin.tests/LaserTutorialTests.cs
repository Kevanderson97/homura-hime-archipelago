using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class LaserTutorialTests
{
    [Theory]
    [InlineData("Level_1_Area_2_Main", "Enemy_Splitter_A_Tutorial (木樁) Variant", true)]
    [InlineData("Level_1_Area_2_Main", "Enemy_Splitter_A_Default (1)", false)] // the ordinary enemies of the area
    [InlineData("Level_1_Area_2_Main", "Enemy_FishHead_Default", false)]
    [InlineData("Level_1_Area_3_Main", "Enemy_Splitter_A_Tutorial (木樁) Variant", false)] // only in this area
    [InlineData("Level_1_Area_2_Main", null, false)]
    public void OnlyTheTutorialEnemyNextToTheGapDismissesTheTip(string scene, string name, bool dismisses)
    {
        Assert.Equal(dismisses, LaserTutorialPatch.IsLaserEnemy(scene, name));
    }
}
