using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using HomuraHimeAP.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The way to the five boss fights of Chapters 6-8 needs the Soul Core of that boss (see <see cref="SoulCores"/>). Without it
/// nothing starts, so the player never sees a prompt they could not follow through. Three places cover the ways there:
/// a door's interact press (before its "Should we leave?" prompt), the walk into the trigger areas before Agasa and Mamiya
/// (before their dialogues) and, for everything else (the Chapter Select, automatic changes of scene), the teleport itself.
/// </summary>
internal static class SoulCoreGate
{
    private const float NoticeSeconds = 2f;

    private static float _noticeAt = float.MinValue;
    private static string _noticeFor;

    /// <summary>True, with a message, when the randomizer is in charge and the way to this destination needs a Soul Core the player lacks.</summary>
    internal static bool Blocks(string portalKey)
    {
        if (!ApClient.Active || !ApClient.Slot.SoulCores) return false;

        var missing = SoulCores.MissingFor(portalKey, PlayerApi.Has);
        if (missing == null) return false;

        Notify(missing);
        return true;
    }

    /// <summary>
    /// Whether this interact trigger (or walk-in trigger area) leads to a boss fight the player may not enter yet. A door is a
    /// small object: its trigger and its PortalController (the destination) sit within a few levels of each other.
    /// </summary>
    internal static bool BlocksDoor(Component triggerAction)
    {
        if (!ApClient.Active || !ApClient.Slot.SoulCores) return false;

        // the doors are all in the scenes of Chapters 6-8; any other trigger of the game ends here at once
        if (!triggerAction.gameObject.scene.name.StartsWith(ChapterScenePrefix)) return false;

        var holder = Lookup(triggerAction);
        return holder?.Key != null && Blocks(holder.Key);
    }

    private const string ChapterScenePrefix = "Level_FinalP";

    private sealed class Destination
    {
        internal string Key;
    }

    // looking for the portal next to a trigger searches a few levels of the scene, so each trigger is searched once
    private static readonly ConditionalWeakTable<Component, Destination> _gated = new();
    private static System.Type _controllerType;
    private static FieldInfo _selectField;

    /// <summary>
    /// The levels of Chapters 6-8 look up all their triggers while they load (behind the loading screen), so no trigger is
    /// searched in the middle of play. Triggers that appear later are searched on their first use.
    /// </summary>
    internal static void Init() => SceneManager.sceneLoaded += (scene, _) =>
    {
        if (!ApClient.Active || !ApClient.Slot.SoulCores || !scene.name.StartsWith(ChapterScenePrefix)) return;

        foreach (var typeName in new[] { "PlayMakerTriggerEnter", "TriggerAction" })
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null) continue;

            foreach (var root in scene.GetRootGameObjects())
            foreach (var trigger in root.GetComponentsInChildren(type, true))
                Lookup(trigger);
        }
    };

    /// <summary>The (remembered) destination needing a Soul Core of the portal next to the trigger; null when it cannot be told yet.</summary>
    private static Destination Lookup(Component trigger)
    {
        if (_gated.TryGetValue(trigger, out var holder)) return holder;

        // without the portal manager no destination can be told; the next use of the trigger tries again
        var manager = Object.FindAnyObjectByType(GameApi.PortalManagerType);
        _controllerType ??= AccessTools.TypeByName("PortalController");
        if (manager == null || _controllerType == null) return null;

        holder = new Destination { Key = GatedDestinationNear(trigger, manager) };
        _gated.Add(trigger, holder);
        return holder;
    }

    /// <summary>The destination that needs a Soul Core, of the PortalController within a few levels of the trigger (or null).</summary>
    private static string GatedDestinationNear(Component trigger, Object manager)
    {
        var transform = trigger.transform;
        for (var depth = 0; transform != null && depth < 3; depth++, transform = transform.parent)
        {
            foreach (var controller in transform.GetComponentsInChildren(_controllerType, true))
            {
                var destination = DestinationOf(controller, manager);
                if (SoulCores.NeededForDestination(destination) != null) return destination;
            }
        }

        return null;
    }

    /// <summary>The key of the sub-chapter a PortalController leads to.</summary>
    private static string DestinationOf(Component controller, Object manager)
    {
        _selectField ??= AccessTools.Field(controller.GetType(), "selectProperty");
        var select = _selectField?.GetValue(controller);
        if (select == null) return null;

        var get = AccessTools.Method(GameApi.PortalManagerType, "GetPortalData", new[] { select.GetType() });
        return get == null ? null : GameApi.PortalKey(get.Invoke(manager, new[] { select }));
    }

    internal static bool IsPlayer(Collider collider) =>
        PlayerApi.PlayerTransform is { } player && collider != null && (collider.transform == player || collider.transform.IsChildOf(player));

    /// <summary>One message per core every couple of seconds (a held button or a trigger that fires twice must not flood the screen).</summary>
    private static void Notify(string definition)
    {
        var now = Time.unscaledTime;
        if (definition == _noticeFor && now - _noticeAt < NoticeSeconds) return;

        (_noticeFor, _noticeAt) = (definition, now);
        Plugin.Log.LogInfo("Boss fight refused: " + SoulCores.Message(definition));
        Toasts.Show(SoulCores.Message(definition), Toasts.Error);
    }
}

/// <summary>
/// An interact press on a door (BehaviorManager.DoAction decides every frame whether the button counts for the trigger the
/// player stands in): a press at a door to a boss fight without its Soul Core does not count, so its prompt never opens.
/// </summary>
[HarmonyPatch]
internal static class SoulCoreDoorPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var type = AccessTools.TypeByName("HonoHime.CharacterSystem.BehaviorManager+DoAction");
        if (type == null) yield break;

        foreach (var name in new[] { "checkActionCondition", "checkActionHoldCondition" })
            if (AccessTools.Method(type, name) is { } method)
                yield return method;
    }

    private static bool Prepare() => TargetMethods().Any();

    // ApClient.Active is checked inside SoulCoreGate; this runs on the frame of a press only
    private static void Postfix(object __0, ref bool __result)
    {
        if (__result && __0 is Component triggerAction && SoulCoreGate.BlocksDoor(triggerAction)) __result = false;
    }
}

/// <summary>
/// The walks into a trigger area that play a dialogue by themselves and then move the player to a boss fight: the memory tree
/// at the end of the Ikai before Agasa, and the line across the end of the last area before Mamiya (whose portal sits next to
/// the trigger). Without the Soul Core the trigger does nothing (it fires again on the next entry).
/// </summary>
[HarmonyPatch]
internal static class SoulCoreWalkInPatch
{
    private const string AgasaScene = "Level_FinalP3_L5_Ikai_Main";

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("PlayMakerTriggerEnter"), "OnTriggerEnter");

    private static bool Prepare() => TargetMethod() != null;

    // ApClient.Active is checked inside SoulCoreGate
    private static bool Prefix(Component __instance, Collider __0)
    {
        if (!SoulCoreGate.IsPlayer(__0)) return true;

        if (__instance.gameObject.scene.name == AgasaScene
            && Hierarchy.Names(__instance.transform, 12).Contains(SoulCores.AgasaTreeRound))
            return !SoulCoreGate.Blocks(SoulCores.AgasaFight);

        return !SoulCoreGate.BlocksDoor(__instance);
    }
}
