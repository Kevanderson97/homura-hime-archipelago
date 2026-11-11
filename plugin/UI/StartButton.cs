using System;
using System.Reflection;
using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>
/// Whether the Start button of a controller was pressed this frame. The game reads its controllers through Unity's Input System, so
/// that is asked first (through reflection: the plugin does not reference the package); the legacy joystick button 7, the Start
/// button of an Xbox controller, is the fallback.
/// </summary>
internal static class StartButton
{
    private static readonly Type GamepadType = Type.GetType("UnityEngine.InputSystem.Gamepad, Unity.InputSystem");
    private static readonly PropertyInfo Current = GamepadType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
    private static readonly PropertyInfo StartControl = GamepadType?.GetProperty("startButton");
    private static PropertyInfo _wasPressed;

    internal static bool Pressed()
    {
        try
        {
            var pad = Current?.GetValue(null);
            var start = pad == null ? null : StartControl?.GetValue(pad);
            if (start != null)
            {
                _wasPressed ??= start.GetType().GetProperty("wasPressedThisFrame");
                if (_wasPressed?.GetValue(start) is true) return true;
            }
        }
        catch (Exception)
        {
            // the Input System is not what it was thought to be: only the legacy button counts then
        }

        try
        {
            return Input.GetKeyDown(KeyCode.JoystickButton7);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
