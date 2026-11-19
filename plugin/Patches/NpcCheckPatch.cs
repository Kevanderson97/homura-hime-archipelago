using System.Linq;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using UnityEngine;
using HutongGames.PlayMaker;
using Opsive.UltimateInventorySystem.Core.InventoryCollections;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The NPCs that ask for an item (the Nurikabes, the elevator, the cabin) check it with
/// <c>NPCInventoryOperator.CheckItems</c>: every item on the NPC's "CheckItems" list must be in the player's inventory. A list
/// that is empty passes, and the list of a freshly loaded scene is empty until the NPC's inventory has been set up (about
/// four seconds of the load, measured at the Chapter 4 Nurikabe), so a check made in that time says "yes" to a player who has
/// nothing. In a randomizer game the items are what the logic counts on, so a check on a list that is not there yet fails.
/// </summary>
[HarmonyPatch]
internal static class NpcCheckPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("NPCInventoryOperator"), "CheckItems", new[] { typeof(string), typeof(string) });

    private static bool Prepare() => TargetMethod() != null;

    /// <summary>A passed check is wrong when nothing was asked for because the NPC had not been set up yet.</summary>
    internal static bool PassedForNothing(bool passed, int listed, bool setUp) => passed && listed == 0 && !setUp;

    private static void Postfix(object __instance, string _collectionName, ref bool __result)
    {
        if (!ApClient.Active || !__result) return;

        var traverse = Traverse.Create(__instance);
        var collection = traverse.Field("m_Inventory").GetValue<Inventory>()?.GetItemCollection(_collectionName);
        var listed = collection == null ? 0 : collection.GetAllItemStacks().Count();
        if (!PassedForNothing(true, listed, traverse.Field("beenInited").GetValue<bool>())) return;

        Plugin.Log.LogInfo("An item check on an NPC that is not set up yet was refused");
        __result = false;
    }
}

/// <summary>
/// The FSM of an NPC that asks for an item stores the result of <c>CheckItems_Default</c> in the boolean IsPlayerHaveItems and
/// branches on it right after. The boolean starts as true in some scenes and keeps that value when the check does not store a
/// result (a scene entered by teleporting, a check that fails), which gives the NPC the item the player does not have. The
/// branch is made on a fresh check instead.
/// </summary>
[HarmonyPatch]
internal static class NpcItemFlagPatch
{
    internal const string Flag = "IsPlayerHaveItems";

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HutongGames.PlayMaker.Actions.BoolTest"), "OnEnter");

    private static bool Prepare() => TargetMethod() != null && AccessTools.TypeByName("NPCInventoryOperator") != null;

    private static void Prefix(FsmStateAction __instance)
    {
        if (!ApClient.Active || Traverse.Create(__instance).Field("boolVariable").GetValue() is not FsmBool { Name: Flag } flag) return;

        var owner = __instance.Fsm?.GameObject;
        var asker = owner == null ? null : owner.transform.root.GetComponentInChildren(AccessTools.TypeByName("NPCInventoryOperator"), true);
        if (asker == null) return;

        var has = Traverse.Create(asker).Method("CheckItems_Default").GetValue<bool>();
        if (!has && Plugin.CfgCountReceived.Value && GiveReceivedItems(asker, owner.name))
            has = Traverse.Create(asker).Method("CheckItems_Default").GetValue<bool>();
        if (has != flag.Value) Plugin.Log.LogInfo($"{owner.name}: {Flag} was {flag.Value}, the inventory check says {has}");
        flag.Value = has;
    }

    /// <summary>
    /// An NPC that asks for an item also counts what the multiworld has sent (the CountReceivedItems setting). An item on the
    /// NPC's list that the player lacks but has received is given now, so the quest can go on (the NPC takes it next, as it
    /// would in the original game). Returns whether anything was given.
    /// </summary>
    private static bool GiveReceivedItems(Component asker, string ownerName)
    {
        var self = Traverse.Create(asker);
        var listName = self.Field("m_CheckItems").GetValue<string>();
        var collection = self.Field("m_Inventory").GetValue<Inventory>()?.GetItemCollection(listName);
        if (collection == null) return false;

        var asked = collection.GetAllItemStacks().Select(stack => stack.Item?.ItemDefinition?.name).Where(name => name != null);
        var missing = OwnedItems.ReceivedButNotOwned(asked, PlayerApi.Has, ApClient.HasReceived);
        foreach (var definition in missing)
        {
            Plugin.Log.LogWarning($"{ownerName} asks for {definition}, which was received but is not in the inventory: given now");
            PlayerApi.Add(definition);
        }

        return missing.Count > 0;
    }
}
