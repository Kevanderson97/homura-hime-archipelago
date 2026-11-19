using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using Xunit;

namespace HomuraHimeAP.Tests;

public class SpawnLimiterTests
{
    /// <summary>A queue of traps, one asked for every frame, the way TrapSpawner asks: returns the frames in which each went off.</summary>
    private static List<int> Release(SpawnLimiter limiter, int traps, int firstFrame = 100, int maxFrames = 10_000)
    {
        var frames = new List<int>();
        var waiting = traps;
        for (var frame = firstFrame; waiting > 0 && frame < firstFrame + maxFrames; frame++)
        {
            if (!limiter.CanAcquire(frame)) continue;
            limiter.Acquired(frame);
            frames.Add(frame);
            waiting--;
        }

        return frames;
    }

    [Fact]
    public void TrapsGoOffTwoInFiveFrames()
    {
        var frames = Release(new SpawnLimiter(TrapSpawner.MaxSpawnsPerWindow, TrapSpawner.SpawnWindowFrames), 6);

        Assert.Equal(new[] { 100, 101, 105, 106, 110, 111 }, frames);
    }

    [Fact]
    public void NoFiveFramesInARowHoldMoreThanTwo()
    {
        var frames = Release(new SpawnLimiter(2, 5), 200);

        for (var start = frames.First(); start <= frames.Last(); start++)
            Assert.True(frames.Count(f => f >= start && f < start + 5) <= 2, $"more than two in the frames from {start}");
    }

    [Fact]
    public void NothingIsDroppedAFloodAllGoesOffInTheEnd()
    {
        var frames = Release(new SpawnLimiter(2, 5), 500);

        Assert.Equal(500, frames.Count);
        Assert.Equal(frames.Count, frames.Distinct().Count()); // never two in one frame (the spawner asks once a frame)
    }

    [Fact]
    public void ARefusedAskIsNotCountedSoItCanAskAgain()
    {
        var limiter = new SpawnLimiter(2, 5);
        limiter.Acquired(10);
        limiter.Acquired(11);

        Assert.False(limiter.CanAcquire(12));
        Assert.False(limiter.CanAcquire(12)); // asking does not use anything up
        Assert.False(limiter.CanAcquire(14));
        Assert.True(limiter.CanAcquire(15)); // the first one has left the window
    }

    [Fact]
    public void AFrameCounterThatGoesBackwardsDoesNotBlockForEver()
    {
        var limiter = new SpawnLimiter(2, 5);
        limiter.Acquired(5000);
        limiter.Acquired(5001);

        Assert.True(limiter.CanAcquire(3)); // a new game started counting again
    }

    [Fact]
    public void ResetForgetsWhatWentOff()
    {
        var limiter = new SpawnLimiter(1, 100);
        limiter.Acquired(1);
        Assert.False(limiter.CanAcquire(2));

        limiter.Reset();

        Assert.True(limiter.CanAcquire(2));
    }

    [Fact]
    public void ItemsAreHandedOverFiveAFrame()
    {
        Assert.Equal(5, ItemGranter.ItemsPerFrame);
        Assert.Equal(2, TrapSpawner.MaxSpawnsPerWindow);
        Assert.Equal(5, TrapSpawner.SpawnWindowFrames);
    }
}
