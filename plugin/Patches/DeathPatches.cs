using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;

namespace HomuraHimeAP.Patches;

/// <summary>DeathLink hooks: the game's own death flow (death menu, then continue or back to the menu; a revive needs no hook).</summary>
[HarmonyPatch]
internal static class PlayerDiedPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("DeadController"), "UseDead");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix() => DeathLinkHandler.OnPlayerDied();
}

[HarmonyPatch]
internal static class PlayerContinuePatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("DeadMenu"), "Continue");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix() => DeathLinkHandler.OnPlayerGaveUp();
}

[HarmonyPatch]
internal static class PlayerBackToMenuPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("DeadMenu"), "BackToMainMenu");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix() => DeathLinkHandler.OnPlayerGaveUp();
}
