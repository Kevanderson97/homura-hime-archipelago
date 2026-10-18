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

    /// <summary>The definition of the item the running purchase sells (only that item is kept out of the inventory).</summary>
    internal static string Purchased;

    /// <summary>How many of the item the player owned before the purchase.</summary>
    private static void Prefix(ItemInfo __1, out int __state)
    {
        var definition = __1.Item.ItemDefinition.name;
        __state = PlayerApi.AmountOwned(definition);
        Taking = ShopLocations.ForPurchase(definition) != null;
        Purchased = Taking ? definition : null;
    }

    /// <summary>Runs even when the purchase throws: the flag must not stay set and hide real pop-ups.</summary>
    private static Exception Finalizer(Exception __exception)
    {
        Taking = false;
        Purchased = null;
        return __exception;
    }

    private static void Postfix(ItemInfo __1, int __state)
    {
        var definition = __1.Item.ItemDefinition.name;
        var location = ShopLocations.ForPurchase(definition);
        if (location == null) return;

        PlayerApi.Remove(definition, PlayerApi.AmountOwned(definition) - __state);
        LocationHooks.Report(location);
        EnsureSoldOut(__1);
    }

    /// <summary>
    /// A slot that left the store ends up in the merchant's sold-out collection, which is what the shop shows as "sold out".
    /// The game does that itself while buying; this makes sure of it (and redraws the list) because the purchase no longer
    /// gives the player the item (<see cref="ShopAddBlockPatch"/>).
    /// </summary>
    private static void EnsureSoldOut(ItemInfo info)
    {
        try
        {
            var store = info.ItemCollection;
            var soldOut = store?.Inventory?.GetItemCollection("SoldOut");
            if (store == null || soldOut == null || info.Item == null) return;

            var inStore = store.GetItemAmount(info.Item);
            var inSoldOut = soldOut.GetItemAmount(info.Item);
            if (Plugin.CfgVerbose.Value)
                Plugin.Log.LogInfo($"Shop purchase of {info.Item.ItemDefinition.name}: {inStore} in the store, {inSoldOut} sold out");
            if (!NeedsSoldOutEntry(inStore, inSoldOut)) return;

            soldOut.AddItem(info.Item);
            AccessTools.Method(AccessTools.TypeByName("Opsive.Shared.Events.EventHandler"), "ExecuteEvent", new[] { typeof(object), typeof(string) })
                ?.Invoke(null, new object[] { store.Inventory, "Inventory_OnUpdate" }); // the list redraws
            Plugin.Log.LogInfo($"Shop: {info.Item.ItemDefinition.name} was missing from the sold-out list and has been added");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not check the sold-out state of a purchase: " + e.Message);
        }
    }

    /// <summary>An item that is no longer on sale and is not listed as sold out would just vanish from the shop.</summary>
    internal static bool NeedsSoldOutEntry(int inStore, int inSoldOut) => inStore <= 0 && inSoldOut <= 0;
}

/// <summary>
/// The item a shop slot sells never reaches the player's inventory when the slot is an Archipelago location. Adding it and
/// taking it back is not enough: three shard fragments combine into a crystal at once (the health or spirit bar grows), and
/// the fragments taken back afterwards leave the bar upgraded and the count at zero. Other items fire their own
/// "obtained" effects the same way.
/// </summary>
[HarmonyPatch]
internal static class ShopAddBlockPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(ItemCollection), "AddItem", new[] { typeof(ItemInfo), typeof(ItemStack) });

    private static bool Prepare() => TargetMethod() != null;

    /// <summary>The add is skipped only while a purchase of an Archipelago slot adds the item to the player's own inventory.</summary>
    internal static bool Blocks(bool purchaseOfLocation, bool addsToPlayer) => purchaseOfLocation && addsToPlayer;

    private static bool Prefix(ItemCollection __instance, ItemInfo __0, ref ItemInfo __result)
    {
        if (!ApClient.Active || !ShopBuyPatch.Taking) return true;
        if (__0.Item?.ItemDefinition?.name != ShopBuyPatch.Purchased) return true; // anything else the purchase adds is left alone
        if (!Blocks(true, __instance.Inventory is { } owner && ReferenceEquals(owner, PlayerApi.Inventory))) return true;

        __result = default;
        return false;
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
