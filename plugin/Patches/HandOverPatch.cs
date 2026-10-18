using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Opsive.UltimateInventorySystem.Core;
using Opsive.UltimateInventorySystem.Core.InventoryCollections;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Hand-overs (NPCs and story events giving key items, abilities, notes, ...) and rewards (trials, the tournament,
/// Harumi) all move the items from an NPC's "HandOver"/"Reward" collection into the player's with
/// HHInventoryUtility.SwapAllItem. Before that happens, an item that is the vanilla reward of a location is taken out
/// of the collection and reported as that location instead.
/// </summary>
[HarmonyPatch]
internal static class HandOverPatch
{
    /// <summary>Locations that pay a whole bundle (item + Konpeito + Senbei ...): the Archipelago item replaces all of it.</summary>
    private static readonly HashSet<string> BundleCategories = new() { "trial", "tournament", "harumi" };

    /// <summary>Harumi's reward for the n-th stage is handed out by an operator inside the "Gameplay_階段_n" round.</summary>
    private static readonly Regex HarumiStage = new(@"^Gameplay_階段_(\d+)$");

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHime.Inventory.HHInventoryUtility"), "SwapAllItem");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(ItemCollection __0)
    {
        if (!ApClient.Active || __0?.Inventory == null) return;

        var loc = HarumiLocation(__0) ?? ByHandedOverItem(__0);
        if (loc == null) return;

        if (BundleCategories.Contains(loc.Category))
        {
            RemoveAll(__0);
            if (RewardWaiters.NeedsRelease(__0.Purpose == ItemCollectionPurpose.Reward)) RewardWaiters.ReleaseNextFrame();
            // every thing the screen hands out is a location of its own (a trial pays Konpeito and a note)
            foreach (var part in Catalog.Bundle(loc)) LocationHooks.Report(part);
            return;
        }

        LocationHooks.Report(loc);
    }

    private static LocationRecord HarumiLocation(ItemCollection from)
    {
        var names = Hierarchy.Names(from.Inventory.gameObject.transform, 12);
        if (!names.Contains("HarumiProgressManager")) return null;

        foreach (var name in names)
        {
            var match = HarumiStage.Match(name);
            if (match.Success) return Catalog.Location("harumi/" + match.Groups[1].Value);
        }

        return null;
    }

    /// <summary>The location whose vanilla reward is one of the items being handed over (those items are removed).</summary>
    private static LocationRecord ByHandedOverItem(ItemCollection from)
    {
        var scene = from.Inventory.gameObject.scene.name;
        var handedOver = new List<(string definition, int amount)>();
        foreach (var stack in from.GetAllItemStacks())
            if (stack.Item?.ItemDefinition != null) handedOver.Add((stack.Item.ItemDefinition.name, stack.Amount));

        foreach (var (definition, amount) in handedOver)
        {
            var loc = Catalog.ByVanillaItem(scene, definition);
            if (loc == null) continue;
            from.RemoveItem(InventorySystemManager.GetItemDefinition(definition), amount);
            LocationHooks.KeepJournal(definition, amount);
            return loc;
        }

        return null;
    }

    private static void RemoveAll(ItemCollection collection)
    {
        var stacks = new List<(string definition, int amount)>();
        foreach (var stack in collection.GetAllItemStacks())
            if (stack.Item?.ItemDefinition != null) stacks.Add((stack.Item.ItemDefinition.name, stack.Amount));
        foreach (var (definition, amount) in stacks)
        {
            collection.RemoveItem(InventorySystemManager.GetItemDefinition(definition), amount);
            LocationHooks.KeepJournal(definition, amount); // a trial's journal entry stays, only its Konpeito or Senbei is replaced
        }
    }
}
