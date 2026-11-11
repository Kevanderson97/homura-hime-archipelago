using System;
using System.Reflection;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.UI;

/// <summary>
/// Opening the connection form from a controller: Start, Select, both shoulder buttons and both triggers held together. Six buttons at
/// once are not pressed by accident, and no menu of the game uses the combination. The controller is read through Unity's Input
/// System (through reflection: the plugin does not reference the package), like the Start button in <see cref="StartButton"/>.
/// </summary>
internal static class ControllerChord
{
    private static readonly string[] Buttons = { "startButton", "selectButton", "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger" };

    private static readonly Type GamepadType = Type.GetType("UnityEngine.InputSystem.Gamepad, Unity.InputSystem");
    private static readonly PropertyInfo Current = GamepadType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
    private static readonly PropertyInfo[] Controls = ResolveControls();
    private static PropertyInfo _isPressed;
    private static readonly RisingEdge Edge = new();

    private static PropertyInfo[] ResolveControls()
    {
        if (GamepadType == null) return null;

        var controls = new PropertyInfo[Buttons.Length];
        for (var i = 0; i < Buttons.Length; i++)
        {
            controls[i] = GamepadType.GetProperty(Buttons[i]);
            if (controls[i] == null) return null; // not the Input System this was written for
        }

        return controls;
    }

    /// <summary>True for the one frame in which the last of the six buttons goes down.</summary>
    internal static bool Completed() => Edge.Update(AllHeld());

    private static bool AllHeld()
    {
        if (Controls == null) return false;

        try
        {
            var pad = Current?.GetValue(null);
            if (pad == null) return false;

            foreach (var control in Controls)
            {
                var button = control.GetValue(pad);
                if (button == null) return false;

                _isPressed ??= button.GetType().GetProperty("isPressed");
                if (_isPressed?.GetValue(button) is not true) return false;
            }

            return true;
        }
        catch (Exception)
        {
            return false; // the Input System is not what it was thought to be: no chord
        }
    }
}
