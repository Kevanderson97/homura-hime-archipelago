using System;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The second area of Level 1 has a tutorial about the skill buttons: an enemy that cannot be hurt shoots a laser across a
/// gap, and the player is meant to use a skill to get over. Reaching the far side without a skill (the option
/// advanced_movement_required: parrying the laser carries you over) sends the player back to the start. For a randomizer
/// game the tutorial is completed by reaching the far side instead, and its prompts to parry and to use a skill are gone.
/// The game's own check at the far side is "has the player used a skill": the tutorial's FSM tests the boolean isUseSkill
/// when the player enters the trigger, completes the tutorial (removing the enemy) when it is true and resets the player
/// otherwise.
/// </summary>
internal static class LaserTutorial
{
    internal const string Area = "Level_1_Area_2_Main";
    private const string SkillFlag = "isUseSkill";
    private const string SkillDialogPrefix = "UtageSayDialog_Skill";
    private static readonly string[] TipNames = { "技能教學L1A2", "彈刀" };

    /// <summary>True while the player is in the area with the tutorial and plays a randomizer game.</summary>
    internal static bool Applies(string scene) => ApClient.Active && scene == Area;

    /// <summary>True for the flag the far side's check reads.</summary>
    internal static bool IsSkillFlag(string variable) => variable == SkillFlag;

    /// <summary>True for the two subtitles of the tutorial ("UtageSayDialog_Skill1" and "_Skill2").</summary>
    internal static bool IsSkillDialog(string dialog) => dialog != null && dialog.StartsWith(SkillDialogPrefix, StringComparison.Ordinal);

    /// <summary>True for the side tips about the skill buttons and the parry (named like "LeftSideTipPage - 技能教學L1A2").</summary>
    internal static bool IsTip(string tip)
    {
        if (tip == null) return false;
        foreach (var name in TipNames)
            if (tip.EndsWith(name, StringComparison.Ordinal)) return true;
        return false;
    }
}

/// <summary>The far side's check passes: the trigger completes the tutorial for every player who reaches it.</summary>
[HarmonyPatch]
internal static class LaserTutorialCheckPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HutongGames.PlayMaker.Actions.BoolTest"), "OnEnter");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(FsmStateAction __instance)
    {
        var flag = Traverse.Create(__instance).Field("boolVariable").GetValue() as FsmBool;
        if (flag == null || !LaserTutorial.IsSkillFlag(flag.Name)) return;
        var owner = __instance.Fsm?.GameObject;
        if (owner == null || !LaserTutorial.Applies(owner.scene.name)) return;
        flag.Value = true;
    }
}

/// <summary>The two subtitles that ask for a skill do not appear.</summary>
[HarmonyPatch]
internal static class LaserTutorialSubtitlePatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("SayDialogByTypeAction"), "OnEnter");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(FsmStateAction __instance)
    {
        var owner = __instance.Fsm?.GameObject;
        if (owner == null || !LaserTutorial.Applies(owner.scene.name)) return true;
        var dialog = Traverse.Create(__instance).Field("utageSayDialog").GetValue() as Component;
        if (dialog == null || !LaserTutorial.IsSkillDialog(dialog.name)) return true;
        __instance.Finish();
        return false;
    }
}

/// <summary>The side tips about the skill buttons and the parry do not appear.</summary>
[HarmonyPatch]
internal static class LaserTutorialTipPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("SideTip"), "Show");

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(Component __instance) =>
        !(LaserTutorial.Applies(__instance.gameObject.scene.name) && LaserTutorial.IsTip(__instance.name));
}

/// <summary>A tip that the tutorial turns on by activating its object (which shows it from OnEnable) is dismissed again.</summary>
[HarmonyPatch]
internal static class LaserTutorialTipEnablePatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("SideTip"), "OnEnable");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(Component __instance)
    {
        if (!LaserTutorial.Applies(__instance.gameObject.scene.name) || !LaserTutorial.IsTip(__instance.name)) return;
        try
        {
            Traverse.Create(__instance).Method("Hide").GetValue();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not dismiss the tip " + __instance.name + ": " + e.Message);
        }
    }
}
