using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Items;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The Chapter 4 Nurikabe asks for fish in a dialogue that ends with Anne counting her three fish and a choice between giving
/// one and reconsidering. The game only gets here with the fish in hand. Fresh Fish is an Archipelago item now, so the player can
/// stand here without it; the dialogue then ends where the Nurikabe says it will not budge until it gets to eat fish.
/// </summary>
internal static class NurikabeDialogue
{
    internal const string Label = "Level4_C1A3_Dialogue_NurikabeMission";
    internal const string Fish = "Codex_Important_Level4_FreshFish";

    /// <summary>The last command that is played without the fish (the Nurikabe's "won't budge" line), counted from 0.</summary>
    internal const int LastCommandWithoutFish = 13;

    /// <summary>True for a command of the dialogue that comes after the line the Nurikabe's refusal ends on.</summary>
    internal static bool Cuts(string label, int commandIndex, bool hasFish) =>
        label == Label && commandIndex > LastCommandWithoutFish && !hasFish;
}

/// <summary>Ends the Nurikabe's dialogue early (see <see cref="NurikabeDialogue"/>) the way a scenario's own End command does.</summary>
[HarmonyPatch]
internal static class NurikabeDialoguePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "Utage.AdvCommandCharacter", "Utage.AdvCommandText" })
        {
            var method = AccessTools.Method(AccessTools.TypeByName(name), "DoCommand");
            if (method != null) yield return method;
        }
    }

    private static bool Prepare() => AccessTools.TypeByName("Utage.AdvCommandText") != null;

    private static bool Prefix(object __instance, object __0)
    {
        if (!ApClient.Active) return true;

        var player = Traverse.Create(__0).Property("ScenarioPlayer");
        var label = player.Property("MainThread").Field("CurrentLabelData");
        var name = label.Property("ScenarioLabel").GetValue<string>();
        if (name != NurikabeDialogue.Label) return true;

        var commands = label.Property("CommandList").GetValue() as IList;
        var index = commands?.IndexOf(__instance) ?? -1;
        if (!NurikabeDialogue.Cuts(name, index, PlayerApi.Has(NurikabeDialogue.Fish))) return true;

        player.Property("IsReservedEndScenario").SetValue(true);
        return false;
    }
}
