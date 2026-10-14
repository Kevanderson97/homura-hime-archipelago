using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The second area of Level 1 teaches the skill buttons with a side tip that stays on screen until the player has used a
/// skill (nothing hides it otherwise). A player who gets over the gap without a dash skill (the option
/// advanced_movement_required: parrying the laser carries you over) has no skill to use, so the tip would stay for good.
/// Hitting the enemy that shoots the laser dismisses it as well.
/// </summary>
[HarmonyPatch]
internal static class LaserTutorialPatch
{
    private const string Area = "Level_1_Area_2_Main";
    private const string LaserEnemy = "Enemy_Splitter_A_Tutorial";
    private const string TipName = "技能教學L1A2";

    private static int _dismissedIn = -1;

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHime.CharacterSystem.Character"), "SetDamage");

    private static bool Prepare() => TargetMethod() != null;

    /// <summary>True for the tutorial enemy next to the gap (named like "Enemy_Splitter_A_Tutorial (木樁) Variant").</summary>
    internal static bool IsLaserEnemy(string scene, string objectName) =>
        scene == Area && objectName != null && objectName.StartsWith(LaserEnemy, StringComparison.Ordinal);

    private static void Postfix(Component __instance)
    {
        if (!ApClient.Active) return;
        var scene = __instance.gameObject.scene;
        if (scene.handle == _dismissedIn || !IsLaserEnemy(scene.name, __instance.gameObject.name)) return;

        try
        {
            var tip = UnityEngine.Object.FindObjectsByType(AccessTools.TypeByName("SideTip"), FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Cast<Component>().FirstOrDefault(c => c.name.Contains(TipName));
            if (tip == null) return;
            Traverse.Create(tip).Method("Hide").GetValue();
            _dismissedIn = scene.handle;
            Plugin.Log.LogInfo("The skill tutorial of Level 1 Area 2 was dismissed by hitting the laser enemy");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not dismiss the skill tutorial: " + e.Message);
        }
    }
}
