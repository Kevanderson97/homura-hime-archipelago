using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.UI;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// A Senbei lying on the map that now holds an Archipelago item looks like the Archipelago logo (tinted by how
/// important the item is) instead of a Senbei. The pickup, its hitbox and its behaviour stay as they are.
/// </summary>
internal static class SenbeiVisuals
{
    /// <summary>The pickup is still at the place it was recorded when it appears, so a small radius is enough.</summary>
    private const float SpawnRadius = 3f;
    private const float IconSize = 0.9f;
    private const string IconName = "AP Icon";

    /// <summary>
    /// The pickup was collected. The game hides only its model and switches the collider off (TriggerPickUp), and the logo is a
    /// child of the pickup itself, so without this it would stay in the air at the player's feet.
    /// </summary>
    internal static void Remove(Component pickup)
    {
        var icon = pickup.transform.Find(IconName);
        if (icon != null) Object.Destroy(icon.gameObject);
    }

    internal static void Apply(Component pickup)
    {
        if (!ApClient.Active || !pickup.name.Contains("Senbei")) return;
        if (pickup.transform.Find(IconName) != null) return;

        var loc = Catalog.Nearest(pickup.gameObject.scene.name, pickup.transform.position, SpawnRadius, "senbei");
        if (loc == null || ApClient.State.CheckedLocations.Contains(loc.Id)) return;

        foreach (var renderer in pickup.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

        var icon = new GameObject(IconName);
        icon.transform.SetParent(pickup.transform, false);
        icon.transform.localScale = Vector3.one * IconSize;
        var sprite = icon.AddComponent<SpriteRenderer>();
        sprite.sprite = Assets.Logo;
        var scouted = ScoutCache.Get(loc.Id);
        sprite.color = scouted == null ? Color.white : Toasts.ForFlags(scouted.Flags);
        icon.AddComponent<SenbeiBillboard>();
    }
}

[HarmonyPatch]
internal static class SenbeiVisualItemPickupPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("Opsive.UltimateInventorySystem.DropsAndPickups.ItemPickup"), "Start");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(Component __instance) => SenbeiVisuals.Apply(__instance);
}

[HarmonyPatch]
internal static class SenbeiVisualTriggerPickupPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("TriggerPickUp"), "OnEnable");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(Component __instance) => SenbeiVisuals.Apply(__instance);
}
