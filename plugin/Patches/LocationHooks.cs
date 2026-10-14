using System.Linq;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using Opsive.UltimateInventorySystem.Core;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Shared logic of the location hooks. While connected to an Archipelago server the vanilla reward of a location is
/// replaced by a location check (the item then arrives as a received item); without a connection the game behaves as
/// usual. Locations are found in data/locations.json by scene and position, by the item they normally give, or by the
/// name of the object that hands the reward out.
/// </summary>
internal static class LocationHooks
{
    // Senbei fly towards the player when he comes near, so their position at pickup time differs from where they lie.
    // No two Senbei are anywhere near each other, so a generous radius is safe.
    private const float SenbeiRadius = 12f;

    internal static void Report(LocationRecord loc)
    {
        Plugin.Log.LogInfo($"Location check: {loc.Name} [{loc.Key}]");
        ApClient.CheckLocation(loc.Id);
    }

    /// <summary>A journal entry (Codex note).</summary>
    internal static bool IsJournal(string definition) => definition != null && definition.StartsWith("Codex_Note_");

    /// <summary>
    /// Journal entries give no benefit, so their locations hold other items, but the entry itself stays in the game: it is
    /// added to the player's inventory without the game's pickup message, so the only notification is the one for the check.
    /// </summary>
    internal static void KeepJournal(string definition, int amount)
    {
        if (IsJournal(definition) && !PlayerApi.Has(definition)) PlayerApi.Add(definition, amount);
    }

    /// <summary>The location of an object by its position, or null when it is not one (or we are not in a randomizer).</summary>
    internal static LocationRecord Find(Component component, float tolerance, params string[] types)
    {
        if (!ApClient.Active) return null;
        var scene = component.gameObject.scene.name;
        var loc = Catalog.Nearest(scene, component.transform.position, tolerance, types);
        if (loc == null && Plugin.CfgVerbose.Value)
            Plugin.Log.LogInfo($"No location for {component.name} in {scene} at {component.transform.position}");
        return loc;
    }

    /// <summary>
    /// Chests that spawn after an encounter (battle chests, head-chase chests) have no fixed position; they are matched
    /// by scene, taking the first such location that has not been checked yet.
    /// </summary>
    internal static LocationRecord FindSpawnedReward(Component chest) => FirstUnchecked(Catalog.Unplaced(chest.gameObject.scene.name, "head_chase", "ambush_chest"));

    /// <summary>
    /// Turns a pickup into a location check. Returns true when the pickup was replaced, so the caller skips the
    /// original method (which would add the vanilla item).
    /// </summary>
    internal static bool TryReplacePickup(Component pickup)
    {
        var loc = FindPickupLocation(pickup);
        if (loc == null) return false;
        Report(loc);
        var itemObject = pickup.GetComponentInChildren<ItemObject>(true);
        KeepJournal(itemObject?.Item?.ItemDefinition?.name, itemObject?.Amount ?? 1);
        Traverse.Create(pickup).Method("NotifyPickupSuccess").GetValue();
        SenbeiVisuals.Remove(pickup);
        return true;
    }

    /// <summary>
    /// Senbei have no distinguishing item and are matched by position; the few that an encounter hands out when it ends
    /// (settlement Senbei) are moved into place at that moment, so they take the first such location still open in the
    /// scene. Every other pickup is identified by the item it holds, which is the vanilla reward of one location (notes,
    /// key items such as the Pretty Flower, ...).
    /// </summary>
    private static LocationRecord FindPickupLocation(Component pickup)
    {
        if (!ApClient.Active) return null;
        var scene = pickup.gameObject.scene.name;
        if (pickup.name.Contains("Senbei"))
            return Find(pickup, SenbeiRadius, "senbei") ?? FirstUnchecked(Catalog.Settlement(scene));

        var definition = pickup.GetComponentInChildren<ItemObject>(true)?.Item?.ItemDefinition?.name;
        var loc = definition == null ? null : Catalog.ByVanillaItem(scene, definition);
        if (loc == null && Plugin.CfgVerbose.Value) Plugin.Log.LogInfo($"Pickup {pickup.name} ({definition}) is not a location");
        return loc;
    }

    private static LocationRecord FirstUnchecked(System.Collections.Generic.IEnumerable<LocationRecord> candidates) =>
        ApClient.Active ? candidates.FirstOrDefault(loc => !ApClient.State.CheckedLocations.Contains(loc.Id)) : null;
}
