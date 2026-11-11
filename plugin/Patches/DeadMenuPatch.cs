using System.Collections;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Brings the death menu back when the game loses it (see <see cref="DeadMenuGuard"/>): a DeathLink can kill the player while the
/// window is in the background, and the long frame when it comes back switches the menu off for good.
/// </summary>
[HarmonyPatch]
internal static class DeadMenuPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("DeadHUD"), "Update");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(object __instance)
    {
        if (!ApClient.Active) return;

        var hud = Traverse.Create(__instance);
        if (!hud.Field("isUse").GetValue<bool>()) return; // the cheap test first: this runs every frame

        var menu = hud.Field("deadMenu").GetValue<Component>();
        var controller = hud.Field("playableDirectorController").GetValue();
        if (menu == null || controller == null || menu.gameObject.activeSelf) return;

        var director = Traverse.Create(controller).Field("playableDirector").GetValue();
        if (director == null) return;

        var directorTraverse = Traverse.Create(director);
        var paused = directorTraverse.Property("state").GetValue().ToString() == "Paused";
        var time = directorTraverse.Property("time").GetValue<double>();
        if (!DeadMenuGuard.NeedsRestore(true, false, paused, time, EnterPauseTime(controller))) return;

        directorTraverse.Method("Evaluate").GetValue();
        Plugin.Log.LogInfo($"The death menu had been switched off at {time:0.00} s of its timeline; switched on again");
    }

    /// <summary>Where the "Enter" part of the death screen's timeline stops (8 s in the game).</summary>
    private static double EnterPauseTime(object controller)
    {
        if (Traverse.Create(controller).Field("regionDict").GetValue() is IDictionary regions && regions.Contains("Enter"))
            return Traverse.Create(regions["Enter"]).Field("pauseTime").GetValue<float>();
        return double.NaN; // the comparison with NaN is false: do nothing
    }
}
