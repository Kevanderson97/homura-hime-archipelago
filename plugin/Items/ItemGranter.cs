using System;
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

    /// <summary>Permanent equipment that should always be owned once received. Consumable-like types (keys, upgrades) are left out.</summary>
    private static readonly string[] OwnedForGood = { "ability", "dash_skill", "skill", "combo", "omamori", "gun" };

    private static float _nextTry;
    private static int _attemptedIndex = -1, _attempts;
    private static bool _checkOwnership, _retryUnresolved;

    internal static void Wake() => _nextTry = 0f;

    /// <summary>A save was loaded or bound: check once that its permanent items are owned and retry the unresolved ones.</summary>
    internal static void Recheck()
    {
        (_checkOwnership, _retryUnresolved) = (true, true);
        _attemptedIndex = -1;
        Wake();
    }

    internal static void Tick(float time)
    {
        if (!ApClient.CanReceive || time < _nextTry) return;
        _nextTry = time + 0.5f;
        if (!SceneClock.Settled(time, SettleSeconds)) return;
        // The tutorial has its own rewards and loads the hub right after; hold items until it is over.
        // Switched off for now: items are granted during the tutorial too.
        // if (!TutorialTracker.Done) return;
        if (!PlayerApi.InGame) return;

        var items = ApClient.Session.Items.AllItemsReceived;
        var state = ApClient.State;
        var granted = 0;

        while (state.ReceivedIndex < items.Count && granted < 20)
        {
            var info = items[state.ReceivedIndex];
            var record = Catalog.Item(info.ItemId);
            var result = record == null ? GrantResult.Impossible($"unknown item id {info.ItemId}") : TryGrant(record);

            if (result.Retry)
            {
                if (!result.CountsAgainstItem || !GiveUp(state.ReceivedIndex)) return; // the same item again next tick
                result = GrantResult.Impossible("the game did not take it");
            }

            if (result.Problem != null) SetAside(state, info.ItemId, record?.Name ?? info.ItemId.ToString(), result.Problem);
            state.ReceivedIndex++;
            _attemptedIndex = -1;
            granted++;
            if (result.Problem == null) Notify(info, record);
        }

        if (granted > 0) state.Save();
        if (state.ReceivedIndex < items.Count) return;

        if (_retryUnresolved) RetryUnresolved(state);
        if (_checkOwnership) EnsureOwnership(state, items);
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
    /// The received index says an item was granted, the inventory is what the game saves. If the two disagree for permanent
    /// equipment (a save written before the grant, an inventory reset), the item is given again.
    /// </summary>
    private static void EnsureOwnership(SessionState state, System.Collections.ObjectModel.ReadOnlyCollection<global::Archipelago.MultiClient.Net.Models.ItemInfo> items)
    {
        _checkOwnership = false;
        for (var i = 0; i < Math.Min(state.ReceivedIndex, items.Count); i++)
        {
            var record = Catalog.Item(items[i].ItemId);
            if (record == null || !OwnedForGood.Contains(record.Type) || !PlayerApi.DefinitionExists(record.Game)) continue;
            if (PlayerApi.Has(record.Game)) continue;

            Plugin.Log.LogWarning($"{record.Name} was received but is not owned, giving it again");
            TryGrant(record);
        }
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
