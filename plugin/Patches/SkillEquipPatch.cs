using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The story hands out Phantom Blade (Skill_Blade Warp) by running EquipSkillController.EquipSkill at the start of Level 1
/// Area 2. In an Archipelago game that script reports the location "handover/phantom_blade" instead; the skill itself
/// only arrives as a received item (and is equipped from the skill menu like any other).
/// </summary>
[HarmonyPatch]
internal static class SkillEquipPatch
{
    private const string BladeWarp = "Skill_Blade Warp";
    private const string LocationKey = "handover/phantom_blade";

    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("EquipSkillController"), "EquipSkill", new[] { typeof(int) });

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(object __instance)
    {
        if (!ApClient.Active) return true; // not playing a randomizer: vanilla behaviour
        return !Report(__instance);
    }

    /// <summary>
    /// Reports the location when the controller is the one that equips Phantom Blade; true when it was. Also used when the node
    /// that holds the script is skipped as a tutorial (the check must not depend on the cutscene being played).
    /// </summary>
    internal static bool Report(object controller)
    {
        var definition = Traverse.Create(controller).Field("skillDefinition").GetValue<UnityEngine.Object>();
        if (definition == null || !IsBladeWarp(definition.name)) return false;

        var location = Catalog.Location(LocationKey);
        if (location != null)
        {
            ApClient.CheckLocation(location.Id);
            Plugin.Log.LogInfo("Phantom Blade script -> location check " + LocationKey);
        }

        return true;
    }

    internal static bool IsBladeWarp(string definition) => definition == BladeWarp;
}
