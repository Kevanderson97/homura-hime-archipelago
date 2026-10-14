using System.Collections.Generic;
using global::Archipelago.MultiClient.Net.Enums;
using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>
/// Notification queue. Messages are shown with the game's own HUD strip (see <see cref="HudNotifier"/>); while the HUD is
/// not loaded (title screen) they fall back to simple colored IMGUI labels in the top-right corner.
/// </summary>
internal static class Toasts
{
    internal static readonly Color Progression = Hex(0xAF, 0x99, 0xEF);
    internal static readonly Color Useful = Hex(0x6D, 0x8B, 0xE8);

    /// <summary>Progression and useful at once.</summary>
    internal static readonly Color Gold = Hex(0xFF, 0xC8, 0x3D);
    internal static readonly Color Filler = Hex(0x00, 0xEE, 0xEE);
    internal static readonly Color Trap = Hex(0xFA, 0x80, 0x72);
    internal static readonly Color Info = Color.white;
    internal static readonly Color Error = new(1f, 0.45f, 0.45f);

    private const float Lifetime = 4f;
    private const int MaxVisible = 6;

    private static readonly List<Toast> Visible = new();
    private static readonly ToastQueue Pending = new();
    private static GUIStyle _style;

    internal static Color ForFlags(ItemFlags flags)
    {
        if ((flags & ItemFlags.Trap) != 0) return Trap;
        if ((flags & ItemFlags.Advancement) != 0 && (flags & ItemFlags.NeverExclude) != 0) return Gold;
        if ((flags & ItemFlags.Advancement) != 0) return Progression;
        if ((flags & ItemFlags.NeverExclude) != 0) return Useful;
        return Filler;
    }

    /// <summary>How many item notifications are shown (the `ItemNotifications` setting).</summary>
    internal static ToastVerbosity Verbosity
    {
        get => Pending.Verbosity;
        set => Pending.Verbosity = value;
    }

    /// <summary>Queues a status message (connection, errors, goal). The icon is shown next to it; null means the Archipelago logo.</summary>
    internal static void Show(string text, Color color, Sprite icon = null) =>
        Pending.Enqueue(new Toast { Text = text, Color = color, Icon = icon, Important = true });

    /// <summary>An item with none of the classifications (progression, useful, trap).</summary>
    internal static bool IsPlainFiller(ItemFlags flags) => (flags & (ItemFlags.Advancement | ItemFlags.NeverExclude | ItemFlags.Trap)) == 0;

    /// <summary>The colour of an item's name: its classification, except for the plain filler of this game, which stays uncoloured.</summary>
    internal static Color? NameColor(ItemFlags flags, bool fromOtherPlayer) =>
        !fromOtherPlayer && IsPlainFiller(flags) ? null : ForFlags(flags);

    /// <summary>Queues the notification for an item that was found or received.</summary>
    internal static void ShowItem(string text, ItemFlags flags, bool fromOtherPlayer, Sprite icon = null) =>
        Pending.Enqueue(new Toast
        {
            Text = text, Color = NameColor(flags, fromOtherPlayer) ?? Info, Icon = icon, IsItem = true,
            Important = fromOtherPlayer || (flags & (ItemFlags.Advancement | ItemFlags.NeverExclude | ItemFlags.Trap)) != 0,
        });

    /// <summary>
    /// While true, queued messages wait instead of being shown. Used when the scene is about to change (the strips of the
    /// old scene's HUD would be gone with it), so they appear in the new scene.
    /// </summary>
    internal static bool Hold;

    /// <summary>Moves queued messages into the HUD, once it is on screen and as room becomes available; call every frame.</summary>
    internal static void Update()
    {
        HudNotifier.Update();
        if (Hold) return;
        // messages wait while the HUD is not on screen (a cutscene, a menu, a loading screen): they would pass unseen
        while (HudNotifier.OnScreen && HudNotifier.HasRoom && Pending.TryDequeue(out var toast))
            HudNotifier.Show(toast.Text, toast.Color, toast.Icon);
    }

    /// <summary>IMGUI fallback, only used while the game HUD is not available.</summary>
    internal static void Draw()
    {
        if (HudNotifier.Available) return;
        var now = Time.unscaledTime;
        Visible.RemoveAll(t => t.Until < now);
        while (!Hold && Visible.Count < MaxVisible && Pending.TryDequeue(out var t))
        {
            t.Until = now + Lifetime;
            Visible.Add(t);
        }

        if (Visible.Count == 0) return;
        _style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

        const float width = 520f, height = 28f;
        var y = 40f;
        foreach (var t in Visible)
        {
            var rect = new Rect(Screen.width - width - 16f, y, width, height);
            _style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), t.Text, _style);
            _style.normal.textColor = t.Color;
            GUI.Label(rect, t.Text, _style);
            y += height;
        }
    }

    private static Color Hex(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);
}
