using System;
using System.Threading.Tasks;
using global::Archipelago.MultiClient.Net;
using HomuraHimeAP.Core;
using HomuraHimeAP.UI;

namespace HomuraHimeAP.Archipelago;

/// <summary>
/// Notices when the connection drops and brings it back. While reconnecting the randomizer stays active (checks are recorded
/// locally), and every successful connect runs the same reconciliation as the first one.
/// </summary>
internal static class ConnectionMonitor
{
    /// <summary>Seconds to wait before each reconnect attempt; the last value repeats.</summary>
    private static readonly int[] Backoff = { 2, 5, 10, 30 };

    /// <summary>Starts watching a session. Its closing is reported through <see cref="ApClient"/> on the game thread.</summary>
    internal static void Watch(ArchipelagoSession session) => session.Socket.SocketClosed += _ => MainThread.Run(() => ApClient.OnSocketClosed(session));

    /// <summary>Tries to log in again until it works, the slot or password is rejected, or <paramref name="generation"/> is superseded.</summary>
    internal static void Reconnect(int generation)
    {
        Task.Run(async () =>
        {
            for (var attempt = 0; ; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(Backoff[Math.Min(attempt, Backoff.Length - 1)]));
                if (ApClient.Generation != generation) return;

                var outcome = ApClient.TryLogin();
                MainThread.Run(() => ApClient.OnReconnectAttempt(outcome, generation));
                if (outcome.Succeeded || outcome.Rejected) return;
            }
        });
    }
}
