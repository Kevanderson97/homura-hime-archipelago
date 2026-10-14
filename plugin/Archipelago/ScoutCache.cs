using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Archipelago.MultiClient.Net;
using global::Archipelago.MultiClient.Net.Enums;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Archipelago;

/// <summary>What the server placed at a location of this slot (looked up once per connection with a scout).</summary>
internal sealed class ScoutedItem
{
    internal long ItemId;
    internal string Name;
    internal string PlayerName;
    internal ItemFlags Flags;

    /// <summary>The item belongs to this slot's own game (it exists in data/items.json), not to another player's.</summary>
    internal bool IsOwn;

    /// <summary>For own items: the catalogue entry, which knows the game-side item and its icon.</summary>
    internal ItemRecord Record;
}

/// <summary>
/// Knows the item behind every location of the slot, so the game can show what a pickup or shop slot really holds
/// before it is collected. Filled right after connecting; lookups before that finish simply find nothing.
/// </summary>
internal static class ScoutCache
{
    private static readonly Dictionary<long, ScoutedItem> Items = new();

    internal static ScoutedItem Get(long locationId) => Items.TryGetValue(locationId, out var item) ? item : null;

    internal static bool HasEntries => Items.Count > 0;

    internal static void Clear() => Items.Clear();

    /// <summary>Fills the table from a stored snapshot (offline play). Replaced by the real table once a connection scouts.</summary>
    internal static void Restore(IEnumerable<ScoutEntry> entries)
    {
        Items.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
        {
            Items[entry.LocationId] = new ScoutedItem
            {
                ItemId = entry.ItemId,
                Name = entry.ItemName,
                PlayerName = entry.PlayerName,
                Flags = (ItemFlags)entry.Flags,
                IsOwn = entry.Own,
                Record = entry.Own ? Catalog.Item(entry.ItemId) : null,
            };
            if (Items[entry.LocationId].Record is { } record) Items[entry.LocationId].Flags = record.OwnFlags;
        }
    }

    /// <summary>
    /// Scouts every location of the slot in the background; one retry after a failure, which is logged. The finished table is
    /// also handed to <paramref name="onLoaded"/> (on the game thread) so it can be stored for offline play.
    /// </summary>
    internal static void Reload(ArchipelagoSession session, Action<List<ScoutEntry>> onLoaded)
    {
        Task.Run(async () =>
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    await LoadAsync(session, onLoaded);
                    return;
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"Scouting the locations failed (attempt {attempt}): {e.Message}");
                    await Task.Delay(3000);
                }
            }
        });
    }

    private static async Task LoadAsync(ArchipelagoSession session, Action<List<ScoutEntry>> onLoaded)
    {
        var ids = session.Locations.AllLocations.ToArray();
        var scouted = await session.Locations.ScoutLocationsAsync(HintCreationPolicy.None, ids);
        var ownSlot = session.ConnectionInfo.Slot;

        // Item names are resolved from the room's data package, which can mean waiting for the server. That must never happen
        // on the game thread (the socket library needs it to finish, so it would deadlock), so everything is read here, on the
        // thread the scout finished on, and only the finished table is handed to the game thread.
        var table = new Dictionary<long, ScoutedItem>();
        var snapshot = new List<ScoutEntry>();
        foreach (var (id, info) in scouted)
        {
            var own = info.Player.Slot == ownSlot;
            table[id] = new ScoutedItem
            {
                ItemId = info.ItemId,
                Name = info.ItemName,
                PlayerName = info.Player.Name,
                Flags = own && Catalog.Item(info.ItemId) is { } known ? known.OwnFlags : info.Flags,
                IsOwn = own,
                Record = own ? Catalog.Item(info.ItemId) : null,
            };
            snapshot.Add(new ScoutEntry { LocationId = id, ItemId = info.ItemId, ItemName = info.ItemName, PlayerName = info.Player.Name, Flags = (int)table[id].Flags, Own = own });
        }

        MainThread.Run(() =>
        {
            Items.Clear();
            foreach (var entry in table) Items[entry.Key] = entry.Value;
            Plugin.Log.LogInfo($"Scouted {Items.Count} locations");
            onLoaded?.Invoke(snapshot);
        });
    }
}
