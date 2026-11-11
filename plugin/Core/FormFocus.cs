namespace HomuraHimeAP.Core;

/// <summary>
/// The things of the connection form that the keyboard (or a controller) can select. The form has two columns: at the left the three
/// fields and the two buttons, at the right the box for commands. Up and Down move inside the left column, Tab goes through everything,
/// Right moves to the command box and Left back to where the left column was left. A thing that cannot be used now is skipped: the
/// Disconnect button while there is nothing to disconnect, the command box while there is no connection to send to.
/// </summary>
internal static class FormFocus
{
    internal const int Server = 0, Slot = 1, Password = 2, Connect = 3, Disconnect = 4, Command = 5;
    internal const int Count = 6;

    /// <summary>Whether the thing can be selected now.</summary>
    internal static bool Allowed(int focus, bool disconnectEnabled, bool commandEnabled) =>
        !(focus == Disconnect && !disconnectEnabled) && !(focus == Command && !commandEnabled);

    /// <summary>Tab: the next thing of the whole form that can be selected, around again at the end.</summary>
    internal static int Next(int focus, bool disconnectEnabled, bool commandEnabled)
    {
        for (var step = 1; step <= Count; step++)
        {
            var next = (focus + step) % Count;
            if (Allowed(next, disconnectEnabled, commandEnabled)) return next;
        }

        return focus;
    }

    /// <summary>Shift+Tab.</summary>
    internal static int Previous(int focus, bool disconnectEnabled, bool commandEnabled)
    {
        for (var step = 1; step <= Count; step++)
        {
            var previous = (focus + Count - step) % Count;
            if (Allowed(previous, disconnectEnabled, commandEnabled)) return previous;
        }

        return focus;
    }

    /// <summary>Down: the next thing of the left column (the first one after the last); the command box has nothing below it.</summary>
    internal static int Down(int focus, bool disconnectEnabled)
    {
        if (focus == Command) return focus;

        for (var step = 1; step <= Disconnect + 1; step++)
        {
            var next = (focus + step) % (Disconnect + 1);
            if (Allowed(next, disconnectEnabled, true)) return next;
        }

        return focus;
    }

    /// <summary>Up: the previous thing of the left column (the last one before the first).</summary>
    internal static int Up(int focus, bool disconnectEnabled)
    {
        if (focus == Command) return focus;

        for (var step = 1; step <= Disconnect + 1; step++)
        {
            var previous = (focus + Disconnect + 1 - step) % (Disconnect + 1);
            if (Allowed(previous, disconnectEnabled, true)) return previous;
        }

        return focus;
    }

    /// <summary>Right: from Connect to Disconnect when it can be used, otherwise to the command box when it can be used, otherwise nowhere.</summary>
    internal static int Right(int focus, bool disconnectEnabled, bool commandEnabled)
    {
        if (focus == Connect && disconnectEnabled) return Disconnect;
        return commandEnabled ? Command : focus;
    }

    /// <summary>
    /// Left: from the command box back to <paramref name="remembered"/>, the thing of the left column that was selected before it;
    /// from Disconnect to Connect; nowhere from the rest.
    /// </summary>
    internal static int Left(int focus, int remembered, bool disconnectEnabled) =>
        focus == Command ? Usable(remembered, disconnectEnabled) : focus == Disconnect ? Connect : focus;

    /// <summary>A thing of the left column that can be selected now (Disconnect cannot while there is no connection).</summary>
    internal static int Usable(int focus, bool disconnectEnabled) =>
        focus is < Server or >= Command ? Server : focus == Disconnect && !disconnectEnabled ? Connect : focus;

    internal static bool IsField(int focus) => focus is >= Server and <= Password;
}
