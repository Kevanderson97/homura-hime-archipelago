using System.Linq;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Core;
using HomuraHimeAP.Data;
using Opsive.UltimateInventorySystem.Core.DataStructures;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The pause-menu "Char Travel" option (Chapter Select) is hidden by a data-value filter until hub progress reaches 20.
/// Make it available from the start, so a missed location can always be revisited.
/// </summary>
[HarmonyPatch]
internal static class CharTravelAvailablePatch
{
    private const string FilterType =
        "Opsive.UltimateInventorySystem.UI.Panels.ItemViewSlotContainers.GridFilterSorters.InventoryGridFilters.ItemInfoDataValueCategoryFilter";

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName(FilterType), "EvaluateCondition");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(object __instance, ref bool __result)
    {
        if (!Plugin.CfgChapterSelectFromStart.Value || __result || !TutorialTracker.Done) return;
        // The System Options list carries the single instance with a threshold of 20 (post-game) for Char Travel.
        var threshold = Traverse.Create(__instance).Field("m_Threshold").GetValue<float>();
        if (threshold >= 20f) __result = true;
    }
}

/// <summary>
/// Filters the Chapter Select grid: later chapters and later sub-chapters stay hidden until the story (or, in
/// Archipelago modes, the unlock rules) allows them. Only ever hides entries, never reveals ones the game hides.
/// </summary>
[HarmonyPatch]
internal static class ChapterSelectFilterPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("ItemInfoAttributeValueFilter"), "Filter", new[] { typeof(ItemInfo) });

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(ItemInfo __0, ref bool __result)
    {
        if (!__result) return;
        var item = __0.Item;
        var name = item?.ItemDefinition?.name;
        if (name == null) return;

        if (ChapterData.TryGetHeader(name, out var chapter))
        {
            if (!ChapterAccess.IsChapterVisible(chapter)) __result = false;
        }
        else if (ChapterData.TryGetSubChapter(name, out chapter, out var index))
        {
            if (!ChapterAccess.IsSubChapterVisible(chapter, index)) __result = false;
        }
    }
}

/// <summary>
/// Every option of the pause menu's System page opens a panel (<c>OpenOtherPanelItemAction</c>), chosen by the item's
/// category. The Skip Tutorial button is of the category of "Load Latest Save", so it opens that confirmation; this tells
/// <see cref="SkipTutorialOption"/> which option did, so the question can be changed.
/// </summary>
[HarmonyPatch]
internal static class SkipTutorialActionPatch
{
    // (the base class has a second, parameterless InvokeActionInternal, so the name alone does not find the right one)
    private static MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(AccessTools.TypeByName("Opsive.UltimateInventorySystem.ItemActions.OpenOtherPanelItemAction"))
            .FirstOrDefault(method => method.Name == "InvokeActionInternal" && method.GetParameters().Length == 2);

    private static bool Prepare() => TargetMethod() != null;

    // (a problem here must never swallow the click: the option then does what it always did)
    private static void Prefix(ItemInfo __0)
    {
        try
        {
            SkipTutorialOption.OptionChosen(__0.Item?.ItemDefinition?.name);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("Skip Tutorial button (option chosen): " + e);
        }
    }
}

/// <summary>The confirm button of the "Load Latest Save" question: for the Skip Tutorial button it skips instead.</summary>
[HarmonyPatch]
internal static class SkipTutorialConfirmPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("SystemConfirmBoxActions"), "ConfirmAction");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(object __instance)
    {
        try
        {
            if (!SkipTutorialOption.Pending || Traverse.Create(__instance).Field("systemActionType").GetValue().ToString() != "ReturnToCheckPoint")
                return true;
            SkipTutorialOption.Confirmed(__instance);
            return false;
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("Skip Tutorial button (confirm): " + e);
            return true;
        }
    }
}
