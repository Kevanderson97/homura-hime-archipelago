using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Patches;

/// <summary>
/// With freeform level keys the hub's Torii gate opens the Chapter Select (see <see cref="HubGate"/>). When the player uses an
/// interaction, the character's behavior manager starts the "action" behavior for its TriggerAction (UseBehavior), which raises
/// the trigger's event (that starts the gate's dialogue and departure) and calls TriggerBehavior. Stopping UseBehavior itself,
/// before any of that, leaves nothing running: the player is not locked, the gate stays usable and nothing needs to be finished.
/// </summary>
[HarmonyPatch]
internal static class HubGatePatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("HonoHime.CharacterSystem.BehaviorManager"), "UseBehavior",
            new[] { typeof(string), AccessTools.TypeByName("TriggerAction") });

    private static bool Prepare() => TargetMethod() != null && AccessTools.TypeByName("MainMenuAndDisplayPanelHandler") != null;

    private static bool Prefix(Component triggerAction)
    {
        if (!ApClient.Active || triggerAction == null) return true;

        var position = triggerAction.transform.position;
        if (!HubGate.OpensChapterSelect(ApClient.Slot.UnlockMode, SceneManager.GetActiveScene().name, triggerAction.gameObject.name,
                position.x, position.y, position.z))
            return true;

        if (!HubGate.OpenChapterSelect()) return true; // the game has no such menu: the gate works as it always did

        Plugin.Log.LogInfo($"Torii gate ({triggerAction.gameObject.name}): Chapter Select (freeform level keys)");
        return false;
    }
}
