using System;
using System.Collections.Generic;

namespace HomuraHimeAP.Core;

/// <summary>
/// The traps that are on the field right now, each with the enemies it spawned. At most <see cref="Max"/> traps may be out at
/// once, and a trap only stops counting when every one of its enemies has been defeated, so a flood of traps (hundreds sent at
/// once) is let in six at a time, as the player clears them, instead of putting hundreds of enemies into a scene. The rest wait
/// in the trap queue. <typeparamref name="T"/> is what stands for one enemy (a component of the game, or a number in the tests).
/// </summary>
internal sealed class ActiveTraps<T>
{
    private readonly List<List<T>> _traps = new();

    internal int Max { get; }

    internal ActiveTraps(int max) => Max = max;

    /// <summary>How many traps are out (after <see cref="Prune"/>: how many still have an enemy alive).</summary>
    internal int Count => _traps.Count;

    /// <summary>Whether another trap may be let out.</summary>
    internal bool HasRoom => _traps.Count < Max;

    /// <summary>Takes in a trap that has just been let out with the enemies it spawned.</summary>
    internal void Add(IEnumerable<T> enemies) => _traps.Add(new List<T>(enemies));

    /// <summary>
    /// Forgets the enemies that have been defeated (<paramref name="forgotten"/> is told of each one) and the traps that have none
    /// left. Returns how many traps were cleared.
    /// </summary>
    internal int Prune(Func<T, bool> defeated, Action<T> forgotten = null)
    {
        var cleared = 0;
        for (var i = _traps.Count - 1; i >= 0; i--)
        {
            var enemies = _traps[i];
            for (var e = enemies.Count - 1; e >= 0; e--)
                if (defeated(enemies[e]))
                {
                    forgotten?.Invoke(enemies[e]);
                    enemies.RemoveAt(e);
                }
            if (enemies.Count > 0) continue;
            _traps.RemoveAt(i);
            cleared++;
        }

        return cleared;
    }

    /// <summary>Forgets every trap (a new game).</summary>
    internal void Clear() => _traps.Clear();
}
