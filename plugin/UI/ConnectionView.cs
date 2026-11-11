using HomuraHimeAP.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HomuraHimeAP.UI;

/// <summary>
/// The connection form in the look of the game's menus (see <see cref="GameStyle"/>), a page of its own like the Continue panel: the
/// title banner and the connection status along the top, save-card style rows for the server, the slot name and the password
/// at the left (the selected one with the red header bar and the arrow, the others dimmed), the Connect and Disconnect buttons under
/// them, and at the right a card with what the server says and the box for commands. When the form is closed only a small bar with the
/// connection status stays in the corner. It is drawn with uGUI on a canvas of its own (1920 x 1080 reference, like the game's); the
/// keyboard is handled by <see cref="ConnectionPanel"/>.
/// </summary>
internal static class ConnectionView
{
    private const int SortingOrder = 32000; // above the menus (the highest of them is 30000), below the game's mouse blocker
    private static readonly Vector2 Reference = new(1920f, 1080f);

    // the rows are the save cards of the Continue panel, scaled to leave room for the rest
    private const float CardScale = 0.8f, CardWidth = 1195.4f * CardScale, CardHeight = 240.5f * CardScale, CardSpacing = 206f;
    private const float FirstCardY = 225f, CardX = -380f;
    private const float ButtonY = -355f, ButtonWidth = 410f, ButtonHeight = 70f;
    private const float ConsoleX = 545f, ConsoleWidth = 700f, LogY = 90f, LogHeight = 610f, InputY = -300f, InputHeight = 100f;
    private const int LogLines = 40;
    // the corner label: small (about the size the old label had) and, while connected, faint and without a bar behind it
    private const int PillFontOff = 11, PillFontOn = 10;

    private sealed class Row
    {
        internal RectTransform Rect;
        internal CanvasGroup Group;
        internal Image Card, Header;
        internal Text Label, Value;
        internal string LastLabel, LastValue;
        internal bool LastSelected;
    }

    private sealed class ButtonView
    {
        internal Image Face;
        internal CanvasGroup Group;
        internal Text Text;
        internal bool LastSelected, LastEnabled;
    }

    private static GameObject _root, _form, _pill;
    private static Image _background, _titleBar, _hintBar, _statusBar, _arrow, _pillBar, _pillStripe, _logCard, _logHeader, _inputCard, _inputStripe;
    private static Text _titleText, _hintText, _statusText, _pillText, _versionText, _logLabel, _logText, _inputText;
    private static readonly Row[] Rows = new Row[3];
    private static readonly ButtonView[] Buttons = new ButtonView[2];
    private static bool _styled;
    private static CanvasGroup _logGroup, _inputGroup;
    private static string _lastStatus, _lastPill, _lastVersion, _lastCommand;
    private static bool _lastPillBackground = true;
    private static int _lastLogVersion = -1;

    /// <summary>Called every frame (cheap: it only touches the canvas when something changed).</summary>
    internal static void Refresh(ConnectionViewModel m, ConsoleLog log)
    {
        if (m.Hidden)
        {
            if (_root != null) _root.SetActive(false);
            return;
        }

        GameStyle.Capture();
        if (_root == null) Build();
        if (!_styled && GameStyle.HasCards) Style();
        _root.SetActive(true);
        _form.SetActive(m.Open);
        _pill.SetActive(!m.Open);

        if (!m.Open)
        {
            RefreshPill(m);
            return;
        }

        var blink = (int)(Time.unscaledTime * 2f) % 2 == 0;
        SetText(_statusText, ref _lastStatus, m.Status);
        for (var i = 0; i < Rows.Length; i++) UpdateRow(Rows[i], m.Labels[i], m.Values[i], m.Secret[i], m.Focus == FormFocus.Server + i, blink);
        UpdateButton(Buttons[0], m.Focus == FormFocus.Connect, !m.Connecting);
        UpdateButton(Buttons[1], m.Focus == FormFocus.Disconnect, m.DisconnectEnabled);

        if (_lastLogVersion != m.LogVersion)
        {
            _lastLogVersion = m.LogVersion;
            _logText.text = log.Tail(LogLines);
        }

        // the log and the box for commands are for a connection that is up: grey and dimmed otherwise, like a Disconnect with nothing to disconnect
        var commandEnabled = m.CommandEnabled;
        var commandFocused = commandEnabled && m.Focus == FormFocus.Command;
        _logGroup.alpha = _inputGroup.alpha = commandEnabled ? 1f : 0.5f;
        _inputStripe.enabled = commandFocused;
        SetHeader(_logHeader, commandFocused, !commandEnabled);
        var command = m.Command.Length == 0 && !commandFocused ? null : m.Command + (commandFocused && blink ? "|" : " ");
        SetText(_inputText, ref _lastCommand, command ?? (commandEnabled ? "Message or command" : "Connect to send messages"));
        _inputText.color = m.Command.Length == 0 && !commandFocused ? new Color(0.55f, 0.55f, 0.55f, 1f) : GameStyle.CardText;
        PlaceArrow(commandEnabled || m.Focus != FormFocus.Command ? m.Focus : FormFocus.Server - 1);
    }

    /// <summary>The arrow points at the selected field or at the command box; a button shows its selection by turning red.</summary>
    private static void PlaceArrow(int focus)
    {
        var show = FormFocus.IsField(focus) || focus == FormFocus.Command;
        _arrow.enabled = show;
        if (!show) return;

        _arrow.rectTransform.anchoredPosition = focus == FormFocus.Command
            ? new Vector2(ConsoleX - ConsoleWidth / 2f - 52f, InputY)
            : new Vector2(CardX - CardWidth / 2f - 52f, FirstCardY - focus * CardSpacing + CardHeight / 2f - 30f);
    }

    private static void UpdateRow(Row row, string label, string value, bool secret, bool selected, bool blink)
    {
        var shown = secret ? new string('*', value.Length) : value;
        if (selected) shown += blink ? "|" : " ";
        else if (shown.Length == 0) shown = "-";

        SetText(row.Label, ref row.LastLabel, label);
        SetText(row.Value, ref row.LastValue, shown);
        if (row.LastSelected == selected && row.Header.sprite == HeaderSprite(selected, false)) return;

        row.LastSelected = selected;
        SetHeader(row.Header, selected, false);
    }

    private static void UpdateButton(ButtonView button, bool selected, bool enabled)
    {
        button.Group.alpha = enabled ? 1f : 0.5f;
        if (button.LastSelected == selected && button.LastEnabled == enabled && button.Face.sprite == HeaderSprite(selected, !enabled)) return;

        button.LastSelected = selected;
        button.LastEnabled = enabled;
        SetHeader(button.Face, selected, !enabled);
    }

    /// <summary>The bar of a title: red for the selected thing, the grey one for a thing that cannot be used, black for the rest.</summary>
    private static Sprite HeaderSprite(bool selected, bool disabled) => selected ? GameStyle.HeaderSelected : disabled ? GameStyle.Header : GameStyle.Title;

    private static void SetHeader(Image bar, bool selected, bool disabled)
    {
        bar.sprite = HeaderSprite(selected, disabled);
        // flat colors when the game's bars are not known
        bar.color = bar.sprite != null ? Color.white : selected ? GameStyle.Red : disabled ? new Color(0.65f, 0.65f, 0.65f, 1f) : new Color(0.07f, 0.07f, 0.07f, 1f);
    }

    private static void SetText(Text text, ref string last, string value)
    {
        if (last == value) return;
        last = value;
        text.text = value;
    }

    // ------------------------------------------------------------------ building

    private static void Build()
    {
        _root = new GameObject("HHAP_ConnectionCanvas");
        Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Reference;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>(); // the buttons answer to a mouse click too

        BuildForm();
        BuildPill();
        Style();
    }

    private static void BuildForm()
    {
        _form = Child("Form", _root.transform, Vector2.zero, Reference);
        Stretch(_form.GetComponent<RectTransform>());

        _background = Picture("Background", _form.transform, Vector2.zero, Reference);
        Stretch(_background.rectTransform);

        _titleBar = Picture("Title", _form.transform, new Vector2(-588f, 456f), new Vector2(646.8f, 80.8f));
        _titleText = Label("Text", _titleBar.transform, new Vector2(-303f, 0f), new Vector2(700f, 160f), 84, Color.white, TextAnchor.MiddleLeft);
        _titleText.rectTransform.pivot = new Vector2(0f, 0.5f);
        _titleText.text = "Archipelago";

        // the connection status, along the top next to the title
        _statusBar = Picture("StatusBar", _form.transform, new Vector2(335f, 456f), new Vector2(1100f, 76f));
        _statusBar.color = new Color(0f, 0f, 0f, 0.9f);
        var stripe = Picture("Stripe", _statusBar.transform, new Vector2(-547f, 0f), new Vector2(6f, 76f));
        stripe.color = GameStyle.Red;
        _statusText = Label("Text", _statusBar.transform, new Vector2(20f, 0f), new Vector2(1030f, 76f), 44, Color.white, TextAnchor.MiddleLeft);

        for (var i = 0; i < Rows.Length; i++) Rows[i] = BuildRow(i);
        Buttons[0] = BuildButton("Connect", -CardWidth / 2f + CardX + ButtonWidth / 2f, ConnectionPanel.ConnectNow);
        Buttons[1] = BuildButton("Disconnect", -CardWidth / 2f + CardX + ButtonWidth * 1.5f + 70f, ConnectionPanel.DisconnectNow);

        BuildConsole();
        _arrow = Picture("Arrow", _form.transform, Vector2.zero, new Vector2(70f, 69.7f));

        _hintBar = Picture("HintBar", _form.transform, new Vector2(0f, -454f), new Vector2(1800f, 80f));
        _hintText = Label("Right", _hintBar.transform, new Vector2(125f, 0f), new Vector2(1400f, 80f), 40, Color.white, TextAnchor.MiddleRight); // ends (x = 825) before the torn edge of the bar
        _hintText.text = "F11: Connect     F12: Disconnect     Esc/Start: Close";
    }

    private static Row BuildRow(int index)
    {
        var row = new Row();
        var card = Picture("Card" + index, _form.transform, new Vector2(CardX, FirstCardY - index * CardSpacing), new Vector2(CardWidth, CardHeight));
        row.Card = card;
        row.Rect = card.rectTransform;
        row.Group = card.gameObject.AddComponent<CanvasGroup>();

        row.Header = Picture("Header", card.transform, new Vector2(-207f, 45f), new Vector2(604.5f * CardScale, 62.4f * CardScale));
        row.Label = Label("Text", row.Header.transform, new Vector2(47f, 0f), new Vector2(420f, 75f), 46, Color.white, TextAnchor.MiddleLeft);

        row.Value = Label("Value", card.transform, new Vector2(-CardWidth / 2f + 60f, -26f), new Vector2(CardWidth - 90f, 100f), 52, GameStyle.CardText, TextAnchor.MiddleLeft);
        row.Value.rectTransform.pivot = new Vector2(0f, 0.5f);
        return row;
    }

    private static ButtonView BuildButton(string label, float x, UnityEngine.Events.UnityAction onClick)
    {
        var button = new ButtonView();
        button.Face = Picture(label + "Button", _form.transform, new Vector2(x, ButtonY), new Vector2(ButtonWidth, ButtonHeight));
        button.Face.raycastTarget = true;
        button.Group = button.Face.gameObject.AddComponent<CanvasGroup>();
        var click = button.Face.gameObject.AddComponent<Button>();
        click.transition = Selectable.Transition.None;
        click.navigation = new Navigation { mode = Navigation.Mode.None };
        click.onClick.AddListener(onClick);

        button.Text = Label("Text", button.Face.transform, Vector2.zero, new Vector2(ButtonWidth, ButtonHeight), 46, Color.white, TextAnchor.MiddleCenter);
        button.Text.text = label;
        return button;
    }

    /// <summary>The card at the right: what the server says, and under it the box for what the player sends.</summary>
    private static void BuildConsole()
    {
        _logCard = Picture("LogCard", _form.transform, new Vector2(ConsoleX, LogY), new Vector2(ConsoleWidth, LogHeight));
        _logHeader = Picture("Header", _logCard.transform, new Vector2(-ConsoleWidth / 2f + 250f, LogHeight / 2f - 40f), new Vector2(483f, 50f));
        _logLabel = Label("Text", _logHeader.transform, new Vector2(47f, 0f), new Vector2(420f, 75f), 40, Color.white, TextAnchor.MiddleLeft);
        _logLabel.text = "Server";

        // the newest lines sit at the bottom of the card, the older ones are cut off at the top
        var mask = Child("Mask", _logCard.transform, new Vector2(0f, -17f), new Vector2(ConsoleWidth - 60f, LogHeight - 160f));
        mask.AddComponent<RectMask2D>();
        _logText = Label("Log", mask.transform, Vector2.zero, new Vector2(ConsoleWidth - 60f, LogHeight - 160f), 26, GameStyle.CardText, TextAnchor.LowerLeft);
        Stretch(_logText.rectTransform);
        _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _logText.verticalOverflow = VerticalWrapMode.Overflow;

        _inputCard = Picture("InputCard", _form.transform, new Vector2(ConsoleX, InputY), new Vector2(ConsoleWidth, InputHeight));
        _inputStripe = Picture("Stripe", _inputCard.transform, new Vector2(-ConsoleWidth / 2f + 3f, 0f), new Vector2(6f, InputHeight));
        _logGroup = _logCard.gameObject.AddComponent<CanvasGroup>();
        _inputGroup = _inputCard.gameObject.AddComponent<CanvasGroup>();
        _inputStripe.color = GameStyle.Red;
        _inputText = Label("Text", _inputCard.transform, new Vector2(-ConsoleWidth / 2f + 30f, 0f), new Vector2(ConsoleWidth - 60f, InputHeight), 42, GameStyle.CardText, TextAnchor.MiddleLeft);
        _inputText.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    private static void BuildPill()
    {
        _pill = Child("Pill", _root.transform, Vector2.zero, Reference);
        Stretch(_pill.GetComponent<RectTransform>());

        // small, in the corner: the version of the plugin and the connection status. The dark bar behind them is only there while the
        // game is not connected; once it is, only the small white text stays
        _pillBar = Picture("Bar", _pill.transform, Vector2.zero, new Vector2(300f, 36f));
        TopLeft(_pillBar.rectTransform, new Vector2(4f, -4f));
        _pillBar.color = new Color(0f, 0f, 0f, 0.85f);
        _pillStripe = Picture("Stripe", _pillBar.transform, Vector2.zero, new Vector2(3f, 36f));
        TopLeft(_pillStripe.rectTransform, Vector2.zero);
        _pillStripe.color = GameStyle.Red;

        _versionText = PillLabel("Version", _pillBar.transform);
        _pillText = PillLabel("Status", _pillBar.transform);
    }

    /// <summary>One line of the corner text; its size and color follow the state of the connection (see <see cref="RefreshPill"/>).</summary>
    private static Text PillLabel(string name, Transform parent)
    {
        var text = Label(name, parent, Vector2.zero, new Vector2(800f, 16f), PillFontOff, Color.white, TextAnchor.MiddleLeft);
        TopLeft(text.rectTransform, Vector2.zero);
        return text;
    }

    private static void RefreshPill(ConnectionViewModel m)
    {
        var hasVersion = !string.IsNullOrEmpty(m.Version);
        var background = !m.Connected;
        var changed = _lastVersion != (m.Version ?? "") || _lastPill != m.Pill || _lastPillBackground != background;
        if (!changed) return;

        _versionText.gameObject.SetActive(hasVersion);
        SetText(_versionText, ref _lastVersion, m.Version ?? "");
        SetText(_pillText, ref _lastPill, m.Pill);
        _lastPillBackground = background;
        _pillBar.enabled = _pillStripe.enabled = background;

        // not connected: white text on the dark bar; connected: the same text a little smaller and faint, with nothing behind it
        var size = background ? PillFontOff : PillFontOn;
        var color = background ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        var line = size * 1.3f;
        foreach (var text in new[] { _versionText, _pillText })
        {
            text.fontSize = size;
            text.color = color;
            text.rectTransform.sizeDelta = new Vector2(800f, line);
        }

        // the version on the first line, the status under it; the bar is as wide as the longer one
        var lines = hasVersion ? 2 : 1;
        var left = background ? 9f : 2f;
        var top = background ? -4f : -1f;
        _versionText.rectTransform.anchoredPosition = new Vector2(left, top);
        _pillText.rectTransform.anchoredPosition = new Vector2(left, top - (hasVersion ? line : 0f));
        var width = Mathf.Max(hasVersion ? _versionText.preferredWidth : 0f, _pillText.preferredWidth) + 18f;
        var height = lines * line + 8f;
        _pillBar.rectTransform.sizeDelta = new Vector2(width, height);
        _pillStripe.rectTransform.sizeDelta = new Vector2(3f, height);
    }

    private static void TopLeft(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
    }

    /// <summary>Puts the game's sprites and fonts on the pieces; called again when the style was found late (the title scene is only there at the start).</summary>
    private static void Style()
    {
        Apply(_background, GameStyle.Background, new Color(0.86f, 0.87f, 0.9f, 1f));
        Apply(_titleBar, GameStyle.Title, Color.black);
        Apply(_arrow, GameStyle.Arrow, GameStyle.Red);
        Apply(_hintBar, GameStyle.HintBar, new Color(0f, 0f, 0f, 0.92f));
        Apply(_logCard, GameStyle.Card, new Color(1f, 1f, 1f, 0.96f));
        Apply(_inputCard, GameStyle.Card, new Color(1f, 1f, 1f, 0.96f));
        SetHeader(_logHeader, false, true);
        foreach (var row in Rows)
        {
            Apply(row.Card, GameStyle.Card, new Color(1f, 1f, 1f, 0.96f));
            row.LastSelected = !row.LastSelected; // the header bar is put on again by the next refresh
            row.Header.sprite = null;
        }

        foreach (var button in Buttons)
        {
            button.LastSelected = !button.LastSelected;
            button.Face.sprite = null;
        }

        var bold = GameStyle.Bold != null ? GameStyle.Bold : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var regular = GameStyle.Regular != null ? GameStyle.Regular : bold;
        foreach (var text in new[] { _titleText, _statusText, _logLabel, _inputText, Buttons[0].Text, Buttons[1].Text }) text.font = bold;
        foreach (var row in Rows)
        {
            row.Label.font = bold;
            row.Value.font = bold;
        }

        foreach (var text in new[] { _hintText, _pillText, _versionText, _logText }) text.font = regular;
        _styled = GameStyle.HasCards;
    }

    /// <summary>The game's sprite, or a plain colored shape when it is not known (yet).</summary>
    private static void Apply(Image image, Sprite sprite, Color plain)
    {
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : plain;
    }

    // ------------------------------------------------------------------ small helpers

    private static GameObject Child(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    private static Image Picture(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = Child(name, parent, position, size);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor anchor)
    {
        var go = Child(name, parent, position, size);
        var text = go.AddComponent<Text>();
        text.raycastTarget = false;
        text.supportRichText = false;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}


