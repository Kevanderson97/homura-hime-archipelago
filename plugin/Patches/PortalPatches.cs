using System;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Every scene change in a level (hub departures, area-to-area portals and the Chapter Select) ends in
/// PortalManager.Teleport(PortalData, Action), so this single hook is the place to observe, and later redirect,
/// level transitions.
/// </summary>
[HarmonyPatch]
internal static class TeleportPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(GameApi.PortalManagerType, "Teleport", new[] { GameApi.PortalDataType, typeof(Action) });

    private static bool Prepare() => GameApi.PortalDataType != null && TargetMethod() != null;

    private static bool Prefix(object __0, Action __1)
    {
        var key = GameApi.PortalKey(__0);
        if (Plugin.CfgVerbose.Value) Plugin.Log.LogInfo("Teleport -> " + key);

        // the Chapter Select lists the post-game Mihare Shrine from the start; before the post-game it leads to the story's
        if (PostGameHub.Redirect(key, __1)) return false;

        // a hub mission board leads to the story's next chapter; with another world order it leads to the next world instead
        if (HubBoards.Redirect(key, __1)) return false;

        // hub mission boards and story triggers start the level the story expects, which may not be unlocked yet
        if (ChapterAccess.IsLockedDestination(key))
        {
            Plugin.Log.LogInfo("Blocked teleport to locked chapter: " + key);
            UI.Toasts.Show("This chapter is not unlocked yet", UI.Toasts.Error);
            return false;
        }

        ChapterAccess.OnPortalUsed(key);
        return true;
    }
}
