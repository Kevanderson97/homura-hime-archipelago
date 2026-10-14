using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Archipelago.MultiClient.Net;

namespace HomuraHimeAP.Archipelago;

/// <summary>
/// Sends location checks from thread-pool tasks, never from the game thread (the other Archipelago mods do the same). A check
/// that cannot be sent is not lost: it stays in the session's checked locations and goes out with the next reconciliation.
/// </summary>
internal static class CheckSender
{
    internal static void Send(ArchipelagoSession session, params long[] ids) => Send(session, (IEnumerable<long>)ids);

    internal static void Send(ArchipelagoSession session, IEnumerable<long> ids)
    {
        var batch = ids.ToArray();
        if (batch.Length == 0) return;

        Task.Run(() =>
        {
            try
            {
                session.Locations.CompleteLocationChecks(batch);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not send {batch.Length} location check(s), they are sent again after the next reconnect: {e.Message}");
            }
        });
    }
}
