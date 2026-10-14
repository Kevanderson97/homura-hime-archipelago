using System;
using System.Collections.Generic;
using System.Reflection;
using HomuraHimeAP.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomuraHimeAP.UI;

/// <summary>
/// Shows notifications with the game's own "item added" HUD strip (the one that announces new skills, top left of the
/// screen). The strip's item-view prefab is cloned from the game's InventoryMonitor and stripped of its inventory
/// bindings, so any text can be shown with the game's look.
/// </summary>
internal static class HudNotifier
{
    private const int MaxVisible = 4;
    private const float StepY = 62f;
    private const float FadeIn = 0.35f;
    private const float Hold = 4f;
    private const float FadeOut = 0.5f;

    private const string MonitorType = "Opsive.UltimateInventorySystem.UI.Monitors.InventoryMonitor";
    private const string PrefabField = "m_ItemViewPrefab";

    /// <summary>Behaviours of the item-view prefab that fill it from an inventory item; removed from the clones.</summary>
    private static readonly string[] BindingComponents =
    {
        "CodexButtomItemView", "LocalizeItemView", "LocalizationTableItemView", "IconItemView", "ItemView",
        "LocalizeStringEvent", "TextRemoveRubyTags",
    };

    /// <summary>Parts of the prefab that belong to the item description line, hidden in our clones.</summary>
    private static readonly string[] HiddenParts = { "Content Panel Line2", "InputIcon", "countdown", "Dynamic" };

    private sealed class Entry
    {
        public GameObject Root;
        public Material[] Dissolves;
        public float Born;
    }

    private static readonly List<Entry> Visible = new();
    private static GameObject _prefab;
    private static RectTransform _container;
    private static int _layer;

    /// <summary>True when the game's HUD is loaded and notifications can be shown with it.</summary>
    internal static bool Available => EnsureContainer();

    internal static bool HasRoom => Visible.Count < MaxVisible;

    /// <summary>
    /// True when messages can be seen: the HUD strip exists, the gameplay panel it sits in is open (it is not during menus,
    /// cutscenes and loading) and the game has not hidden its HUD. Messages wait for this, so none is missed unseen.
    /// </summary>
    internal static bool OnScreen => EnsureContainer() && _container.gameObject.activeInHierarchy && !GameApi.HudHidden;

    internal static void Show(string text, Color tint, Sprite icon = null)
    {
        if (!EnsureContainer()) return;
        var root = Object.Instantiate(_prefab, _container, false);
        root.name = "AP Notification";
        StripBindings(root);
        foreach (var part in HiddenParts) SetActive(root, part, false);
        ApplyContent(root, text, tint, icon);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = _layer;

        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        root.SetActive(true);

        var entry = new Entry { Root = root, Born = Time.unscaledTime, Dissolves = DissolveMaterials(root) };
        Visible.Add(entry);
        Layout();
        SetDissolve(entry, 1f);
    }

    /// <summary>Advances the fade animations; call every frame.</summary>
    internal static void Update()
    {
        if (Visible.Count == 0) return;
        Visible.RemoveAll(shown => shown.Root == null); // the HUD went away with its scene
        if (Visible.Count == 0) return;
        if (!OnScreen)
        {
            foreach (var shown in Visible) shown.Born += Time.unscaledDeltaTime; // a message is not used up while it cannot be seen
            return;
        }

        var now = Time.unscaledTime;
        for (var i = Visible.Count - 1; i >= 0; i--)
        {
            var entry = Visible[i];
            if (entry.Root == null) { Visible.RemoveAt(i); continue; }

            var age = now - entry.Born;
            if (age > FadeIn + Hold + FadeOut)
            {
                Object.Destroy(entry.Root);
                Visible.RemoveAt(i);
                Layout();
                continue;
            }

            var dissolve = age < FadeIn ? 1f - age / FadeIn
                : age < FadeIn + Hold ? 0f
                : (age - FadeIn - Hold) / FadeOut;
            SetDissolve(entry, dissolve);
        }
    }

    // ------------------------------------------------------------------ setup

    /// <summary>Seconds between searches for the HUD while there is none (title screen, loading): a scene search is slow.</summary>
    private const float SearchInterval = 1f;

    private static Type _monitorType;
    private static float _nextSearch;

    private static bool EnsureContainer()
    {
        if (_container != null) return true;
        if (Time.unscaledTime < _nextSearch) return false;
        _nextSearch = Time.unscaledTime + SearchInterval;

        _monitorType ??= Type.GetType(MonitorType + ", Opsive.UltimateInventorySystem")
                         ?? HarmonyLib.AccessTools.TypeByName(MonitorType);
        var monitorType = _monitorType;
        if (monitorType == null) return false;
        var monitor = Object.FindAnyObjectByType(monitorType) as Component;
        if (monitor == null) return false;

        _prefab = monitorType.GetField(PrefabField, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(monitor) as GameObject;
        if (_prefab == null) return false;

        _layer = monitor.gameObject.layer;
        var go = new GameObject("AP Notifications", typeof(RectTransform)) { layer = _layer };
        _container = (RectTransform)go.transform;
        _container.SetParent(monitor.transform.parent, false);
        // left edge, just above the vertical middle of the screen, growing downwards
        _container.anchorMin = _container.anchorMax = new Vector2(0, 0.5f);
        _container.pivot = new Vector2(0, 1);
        _container.anchoredPosition = new Vector2(0, 60);
        _container.sizeDelta = new Vector2(485, 100);
        return true;
    }

    /// <summary>Removes everything that would refill the clone from an inventory item (order matters: dependants first).</summary>
    private static void StripBindings(GameObject root)
    {
        foreach (var name in BindingComponents)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != name) continue;
                try { Object.DestroyImmediate(component); }
                catch (Exception e) { Plugin.Log.LogWarning($"HUD notification: could not remove {name}: {e.Message}"); }
            }
        }
    }

    private static void ApplyContent(GameObject root, string text, Color tint, Sprite icon)
    {
        foreach (var graphic in root.GetComponentsInChildren<Text>(true))
            if (graphic.name == "Name") graphic.text = text;

        // the masks only exist for the game's own reveal animation and would hide the text
        foreach (var mask in root.GetComponentsInChildren<Mask>(true)) mask.enabled = false;
        foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;

        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "BG") image.color = tint;
            else if (image.name == "Icon")
            {
                var sprite = icon != null ? icon : Assets.Logo;
                if (sprite == null) continue;
                image.sprite = sprite;
                image.color = Color.white;
            }
        }
    }

    private static void SetActive(GameObject root, string childName, bool active)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) t.gameObject.SetActive(active);
    }

    // ------------------------------------------------------------------ animation

    private static void Layout()
    {
        for (var i = 0; i < Visible.Count; i++)
            ((RectTransform)Visible[i].Root.transform).anchoredPosition = new Vector2(0, -i * StepY);
    }

    /// <summary>The strip's background materials reveal themselves through a "_Dissolves" parameter (1 = hidden).</summary>
    private static Material[] DissolveMaterials(GameObject root)
    {
        var materials = new List<Material>();
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            var material = image.material;
            if (material != null && material.HasProperty("_Dissolves")) materials.Add(material);
        }

        return materials.ToArray();
    }

    private static void SetDissolve(Entry entry, float value)
    {
        foreach (var material in entry.Dissolves)
            if (material != null) material.SetFloat("_Dissolves", value);
    }
}
