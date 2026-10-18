using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The hub reads its progress once, when its gameplay starts (<c>Hub_GameplayRoundManager.SetGameplayRoundUse</c>), to decide
/// which of its scenes and characters are up. The plugin's own hub progress is brought in step with the cleared worlds right
/// before that, so it holds whatever order the save data and the scene finished loading in (writing it when the scene loads was
/// not late enough: a value restored afterwards left the hub a stage behind).
/// </summary>
[HarmonyPatch]
internal static class HubSyncPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(GameApi.HubProgressManagerType, "SetGameplayRoundUse");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix()
    {
        if (WorldProgress.Active) WorldProgress.SyncHub();
    }
}
