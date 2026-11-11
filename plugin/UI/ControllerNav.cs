using System;
using System.Collections.Generic;
using System.Reflection;

namespace HomuraHimeAP.UI;

/// <summary>What a controller asked of the connection form in one frame.</summary>
internal struct NavPress
{
    internal bool Up, Down, Left, Right, Accept, Back;

    internal bool Any => Up || Down || Left || Right || Accept || Back;
}

/// <summary>
/// The controller as a way to move through the connection form: the D-Pad or the left stick moves the selection, the bottom button
/// (A, Cross) does what Enter does and the right button (B, Circle) closes the form. Read through Unity's Input System, which the game
/// uses for its controllers (through reflection: the plugin does not reference the package), like <see cref="StartButton"/>.
/// Each press counts once, in the frame it begins.
/// </summary>
internal static class ControllerNav
{
    private static readonly Type GamepadType = Type.GetType("UnityEngine.InputSystem.Gamepad, Unity.InputSystem");
    private static readonly PropertyInfo Current = GamepadType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
    private static readonly PropertyInfo Dpad = GamepadType?.GetProperty("dpad");
    private static readonly PropertyInfo LeftStick = GamepadType?.GetProperty("leftStick");
    private static readonly PropertyInfo South = GamepadType?.GetProperty("buttonSouth");
    private static readonly PropertyInfo East = GamepadType?.GetProperty("buttonEast");
    private static readonly Dictionary<(Type, string), PropertyInfo> Properties = new();

    internal static NavPress Poll()
    {
        var press = new NavPress();
        try
        {
            var pad = Current?.GetValue(null);
            if (pad == null) return press;

            ReadDirections(Dpad?.GetValue(pad), ref press);
            ReadDirections(LeftStick?.GetValue(pad), ref press);
            press.Accept = JustPressed(South?.GetValue(pad));
            press.Back = JustPressed(East?.GetValue(pad));
        }
        catch (Exception)
        {
            // the Input System is not what it was thought to be: the keyboard still works
        }

        return press;
    }

    /// <summary>A D-Pad and a stick both have a button for each direction (up, down, left, right).</summary>
    private static void ReadDirections(object control, ref NavPress press)
    {
        if (control == null) return;

        press.Up |= JustPressed(Get(control, "up"));
        press.Down |= JustPressed(Get(control, "down"));
        press.Left |= JustPressed(Get(control, "left"));
        press.Right |= JustPressed(Get(control, "right"));
    }

    private static object Get(object control, string name)
    {
        var key = (control.GetType(), name);
        if (!Properties.TryGetValue(key, out var property)) Properties[key] = property = control.GetType().GetProperty(name);
        return property?.GetValue(control);
    }

    private static bool JustPressed(object button) => button != null && Get(button, "wasPressedThisFrame") is true;
}
