using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HomuraHimeAP.Archipelago;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The Transformation meter charges from hits and from parries, but the player only feeds it when she owns the ultimate
/// finisher (<c>TransformationSystem.IsHaveSpecialArtAblity</c>). The meter itself (<c>AddChargeValue</c>) accepts either
/// ability, so with Transformation alone it stayed empty unless something else charged it (the Ultimate Charge item, a
/// story event). In a randomizer game the two abilities are separate items, so either one lets the meter charge.
/// </summary>
internal static class TransformationCharge
{
    /// <summary>The meter charges when the player has the ultimate, or in a randomizer game when she has Transformation.</summary>
    internal static bool Decide(bool hasUltimate, bool hasTransformation, bool randomizer) =>
        hasUltimate || (randomizer && hasTransformation);

    /// <summary>Replaces the game's <c>IsHaveSpecialArtAblity</c> in the two places that charge the meter.</summary>
    internal static bool CanCharge() =>
        Decide(Property("IsHaveSpecialArtAblity"), Property("IsHaveTransformationAbility"), ApClient.Active);

    private static bool Property(string name) =>
        AccessTools.Property(AccessTools.TypeByName("TransformationSystem"), name)?.GetValue(null) as bool? ?? false;
}

[HarmonyPatch]
internal static class TransformationChargePatch
{
    private static readonly string[] Methods = { "DamageManager_DamageCallbackEvent", "OnGuardSuccessEvent" };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var player = AccessTools.TypeByName("Player");
        return player == null ? Enumerable.Empty<MethodBase>() : Methods.Select(name => AccessTools.Method(player, name)).Where(m => m != null);
    }

    private static bool Prepare() => TargetMethods().Count() == Methods.Length;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var replacement = AccessTools.Method(typeof(TransformationCharge), nameof(TransformationCharge.CanCharge));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(AccessTools.PropertyGetter(AccessTools.TypeByName("TransformationSystem"), "IsHaveSpecialArtAblity")))
                yield return new CodeInstruction(OpCodes.Call, replacement);
            else
                yield return instruction;
        }
    }
}
