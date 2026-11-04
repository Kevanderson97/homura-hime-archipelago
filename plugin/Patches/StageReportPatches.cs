using System;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Swaps the stage category of the hub's post-mission report and clear reward for the world that was just cleared (see
/// <see cref="StageReport"/>). The category is a value of the FSM action itself, so it is put back right after the action has
/// read it: the action must still be the vanilla one for a game that is not a randomizer game.
/// </summary>
internal static class StageReportSwap
{
    private static readonly Type CategoryType = AccessTools.Inner(AccessTools.TypeByName("StageData"), "Category");

    /// <summary>The category the action was set up with (what the hub asks for), or null when it cannot be read.</summary>
    internal static string Shown(object action)
    {
        var field = Traverse.Create(action).Field("category").GetValue();
        return field == null ? null : AccessTools.Property(field.GetType(), "Value")?.GetValue(field)?.ToString();
    }

    /// <summary>Replaces the action's category when a world was just cleared; returns what to put back, or null.</summary>
    internal static object[] Swap(object action)
    {
        var pending = ApClient.State?.PendingReport;
        if (!ApClient.Active || pending == null || CategoryType == null) return null;

        var field = Traverse.Create(action).Field("category").GetValue();
        var value = field == null ? null : AccessTools.Property(field.GetType(), "Value");
        var replacement = StageReport.Replacement(value?.GetValue(field)?.ToString(), pending);
        if (replacement == null) return null;

        var original = value.GetValue(field);
        value.SetValue(field, Enum.Parse(CategoryType, replacement));
        Plugin.Log.LogInfo($"Post-mission screen: {pending} instead of {original}");
        return new[] { field, original };
    }

    internal static void Restore(object[] swapped)
    {
        if (swapped != null) AccessTools.Property(swapped[0].GetType(), "Value").SetValue(swapped[0], swapped[1]);
    }
}

/// <summary>The report screen of the hub (UseStageResultManager).</summary>
[HarmonyPatch]
internal static class StageReportScreenPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UseStageResultManager"), "OnEnter");

    private static bool Prepare() => TargetMethod() != null && StageReportPatchesAvailable.Yes;

    private static void Prefix(object __instance, out object[] __state) => __state = StageReportSwap.Swap(__instance);

    private static void Postfix(object[] __state) => StageReportSwap.Restore(__state);
}

/// <summary>
/// The clear reward that follows the report (StageResultRewardAction). It is the one that is done with the cleared world,
/// so the world stops being "just cleared" here.
/// </summary>
[HarmonyPatch]
internal static class StageRewardCategoryPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("StageResultRewardAction"), "OnEnter");

    private static bool Prepare() => TargetMethod() != null && StageReportPatchesAvailable.Yes;

    private static void Prefix(object __instance, out object[] __state)
    {
        __state = StageReportSwap.Swap(__instance);

        // a chapter's reward ends the "just cleared" state, whether or not the hub asked for the right chapter
        var pending = ApClient.Active ? ApClient.State?.PendingReport : null;
        if (pending != null && StageReport.IsChapter(StageReportSwap.Shown(__instance)))
        {
            ApClient.State.PendingReport = null;
            ApClient.State.Save();
        }
    }

    private static void Postfix(object[] __state) => StageReportSwap.Restore(__state);
}

/// <summary>True when the stage categories exist in this game build (the patches do nothing without them).</summary>
internal static class StageReportPatchesAvailable
{
    internal static readonly bool Yes = AccessTools.Inner(AccessTools.TypeByName("StageData"), "Category") != null;
}
