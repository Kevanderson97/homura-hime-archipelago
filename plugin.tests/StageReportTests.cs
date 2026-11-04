using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class StageReportTests
{
    public StageReportTests() => TestEnvironment.Ensure();

    [Theory]
    [InlineData("Level1", "Level3", "Level3")]                  // Chapter 3 cleared first: the hub asks for Chapter 1's report
    [InlineData("Level2", "LevelFinalP0", "LevelFinalP0")]      // the Climax is a world of its own
    [InlineData("LevelFinalP2", "Level5", "Level5")]
    [InlineData("Level1", "Level1", null)]                      // the hub asked for the right one
    [InlineData("Level1", null, null)]                          // nothing was just cleared
    [InlineData("Level0", "Level3", null)]                      // the tutorial's report is never replaced
    [InlineData("HubWorld", "Level3", null)]
    [InlineData("Others", "Level3", null)]
    [InlineData("Level1", "Level0", null)]
    [InlineData(null, "Level3", null)]
    public void TheReportShownIsTheClearedWorldsNotTheStorys(string shown, string justCleared, string expected)
    {
        Assert.Equal(expected, StageReport.Replacement(shown, justCleared));
    }

    [Theory]
    [InlineData("Level1", true)]
    [InlineData("LevelFinalP4", true)]
    [InlineData("Level0", false)]
    [InlineData("HubWorld", false)]
    [InlineData(null, false)]
    public void OnlyChaptersAreChapters(string category, bool chapter) => Assert.Equal(chapter, StageReport.IsChapter(category));

    [Fact]
    public void TheClearedWorldWaitingForItsReportBelongsToTheSave()
    {
        var state = SessionState.Load("Pen", "seedP");
        state.PendingReport = "Level3";

        var saved = state.CapturePlaythrough();
        Assert.Equal("Level3", saved.PendingReport);

        state.ApplyPlaythrough(new Playthrough()); // another save
        Assert.Null(state.PendingReport);
        state.ApplyPlaythrough(saved);
        Assert.Equal("Level3", state.PendingReport);
    }
}
