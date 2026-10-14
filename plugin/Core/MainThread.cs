using System;
using System.Collections.Concurrent;

namespace HomuraHimeAP.Core;

/// <summary>
/// Archipelago callbacks arrive on socket threads; Unity (and the game's inventory) may only be touched from the main
/// thread. Everything that reaches the game goes through this queue, drained from Plugin.Update.
/// </summary>
internal static class MainThread
{
    private static readonly ConcurrentQueue<Action> Queue = new();

    internal static void Run(Action action) => Queue.Enqueue(action);

    internal static void Drain()
    {
        var budget = 64;
        while (budget-- > 0 && Queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Main-thread action failed: " + e);
            }
        }
    }
}
