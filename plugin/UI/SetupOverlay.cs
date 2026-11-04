using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>
/// A full-screen black cover with a message, faded in and out, for work that must not be seen (the trap setup loads scenes in
/// the background). Drawn last with IMGUI, so it covers the game and the other plugin windows.
/// </summary>
internal static class SetupOverlay
{
    private const float FadeOutSeconds = 0.5f, FadeInSeconds = 0.9f;

    /// <summary>The most a single frame may advance the fade: a stall (a load, a cleanup) must not skip it.</summary>
    private const float MaxStep = 1f / 30f;

    private static float _alpha;
    private static bool _wanted;
    private static string _message = "";
    private static GUIStyle _style;

    /// <summary>The screen is fully black: whatever happens behind it can no longer be seen.</summary>
    internal static bool Opaque => _alpha >= 1f;

    /// <summary>Anything of the cover is still on screen (also while it fades out).</summary>
    internal static bool Visible => _wanted || _alpha > 0f;

    internal static void Show(string message)
    {
        _message = message;
        _wanted = true;
    }

    /// <summary>Starts fading the cover out.</summary>
    internal static void Hide() => _wanted = false;

    internal static void Update()
    {
        var target = _wanted ? 1f : 0f;
        if (_alpha == target) return;

        var step = Mathf.Min(Time.unscaledDeltaTime, MaxStep) / (_wanted ? FadeOutSeconds : FadeInSeconds);
        _alpha = Mathf.MoveTowards(_alpha, target, step);
    }

    internal static void Draw()
    {
        if (_alpha <= 0f || Event.current.type != EventType.Repaint) return;

        var oldColor = GUI.color;
        var oldDepth = GUI.depth;
        GUI.depth = -10000;
        var shown = _alpha * _alpha * (3f - 2f * _alpha); // eased: slow at both ends
        GUI.color = new Color(0f, 0f, 0f, shown);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, wordWrap = true };
        _style.normal.textColor = Color.white;
        GUI.color = new Color(1f, 1f, 1f, shown);
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), _message, _style);

        (GUI.color, GUI.depth) = (oldColor, oldDepth);
    }
}
