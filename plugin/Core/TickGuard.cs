using System;
using System.Collections.Generic;

namespace HomuraHimeAP.Core;

/// <summary>
/// Runs the plugin's per-frame work one subsystem at a time, so an exception in one of them is logged once instead of every
/// frame and does not stop the others. A subsystem that keeps failing is paused for a few seconds before it is tried again.
/// </summary>
internal static class TickGuard
{
    /// <summary>Failures in a row before a subsystem is paused.</summary>
    internal const int FailuresBeforePause = 3;

    internal const float PauseSeconds = 5f;

    private sealed class Health
    {
        internal int Failures;
        internal float PausedUntil;
        internal string LastMessage;
    }

    private static readonly Dictionary<string, Health> Subsystems = new();

    /// <summary>Where failures are reported; tests replace it.</summary>
    internal static Action<string> Log = message => Plugin.Log?.LogError(message);

    internal static void Run(string name, float now, Action action) => Run(name, now, action, static (a, _) => a());

    /// <summary>For subsystems that take the current time (no closure over it is allocated every frame).</summary>
    internal static void Run(string name, float now, Action<float> tick) => Run(name, now, tick, static (t, n) => t(n));

    private static void Run<T>(string name, float now, T work, Action<T, float> invoke)
    {
        if (!Subsystems.TryGetValue(name, out var health)) Subsystems[name] = health = new Health();
        if (now < health.PausedUntil) return;

        try
        {
            invoke(work, now);
            health.Failures = 0;
        }
        catch (Exception e)
        {
            health.Failures++;
            // the same failure repeating is reported once; a different one is a new report
            if (e.Message != health.LastMessage)
            {
                health.LastMessage = e.Message;
                Log($"{name} failed: {e}");
            }

            if (health.Failures >= FailuresBeforePause)
            {
                health.PausedUntil = now + PauseSeconds;
                health.Failures = 0;
                Log($"{name} paused for {PauseSeconds} s after repeated failures");
            }
        }
    }

    internal static void Reset() => Subsystems.Clear();
}
