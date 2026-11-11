using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>What <see cref="ConnectionView"/> shows; filled by <see cref="ConnectionPanel"/> every frame (one instance, nothing is allocated).</summary>
internal sealed class ConnectionViewModel
{
    internal bool Open, Hidden, Connected, Connecting, DisconnectEnabled, CommandEnabled;
    internal string[] Labels, Values;
    internal bool[] Secret;
    internal int Focus;
    internal string Status, Pill, Version, Command, Log;
    internal int LogVersion;
}

/// <summary>
/// Connection form, driven by the keyboard (or a controller) because the game locks the mouse. F10 opens it, Escape, the Start
/// button of a controller or F10 close it. Up and Down go through the three fields and the two buttons (Disconnect only while there is
/// a connection), Right goes to the box for commands and Left back, Tab goes through everything (<see cref="FormFocus"/>), typing edits the selected field or the command, Ctrl+V pastes. Enter does what the selected thing does:
/// a field or Connect connects, Disconnect disconnects and the command box sends its text to the server as the connected slot.
/// F11 connects and F12 disconnects from anywhere in the form. While it is open the game ignores the keyboard (see
/// <see cref="InputBlocker"/>). What the form looks like is up to <see cref="ConnectionView"/>.
/// </summary>
internal static class ConnectionPanel
{
    internal static bool Open;

    private static readonly string[] Labels = { "Server", "Slot name", "Password" };
    private static readonly bool[] Secret = { false, false, true };
    private static readonly string[] Values = { "", "", "" };
    private static readonly ConnectionViewModel Model = new() { Labels = Labels, Values = Values, Secret = Secret };
    private static string _command = "";
    private static bool _loaded;
    private static bool _wasConnected;
    private static int _focus;
    private static int _lastLeft; // the thing of the left column that was selected before the command box
    private static int _connectFrame = -1;
    private static int _openedFrame = -1;

    internal static void Toggle() => Open = !Open;

    /// <summary>
    /// Opened by the buttons of a controller (see <see cref="ControllerChord"/>). Start is one of them, and it also closes the form:
    /// the press that completes the combination must not close what it has just opened.
    /// </summary>
    internal static void OpenFromController()
    {
        Open = true;
        _openedFrame = Time.frameCount;
    }

    /// <summary>Disconnecting means something while there is a connection, one being made or one being brought back.</summary>
    private static bool DisconnectEnabled =>
        ApClient.Status is ConnectionStatus.Connected or ConnectionStatus.Connecting or ConnectionStatus.Reconnecting;

    /// <summary>The box for commands and the log beside it are only for a connection that is up.</summary>
    private static bool CommandEnabled => ApClient.Status == ConnectionStatus.Connected;

    private static void MoveTo(int focus)
    {
        _focus = focus;
        if (focus != FormFocus.Command) _lastLeft = focus;
    }

    /// <summary>Connects with what the form holds. Called from the keys, the Connect button and the game's Update (see <see cref="PollKeys"/>).</summary>
    internal static void ConnectNow()
    {
        Load();
        if (_connectFrame == Time.frameCount) return; // the same press, seen through both ways of reading the keyboard
        _connectFrame = Time.frameCount;

        Plugin.CfgHost.Value = Values[0].Trim();
        Plugin.CfgSlot.Value = Values[1].Trim();
        Plugin.CfgPassword.Value = Plugin.CfgRememberPassword.Value ? Values[2] : "";
        ApClient.Connect(Values[0], Values[1].Trim(), Values[2]);
    }

    internal static void DisconnectNow() => ApClient.Disconnect();

    /// <summary>Sends the text of the command box to the server; the answer shows up in the form's log.</summary>
    private static void SendCommand()
    {
        var text = _command.Trim();
        if (text.Length == 0) return;

        if (ApClient.Say(text)) _command = "";
        else ApClient.ServerLog.Add("Not connected: connect first, then send your message.");
    }

    /// <summary>
    /// The keys that must not depend on the GUI's key events are read here, from the game's Update like F10: a key event of the GUI is
    /// not always delivered (an input method, a keyboard tool or the window not being the one with the keyboard focus take them),
    /// the polled key is.
    /// </summary>
    internal static void PollKeys()
    {
        if (!Open) return;
        if (Input.GetKeyDown(KeyCode.F11)) ConnectNow();
        if (Input.GetKeyDown(KeyCode.F12)) DisconnectNow();
        if (_openedFrame == Time.frameCount) return;
        if (Input.GetKeyDown(KeyCode.Escape) || StartButton.Pressed()) Open = false;
        if (Open) HandleController(ControllerNav.Poll());
    }

    /// <summary>The D-Pad (or left stick) moves the selection, the bottom button does what Enter does and the right button closes the form.</summary>
    private static void HandleController(NavPress press)
    {
        if (!press.Any) return;

        if (press.Back) Open = false;
        else if (press.Accept) Activate();
        else if (press.Up) MoveTo(FormFocus.Up(_focus, DisconnectEnabled));
        else if (press.Down) MoveTo(FormFocus.Down(_focus, DisconnectEnabled));
        else if (press.Left) MoveTo(FormFocus.Left(_focus, _lastLeft, DisconnectEnabled));
        else if (press.Right) MoveTo(FormFocus.Right(_focus, DisconnectEnabled, CommandEnabled));
    }

    /// <summary>The saved connection settings are the form's values until the player edits them.</summary>
    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        Values[0] = Plugin.CfgHost.Value;
        Values[1] = Plugin.CfgSlot.Value;
        Values[2] = Plugin.CfgPassword.Value;
    }

    /// <summary>Called every frame, after the game's own Update: keeps the OS cursor free while the form is up and shows the form.</summary>
    internal static void Update()
    {
        // a successful connection closes the form (the trap setup used to, as a side effect, when it ran on connecting)
        var connected = ApClient.Status == ConnectionStatus.Connected;
        if (connected && !_wasConnected) Open = false;
        _wasConnected = connected;

        InputBlocker.Set(Open);
        if (Open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        ApClient.ListenToMessages(Open);

        Load();
        if (_focus == FormFocus.Disconnect && !DisconnectEnabled) MoveTo(FormFocus.Connect); // it has just become unusable
        if (_focus == FormFocus.Command && !CommandEnabled) MoveTo(FormFocus.Usable(_lastLeft, DisconnectEnabled));
        Model.Open = Open;
        Model.Hidden = SetupOverlay.Visible;
        Model.Connected = connected;
        Model.Connecting = ApClient.Status == ConnectionStatus.Connecting;
        Model.DisconnectEnabled = DisconnectEnabled;
        Model.CommandEnabled = CommandEnabled;
        Model.Focus = _focus;
        Model.Status = ApClient.StatusText;
        Model.Pill = $"AP: {ApClient.StatusText}   (F10: connection form)";
        Model.Version = Plugin.CfgShowLabel.Value ? $"{Plugin.PluginName} v{Plugin.PluginVersion}" : null;
        Model.Command = _command;
        Model.LogVersion = ApClient.ServerLog.Version;
        Model.Log = null;
        ConnectionView.Refresh(Model, ApClient.ServerLog);
    }

    /// <summary>Called for every event of the GUI: the form reads its keys here.</summary>
    internal static void HandleGui()
    {
        if (Open) HandleKeys(Event.current);
    }

    private static void HandleKeys(Event e)
    {
        if (e == null || e.type != EventType.KeyDown) return;

        switch (e.keyCode)
        {
            case KeyCode.Tab when e.shift:
                MoveTo(FormFocus.Previous(_focus, DisconnectEnabled, CommandEnabled));
                e.Use();
                return;
            case KeyCode.Tab:
                MoveTo(FormFocus.Next(_focus, DisconnectEnabled, CommandEnabled));
                e.Use();
                return;
            case KeyCode.UpArrow:
                MoveTo(FormFocus.Up(_focus, DisconnectEnabled));
                e.Use();
                return;
            case KeyCode.DownArrow:
                MoveTo(FormFocus.Down(_focus, DisconnectEnabled));
                e.Use();
                return;
            case KeyCode.LeftArrow:
                MoveTo(FormFocus.Left(_focus, _lastLeft, DisconnectEnabled));
                e.Use();
                return;
            case KeyCode.RightArrow:
                MoveTo(FormFocus.Right(_focus, DisconnectEnabled, CommandEnabled));
                e.Use();
                return;
            case KeyCode.Escape:
                Open = false;
                e.Use();
                return;
            case KeyCode.F11:
                ConnectNow();
                e.Use();
                return;
            case KeyCode.F12:
                DisconnectNow();
                e.Use();
                return;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Activate();
                e.Use();
                return;
            case KeyCode.Backspace:
                Edit(text => text.Length > 0 ? text.Substring(0, text.Length - 1) : text);
                e.Use();
                return;
            case KeyCode.V when e.control || e.command:
                var pasted = (GUIUtility.systemCopyBuffer ?? "").Replace("\r", "").Replace("\n", "").Trim();
                Edit(text => text + pasted);
                e.Use();
                return;
        }

        if (e.character >= ' ' && e.character != 127)
        {
            var typed = e.character;
            Edit(text => text + typed);
            e.Use();
        }
    }

    /// <summary>What Enter does for the selected thing.</summary>
    private static void Activate()
    {
        switch (_focus)
        {
            case FormFocus.Disconnect:
                DisconnectNow();
                break;
            case FormFocus.Command:
                if (CommandEnabled) SendCommand();
                break;
            default: // a field or Connect
                ConnectNow();
                break;
        }
    }

    /// <summary>Changes the text of the selected field or of the command box; the buttons have none.</summary>
    private static void Edit(System.Func<string, string> change)
    {
        if (FormFocus.IsField(_focus)) Values[_focus] = change(Values[_focus]);
        else if (_focus == FormFocus.Command) _command = change(_command);
    }
}

