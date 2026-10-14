using System;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>Maps the game's stage category (StageData.Category) to the location its clear reward pays out.</summary>
internal static class StageLocations
{
    internal const string Tutorial = "Level0";

    internal static LocationRecord For(string category)
    {
        if (!ApClient.Active) return null;
        return Catalog.Location(category == Tutorial ? "tutorial/clear" : "stage/" + category);
    }
}

/// <summary>
/// The results screen hands out the stage-clear reward through NPCInventoryOperator.Reward(onComplete, ..., category, rank).
/// The game waits for the reward panel to close before it continues, so the replacement reports the location and then
/// calls onComplete itself instead of showing the vanilla reward.
/// </summary>
[HarmonyPatch]
internal static class StageRewardPatch
{
    private static MethodBase TargetMethod()
    {
        var stageData = AccessTools.TypeByName("StageData");
        var category = stageData == null ? null : AccessTools.Inner(stageData, "Category");
        var rank = AccessTools.TypeByName("HonoHime.ScoreRankSystem.ScoreRank");
        if (category == null || rank == null) return null;
        return AccessTools.Method(AccessTools.TypeByName("NPCInventoryOperator"), "Reward",
            new[] { typeof(Action), typeof(bool), category, rank });
    }

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(object[] __args)
    {
        var loc = StageLocations.For(__args[2].ToString());
        if (loc == null) return true;
        LocationHooks.Report(loc);
        (__args[0] as Action)?.Invoke();
        return false;
    }
}

/// <summary>
/// The hub's currency boards pay Konpeito by rank when a stage is cleared. In a randomizer game none of them pays: the
/// Konpeito of the game comes from enemies, pickups and checks. The tutorial's board is the location "tutorial/clear" and is
/// reported instead; the boards of the other stages are only skipped (their item boards are the stage-clear locations).
/// </summary>
[HarmonyPatch]
internal static class CurrencyBoardPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("CurrencyStageRewarder"), "TryReward");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(object[] __args, ref bool __result)
    {
        if (!ApClient.Active) return true;

        if (__args[0].ToString() == StageLocations.Tutorial)
        {
            var loc = StageLocations.For(StageLocations.Tutorial);
            if (loc != null) LocationHooks.Report(loc);
        }

        __result = true; // the game goes on as if the board had paid
        return false;
    }
}

/// <summary>
/// Encounter clears: every BattleRound is a location, sent when the round finishes. Nothing is taken away, the fights
/// pay no item of their own.
/// </summary>
[HarmonyPatch]
internal static class EncounterClearPatch
{
    /// <summary>The recon identified a round by its own name and up to four parent names.</summary>
    private const int ChainLength = 5;

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("BattleRound"), "Finish");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(UnityEngine.Component __instance)
    {
        if (!ApClient.Active) return;
        var chain = Hierarchy.Chain(__instance.transform, ChainLength);
        var loc = Catalog.Encounter(__instance.gameObject.scene.name, chain);
        if (loc != null) LocationHooks.Report(loc);
        else if (Plugin.CfgVerbose.Value) Plugin.Log.LogInfo($"Encounter {chain} is not a location");
    }
}
