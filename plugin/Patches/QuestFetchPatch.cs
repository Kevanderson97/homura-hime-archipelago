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

/// <summary>
/// A quest that is finished without the hand-over it pays a location for (see <see cref="QuestFetch.Completion"/>) still checks
/// that location: the Level 1 Area 1 quest can be finished with a Pretty Little Flower that the multiworld sent, and nothing else
/// would ever report the hand-over of the one the story gives. Finishing the quest the usual way has checked it already, so this
/// does nothing then.
/// </summary>
[HarmonyPatch]
internal static class QuestCompletionPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Gameplay"), "Finish", Type.EmptyTypes);

    private static bool Prepare()
    {
        if (TargetMethod() != null) return true;
        Plugin.Log.LogWarning("Gameplay.Finish was not found: a quest finished early will not check its hand-over location");
        return false;
    }

    private static void Postfix(MonoBehaviour __instance)
    {
        if (!ApClient.Active || ApClient.State == null) return;

        var quest = QuestFetch.FindCompletion(__instance.gameObject.scene.name, __instance.name);
        if (quest == null) return;

        foreach (var key in quest.Locations)
        {
            // only a location of this slot (Catalog.Location leaves out what the options switched off) that is not checked yet
            if (Catalog.Location(key) is not { } loc || ApClient.State.CheckedLocations.Contains(loc.Id)) continue;
            Plugin.Log.LogInfo($"Quest {__instance.name} finished without {loc.Name}: checking it now");
            LocationHooks.Report(loc);
        }
    }
}

