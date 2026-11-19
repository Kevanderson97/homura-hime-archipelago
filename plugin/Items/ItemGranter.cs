using System;
using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.UI;

namespace HomuraHimeAP.Items;

/// <summary>
/// Turns received Archipelago items into game state. Items are processed in server order starting at the save's received
/// index; the index only advances once an item was really granted (and, for permanent items, is really in the inventory), so
/// items received while on the title screen or while the player is dead are simply granted later. An item the game cannot
/// take is not dropped silently: it goes to the list of unresolved items and is tried again after the next load.
/// </summary>
internal static class ItemGranter
{
    /// <summary>Items are only applied once a scene has been stable for this long (never while a level is loading).</summary>
    private const float SettleSeconds = 4f;

    /// <summary>How often an item the game refused is retried (about every half second) before it is set aside.</summary>
    private const int MaxAttempts = 10;

    /// <summary>
    /// How many received items are handed to the game in one frame. Hundreds can arrive at once (a reconnect after a long time
    /// away, a release); each one is a few inventory changes, a toast and sometimes a trap, so they are spread over frames, in the
    /// order they were received, instead of one long frame.
    /// </summary>
    internal const int ItemsPerFrame = 5;

    /// <summary>The pace of everything that is not a batch: looking for new items, retrying what the game refused, the checks.</summary>
    private const float PollSeconds = 0.5f;

    /// <summary>
    /// After a batch of items was granted, how long the game gets before the inventory is checked for what it did not keep
    /// (hundreds of items sent at once, a reconnect after a long time offline).
    /// </summary>
    private const float VerifyAfterSeconds = 3f;

    /// <summary>How often a repair that the game was not ready for (no character yet) is tried again, once a second, before it is given up.</summary>
    private const int MaxRepairRounds = 30;

    private static float _nextTry;
    private static int _attemptedIndex = -1, _attempts;
    private static bool _checkOwnership, _retryUnresolved;
    private static float _ownershipDue;
    private static int _repairRounds;

    internal static void Wake() => _nextTry = 0f;

    /// <summary>A save was loaded or bound: check once that its permanent items are owned and retry the unresolved ones.</summary>
    internal static void Recheck()
    {
        (_checkOwnership, _retryUnresolved) = (true, true);
        (_ownershipDue, _repairRounds) = (0f, 0);
        _attemptedIndex = -1;
        Wake();
    }

    internal static void Tick(float time)
    {
        if (!ApClient.CanReceive || time < _nextTry) return;
        // not ready (a scene is loading, no character): look again at the idle pace, so nothing is searched for every frame
        if (!SceneClock.Settled(time, SettleSeconds) || !PlayerApi.InGame)
        {
            _nextTry = time + PollSeconds;
            return;
        }

        var items = ApClient.Session.Items.AllItemsReceived;
        var state = ApClient.State;
        var granted = 0;

        while (state.ReceivedIndex < items.Count && granted < ItemsPerFrame)
        {
            var info = items[state.ReceivedIndex];
            var record = Catalog.Item(info.ItemId);
            var result = record == null ? GrantResult.Impossible($"unknown item id {info.ItemId}") : TryGrant(record);

            if (result.Retry)
            {
                if (!result.CountsAgainstItem || !GiveUp(state.ReceivedIndex))
                {
                    // the same item again a little later (not every frame: each try is logged)
                    if (granted > 0) state.Save();
                    _nextTry = time + PollSeconds;
                    return;
                }

                result = GrantResult.Impossible("the game did not take it");
            }

            if (result.Problem != null) SetAside(state, info.ItemId, record?.Name ?? info.ItemId.ToString(), result.Problem);
            state.ReceivedIndex++;
            _attemptedIndex = -1;
            granted++;
            if (result.Problem == null) Notify(info, record);
        }

        if (granted > 0) state.Save();
        if (state.ReceivedIndex < items.Count) return; // more are waiting: the next frame takes the next ones

        _nextTry = time + PollSeconds; // caught up: back to the idle pace

        // a batch has just been granted: look at the inventory once the game has had time to settle, to be sure it all stayed
        if (granted > 0) (_checkOwnership, _ownershipDue, _repairRounds) = (true, time + VerifyAfterSeconds, 0);

        if (_retryUnresolved) RetryUnresolved(state);
        if (_checkOwnership && time >= _ownershipDue) EnsureOwnership(state, items, time);
    }

    /// <summary>True once the same item was refused often enough to stop trying.</summary>
    private static bool GiveUp(int index)
    {
        if (_attemptedIndex != index) (_attemptedIndex, _attempts) = (index, 0);
        return ++_attempts >= MaxAttempts;
    }

    private static void SetAside(SessionState state, long itemId, string name, string problem)
    {
        if (state.Unresolved.Any(u => u.ItemId == itemId)) return;
        state.Unresolved.Add(new UnresolvedItem { ItemId = itemId, Name = name, Reason = problem });
        Plugin.Log.LogError($"Could not grant {name}: {problem}");
        Toasts.Show($"Could not give {name} ({problem})", Toasts.Error);
    }

    /// <summary>Items set aside earlier (for example a definition this game build lacked) get another chance after a load.</summary>
    private static void RetryUnresolved(SessionState state)
    {
        _retryUnresolved = false;
        foreach (var unresolved in state.Unresolved.ToList())
        {
            var record = Catalog.Item(unresolved.ItemId);
            if (record == null) continue;
            var result = TryGrant(record);
            if (result.Retry || result.Problem != null) continue;
            state.Unresolved.Remove(unresolved);
            Plugin.Log.LogInfo($"Granted {record.Name}, which had been set aside");
        }

        state.Save();
    }

    /// <summary>
    /// The received index says an item was granted, the inventory is what the game saves. If the two disagree for an item that
    /// must be kept (see <see cref="OwnedItems"/>: equipment, key items, Soul Cores), the item is given again. A repair the game
    /// was not ready for is tried again a second later, up to <see cref="MaxRepairRounds"/> times.
    /// </summary>
    private static void EnsureOwnership(SessionState state, System.Collections.ObjectModel.ReadOnlyCollection<global::Archipelago.MultiClient.Net.Models.ItemInfo> items, float time)
    {
        var again = false;
        var granted = new List<ItemRecord>();
        for (var i = 0; i < Math.Min(state.ReceivedIndex, items.Count); i++) granted.Add(Catalog.Item(items[i].ItemId));

        foreach (var record in OwnedItems.Missing(granted, PlayerApi.DefinitionExists, PlayerApi.Has))
        {
            Plugin.Log.LogWarning($"{record.Name} was received but is not owned, giving it again");
            var result = TryGrant(record);
            if (result.Retry) again = true;
            else if (result.Problem != null) Plugin.Log.LogError($"Could not give {record.Name} again: {result.Problem}");
        }

        if (again && ++_repairRounds < MaxRepairRounds)
        {
            _ownershipDue = time + 1f; // _checkOwnership stays set
            return;
        }

        if (again) Plugin.Log.LogError("Some items that were received are still not owned; giving up until the next load");
        _checkOwnership = false;
    }

    private static void Notify(global::Archipelago.MultiClient.Net.Models.ItemInfo info, ItemRecord record)
    {
        var self = ApClient.Session.ConnectionInfo.Slot == info.Player.Slot;
        // items found by this player were already announced when the location was checked
        if (self && ApClient.State.CheckedLocations.Contains(info.LocationId)) return;
        var text = self ? ItemMessages.ReceivedFromSelf(record.Name) : ItemMessages.Received(record.Name, info.Player.Name);
        Toasts.ShowItem(text, self ? record.OwnFlags : info.Flags, !self, self ? ItemIcons.For(record) : Assets.Logo);
    }

    private readonly struct GrantResult
    {
        /// <summary>Not applied; try again shortly.</summary>
        internal bool Retry { get; }

        /// <summary>The game was reachable but did not end up with the item, so the attempts are counted.</summary>
        internal bool CountsAgainstItem { get; }

        /// <summary>The item can never be applied here; the reason is shown and stored.</summary>
        internal string Problem { get; }

        private GrantResult(bool retry, bool counts, string problem) { Retry = retry; CountsAgainstItem = counts; Problem = problem; }

        internal static GrantResult Done => new(false, false, null);

        /// <summary>No character, dead, ...: nothing is wrong with the item, wait as long as it takes.</summary>
        internal static GrantResult Later => new(true, false, null);

        /// <summary>Added, but the inventory does not show it.</summary>
        internal static GrantResult Refused => new(true, true, null);

        internal static GrantResult Impossible(string problem) => new(false, false, problem);
    }

    private static GrantResult TryGrant(ItemRecord item)
    {
        Plugin.Log.LogInfo($"Granting {item.Name} ({item.Type}/{item.Game})");
        try
        {
            return Grant(item);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Granting {item.Name} failed: {e}");
            return GrantResult.Refused;
        }
    }

    private static GrantResult Grant(ItemRecord item)
    {
        var state = ApClient.State;
        switch (item.Type)
        {
            case "key":
            case "soul_core":
            case "ability":
            case "dash_skill":
            case "skill":
            case "combo":
            case "omamori":
            case "gun":
            case "upgrade":
            {
                if (!PlayerApi.DefinitionExists(item.Game)) return GrantResult.Impossible($"{item.Game} is missing in this game build");
                if (!PlayerApi.Has(item.Game) && !PlayerApi.Add(item.Game)) return GrantResult.Later;
                if (!PlayerApi.Has(item.Game)) return GrantResult.Refused;
                if (item.Type == "ability") ApplyBankedCharge();
                return GrantResult.Done;
            }

            case "shard":
            case "crystal":
            case "consumable":
                if (!PlayerApi.DefinitionExists(item.Game)) return GrantResult.Impossible($"{item.Game} is missing in this game build");
                return PlayerApi.Add(item.Game, 1) ? GrantResult.Done : GrantResult.Later;

            case "currency":
                return PlayerApi.AddCurrency(item.Game, item.Amount) ? GrantResult.Done : GrantResult.Later;

            case "unlock":
                state.UnlockedChapters.Add(item.Game);
                return GrantResult.Done;

            case "unlock_progressive":
                state.ProgressiveKeys++;
                return GrantResult.Done;

            case "trap":
                TrapSpawner.Queue(item.Game);
                return GrantResult.Done;

            case "utility":
                return item.Game switch
                {
                    "FullHeal" => PlayerApi.FullHeal() ? GrantResult.Done : GrantResult.Later,
                    "UltimateCharge" => GrantUltimateCharge() ? GrantResult.Done : GrantResult.Later,
                    _ => GrantResult.Impossible($"utility {item.Game} is not handled"),
                };

            default:
                return GrantResult.Impossible($"item type {item.Type} is not handled");
        }
    }

    private static bool GrantUltimateCharge()
    {
        if (PlayerApi.HasChargeAbility() && PlayerApi.ChargeUltimate()) return true;
        ApClient.State.BankedUltimateCharge++;
        return true;
    }

    private static void ApplyBankedCharge()
    {
        var state = ApClient.State;
        if (state.BankedUltimateCharge <= 0 || !PlayerApi.HasChargeAbility()) return;
        PlayerApi.ChargeUltimate();
        state.BankedUltimateCharge = 0;
    }
}
