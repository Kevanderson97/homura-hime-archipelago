using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Data;

namespace HomuraHimeAP.Core;

/// <summary>What a write of the game's hub progress means for a randomizer game (see <see cref="WorldRules.Decide"/>).</summary>
internal readonly struct HubWrite
{
    internal static readonly HubWrite None = new(null, null, false, null);

    /// <summary>The climax prelude (played as the start of Chapter 6) was cleared by this write.</summary>
    internal readonly bool PreludeCleared;

    /// <summary>The chapter the game thinks was entered or cleared.</summary>
    internal readonly Chapter Chapter;

    /// <summary>A world that was cleared for the first time by this write.</summary>
    internal readonly string NewWorld;

    /// <summary>The goal (the finale) was cleared.</summary>
    internal readonly bool Goal;

    /// <summary>The hub progress that should be stored instead, or null when the write is to be ignored.</summary>
    internal readonly int? HubValue;

    internal HubWrite(Chapter chapter, string newWorld, bool goal, int? hubValue, bool preludeCleared = false)
    {
        PreludeCleared = preludeCleared;
        Chapter = chapter;
        NewWorld = newWorld;
        Goal = goal;
        HubValue = hubValue;
    }
}

/// <summary>
/// The rules of the world order of a randomizer game, kept free of game and network state so they can be tested on their
/// own: which chapters the Chapter Select may list (the four modes of plan section 2.9) and how the game's story-ordered
/// hub progress is turned into "number of cleared worlds".
/// </summary>
internal sealed class WorldRules
{
    internal const string Prelude = "LevelFinalP0";
    internal const string PreludeOwner = "LevelFinalP1";
    internal const string Finale = "LevelFinalP4";

    private readonly List<string> _order;
    private readonly bool _unlockItems;
    private readonly bool _randomized;

    /// <param name="order">The eight worlds in play order.</param>
    /// <param name="unlockItems">Worlds need their "Chapter N Unlock" item.</param>
    /// <param name="randomized">The order is shuffled (with unlock items the item alone opens a world).</param>
    internal WorldRules(IEnumerable<string> order, bool unlockItems, bool randomized)
    {
        _order = order.ToList();
        _unlockItems = unlockItems;
        _randomized = randomized;
    }

    /// <summary>The order of the unshuffled game, used when the slot did not send one.</summary>
    internal static IEnumerable<string> VanillaOrder =>
        ChapterData.All.Where(c => c.Id != Prelude && c.Id != Finale).Select(c => c.Id);

    internal int WorldCount => _order.Count;

    /// <summary>
    /// The climax prelude is one more step of the games hub progress than the eight worlds: after it, every value is two higher than
    /// the number of cleared worlds alone would give (Chapter 6 cleared is 14 in the story, Chapter 7 16, Chapter 8 18). It counts as
    /// cleared when the flag says so or when its world is cleared (saves from before the flag).
    /// </summary>
    private static int PreludeOffset(ICollection<string> cleared, bool preludeCleared) =>
        preludeCleared || cleared.Contains(PreludeOwner) ? 2 : 0;

    /// <summary>The hub progress that stands for the cleared worlds (2 each) and the cleared prelude.</summary>
    internal static int HubValueFor(ICollection<string> cleared, bool preludeCleared) =>
        2 * cleared.Count + PreludeOffset(cleared, preludeCleared);

    /// <summary>
    /// What the stored hub progress should be changed to when the hub loads, or null to leave it. The value that stands for the
    /// cleared worlds is right, and so is the one above it: the midway stage of the world being played (the game writes it at the
    /// middle of a chapter, e.g. after Reunion: Nozomi, and the hub shows it when the player comes back).
    /// </summary>
    internal static int? HubCorrection(int current, ICollection<string> cleared, bool preludeCleared)
    {
        var target = HubValueFor(cleared, preludeCleared);
        return current == target || current == target + 1 ? null : target;
    }

    /// <summary>The climax prelude is played as part of Chapter 6, it is not a world of its own.</summary>
    internal static string WorldOf(Chapter chapter) => chapter.Id == Prelude ? PreludeOwner : chapter.Id;

    /// <summary>
    /// The hub's mission board of a story chapter leads to that chapter. When the world order is not the story order the board that
    /// is up leads somewhere else than the world the player is due to play next; this returns the category (the first area of
    /// the next world) to enter instead, or null when the board is right (or the game is over). The climax world starts with
    /// its prelude.
    /// </summary>
    internal string RedirectBoard(Chapter boardChapter, ICollection<string> cleared, bool preludeCleared = false)
    {
        if (boardChapter == null || boardChapter.Id == Finale || cleared.Count >= _order.Count) return null;
        var next = _order[cleared.Count];
        if (WorldOf(boardChapter) == next) return null;
        // the prelude is the start of its world; once it is cleared the board leads on to the world proper
        return next == PreludeOwner && !preludeCleared ? Prelude : next;
    }

    /// <summary>Whether a chapter of the Chapter Select may be listed.</summary>
    internal bool IsAvailable(Chapter chapter, ICollection<string> cleared, ICollection<string> unlocked)
    {
        if (chapter.Id == Finale) return cleared.Count >= _order.Count;

        var world = WorldOf(chapter);
        if (cleared.Contains(world)) return true;

        var index = _order.IndexOf(world);
        if (index < 0) return false;

        var itemsOnly = _unlockItems && _randomized;
        if (!itemsOnly && index > 0 && !cleared.Contains(_order[index - 1])) return false;
        return !_unlockItems || unlocked.Contains(world);
    }

    /// <summary>
    /// The game writes its hub progress as a story value (odd = a chapter entered, even = a chapter cleared). In a
    /// randomizer game the value stored counts cleared worlds instead: 2 per world, plus 1 while inside an unfinished
    /// one; replays change nothing, the climax prelude counts as part of Chapter 6, clearing the finale moves the hub to the finished game (the goal itself is the Kill ending).
    /// </summary>
    internal HubWrite Decide(int value, ICollection<string> cleared, bool preludeCleared = false)
    {
        var offset = PreludeOffset(cleared, preludeCleared);
        var clears = value % 2 == 0;
        var chapter = ChapterData.All.FirstOrDefault(c => clears ? c.ClearedProgress == value : c.RequiredProgress + 1 == value);
        if (chapter == null) return HubWrite.None;

        var world = WorldOf(chapter);
        if (!clears)
            return cleared.Contains(world) ? HubWrite.None : new HubWrite(chapter, null, false, 2 * cleared.Count + offset + 1);

        // the prelude is no world of its own, but the hub changes at its end (Ann speaks of what happened, the gate leads on):
        // the hub shows the state of the world after it, which is what clearing the world will write anyway
        if (chapter.Id == Prelude)
            return cleared.Contains(PreludeOwner) ? HubWrite.None : new HubWrite(chapter, null, false, 2 * cleared.Count + 2, preludeCleared: true);
        // clearing the finale ends the story for the hub, but the goal is not reported here: only the Kill ending completes it
        // (see KillEndingPatch), the game also gets here when Ann is spared
        if (chapter.Id == Finale) return new HubWrite(chapter, null, false, ChapterData.GameCompleteProgress);
        if (cleared.Contains(world)) return HubWrite.None;
        // clearing the world of the prelude counts the prelude too, whether or not its clear was seen
        return new HubWrite(chapter, world, false, 2 * (cleared.Count + 1) + (world == PreludeOwner ? 2 : offset));
    }
}
