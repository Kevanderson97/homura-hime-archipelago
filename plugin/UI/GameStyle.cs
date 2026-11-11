using UnityEngine;
using UnityEngine.UI;

namespace HomuraHimeAP.UI;

/// <summary>
/// The look of the game's own menus, taken from the Continue panel of the title screen (its <c>SaveCanvas</c>): the sprites of the
/// title banner, of a save card, of its red (selected) and grey header bar, of the arrow and of the black hint bar, and the two
/// fonts. The connection form is built from the same pieces, so it looks like another page of the game's menu. Everything is
/// optional: what is not found stays null and the form draws plain shapes instead. The sprites are kept in static fields, which
/// keeps them in memory when the title scene is gone.
/// </summary>
internal static class GameStyle
{
    internal static Sprite Background, Title, Card, Header, HeaderSelected, Arrow, HintBar;
    internal static Font Bold, Regular;

    /// <summary>The dark grey of the text on a save card, and the white and red of its header.</summary>
    internal static readonly Color CardText = new(0.196f, 0.196f, 0.196f, 1f);

    internal static readonly Color Red = new(1f, 0.3f, 0.35f, 1f);

    private const string Canvas = "SaveCanvas";
    private const float RetrySeconds = 1f;

    private static float _nextTry;

    /// <summary>True when the card and the two header bars are known: the part of the style the form cannot do without.</summary>
    internal static bool HasCards => Card != null && Header != null && HeaderSelected != null;

    /// <summary>Looks for what is missing; cheap enough to call every frame (it only searches once a second, and only while something is missing).</summary>
    internal static void Capture()
    {
        if (HasCards && Title != null && HintBar != null && Background != null && Arrow != null && Bold != null && Regular != null) return;
        if (Time.unscaledTime < _nextTry) return;
        _nextTry = Time.unscaledTime + RetrySeconds;

        if (Bold == null) Bold = FindFont("Roboto-Bold");
        if (Regular == null) Regular = FindFont("Roboto-Regular");

        var canvas = GameObject.Find(Canvas);
        if (canvas == null) return;

        var root = canvas.transform;
        if (Background == null) Background = SpriteAt(root, "BG");
        if (Title == null) Title = SpriteAt(root, "Title");
        if (Arrow == null) Arrow = SpriteAt(root, "Content/Arrow");
        if (HintBar == null) HintBar = SpriteAt(root, "Content/Hint/BG");

        var card = root.Find("Content/Group/SaveView");
        if (card == null) return;

        var header = card.Find("Button (Legacy)");
        if (Card == null) Card = card.GetComponent<Image>()?.sprite;
        if (Header == null && header != null) Header = header.GetComponent<Image>()?.sprite;
        if (HeaderSelected == null && header != null && header.GetComponent<Selectable>() is { } selectable) HeaderSelected = selectable.spriteState.selectedSprite;
    }

    private static Sprite SpriteAt(Transform root, string path) => root.Find(path)?.GetComponent<Image>()?.sprite;

    private static Font FindFont(string name)
    {
        foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
            if (font != null && font.name == name) return font;
        return null;
    }
}
