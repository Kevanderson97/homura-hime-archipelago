using System;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Tells <see cref="SaveBinding"/> which of the game's save slots is played, loaded or deleted. The game funnels them through
/// <c>GameSaveManager.Load/Delete(int slot, ...)</c> and the save manager's <c>OnSaveStarted(int slot)</c> event, which also
/// covers autosaves and the save on quitting.
/// </summary>
[HarmonyPatch]
internal static class SaveLoadedPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(SaveSlots.ManagerType, "Load", new[] { typeof(int), typeof(bool) });

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(int __0, bool __result)
    {
        if (__result) SaveSlots.Guard(() => SaveBinding.Loaded(__0));
    }
}

[HarmonyPatch]
internal static class SaveDeletedPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(SaveSlots.ManagerType, "Delete", new[] { typeof(int) });

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(int __0, bool __result)
    {
        if (__result) SaveSlots.Guard(() => SaveBinding.Deleted(__0));
    }
}

/// <summary>Shared pieces of the save-slot hooks, and the subscription to the save manager's "save started" event.</summary>
internal static class SaveSlots
{
    internal static Type ManagerType => AccessTools.TypeByName("HonoHime.SaveSystem.GameSaveManager");

    /// <summary>Runs a binding action; a failure must never break the game's own save or load.</summary>
    internal static void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { Plugin.Log.LogError("Save binding failed: " + e); }
    }

    /// <summary>Subscribes to the static <c>OnSaveStarted</c> event of the save manager; false when this game build lacks it.</summary>
    internal static bool Subscribe()
    {
        var add = AccessTools.Method(AccessTools.TypeByName("Opsive.UltimateInventorySystem.SaveSystem.SaveSystemManagerWithBackup"), "add_OnSaveStarted");
        if (add == null) return false;

        Action<int> handler = slot => Guard(() => SaveBinding.Saving(slot));
        add.Invoke(null, new object[] { handler });        return true;
    }
}
