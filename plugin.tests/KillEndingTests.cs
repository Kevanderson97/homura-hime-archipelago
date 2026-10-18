using System.Collections.Generic;
using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class KillEndingTests
{
    [Theory]
    [InlineData("Kill", "EndingInteracterFSM", "WaitForChoise", true)]     // Kill chosen
    [InlineData("NotKill", "EndingInteracterFSM", "WaitForChoise", false)] // Ann is spared
    [InlineData("Kill", "EndingInteracterFSM", "HH Trigger anim", false)]  // a repeat once the kill is under way
    [InlineData("Kill", "SomeOtherFSM", "WaitForChoise", false)]           // another FSM using the same event name
    public void OnlyKillWhileTheChoiceIsWaitingCompletesTheGoal(string evt, string fsmObject, string state, bool kills)
    {
        Assert.Equal(kills, KillEnding.IsKill(evt, fsmObject, state));
    }

    [Theory]
    [InlineData(true, false, true)]   // the save played in this session is at the ending (also after Spare led back to the title screen)
    [InlineData(false, true, true)]   // the game was restarted: a save file of this slot and seed is at the ending
    [InlineData(false, false, false)] // no save of this game is at the ending: another seed's save, or an early save of this game
    public void TheGoalNeedsASaveOfThisGameAtTheEnding(bool playedAtFinale, bool fileAtFinale, bool counts)
    {
        Assert.Equal(counts, KillEnding.CountsForGoal(playedAtFinale, () => fileAtFinale));
    }

    [Fact]
    public void TheSaveFilesAreNotReadWhenThePlayedSaveIsAtTheEnding()
    {
        Assert.True(KillEnding.CountsForGoal(true, () => throw new System.InvalidOperationException("files were read")));
    }

    [Fact]
    public void TheServerMustHoldTheClearOfTheFinalBoss()
    {
        Assert.True(KillEnding.ServerHasFinalBoss(7300100, new HashSet<long> { 1, 7300100 }));
        Assert.False(KillEnding.ServerHasFinalBoss(7300100, new HashSet<long> { 1, 2 })); // not cleared in this game
        Assert.False(KillEnding.ServerHasFinalBoss(7300100, null));                        // nothing known of the server
    }

    [Fact]
    public void ASeedWithoutEncounterClearsHasNoFinalBossToCompare()
    {
        Assert.True(KillEnding.ServerHasFinalBoss(null, new HashSet<long>()));
    }

    [Theory]
    [InlineData(8, true, 8, true)]   // all eight worlds and the Climax
    [InlineData(7, true, 8, false)]  // a world still to clear
    [InlineData(8, false, 8, false)] // the Climax is still to clear
    [InlineData(0, false, 8, false)] // a new game
    public void ASaveIsAtTheEndingOnceEveryWorldAndTheClimaxAreCleared(int clearedWorlds, bool preludeCleared, int worldCount, bool atFinale)
    {
        var progress = new HomuraHimeAP.Core.Playthrough { PreludeCleared = preludeCleared };
        for (var i = 0; i < clearedWorlds; i++) progress.ClearedWorlds.Add("World" + i);
        Assert.Equal(atFinale, progress.ReachedFinale(worldCount));
    }
}
