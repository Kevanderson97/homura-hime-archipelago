using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class TutorialSkipTests
{
    [Theory]
    [InlineData(true, "HubWorld_Main", true)]
    [InlineData(true, "Level_1_Area_2_Main", true)]
    [InlineData(true, "Level_0_Main", false)] // the first tutorial stays
    [InlineData(false, "HubWorld_Main", false)] // no randomizer on this save: the game as it is
    public void TutorialsAreSkippedInARandomizerGameExceptTheFirstOne(bool randomizer, string scene, bool skipped)
    {
        Assert.Equal(skipped, TutorialSkip.Applies(randomizer, scene));
    }

    [Theory]
    [InlineData("UseTutorialAction", true)]
    [InlineData("MainMenuHandler_TutorialEventAction", true)]
    [InlineData("MainMenuHandler_OmamoriTutorialInputLockAction", true)]
    [InlineData("GameplayFinishAction", false)]
    [InlineData("LockPlayer", false)]
    [InlineData(null, false)]
    public void ATutorialNodeIsRecognisedByItsActions(string action, bool tutorial)
    {
        Assert.Equal(tutorial, TutorialSkip.IsTutorialAction(action));
    }

    [Theory]
    [InlineData("UseCatchHeadMissionAction", true)] // the Kushinada head chase
    [InlineData("OnCatchHeadMissionSuccessAction", true)]
    [InlineData("UseTutorialAction", false)]
    [InlineData("GameplayFinishAction", false)]
    [InlineData(null, false)]
    public void ANodeThatRunsAMissionIsNotSkipped(string action, bool content)
    {
        Assert.Equal(content, TutorialSkip.IsContentAction(action));
    }

    [Theory]
    [InlineData("Skill_Blade Warp", true)]       // Phantom Blade: a location of the randomizer
    [InlineData("Skill_Blaze Rush", false)]
    [InlineData(null, false)]
    public void OnlyPhantomBladeIsTheLocationOfTheSkillScript(string definition, bool location)
    {
        Assert.Equal(location, HomuraHimeAP.Patches.SkillEquipPatch.IsBladeWarp(definition));
    }
}
