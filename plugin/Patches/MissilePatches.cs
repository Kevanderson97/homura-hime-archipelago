using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The cut-content missile gun (Fortunate Misfortune) leaves its red targeting boxes on screen. MissileHUD spawns one box per
/// target and returns them to the pool only when the aim stops; when the aim is started again first (fire pressed again, a gun
/// switch, a cancelled charge) it forgets the boxes without returning them, so they are never updated or hidden again.
/// These two patches return the boxes before the list is forgotten and when the HUD is switched off.
/// </summary>
internal static class MissileHud
{
    internal static readonly System.Type Type = AccessTools.TypeByName("MissileHUD");

    /// <summary>Returns every box of the HUD to the pool and empties its list.</summary>
    internal static void DespawnAll(object hud)
    {
        var boxes = Traverse.Create(hud).Field("missileLockUIs").GetValue<IList>();
        var despawn = AccessTools.Method(AccessTools.TypeByName("SimplePool"), "Despawn", new[] { typeof(GameObject) });
        if (boxes == null || despawn == null) return;

        foreach (var box in boxes)
            if (box is Component component && component != null) despawn.Invoke(null, new object[] { component.gameObject });
        boxes.Clear();
    }
}

[HarmonyPatch]
internal static class MissileAimStartPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(MissileHud.Type, "MissileAssistAim_OnUseEvent");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __instance) => MissileHud.DespawnAll(__instance);
}

[HarmonyPatch]
internal static class MissileHudDisabledPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(MissileHud.Type, "OnDisable");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __instance) => MissileHud.DespawnAll(__instance);
}
