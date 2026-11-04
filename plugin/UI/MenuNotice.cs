using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>
/// A message drawn straight over the main menu. The toasts cannot be used there: they wait for the game's HUD to be on screen,
/// which it never is on the title screen. It is a dark banner between the save slots and the hint bar at the bottom, and goes
/// away by itself.
/// </summary>
internal static class MenuNotice
{
    private const float Seconds = 6f, FadeSeconds = 0.5f;

    private static string _text, _pinned;
    private static float _until;
    private static GUIStyle _style;

    internal static bool Visible => _pinned != null || (_text != null && Time.unscaledTime < _until);

    /// <summary>Removes the message at once, pinned or timed.</summary>
    internal static void Clear() => (_pinned, _text) = (null, null);

    /// <summary>Keeps a message up until it is replaced by null (a panel that stays open); a timed message waits behind it.</summary>
    internal static void Pin(string text) => _pinned = text;

    internal static void Show(string text)
    {
        _text = text;
        _until = Time.unscaledTime + Seconds;
    }

    internal static void Draw()
    {
        if (!Visible || Event.current.type != EventType.Repaint) return;

        var alpha = _pinned != null ? 1f : Mathf.Clamp01((_until - Time.unscaledTime) / FadeSeconds);
        var rect = new Rect(Screen.width * 0.08f, Screen.height * 0.805f, Screen.width * 0.84f, Screen.height * 0.07f);

        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        _style.fontSize = Mathf.RoundToInt(Screen.height * 0.026f);

        var oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.88f * alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.3f, 0.35f, alpha); // the red of the selected slot
        GUI.DrawTexture(new Rect(rect.x, rect.y, 6f, rect.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 24f, rect.height), _pinned ?? _text, _style);
        GUI.color = oldColor;
    }
}
