using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Data;

namespace HomuraHimeAP.Core;

/// <summary>What a write of the game's hub progress means for a randomizer game (see <see cref="WorldRules.Decide"/>).</summary>
internal readonly struct HubWrite
{
    internal static readonly HubWrite None = new(null, null, false, null);

    /// <summary>The chapter the game thinks was entered or cleared.</summary>
    internal readonly Chapter Chapter;

    /// <summary>A world that was cleared for the first time by this write.</summary>
    internal readonly string NewWorld;

    /// <summary>The goal (the finale) was cleared.</summary>
    internal readonly bool Goal;

    /// <summary>The hub progress that should be stored instead, or null when the write is to be ignored.</summary>
    internal readonly int? HubValue;

    internal HubWrite(Chapter chapter, string newWorld, bool goal, int? hubValue)
    {
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

    /// <summary>The climax prelude is played as part of Chapter 6, it is not a world of its own.</summary>
    internal static string WorldOf(Chapter chapter) => chapter.Id == Prelude ? PreludeOwner : chapter.Id;

    /// <summary>
    /// The hub's mission board of a story chapter leads to that chapter. When the world order is not the story order the board that
    /// is up leads somewhere else than the world the player is due to play next; this returns the category (the first area of
    /// the next world) to enter instead, or null when the board is right (or the game is over). The climax world starts with
    /// its prelude.
    /// </summary>
    internal string RedirectBoard(Chapter boardChapter, ICollection<string> cleared)
    {
        if (boardChapter == null || boardChapter.Id == Finale || cleared.Count >= _order.Count) return null;
        var next = _order[cleared.Count];
        if (WorldOf(boardChapter) == next) return null;
        return next == PreludeOwner ? Prelude : next;
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
    /// one; replays change nothing, the climax prelude counts as part of Chapter 6, clearing the finale ends the game.
    /// </summary>
    internal HubWrite Decide(int value, ICollection<string> cleared)
    {
        var clears = value % 2 == 0;
        var chapter = ChapterData.All.FirstOrDefault(c => clears ? c.ClearedProgress == value : c.RequiredProgress + 1 == value);
        if (chapter == null) return HubWrite.None;

        var world = WorldOf(chapter);
        if (!clears)
            return cleared.Contains(world) ? HubWrite.None : new HubWrite(chapter, null, false, 2 * cleared.Count + 1);

        if (chapter.Id == Prelude) return HubWrite.None;
        if (chapter.Id == Finale) return new HubWrite(chapter, null, true, ChapterData.GameCompleteProgress);
        if (cleared.Contains(world)) return HubWrite.None;
        return new HubWrite(chapter, world, false, 2 * (cleared.Count + 1));
    }
}
