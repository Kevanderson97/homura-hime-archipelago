using HomuraHimeAP.Archipelago;
using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>
/// Connection form, driven by the keyboard because the game locks the mouse.
/// F10 opens/closes it (the title screen shows it read-only until then), Tab/Up/Down switch fields, typing edits the
/// selected field, Ctrl+V pastes, F11 connects, F12 disconnects. While it is open the game ignores the keyboard
/// (see <see cref="InputBlocker"/>).
/// </summary>
internal static class ConnectionPanel
{
    internal static bool Open;

    private static readonly string[] Labels = { "Server", "Slot", "Password" };
    private static readonly string[] Values = new string[3];
    private static bool _loaded;
    private static int _field;

    internal static void Toggle() => Open = !Open;

    private static bool Visible
    {
        get
        {
            var onTitle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScreen";
            return Open || (onTitle && ApClient.Status != ConnectionStatus.Connected);
        }
    }

    /// <summary>Called every frame: keep the OS cursor free while the form is up, in case the mouse works.</summary>
    internal static void Update()
    {
        InputBlocker.Set(Open);
        if (!Visible) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    internal static void Draw()
    {
        GUI.Label(new Rect(8, 22, 460, 20), $"AP: {ApClient.StatusText}  (F10: connection form)");
        if (!Visible) return;

        if (!_loaded)
        {
            Values[0] = Plugin.CfgHost.Value;
            Values[1] = Plugin.CfgSlot.Value;
            Values[2] = Plugin.CfgPassword.Value;
            _loaded = true;
        }

        if (Open) HandleKeys(Event.current);

        GUILayout.BeginArea(new Rect(8, 48, 460, 200), GUI.skin.box);
        GUILayout.Label("Archipelago connection");
        for (var i = 0; i < Labels.Length; i++)
        {
            var shown = i == 2 ? new string('*', Values[i].Length) : Values[i];
            var caret = i == _field && (int)(Time.unscaledTime * 2) % 2 == 0 ? "|" : " ";
            GUILayout.Label($"{(i == _field ? ">" : " ")} {Labels[i],-9} {shown}{caret}");
        }

        GUILayout.Label(Open ? "Tab: next field   Ctrl+V: paste   F11: connect   F12: disconnect   F10: close" : "Press F10 to edit the connection");
        GUILayout.Label(ApClient.StatusText);
        GUILayout.EndArea();
    }

    private static void HandleKeys(Event e)
    {
        if (e == null || e.type != EventType.KeyDown) return;

        switch (e.keyCode)
        {
            case KeyCode.Tab:
            case KeyCode.DownArrow:
                _field = (_field + 1) % Values.Length;
                e.Use();
                return;
            case KeyCode.UpArrow:
                _field = (_field + Values.Length - 1) % Values.Length;
                e.Use();
                return;
            case KeyCode.F11:
                Plugin.CfgHost.Value = Values[0].Trim();
                Plugin.CfgSlot.Value = Values[1].Trim();
                Plugin.CfgPassword.Value = Plugin.CfgRememberPassword.Value ? Values[2] : "";
                ApClient.Connect(Values[0], Values[1].Trim(), Values[2]);
                e.Use();
                return;
            case KeyCode.F12:
                ApClient.Disconnect();
                e.Use();
                return;
            case KeyCode.Backspace:
                if (Values[_field].Length > 0) Values[_field] = Values[_field].Substring(0, Values[_field].Length - 1);
                e.Use();
                return;
            case KeyCode.V when e.control || e.command:
                Values[_field] += (GUIUtility.systemCopyBuffer ?? "").Replace("\r", "").Replace("\n", "").Trim();
                e.Use();
                return;
        }

        if (e.character >= ' ' && e.character != 127)
        {
            Values[_field] += e.character;
            e.Use();
        }
    }
}
