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

/// <summary>What the seed asks for before the Finale opens (the world's option goal).</summary>
internal enum GoalMode
{
    /// <summary>The eight chapters and the Climax are all cleared.</summary>
    AllWorlds,

    /// <summary>Chapters 6, 7 and 8 are cleared (which takes the Soul Core of every boss), the rest does not matter.</summary>
    ReturnAllSoulCores,
}

/// <summary>What the level keys of the seed do (the world's option level_unlock_items).</summary>
internal enum UnlockMode
{
    /// <summary>No keys: a world opens when the one before it in the order is beaten.</summary>
    Off,

    /// <summary>Progressive Level Keys: a world needs the previous one beaten and as many keys as its place in the order.</summary>
    Linear,

    /// <summary>Every world has its own key, which opens it at once, whatever the order.</summary>
    Freeform,
}

/// <summary>
/// The rules of the world order of a randomizer game, kept free of game and network state so they can be tested on their
/// own: which chapters the Chapter Select may list and how the game's story-ordered hub progress is turned into "number of
/// cleared worlds". The nine worlds are the eight chapters and the Climax, which is a world of its own (never the first one).
/// </summary>
internal sealed class WorldRules
{
    internal const string Climax = "LevelFinalP0";
    internal const string Finale = "LevelFinalP4";

    /// <summary>Chapters 6, 7 and 8: the worlds that fight the bosses again and need their Soul Cores (see <see cref="SoulCores"/>).</summary>
    internal static readonly string[] SoulCoreWorlds = { "LevelFinalP1", "LevelFinalP2", "LevelFinalP3" };

    private readonly List<string> _order;
    private readonly UnlockMode _mode;
    private readonly HashSet<string> _finaleNeeds;

    /// <param name="order">The nine worlds in play order.</param>
    /// <param name="mode">What the level keys do.</param>
    /// <param name="finaleNeeds">The worlds that must be cleared to open the Finale; null means all nine.</param>
    internal WorldRules(IEnumerable<string> order, UnlockMode mode, IEnumerable<string> finaleNeeds = null)
    {
        _order = order.ToList();
        _mode = mode;
        _finaleNeeds = finaleNeeds?.ToHashSet();
    }

    /// <summary>
    /// Whether the cleared worlds open the Finale: all nine, or (the goal "Return All Soul Cores") Chapters 6, 7 and 8 whatever
    /// else has been cleared.
    /// </summary>
    internal bool FinaleOpen(ICollection<string> cleared) =>
        _finaleNeeds == null ? cleared.Count >= _order.Count : _finaleNeeds.All(cleared.Contains);

    /// <summary>The order of the unshuffled game, used when the slot did not send one.</summary>
    internal static IEnumerable<string> VanillaOrder => ChapterData.All.Where(c => c.Id != Finale).Select(c => c.Id);

    internal int WorldCount => _order.Count;

    /// <summary>
    /// The hub progress that stands for the cleared worlds: 2 each, as in the story (Chapter 8 cleared is 18 there as well,
    /// because the story's eight chapters and the climax make nine worlds).
    /// </summary>
    internal static int HubValueFor(ICollection<string> cleared) => 2 * cleared.Count;

    /// <summary>
    /// What the stored hub progress should be changed to when the hub loads, or null to leave it. The value that stands for the
    /// cleared worlds is right, and so is the one above it: the midway stage of the world being played (the game writes it at the
    /// middle of a chapter, e.g. after Reunion: Nozomi, and the hub shows it when the player comes back).
    /// </summary>
    internal static int? HubCorrection(int current, ICollection<string> cleared)
    {
        var target = HubValueFor(cleared);
        return current == target || current == target + 1 ? null : target;
    }

    /// <summary>
    /// The hub's mission board of a story chapter leads to that chapter. When the world order is not the story order the board that
    /// is up leads somewhere else than the world the player is due to play next; this returns the id of the world to enter instead
    /// (the game leads to its first area), or null when the board is right (or the game is over).
    /// </summary>
    internal string RedirectBoard(Chapter boardChapter, ICollection<string> cleared)
    {
        // with freeform keys the player picks the world (the gate opens the Chapter Select), there is no "next" one
        if (_mode == UnlockMode.Freeform) return null;
        if (boardChapter == null || boardChapter.Id == Finale || cleared.Count >= _order.Count) return null;
        var next = _order[cleared.Count];
        return boardChapter.Id == next ? null : next;
    }

    /// <summary>
    /// Whether a chapter of the Chapter Select may be listed. <paramref name="unlocked"/> holds the worlds whose own key was
    /// received (freeform keys), <paramref name="progressiveKeys"/> the Progressive Level Keys (linear keys).
    /// </summary>
    internal bool IsAvailable(Chapter chapter, ICollection<string> cleared, ICollection<string> unlocked, int progressiveKeys = 0)
    {
        if (chapter.Id == Finale) return FinaleOpen(cleared);

        var world = chapter.Id;
        if (cleared.Contains(world)) return true;

        var index = _order.IndexOf(world);
        if (index < 0) return false;

        // the climax is never the first world, whatever the unlock items allow
        if (world == Climax && cleared.Count == 0) return false;

        // in the play order the previous world must be beaten (not with freeform keys: the key alone opens a world)
        if (_mode != UnlockMode.Freeform && index > 0 && !cleared.Contains(_order[index - 1])) return false;
        return _mode switch
        {
            UnlockMode.Freeform => unlocked.Contains(world),
            UnlockMode.Linear => progressiveKeys >= index + 1,
            _ => true,
        };
    }

    /// <summary>
    /// The game writes its hub progress as a story value (odd = a chapter entered, even = a chapter cleared). In a
    /// randomizer game the value stored counts cleared worlds instead: 2 per world, plus 1 while inside an unfinished
    /// one; replays change nothing, clearing the finale moves the hub to the finished game (the goal itself is the Kill ending).
    /// </summary>
    internal HubWrite Decide(int value, ICollection<string> cleared)
    {
        var clears = value % 2 == 0;
        var chapter = ChapterData.All.FirstOrDefault(c => clears ? c.ClearedProgress == value : c.RequiredProgress + 1 == value);
        if (chapter == null) return HubWrite.None;

        var world = chapter.Id;
        if (!clears)
            return cleared.Contains(world) ? HubWrite.None : new HubWrite(chapter, null, false, HubValueFor(cleared) + 1);

        // clearing the finale ends the story for the hub, but the goal is not reported here: only the Kill ending completes it
        // (see KillEndingPatch), the game also gets here when Ann is spared
        if (chapter.Id == Finale) return new HubWrite(chapter, null, false, ChapterData.GameCompleteProgress);
        if (cleared.Contains(world)) return HubWrite.None;
        return new HubWrite(chapter, world, false, HubValueFor(cleared) + 2);
    }
}
