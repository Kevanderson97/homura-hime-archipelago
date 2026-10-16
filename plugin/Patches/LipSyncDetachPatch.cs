using System.Reflection;
using FMOD;
using FMOD.Studio;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The game's lip-sync component (<c>uLipSyncFMOD</c>) taps the voice of an FMOD event with a DSP and takes the DSP off the
/// event's channel group when it is disabled. When a scene unloads while a voice line has just ended, the group is already
/// released by then (a skipped cutscene or dialogue stops its voice at once, which does the same), and removing the DSP from
/// the stale handle crashes FMOD (a native crash inside FMOD_System_Update, seen on the teleport from Level 4 Area 2 into the
/// Ikai; the game's own check only asks whether the handle is non-null). The DSP is removed only when the event still owns
/// the group the component attached to; otherwise the component just forgets it.
/// </summary>
[HarmonyPatch]
internal static class LipSyncDetachPatch
{
    private static bool _logged;

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("uLipSyncFMOD"), "SafeDetach");

    private static bool Prepare() => TargetMethod() != null;

    /// <summary>The DSP may be removed only from a group that its event still has.</summary>
    internal static bool GroupIsAlive(bool eventValid, bool eventHasGroup, bool sameGroup) => eventValid && eventHasGroup && sameGroup;

    private static void Prefix(MonoBehaviour __instance)
    {
        if (!ApClient.Active) return;

        var state = Traverse.Create(__instance);
        if (!state.Field("_dspAdded").GetValue<bool>()) return;

        var instance = state.Field("_eventInstance").GetValue<EventInstance>();
        var group = state.Field("_channelGroup").GetValue<ChannelGroup>();
        var valid = instance.isValid();
        var hasGroup = valid && instance.getChannelGroup(out var current) == RESULT.OK;
        var same = false;
        if (hasGroup)
        {
            instance.getChannelGroup(out var live);
            same = live.handle == group.handle;
        }

        if (GroupIsAlive(valid, hasGroup, same)) return;

        state.Field("_dspAdded").SetValue(false); // the game then releases its DSP without touching the dead group
        if (_logged) return;
        _logged = true;
        Plugin.Log.LogInfo("Lip-sync DSP left on a released audio group (it would have crashed FMOD)");
    }
}
