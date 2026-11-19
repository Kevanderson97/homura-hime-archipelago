using System.Collections.Generic;

namespace HomuraHimeAP.Core;

/// <summary>
/// Lets at most <see cref="Max"/> things happen in any <see cref="WindowFrames"/> frames in a row, counted in frames, not seconds,
/// so a slow frame does not let a backlog through at once. Used for the traps: a flood of them (hundreds sent in one go) is read
/// in and queued at once, but each one is a handful of enemies appearing, and spreading them out keeps the frame rate up and the
/// game from being overrun. Nothing is dropped: whoever asked and was refused asks again on a later frame.
/// </summary>
internal sealed class SpawnLimiter
{
    private readonly Queue<int> _recent = new(); // the frames of the last permitted spawns, oldest first

    internal int Max { get; }
    internal int WindowFrames { get; }

    internal SpawnLimiter(int max, int windowFrames)
    {
        Max = max;
        WindowFrames = windowFrames;
    }

    /// <summary>Whether one more may happen in this frame (without counting it).</summary>
    internal bool CanAcquire(int frame)
    {
        Forget(frame);
        return _recent.Count < Max;
    }

    /// <summary>Counts one that happened in this frame.</summary>
    internal void Acquired(int frame)
    {
        Forget(frame);
        _recent.Enqueue(frame);
    }

    /// <summary>Starts over (a new game, a new scene).</summary>
    internal void Reset() => _recent.Clear();

    // an entry leaves the window once WindowFrames frames have passed since it, and when the frame counter went backwards
    private void Forget(int frame)
    {
        while (_recent.Count > 0 && (frame - _recent.Peek() >= WindowFrames || frame < _recent.Peek())) _recent.Dequeue();
    }
}
