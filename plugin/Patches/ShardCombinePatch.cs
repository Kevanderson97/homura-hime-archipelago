using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Items;
using Opsive.UltimateInventorySystem.Core.DataStructures;
using Opsive.UltimateInventorySystem.Core.InventoryCollections;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Three shards combine into a crystal, and the crystals are what raise the health or spirit bar. The game's shard screen
/// (<c>ShardCrystalMonitor</c>) opens when a shard shows up in the inventory and combines one crystal, then looks no further. A
/// single pick-up never leaves a full set behind, but several shards received together do. When the shard screen is closed
/// and a full set is still there, the screen is opened again for it, the same way a new shard would open it, until fewer than
/// three shards are left.
/// </summary>
[HarmonyPatch]
internal static class ShardCombinePatch
{
    internal const int ShardsPerCrystal = 3;

    /// <summary>The panel is still closing right after the close; the next screen waits for it.</summary>
    private const float ReopenDelay = 0.6f;

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("ShardCrystalMonitor"), "CloseInternal");

    private static bool Prepare() => TargetMethod() != null;

    /// <summary>True when the shards left over make another crystal.</summary>
    internal static bool CrystalLeft(int shards) => shards >= ShardsPerCrystal;

    private static void Postfix(MonoBehaviour __instance)
    {
        if (!ApClient.Active) return;
        var definition = Traverse.Create(__instance).Field("m_FilterDefinition").GetValue<Opsive.UltimateInventorySystem.Core.ItemDefinition>();
        if (definition == null || !CrystalLeft(PlayerApi.AmountOwned(definition.name))) return;

        __instance.StartCoroutine(Reopen(__instance, definition.name));
    }

    private static IEnumerator Reopen(MonoBehaviour monitor, string shard)
    {
        yield return new WaitForSecondsRealtime(ReopenDelay);
        if (monitor == null || !CrystalLeft(PlayerApi.AmountOwned(shard))) yield break;

        var stack = PlayerApi.Inventory?.MainItemCollection.GetAllItemStacks().FirstOrDefault(s => s.Item?.ItemDefinition?.name == shard);
        if (stack == null) yield break;

        Plugin.Log.LogInfo($"{stack.Amount} {shard}s are left over: the shard screen opens again for the next crystal");
        Traverse.Create(monitor).Method("Pop", new[] { typeof(ItemInfo), typeof(ItemStack) }, new object[] { new ItemInfo(stack.Item, stack.Amount), stack }).GetValue();
    }
}
