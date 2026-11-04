using HarmonyLib;

namespace HomuraHimeAP.Core;

/// <summary>
/// The hub's great Torii gate. It is not one object: every stage and story state of the hub has its own interaction trigger at
/// the gate (about thirty, named "TriggerAction", "TriggerAction DE Departure", "TriggerAction_Dialog_StageBase_大鳥居 ..." and so
/// on, one for the dialogue before leaving, one for leaving, one for the way back into a level), each playing the story's
/// dialogue and sending the player to the chapter the story expects next. With freeform level keys the player picks the world,
/// so any of them opens the Chapter Select instead. That is what the gate of the post-game hub does: its interaction ends with
/// <c>HHMenuPanelOpener.OpenInventoryChapterPortalPanel</c>, which resets the input permissions and calls
/// <c>MainMenuAndDisplayPanelHandler.OpenChapterPortal</c>. The triggers are told apart from the hub's other interactions (the
/// NPCs, whose triggers are called "NPC_MVC_Controller") by their name and by standing at the gate.
/// </summary>
internal static class HubGate
{
    private const string HubScene = "HubWorld_Main";

    /// <summary>Every trigger of the gate is called "TriggerAction ...".</summary>
    private const string TriggerName = "TriggerAction";

    // where the gate's triggers stand (they all sit within a metre of (0, 2, 65.4))
    private const float GateX = 0f, GateY = 2f, GateZ = 65.4f, GateRadius = 3f;

    /// <summary>
    /// Whether using the trigger called <paramref name="objectName"/> at (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>) in <paramref name="scene"/> opens the Chapter Select.
    /// </summary>
    internal static bool OpensChapterSelect(UnlockMode mode, string scene, string objectName, float x, float y, float z) =>
        mode == UnlockMode.Freeform && scene == HubScene && objectName != null && objectName.StartsWith(TriggerName) &&
        System.Math.Abs(x - GateX) <= GateRadius && System.Math.Abs(y - GateY) <= GateRadius && System.Math.Abs(z - GateZ) <= GateRadius;

    /// <summary>Opens the Chapter Select the way the post-game gate does. False when this game build has no such menu.</summary>
    internal static bool OpenChapterSelect()
    {
        var handler = AccessTools.Field(AccessTools.TypeByName("MainMenuAndDisplayPanelHandler"), "ins")?.GetValue(null);
        var open = handler == null ? null : AccessTools.Method(handler.GetType(), "OpenChapterPortal");
        if (open == null) return false;

        var permissionType = AccessTools.TypeByName("InputHUDPermissionManager");
        var permissions = permissionType == null ? null : AccessTools.Property(permissionType, "Ins")?.GetValue(null);
        if (permissions != null) AccessTools.Method(permissionType, "ResetStat")?.Invoke(permissions, null);

        open.Invoke(handler, null);
        return true;
    }
}
