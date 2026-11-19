using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ActiveTrapsTests
{
    // an enemy is a number here; the ones in "dead" have been defeated
    private static bool Dead(HashSet<int> dead, int enemy) => dead.Contains(enemy);

    [Fact]
    public void AtMostSixTrapsAreOutAtOnce()
    {
        var traps = new ActiveTraps<int>(TrapSpawner.MaxActiveTraps);
        var next = 0;
        while (traps.HasRoom) traps.Add(new[] { next++, next++ });

        Assert.Equal(6, traps.Count);
        Assert.Equal(6, TrapSpawner.MaxActiveTraps);
        Assert.False(traps.HasRoom);
    }

    [Fact]
    public void ATrapStaysOutUntilEveryEnemyOfItIsDefeated()
    {
        var traps = new ActiveTraps<int>(1);
        traps.Add(new[] { 1, 2, 3 });
        var dead = new HashSet<int> { 1, 2 };

        Assert.Equal(0, traps.Prune(e => Dead(dead, e)));
        Assert.False(traps.HasRoom); // one enemy of three is still alive

        dead.Add(3);
        Assert.Equal(1, traps.Prune(e => Dead(dead, e)));
        Assert.True(traps.HasRoom);
        Assert.Equal(0, traps.Count);
    }

    [Fact]
    public void ADefeatedTrapMakesRoomForExactlyOneMore()
    {
        var traps = new ActiveTraps<int>(3);
        traps.Add(new[] { 1 });
        traps.Add(new[] { 2 });
        traps.Add(new[] { 3 });
        var dead = new HashSet<int> { 2 };

        traps.Prune(e => Dead(dead, e));

        Assert.Equal(2, traps.Count);
        Assert.True(traps.HasRoom);
        traps.Add(new[] { 4 });
        Assert.False(traps.HasRoom);
    }

    [Fact]
    public void AFloodOfTrapsIsLetOutAsTheyAreCleared()
    {
        // 40 traps of 8 enemies each; every "frame" the player defeats one enemy of the oldest trap out
        var traps = new ActiveTraps<int>(6);
        var queue = 40;
        var next = 0;
        var dead = new HashSet<int>();
        var outAtOnce = 0;
        var order = new Queue<int>();
        for (var frame = 0; frame < 100_000 && (queue > 0 || traps.Count > 0); frame++)
        {
            traps.Prune(e => Dead(dead, e));
            while (queue > 0 && traps.HasRoom)
            {
                var enemies = Enumerable.Range(next, 8).ToList();
                next += 8;
                foreach (var e in enemies) order.Enqueue(e);
                traps.Add(enemies);
                queue--;
            }

            outAtOnce = System.Math.Max(outAtOnce, traps.Count);
            if (order.Count > 0) dead.Add(order.Dequeue());
        }

        Assert.Equal(6, outAtOnce); // never more than six out
        Assert.Equal(0, queue); // all forty went off in the end
        Assert.Equal(0, traps.Count);
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var traps = new ActiveTraps<int>(2);
        traps.Add(new[] { 1 });
        traps.Add(new[] { 2 });

        traps.Clear();

        Assert.Equal(0, traps.Count);
        Assert.True(traps.HasRoom);
    }

    [Theory]
    [InlineData(true, true, false)]   // a trio while a Blue Hexed Doll is alive: it waits
    [InlineData(true, false, true)]   // a trio with none alive: it goes off
    [InlineData(false, true, true)]   // a trap without a Blue doll (the puffers) never waits for it
    [InlineData(false, false, true)]
    public void ATrioWaitsForTheBlueHexedDollToBeDefeated(bool bringsBlueDoll, bool blueDollAlive, bool mayGoOff) =>
        Assert.Equal(mayGoOff, TrapPlacement.MayGoOff(bringsBlueDoll, blueDollAlive));

    [Fact]
    public void TheBlueDollIsTheCodexEntryOfAoi() => Assert.Equal("Codex_Enemy_CursedDollBlue", TrapPlacement.BlueDollCodex);

    [Fact]
    public void AllTrapsAreClearedAtOnceWhenTheirSceneGoesAway()
    {
        var traps = new ActiveTraps<int>(6);
        for (var i = 0; i < 6; i++) traps.Add(new[] { i * 10, i * 10 + 1, i * 10 + 2 });
        Assert.False(traps.HasRoom);

        // the scene unloaded: every enemy of every trap is gone (destroyed), though none was killed
        var cleared = traps.Prune(_ => true);

        Assert.Equal(6, cleared);
        Assert.Equal(0, traps.Count);
        Assert.True(traps.HasRoom);
    }

    [Fact]
    public void EveryDefeatedEnemyIsReportedOnceSoItCanBeCleanedUp()
    {
        var traps = new ActiveTraps<int>(3);
        traps.Add(new[] { 1, 2 });
        traps.Add(new[] { 3 });
        var forgotten = new List<int>();

        traps.Prune(e => e != 2, forgotten.Add); // everything but 2 is defeated
        traps.Prune(e => e != 2, forgotten.Add);

        Assert.Equal(new[] { 3, 1 }, forgotten); // the newest trap first, each once; 2 is still alive
        Assert.Equal(1, traps.Count);
    }

    [Fact]
    public void TheBlueDollIsTheCOneOfTheTrio() => Assert.Equal("Enemy_CursedDoll_C_Default", TrapPlacement.BlueDollTemplate);
}
