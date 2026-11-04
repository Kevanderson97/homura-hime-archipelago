using System;

namespace HomuraHimeAP.Core;

/// <summary>Where the enemies of a trap appear around the player (no Unity types, so it can be tested).</summary>
internal static class TrapPlacement
{
    internal const float MinDistance = 6f, MaxDistance = 20f;

    /// <summary>
    /// Whether a scene is an actual level (the chapters, their bosses and the Final chapter all start with "Level"). The hubs
    /// (HubWorld_Main, the post-game hub, the training ground) and the menu scenes are not: traps wait until the player is in a level.
    /// </summary>
    internal static bool IsLevel(string scene) => scene != null && scene.StartsWith("Level", StringComparison.Ordinal);

    /// <summary>The highest a flying enemy may spawn above the player's height, as an angle: never straight overhead.</summary>
    internal const float MaxElevationDegrees = 40f;

    /// <summary>How far the members of a group stand from its centre.</summary>
    internal const float GroupSpread = 1.5f;

    /// <summary>
    /// A random offset from the player, between <see cref="MinDistance"/> and <see cref="MaxDistance"/> metres away (straight
    /// line), in any direction around and up to <paramref name="maxElevationDegrees"/> above (0: level with the player).
    /// Nothing is placed below the player's feet, where the floor is.
    /// </summary>
    internal static (float X, float Y, float Z) Offset(Random random, float maxElevationDegrees = MaxElevationDegrees)
    {
        var distance = MinDistance + (float)random.NextDouble() * (MaxDistance - MinDistance);
        var around = (float)(random.NextDouble() * 2 * Math.PI);
        var up = (float)(random.NextDouble() * maxElevationDegrees * Math.PI / 180);

        var flat = distance * (float)Math.Cos(up);
        return (flat * (float)Math.Cos(around), distance * (float)Math.Sin(up), flat * (float)Math.Sin(around));
    }

    /// <summary>The distance range of a group of ground enemies (closer than the flying ones: they have to walk).</summary>
    internal const float GroupMinDistance = 4f, GroupMaxDistance = 8f;

    /// <summary>
    /// A group standing together on the ground: <paramref name="count"/> offsets spread evenly on a small ring around a random
    /// centre, the centre placed so that every member is still between <paramref name="min"/> and <paramref name="max"/>
    /// metres from the player.
    /// </summary>
    internal static (float X, float Z)[] Group(Random random, int count, float min = GroupMinDistance, float max = GroupMaxDistance)
    {
        var distance = min + GroupSpread + (float)random.NextDouble() * (max - min - 2 * GroupSpread);
        var around = (float)(random.NextDouble() * 2 * Math.PI);
        var (centerX, centerZ) = (distance * (float)Math.Cos(around), distance * (float)Math.Sin(around));

        var phase = (float)(random.NextDouble() * 2 * Math.PI);
        var members = new (float X, float Z)[count];
        for (var i = 0; i < count; i++)
        {
            var angle = phase + i * 2 * (float)Math.PI / count;
            members[i] = (centerX + GroupSpread * (float)Math.Cos(angle), centerZ + GroupSpread * (float)Math.Sin(angle));
        }

        return members;
    }
}
