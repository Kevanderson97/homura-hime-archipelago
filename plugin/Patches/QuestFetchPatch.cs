using System;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using UnityEngine;
using UnityEngine.Events;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Finishes the fetch step of an item quest (see <see cref="QuestFetch"/>) when the player already owns the item. The step is a
/// story node (<c>Gameplay</c>) that is finished the way the tutorial nodes are: its start event, <c>StartGameplay</c> and
/// <c>Finish</c>. The locations the step would have paid out are reported, since nothing else will report them.
/// </summary>
[HarmonyPatch]
internal static class QuestFetchPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Gameplay"), "Use");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(MonoBehaviour __instance)
    {
        if (!ApClient.Active) return true;

        var step = QuestFetch.Find(__instance.gameObject.scene.name, __instance.name);
        if (step == null || !QuestFetch.ShouldSkip(ApClient.Active, PlayerApi.Has(step.Item))) return true;

        try
        {
            var node = Traverse.Create(__instance);
            node.Field("OnUseEvent").GetValue<UnityEvent>()?.Invoke();
            node.Method("StartGameplay").GetValue();
            FinishFights(__instance);
            foreach (var key in step.Locations)
                if (Catalog.Location(key) is { } loc) LocationHooks.Report(loc);
            node.Method("Finish").GetValue();
            Plugin.Log.LogInfo($"Quest fetch step skipped, the player owns {step.Item}: {__instance.name}");
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not skip the quest step {__instance.name}: {e.Message}");
            return true;
        }
    }

    /// <summary>
    /// The fights of the step end as if they were won: the fight reports its encounter location when it finishes, and its
    /// finish event is what hands out the reward (the quest item, a chest) and opens what the fight blocks.
    /// </summary>
    private static void FinishFights(Component step)
    {
        var fightType = AccessTools.TypeByName("BattleRound");
        if (fightType == null) return;
        foreach (var fight in step.GetComponentsInChildren(fightType, true))
        {
            try
            {
                var round = Traverse.Create(fight);
                round.Method("Finish").GetValue();
                round.Field("OnFinishEvent").GetValue<UnityEvent>()?.Invoke();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not finish the fight {fight.name} of the skipped quest step: {e.Message}");
            }
        }
    }
}