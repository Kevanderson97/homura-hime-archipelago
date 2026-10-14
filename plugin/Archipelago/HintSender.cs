using System;
using System.Linq;
using System.Threading.Tasks;
using global::Archipelago.MultiClient.Net.Enums;
using HomuraHimeAP.Core;
using HomuraHimeAP.UI;
using UnityEngine;

namespace HomuraHimeAP.Archipelago;

/// <summary>
/// Shop hints: a shop entry that is on screen and holds an item for another player is announced to the multiworld as a
/// normal Archipelago hint (free, no hint points), so the owner learns where their item is and spoiler trackers see it. Entries
/// the shop filters out are never displayed and therefore never hinted. Each location is hinted once per slot; entries seen
/// while offline wait in the session and are sent on the next connection. Requests go out in batches from the thread pool.
/// </summary>
internal static class HintSender
{
    private const float BatchSeconds = 0.5f;
    private const float RetrySeconds = 10f;
    private const int MaxPerRequest = 40;

    private static float _nextSend;
    private static bool _sending;

    /// <summary>The world option (slot data) and the player's own setting, which can only turn hints off.</summary>
    internal static bool Enabled => ApClient.Slot.ShopHints && Plugin.CfgShopHints.Value;

    /// <summary>A shop entry for this location is on screen.</summary>
    internal static void Seen(LocationRecord location)
    {
        var state = ApClient.State;
        if (state == null || !Enabled) return;

        var id = location.Id;
        if (state.HintedLocations.Contains(id) || state.PendingHints.Contains(id)) return;

        var scouted = ScoutCache.Get(id);
        if (scouted == null || scouted.IsOwn) return; // only items that belong to other players, and only once they are known

        state.PendingHints.Add(id);
        state.Save();
    }

    /// <summary>Called every frame: sends the entries seen since the last batch.</summary>
    internal static void Tick(float now)
    {
        var state = ApClient.State;
        var session = ApClient.Session;
        if (_sending || state == null || session == null || state.PendingHints.Count == 0 || now < _nextSend) return;

        _nextSend = now + BatchSeconds;
        _sending = true;
        var batch = state.PendingHints.Take(MaxPerRequest).ToArray();
        Task.Run(async () =>
        {
            try
            {
                await session.Locations.ScoutLocationsAsync(HintCreationPolicy.CreateAndAnnounceOnce, batch);
                MainThread.Run(() => Sent(state, batch));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not send the shop hints, trying again later: " + e.Message);
                MainThread.Run(() => (_sending, _nextSend) = (false, Time.unscaledTime + RetrySeconds));
            }
        });
    }

    private static void Sent(SessionState state, long[] batch)
    {
        _sending = false;
        foreach (var id in batch)
        {
            state.PendingHints.Remove(id);
            state.HintedLocations.Add(id);
        }

        state.Save();
        Plugin.Log.LogInfo($"Sent {batch.Length} shop hint(s)");
        Toasts.Show(batch.Length == 1 ? "Hinted 1 shop item" : $"Hinted {batch.Length} shop items", Toasts.Info);
    }

    internal static void Reset() => (_sending, _nextSend) = (false, 0f);
}
