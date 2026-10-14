using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Hub progress may only go up. The vanilla game rewrites it on every level entry and every boss clear, so replaying an
/// earlier chapter (which the Chapter Select now allows from the start) would otherwise push it back and re-hide shop
/// tiers and hub objects.
/// </summary>
[HarmonyPatch]
internal static class ProgressGuardPatch
{
    /// <summary>Set while the plugin itself (or a debug tool) needs to write a lower value on purpose.</summary>
    public static bool Bypass;

    private static MethodBase TargetMethod() =>
        AccessTools.Method(GameApi.HubProgressManagerType, "SetDataValue", new[] { typeof(int) });

    private static bool Prepare() => TargetMethod() != null;

    // The game names the parameter "_value"; inject it by position so a rename cannot break the patch.
    private static bool Prefix(int __0)
    {
        if (Bypass) return true;
        if (WorldProgress.Active)
        {
            WorldProgress.HandleHubWrite(__0);
            return false;
        }

        if (!Plugin.CfgMonotonicProgress.Value) return true;
        var current = GameApi.HubProgress;
        if (current >= 0 && __0 <= current)
        {
            if (Plugin.CfgVerbose.Value)
                Plugin.Log.LogInfo($"Progress guard: ignored write {__0} (current {current})");
            return false;
        }

        return true;
    }

    private static void Postfix(int __0, bool __runOriginal)
    {
        // in a randomizer game WorldProgress keeps the sub-chapter bookkeeping itself
        if (__runOriginal && !WorldProgress.Active) ChapterAccess.OnHubProgressWritten(__0);
    }
}
