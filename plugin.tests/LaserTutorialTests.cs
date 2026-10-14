using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class LaserTutorialTests
{
    [Theory]
    [InlineData("isUseSkill", true)]
    [InlineData("isUsePermission", false)] // the dialog stages share the check action
    [InlineData(null, false)]
    public void OnlyTheSkillFlagOfTheFarSideIsForced(string variable, bool forced)
    {
        Assert.Equal(forced, LaserTutorial.IsSkillFlag(variable));
    }

    [Theory]
    [InlineData("UtageSayDialog_Skill1", true)]
    [InlineData("UtageSayDialog_Skill2", true)]
    [InlineData("UtageSayDialog_Main", false)]
    [InlineData(null, false)]
    public void OnlyTheSkillSubtitlesAreRemoved(string dialog, bool removed)
    {
        Assert.Equal(removed, LaserTutorial.IsSkillDialog(dialog));
    }

    [Theory]
    [InlineData("LeftSideTipPage - 技能教學L1A2", true)]
    [InlineData("LeftSideTipPage - 彈刀", true)]
    [InlineData("LeftSideTipPage - 畫符", false)]
    [InlineData("LeftSideTipPage - 技能", false)]
    [InlineData(null, false)]
    public void OnlyTheSkillAndParryTipsAreRemoved(string tip, bool removed)
    {
        Assert.Equal(removed, LaserTutorial.IsTip(tip));
    }
}
