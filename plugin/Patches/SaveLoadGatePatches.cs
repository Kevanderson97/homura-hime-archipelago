using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomuraHimeAP.Patches;

/// <summary>
/// A save that belongs to an Archipelago slot can only be loaded from the main menu while connected to that slot and seed. The
/// plugin prepares things at the title screen once it is connected (see TrapSpawner), and a save needs its slot from the moment
/// it loads: connecting after loading is not supported. The check is on the slot button of the Continue panel
/// (<c>SavePanelHandler.Continue(int index)</c>, what every slot button of the panel calls), so nothing has started when it says
/// no: the panel stays open and a message says why. The title menu's other load, <c>StartScreenManager.ContinueDirect</c>, reads a
/// fixed special slot of an event menu (index 100), never one of the three save slots, so it needs no check.
/// </summary>
[HarmonyPatch]
internal static class SavePanelContinuePatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("SavePanelHandler"), "Continue", new[] { typeof(int) });

    private static bool Prepare() => TargetMethod() != null;

    private static bool Prefix(int index) => SaveLoadGate.MayLoad(index);
}

internal static class SaveLoadGate
{
    /// <summary>True when the save in this slot may be loaded now; otherwise tells the player why.</summary>
    internal static bool MayLoad(int saveSlot)
    {
        try
        {
            var entry = SaveBinding.RecordOfSlot(saveSlot);
            var slot = ApClient.ConnectedSlot;
            if (SaveBinding.MayLoad(slot, ApClient.ConnectedSeed, entry)) return true;

            var message = SaveBinding.LoadBlockedMessage(slot, entry);
            Plugin.Log.LogWarning($"Loading save {saveSlot} was blocked: {message}");
            MenuNotice.Show(message); // not a toast: those wait for the in-game HUD, which the main menu does not have
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("The check of the save to load failed, loading it: " + e); // never lock the player out of their own save
            return true;
        }
    }
}

/// <summary>Remembers which kind of panel (NewGame or Continue) is being opened.</summary>
[HarmonyPatch]
internal static class SavePanelOpenPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("SavePanelHandler"), "OpenPanel");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __0) => SavePanelGate.Kind = __0?.ToString();
}

/// <summary>
/// While the Continue panel is open, the saves that cannot be loaded now (they belong to an Archipelago slot other than the
/// connection) are greyed out with the game's own filter (<c>SaveViewData.SetFiltered</c>: dimmed, not selectable), instead of
/// being refused after a click: a refused click left the panel's highlight in a broken state (two slots red, the arrow on the
/// wrong slot). It follows the connection live (connecting with F10 while the panel is open frees the matching saves), says
/// why in a banner, and moves the selection off a greyed slot. Slots the game itself filtered are left alone.
/// </summary>
internal static class SavePanelGate
{
    private static readonly Type PanelType = AccessTools.TypeByName("SavePanelHandler");
    private static readonly Type ViewType = AccessTools.TypeByName("SaveViewData");
    private static readonly FieldInfo Instance = AccessTools.Field(PanelType, "Ins");
    private static readonly FieldInfo IsShow = AccessTools.Field(PanelType, "isShow");
    private static readonly FieldInfo Views = AccessTools.Field(PanelType, "views");
    private static readonly FieldInfo CanvasOfPanel = AccessTools.Field(PanelType, "canvas");
    private static readonly FieldInfo GroupOfPanel = AccessTools.Field(PanelType, "panel");
    private static readonly FieldInfo ButtonOfView = AccessTools.Field(ViewType, "button");
    private static readonly FieldInfo EmptyOfView = AccessTools.Field(ViewType, "m_isEmpty");
    private static readonly FieldInfo DateOfView = AccessTools.Field(ViewType, "dataTime");
    private static readonly FieldInfo PlayTimeOfView = AccessTools.Field(ViewType, "playTime");
    private static readonly MethodInfo SetFiltered = AccessTools.Method(ViewType, "SetFiltered", new[] { typeof(bool) });
    private static readonly MethodInfo RebuildNavigation = AccessTools.Method(PanelType, "RebuildNavigation");

    private const string ContinueKind = "Continue";
    private const string LabelName = "HHAP_ApLabel";

    /// <summary>How far above the date line the label goes: the line of the save's title, at the right of its black bar (local units).</summary>
    private const float LabelRise = 61f;
    private const int SaveSlots = 3;

    /// <summary>The kind of the panel that was opened last ("NewGame" or "Continue").</summary>
    internal static string Kind;

    private static readonly HashSet<int> Filtered = new(); // the slots this gate greyed out
    private static readonly SaveEntry[] Records = new SaveEntry[SaveSlots];
    private static bool _wasShown;

    internal static void Tick()
    {
        if (Instance == null || IsShow == null || Views == null || SetFiltered == null || ButtonOfView == null) return;
        var panel = Instance.GetValue(null);
        if (!IsOnScreen(panel))
        {
            if (_wasShown) Release(panel);
            MenuNotice.Clear(); // the banners belong to the panel: gone with it, whether it was closed or a save was loaded
            return;
        }

        if (!_wasShown)
        {
            _wasShown = true;
            for (var i = 0; i < SaveSlots; i++) Records[i] = SaveBinding.RecordOfSlot(i); // read once per opening
        }

        try
        {
            ShowAssociations((Array)Views.GetValue(panel));
            if (Kind == ContinueKind) Apply(panel);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not grey out the saves of other slots: " + e.Message);
        }
    }

    /// <summary>Puts "Associated with AP: slot (seed)" at the top right of every save that belongs to an Archipelago slot.</summary>
    private static void ShowAssociations(Array views)
    {
        for (var i = 0; i < Math.Min(views.Length, SaveSlots); i++)
        {
            var view = views.GetValue(i);
            if (DateOfView?.GetValue(view) is not Text date) continue;

            var existing = date.rectTransform.parent.Find(LabelName);
            var record = EmptyOfView?.GetValue(view) is true ? null : Records[i];
            if (record == null)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                continue;
            }

            var label = existing != null ? existing.GetComponent<Text>() : CreateLabel(date, PlayTimeOfView?.GetValue(view) as Text);
            label.gameObject.SetActive(true);
            label.text = SaveBinding.AssociationLabel(record);
        }
    }

    /// <summary>
    /// A copy of the save's date text (so it has the game's font and sits in the same card), moved up to the title line and
    /// stretched to end where the Play Time text ends, right aligned.
    /// </summary>
    private static Text CreateLabel(Text date, Text playTime)
    {
        var label = UnityEngine.Object.Instantiate(date.gameObject, date.rectTransform.parent).GetComponent<Text>();
        label.name = LabelName;
        label.alignment = TextAnchor.MiddleRight;
        label.fontSize = 44;
        label.color = new Color(0.85f, 0.15f, 0.22f, 1f); // the red of the selected slot

        var rect = label.rectTransform;
        rect.anchoredPosition += new Vector2(0f, LabelRise);
        if (playTime != null)
        {
            var dateCorners = new Vector3[4];
            var playCorners = new Vector3[4];
            date.rectTransform.GetWorldCorners(dateCorners);
            playTime.rectTransform.GetWorldCorners(playCorners);
            var worldPerUnit = (dateCorners[2].x - dateCorners[0].x) / Mathf.Max(1f, date.rectTransform.rect.width);
            if (worldPerUnit > 0f) rect.sizeDelta = new Vector2((playCorners[2].x - dateCorners[0].x) / worldPerUnit, rect.sizeDelta.y);
        }

        return label;
    }

    private static void Apply(object panel)
    {
        var views = (Array)Views.GetValue(panel);
        var slot = ApClient.ConnectedSlot;
        var seed = ApClient.ConnectedSeed;
        var blockedSlots = new List<string>();
        var changed = false;

        for (var i = 0; i < Math.Min(views.Length, SaveSlots); i++)
        {
            var view = views.GetValue(i);
            if (ButtonOfView.GetValue(view) is not Button button) continue;

            var empty = EmptyOfView?.GetValue(view) is true;
            var blocked = !empty && !SaveBinding.MayLoad(slot, seed, Records[i]);
            if (blocked) blockedSlots.Add(Records[i].Slot);

            if (blocked && button.interactable) // (a slot the game filtered itself is not interactable and is left alone)
            {
                SetFiltered.Invoke(view, new object[] { true });
                Filtered.Add(i);
                changed = true;
            }
            else if (!blocked && Filtered.Remove(i))
            {
                SetFiltered.Invoke(view, new object[] { false });
                changed = true;
            }
        }

        MenuNotice.Pin(blockedSlots.Count == 0 ? null : SaveBinding.GreyedNotice(blockedSlots, slot));
        if (changed) RebuildNavigation?.Invoke(panel, null);
        MoveSelectionToUsableSlot(views);
    }

    /// <summary>If a greyed-out slot (or nothing) is selected, selects the first slot that can be loaded.</summary>
    private static void MoveSelectionToUsableSlot(Array views)
    {
        var system = EventSystem.current;
        if (system == null) return;

        var selected = system.currentSelectedGameObject;
        if (selected != null && selected.GetComponent<Selectable>() is { interactable: true }) return;

        for (var i = 0; i < Math.Min(views.Length, SaveSlots); i++)
            if (ButtonOfView.GetValue(views.GetValue(i)) is Button { interactable: true } usable)
            {
                system.SetSelectedGameObject(usable.gameObject);
                return;
            }
    }

    /// <summary>
    /// Whether the panel is really on screen: it still exists, the title screen is still the scene (loading a save or starting a
    /// game destroys the panel, but its <c>isShow</c> flag stays true), the flag is set, and the canvas and the panel's group
    /// are showing (closing the panel hides those).
    /// </summary>
    private static bool IsOnScreen(object panel)
    {
        if (panel is not UnityEngine.Object alive || alive == null || !SaveBinding.AtTitle) return false;
        if (IsShow.GetValue(panel) is not true) return false;
        if (CanvasOfPanel?.GetValue(panel) is Canvas canvas && canvas != null && !canvas.enabled) return false;
        if (GroupOfPanel?.GetValue(panel) is CanvasGroup group && group != null && group.alpha <= 0.01f) return false;
        return true;
    }

    /// <summary>The panel closed: the greyed slots are given back and the banner goes.</summary>
    private static void Release(object panel)
    {
        _wasShown = false;
        MenuNotice.Clear();
        if (Filtered.Count == 0) return;
        if (panel is not UnityEngine.Object alive || alive == null)
        {
            Filtered.Clear(); // the panel is gone, and its slots with it
            return;
        }

        try
        {
            var views = (Array)Views.GetValue(panel);
            foreach (var i in Filtered)
                if (i < views.Length) SetFiltered.Invoke(views.GetValue(i), new object[] { false });
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not give the greyed-out saves back: " + e.Message);
        }

        Filtered.Clear();
    }
}
