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
    public void VanillaOrderHasTheEightWorlds()
    {
        Assert.Equal(new[] { "Level1", "Level2", "Level3", "Level4", "Level5", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" }, Vanilla);
    }

    [Fact]
    public void ModeA_VanillaOrder_NextWorldAfterPreviousIsCleared()
    {
        var rules = new WorldRules(Vanilla, unlockItems: false, randomized: false);
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set(), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level2"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level2"), Set("Level1"), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level3"), Set("Level1"), Set()));
    }

    [Fact]
    public void ModeB_RandomOrder_FollowsTheRandomOrder()
    {
        var order = new[] { "Level3", "Level1", "Level5", "Level2", "Level4", "LevelFinalP2", "LevelFinalP1", "LevelFinalP3" };
        var rules = new WorldRules(order, unlockItems: false, randomized: true);
        Assert.True(rules.IsAvailable(Chapter("Level3"), Set(), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level1"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set("Level3"), Set()));
        Assert.False(rules.IsAvailable(Chapter("Level5"), Set("Level3"), Set()));
    }

    [Fact]
    public void ModeC_VanillaWithItems_NeedsPreviousClearAndTheItem()
    {
        var rules = new WorldRules(Vanilla, unlockItems: true, randomized: false);
        Assert.False(rules.IsAvailable(Chapter("Level1"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level1"), Set(), Set("Level1")));
        Assert.False(rules.IsAvailable(Chapter("Level2"), Set(), Set("Level2")));
        Assert.False(rules.IsAvailable(Chapter("Level2"), Set("Level1"), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level2"), Set("Level1"), Set("Level2")));
    }

    [Fact]
    public void ModeD_RandomizedWithItems_TheItemAloneOpensAWorld()
    {
        var order = new[] { "Level3", "Level1", "Level5", "Level2", "Level4", "LevelFinalP2", "LevelFinalP1", "LevelFinalP3" };
        var rules = new WorldRules(order, unlockItems: true, randomized: true);
        Assert.False(rules.IsAvailable(Chapter("Level5"), Set(), Set()));
        Assert.True(rules.IsAvailable(Chapter("Level5"), Set(), Set("Level5")));
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP3"), Set(), Set("LevelFinalP3")));
    }

    [Fact]
    public void ClearedWorldsStayAvailable()
    {
        var rules = new WorldRules(Vanilla, unlockItems: true, randomized: false);
        Assert.True(rules.IsAvailable(Chapter("Level2"), Set("Level2"), Set()));
    }

    [Fact]
    public void ClimaxPreludeFollowsChapterSixAndFinaleNeedsAllWorlds()
    {
        var rules = new WorldRules(Vanilla, unlockItems: false, randomized: false);
        var before = Set("Level1", "Level2", "Level3", "Level4", "Level5");
        Assert.Equal(rules.IsAvailable(Chapter("LevelFinalP1"), before, Set()), rules.IsAvailable(Chapter("LevelFinalP0"), before, Set()));
        Assert.False(rules.IsAvailable(Chapter("LevelFinalP4"), before, Set()));
        Assert.True(rules.IsAvailable(Chapter("LevelFinalP4"), new HashSet<string>(Vanilla), Set()));
    }

    // ---------------------------------------------------------------- hub progress

    [Fact]
    public void EnteringAnUnfinishedWorldStoresTwiceTheClearedCountPlusOne()
    {
        var rules = new WorldRules(Vanilla, false, false);
        Assert.Equal(1, rules.Decide(1, Set()).HubValue);                      // Level 1 entered
        Assert.Equal(3, rules.Decide(3, Set("Level1")).HubValue);              // Level 2 entered
        Assert.Equal(1, rules.Decide(7, Set()).HubValue);                      // Level 4 entered first (random order): count stays 0
    }

    [Fact]
    public void ClearingAWorldCountsItOnce()
    {
        var rules = new WorldRules(Vanilla, false, false);
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
        var rules = new WorldRules(Vanilla, false, false);
        var cleared = Set("Level1");
        Assert.Null(rules.Decide(2, cleared).HubValue);                        // Level 1 cleared again
        Assert.Null(rules.Decide(1, cleared).HubValue);                        // Level 1 entered again
        Assert.Null(rules.Decide(0, cleared).HubValue);                        // unknown write (tutorial end)
    }

    [Fact]
    public void PreludeAndFinaleAreSpecial()
    {
        var rules = new WorldRules(Vanilla, false, false);
        Assert.Null(rules.Decide(12, Set()).HubValue);                         // Climax cleared: part of Chapter 6
        Assert.Equal(1, rules.Decide(11, Set()).HubValue);                     // Climax entered counts as entering
        var finale = rules.Decide(20, new HashSet<string>(Vanilla));
        Assert.True(finale.Goal);
        Assert.Equal(ChapterData.GameCompleteProgress, finale.HubValue);
    }

    [Fact]
    public void HubBoard_LeadsToTheNextWorldOfTheSeed()
    {
        // a seed that plays Chapter 3 first, then Chapter 1
        var order = new[] { "Level3", "Level1", "Level2", "Level4", "Level5", "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" };
        var rules = new WorldRules(order, unlockItems: false, randomized: true);

        Assert.Equal("Level3", rules.RedirectBoard(Chapter("Level1"), Set())); // the first board of the story is up, the seed starts with 3
        Assert.Null(rules.RedirectBoard(Chapter("Level3"), Set()));             // already the right one
        Assert.Equal("Level1", rules.RedirectBoard(Chapter("Level2"), Set("Level3")));
    }

    [Fact]
    public void HubBoard_ClimaxWorldStartsWithItsPrelude()
    {
        var order = new[] { "LevelFinalP1", "Level1", "Level2", "Level3", "Level4", "Level5", "LevelFinalP2", "LevelFinalP3" };
        var rules = new WorldRules(order, unlockItems: false, randomized: true);

        Assert.Equal("LevelFinalP0", rules.RedirectBoard(Chapter("Level1"), Set()));
        Assert.Null(rules.RedirectBoard(Chapter("LevelFinalP0"), Set())); // the prelude board of that world is right
        Assert.Null(rules.RedirectBoard(Chapter("LevelFinalP1"), Set()));
    }

    [Fact]
    public void HubBoard_VanillaOrderIsLeftAlone()
    {
        var rules = new WorldRules(Vanilla, unlockItems: false, randomized: false);
        for (var cleared = 0; cleared < 5; cleared++)
            Assert.Null(rules.RedirectBoard(Chapter(Vanilla[cleared]), Set(Vanilla.Take(cleared).ToArray())));
    }

    [Fact]
    public void HubBoard_NothingToRedirectOnceEveryWorldIsCleared()
    {
        var rules = new WorldRules(Vanilla, unlockItems: false, randomized: true);
        Assert.Null(rules.RedirectBoard(Chapter("Level1"), Set(Vanilla)));
        Assert.Null(rules.RedirectBoard(Chapter("LevelFinalP4"), Set()));
    }
}
