using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using Opsive.UltimateInventorySystem.Core.DataStructures;
using Opsive.UltimateInventorySystem.Core.InventoryCollections;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Maps what the merchant sells to the world's shop locations ("shop/&lt;definition&gt;"). HP and SP shards are sold
/// three times at rising prices, those purchases are the locations "shop/HP Shard#1" to "#3".
/// </summary>
internal static class ShopLocations
{
    private const string Prefix = "shop/";
    private const int ShardStages = 3;

    /// <summary>The location a purchase of this item pays out (the first unchecked stage for shards), or null.</summary>
    internal static LocationRecord ForPurchase(string definitionName)
    {
        if (!ApClient.Active) return null;
        var plain = Catalog.Location(Prefix + definitionName);
        if (plain != null) return plain;

        for (var stage = 1; stage <= ShardStages; stage++)
        {
            var staged = Catalog.Location($"{Prefix}{definitionName}#{stage}");
            if (staged != null && !IsChecked(staged)) return staged;
        }

        return null;
    }

    /// <summary>
    /// The location a shop entry shows: what it pays out if bought now, or what it paid out when it is sold out (the last stage
    /// for shards), so a sold-out entry keeps the name of what it gave. Null when the entry is vanilla.
    /// </summary>
    internal static LocationRecord ForDisplay(string definitionName) =>
        ForPurchase(definitionName) ?? Catalog.Location($"{Prefix}{definitionName}#{ShardStages}");

    internal static bool IsChecked(LocationRecord location) => ApClient.State.CheckedLocations.Contains(location.Id);
}

/// <summary>
/// Buying from the merchant. The purchase itself (price, stock, sold-out handling) runs as in the vanilla game; the
/// item it hands to the player is taken back afterwards and the purchase is reported as a location check instead.
/// </summary>
[HarmonyPatch]
internal static class ShopBuyPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("ShopBuyItemHandler"), "OnBuyItem");

    private static bool Prepare() => TargetMethod() != null;

    /// <summary>
    /// True while the game adds the item it sells for a slot that is an Archipelago location. The item is taken away again
    /// right after, so the pop-ups that announce a new item (see <see cref="PurchasePopupPatch"/>) must not play.
    /// </summary>
    internal static bool Taking;

    /// <summary>How many of the item the player owned before the purchase.</summary>
    private static void Prefix(ItemInfo __1, out int __state)
    {
        var definition = __1.Item.ItemDefinition.name;
        __state = PlayerApi.AmountOwned(definition);
        Taking = ShopLocations.ForPurchase(definition) != null;
    }

    /// <summary>Runs even when the purchase throws: the flag must not stay set and hide real pop-ups.</summary>
    private static Exception Finalizer(Exception __exception)
    {
        Taking = false;
        return __exception;
    }

    private static void Postfix(ItemInfo __1, int __state)
    {
        var definition = __1.Item.ItemDefinition.name;
        var location = ShopLocations.ForPurchase(definition);
        if (location == null) return;

        PlayerApi.Remove(definition, PlayerApi.AmountOwned(definition) - __state);
        LocationHooks.Report(location);
    }
}

/// <summary>
/// The game announces every item added to the player's inventory with a pop-up (<c>HHSimpleInventoryMoniter.Pop</c>: the
/// animation of a new spirit or HP shard, the reward panel). The item a shop slot sells is added by the vanilla purchase and
/// taken back afterwards, so that announcement would be wrong: buying what used to be a spirit shard must not play the shard
/// animation. Items the player really receives are added later by the item granter and still announce themselves.
/// </summary>
[HarmonyPatch]
internal static class PurchasePopupPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HHSimpleInventoryMoniter"), "Pop");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix() => !ShopBuyPatch.Taking;
}

/// <summary>
/// The merchant normally sells out an item as soon as the player owns it. With Archipelago the player can receive
/// that item from elsewhere, so a slot is sold out only once its own location has been checked.
/// </summary>
[HarmonyPatch]
internal static class ShopSoldOutPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("StoreInventoryScanner"), "ScanAndMoveSoldOut");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(object __instance)
    {
        if (!ApClient.Active) return true;

        var shop = Traverse.Create(__instance).Field("m_ShopInventory").GetValue<Inventory>();
        var store = shop?.GetItemCollection("Store");
        var soldOut = shop?.GetItemCollection("SoldOut");
        if (store == null || soldOut == null) return false;

        var checkedStacks = new List<ItemStack>();
        foreach (var stack in store.GetAllItemStacks())
        {
            var location = stack.Item?.ItemDefinition == null ? null : Catalog.Location("shop/" + stack.Item.ItemDefinition.name);
            if (location != null && ShopLocations.IsChecked(location)) checkedStacks.Add(stack);
        }

        foreach (var stack in checkedStacks)
        {
            var info = new ItemInfo(stack.Item, stack.Amount);
            store.RemoveItem(info);
            soldOut.AddItem(info);
        }

        return false;
    }
}
