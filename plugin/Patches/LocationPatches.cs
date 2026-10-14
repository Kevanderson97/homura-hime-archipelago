using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>Treasure chests: AddItemToPlayer hands out the chest table's items.</summary>
[HarmonyPatch]
internal static class ChestPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("TreasureChestAddItem"), "AddItemToPlayer");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(Component __instance)
    {
        var loc = LocationHooks.Find(__instance, 2f, "chest") ?? LocationHooks.FindSpawnedReward(__instance);
        if (loc == null) return true;
        LocationHooks.Report(loc);
        return false;
    }
}

/// <summary>Game-made pickups (TriggerPickUp) that add an item to the player's collection when touched.</summary>
[HarmonyPatch]
internal static class TriggerPickupPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("TriggerPickUp"), "TryAddItemToCollection");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(Component __instance) => !LocationHooks.TryReplacePickup(__instance);
}

/// <summary>
/// Opsive ItemPickup components: the Senbei lying around the maps, notes and other world pickups. The pickup still
/// disappears and plays its feedback, only the item is not added.
/// </summary>
[HarmonyPatch]
internal static class ItemPickupPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("Opsive.UltimateInventorySystem.DropsAndPickups.ItemPickup"),
            "TryAddItemToCollection");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(Component __instance) => !LocationHooks.TryReplacePickup(__instance);
}
