using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Core;
using HomuraHimeAP.Data;
using Xunit;

namespace HomuraHimeAP.Tests;

public class WorldRulesTests
{
    private static readonly string[] Vanilla = WorldRules.VanillaOrder.ToArray();

    private static Chapter Chapter(string id)
    {
        Assert.True(ChapterData.TryGetChapter(id, out var chapter), id);
        return chapter;
    }

    private static HashSet<string> Set(params string[] items) => new(items);

    [Fact]
    public void VanillaOrderHasTheNineWorlds()
    {
        Assert.Equal(new[] { "Level1", "Level2", "Level3", "Level4", "Level5", "LevelFinalP0", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" }, Vanilla);
    }

    [Fact]
    public void ModeA_VanillaOrder_NextWorldAfterPreviousIsCleared()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set(), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level2"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level2"), Set("Level1"), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level3"), Set("Level1"), Set()));
    }

    [Fact]
    public void ModeB_RandomOrder_FollowsTheRandomOrder()
    {
        var order = new[] { "Level3", "Level1", "Level5", "Level2", "Level4", "LevelFinalP2", "LevelFinalP0", "LevelFinalP1", "LevelFinalP3" };
        var rules = new WorldRules(order, UnlockMode.Off);
        Assert.True(rules.IsAvailable(Chapter("Level3"), Set(), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level1"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set("Level3"), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level5"), Set("Level3"), Set()));
    }

    [Fact]
    public void ModeC_LinearKeys_NeedThePreviousClearAndOneKeyPerPlaceInTheOrder()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Linear);
        Assert.False(rules.IsAvailable(Chapter("Level1"), Set(), Set(), progressiveKeys: 0));
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set(), Set(), progressiveKeys: 1));
        Assert.False(rules.IsAvailable(Chapter("Level2"), Set(), Set(), progressiveKeys: 2), "the previous world is not beaten");
        Assert.False(rules.IsAvailable(Chapter("Level2"), Set("Level1"), Set(), progressiveKeys: 1), "the second key is missing");
        Assert.True(rules.IsAvailable(Chapter("Level2"), Set("Level1"), Set(), progressiveKeys: 2));
        Assert.False(rules.IsAvailable(Chapter("Level3"), Set("Level1", "Level2"), Set("Level3"), progressiveKeys: 2), "a single key means nothing here");
        Assert.True(rules.IsAvailable(Chapter("Level3"), Set("Level1", "Level2"), Set(), progressiveKeys: 3));
    }

    [Fact]
    public void ModeC_LinearKeysFollowTheRandomOrderStrictly()
    {
        var order = new[] { "Level3", "Level1", "Level5", "Level2", "Level4", "LevelFinalP2", "LevelFinalP0", "LevelFinalP1", "LevelFinalP3" };
        var rules = new WorldRules(order, UnlockMode.Linear);
        // all nine keys and Level 3 beaten: Level 1 is next, nothing further, whatever the keys allow
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set("Level3"), Set(), progressiveKeys: 9));
        Assert.False(rules.IsAvailable(Chapter("Level5"), Set("Level3"), Set(), progressiveKeys: 9), "Level 1 is still to be beaten");
        Assert.False(rules.IsAvailable(Chapter("Level1"), Set(), Set(), progressiveKeys: 9), "Level 3 is still to be beaten");
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP3"), new HashSet<string>(order.Take(8)), Set(), progressiveKeys: 9));
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP3"), new HashSet<string>(order.Take(8)), Set(), progressiveKeys: 8));
    }

    [Fact]
    public void ModeD_FreeformKeys_VanillaOrderDoesNotMatterEither()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Freeform);
        Assert.False(rules.IsAvailable(Chapter("Level1"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set(), Set("Level1")));
        Assert.True(rules.IsAvailable(Chapter("Level4"), Set(), Set("Level4")), "the key alone opens a world");
        Assert.False(rules.IsAvailable(Chapter("Level2"), Set("Level1"), Set()));
    }

    [Fact]
    public void ModeD_RandomizedWithItems_TheItemAloneOpensAWorld()
    {
        var order = new[] { "Level3", "Level1", "Level5", "Level2", "Level4", "LevelFinalP2", "LevelFinalP0", "LevelFinalP1", "LevelFinalP3" };
        var rules = new WorldRules(order, UnlockMode.Freeform);
        Assert.False(rules.IsAvailable(Chapter("Level5"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level5"), Set(), Set("Level5")));
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP3"), Set(), Set("LevelFinalP3")));
    }

    [Fact]
    public void ClearedWorldsStayAvailable()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Freeform);
        Assert.True(rules.IsAvailable(Chapter("Level2"), Set("Level2"), Set()));
    }

    [Fact]
    public void TheClimaxIsAWorldOfItsOwnAndChapterSixFollowsItsClear()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var five = Set(Vanilla.Take(5).ToArray());
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP0"), five, Set()));
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP1"), five, Set()), "Chapter 6 waits for the Climax");
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP1"), Set(Vanilla.Take(6).ToArray()), Set()));
    }

    [Fact]
    public void TheClimaxNeedsAClearedWorldEvenWhenItsKeyOpensItFirst()
    {
        var order = new[] { "Level3", "Level1", "Level5", "Level2", "Level4", "LevelFinalP2", "LevelFinalP0", "LevelFinalP1", "LevelFinalP3" };
        var rules = new WorldRules(order, UnlockMode.Freeform);
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP0"), Set(), Set("LevelFinalP0")), "never the first world");
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP0"), Set("Level3"), Set("LevelFinalP0")));
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP0"), Set("Level3"), Set()), "still needs its key");
    }

    [Fact]
    public void TheFinaleNeedsAllNineWorlds()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP4"), Set(Vanilla.Take(8).ToArray()), Set()));
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP4"), new HashSet<string>(Vanilla), Set()));
    }

    // ---------------------------------------------------------------- hub progress

    [Fact]
    public void EnteringAnUnfinishedWorldStoresTwiceTheClearedCountPlusOne()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        Assert.Equal(1, rules.Decide(1, Set()).HubValue);                      // Level 1 entered
        Assert.Equal(3, rules.Decide(3, Set("Level1")).HubValue);              // Level 2 entered
        Assert.Equal(1, rules.Decide(7, Set()).HubValue);                      // Level 4 entered first (random order): count stays 0
    }

    [Fact]
    public void ClearingAWorldCountsItOnce()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var first = rules.Decide(6, Set());                                    // Level 3 cleared first
        Assert.Equal("Level3", first.NewWorld);
        Assert.Equal(2, first.HubValue);

        var second = rules.Decide(2, Set("Level3"));                           // then Level 1
        Assert.Equal("Level1", second.NewWorld);
        Assert.Equal(4, second.HubValue);
    }

    [Fact]
    public void ReplaysChangeNothing()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var cleared = Set("Level1");
        Assert.Null(rules.Decide(2, cleared).HubValue);                        // Level 1 cleared again
        Assert.Null(rules.Decide(1, cleared).HubValue);                        // Level 1 entered again
        Assert.Null(rules.Decide(0, cleared).HubValue);                        // unknown write (tutorial end)
    }

    [Fact]
    public void ClearingTheClimaxCountsItAsAWorld()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var five = Set(Vanilla.Take(5).ToArray());                             // the story state before the Climax: Level 5 cleared (hub 10)
        var write = rules.Decide(12, five);
        Assert.Equal("LevelFinalP0", write.NewWorld);
        Assert.Equal(12, write.HubValue);                                      // the game's own value of "Climax cleared"
        Assert.Equal(11, rules.Decide(11, five).HubValue);                     // entered
        Assert.Null(rules.Decide(12, Set(Vanilla.Take(6).ToArray())).HubValue); // replaying it later changes nothing
    }

    [Fact]
    public void ClearingTheClimaxFirstInAnotherOrderCountsOneWorld()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var write = rules.Decide(12, Set("Level2"));
        Assert.Equal("LevelFinalP0", write.NewWorld);
        Assert.Equal(4, write.HubValue);
    }

    [Fact]
    public void TheFinaleIsNotTheGoal()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var finale = rules.Decide(20, new HashSet<string>(Vanilla));
        Assert.False(finale.Goal);                                             // the goal is the Kill ending, not the credits
        Assert.Equal(ChapterData.GameCompleteProgress, finale.HubValue);
    }

    [Fact]
    public void HubBoard_LeadsToTheNextWorldOfTheSeed()
    {
        // a seed that plays Chapter 3 first, then Chapter 1
        var order = new[] { "Level3", "Level1", "Level2", "Level4", "Level5", "LevelFinalP0", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" };
        var rules = new WorldRules(order, UnlockMode.Off);

        Assert.Equal("Level3", rules.RedirectBoard(Chapter("Level1"), Set())); // the first board of the story is up, the seed starts with 3
        Assert.Null(rules.RedirectBoard(Chapter("Level3"), Set()));             // already the right one
        Assert.Equal("Level1", rules.RedirectBoard(Chapter("Level2"), Set("Level3")));
    }

    [Fact]
    public void HubBoard_LeadsToTheClimaxWhenItsTurnComes()
    {
        var order = new[] { "Level1", "LevelFinalP0", "Level2", "Level3", "Level4", "Level5", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" };
        var rules = new WorldRules(order, UnlockMode.Off);

        Assert.Equal("LevelFinalP0", rules.RedirectBoard(Chapter("Level2"), Set("Level1"))); // the board of Chapter 2 is up, the Climax is next
        Assert.Null(rules.RedirectBoard(Chapter("LevelFinalP0"), Set("Level1")));
        Assert.Equal("Level2", rules.RedirectBoard(Chapter("LevelFinalP1"), Set("Level1", "LevelFinalP0")));
    }

    [Fact]
    public void HubBoard_FreeformKeysNeverRedirectThePlayerPicksTheWorld()
    {
        var order = new[] { "Level3", "Level1", "Level2", "Level4", "Level5", "LevelFinalP0", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" };
        var rules = new WorldRules(order, UnlockMode.Freeform);
        Assert.Null(rules.RedirectBoard(Chapter("Level1"), Set()));
        Assert.Null(rules.RedirectBoard(Chapter("Level2"), Set("Level3")));
    }

    [Fact]
    public void HubBoard_VanillaOrderIsLeftAlone()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        for (var cleared = 0; cleared < Vanilla.Length; cleared++)
            Assert.Null(rules.RedirectBoard(Chapter(Vanilla[cleared]), Set(Vanilla.Take(cleared).ToArray())));
    }

    [Fact]
    public void HubBoard_NothingToRedirectOnceEveryWorldIsCleared()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        Assert.Null(rules.RedirectBoard(Chapter("Level1"), Set(Vanilla)));
        Assert.Null(rules.RedirectBoard(Chapter("LevelFinalP4"), Set()));
    }

    [Theory]
    [InlineData(5, 10)]    // Level 5 cleared
    [InlineData(6, 12)]    // the Climax cleared
    [InlineData(7, 14)]    // Chapter 6 cleared
    [InlineData(8, 16)]    // Chapter 7 cleared
    [InlineData(9, 18)]    // Chapter 8 cleared: the hub of the finale
    public void TheHubStaysInStepWithTheStory(int worlds, int hub)
    {
        Assert.Equal(hub, WorldRules.HubValueFor(Set(Vanilla.Take(worlds).ToArray())));
    }

    [Fact]
    public void ChaptersSixToEightWriteTheStoryValues()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var seven = Set(Vanilla.Take(7).ToArray());                              // Level 1-5, the Climax, Chapter 6
        Assert.Equal(15, rules.Decide(15, seven).HubValue);                      // Chapter 7 reached its midway point (after Nozomi)
        var cleared = rules.Decide(16, seven);                                   // Chapter 7 cleared (after Yumiko and Kumiko)
        Assert.Equal("LevelFinalP2", cleared.NewWorld);
        Assert.Equal(16, cleared.HubValue);
        var six = Set(Vanilla.Take(6).ToArray());
        Assert.Equal(14, rules.Decide(14, six).HubValue);                        // Chapter 6 cleared
        Assert.Equal(13, rules.Decide(13, six).HubValue);                        // Chapter 6 entered
    }

    [Fact]
    public void ChapterEightHasItsOwnMidwayStage()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Off);
        var eight = Set(Vanilla.Take(8).ToArray());
        Assert.Equal(17, rules.Decide(17, eight).HubValue);                      // midway: Reunion: Agasa done, Mamiya not yet
        var end = rules.Decide(18, eight);                                       // Reunion: Mamiya done
        Assert.Equal("LevelFinalP3", end.NewWorld);
        Assert.Equal(18, end.HubValue);
    }

    [Theory]
    [InlineData(16, 8, null)]     // eight worlds: the hub is right
    [InlineData(17, 8, null)]     // the midway stage of the world being played is kept
    [InlineData(16, 9, 18)]       // a save that is a stage behind is moved on
    [InlineData(18, 9, null)]
    [InlineData(19, 9, null)]
    [InlineData(20, 9, 18)]       // anything else is corrected
    public void TheHubIsCorrectedOnlyWhenItIsOffTheClearedWorlds(int current, int worlds, int? expected)
    {
        Assert.Equal(expected, WorldRules.HubCorrection(current, Set(Vanilla.Take(worlds).ToArray())));
    }

    [Fact]
    public void TheFinaleNeedsEveryWorldUnlessTheGoalSaysOtherwise()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Freeform);
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP4"), Set(WorldRules.SoulCoreWorlds), Set()));
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP4"), Set(Vanilla.Take(8).ToArray()), Set()));
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP4"), Set(Vanilla), Set()));
    }

    [Fact]
    public void WithTheSoulCoreGoalTheFinaleNeedsChaptersSixToEightOnly()
    {
        var rules = new WorldRules(Vanilla, UnlockMode.Freeform, WorldRules.SoulCoreWorlds);
        var finale = Chapter("LevelFinalP4");
        Assert.False(rules.IsAvailable(finale, Set(), Set()));
        Assert.False(rules.IsAvailable(finale, Set("LevelFinalP1", "LevelFinalP2"), Set())); // Chapter 8 still to clear
        Assert.False(rules.IsAvailable(finale, Set(Vanilla.Take(8).ToArray()), Set()));       // everything but Chapter 8
        Assert.True(rules.IsAvailable(finale, Set(WorldRules.SoulCoreWorlds), Set()));        // Chapters 1-5 and the Climax do not matter
        Assert.True(rules.IsAvailable(finale, Set(Vanilla), Set()));
    }

    [Fact]
    public void TheSoulCoreWorldsAreChaptersSixToEight() =>
        Assert.Equal(new[] { "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" }, WorldRules.SoulCoreWorlds);
}
