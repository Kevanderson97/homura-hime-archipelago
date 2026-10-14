using System.Collections.Generic;
using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>Which item notifications are shown. Connection messages and errors are always shown.</summary>
internal enum ToastVerbosity
{
    /// <summary>Every item found or received.</summary>
    All,

    /// <summary>Progression, useful items and traps, and everything that comes from another player.</summary>
    Important,

    /// <summary>No item notifications.</summary>
    Off,
}

internal sealed class Toast
{
    internal string Text;
    internal Color Color;
    internal Sprite Icon;

    /// <summary>An item notification (subject to the verbosity setting), as opposed to a status message.</summary>
    internal bool IsItem;

    /// <summary>Never dropped or collapsed when the queue is busy.</summary>
    internal bool Important;

    internal float Until;
}

/// <summary>
/// The notifications waiting to be shown. A flood (a `!release`, or the first connection with hundreds of items waiting) would
/// otherwise play for minutes, so beyond <see cref="Capacity"/> the unimportant item toasts are only counted and summed up in
/// one "+N more items" toast; important ones are kept up to <see cref="HardLimit"/>.
/// </summary>
internal sealed class ToastQueue
{
    internal const int Capacity = 30;
    internal const int HardLimit = 100;

    private readonly Queue<Toast> _pending = new();
    private int _collapsed;

    internal ToastVerbosity Verbosity = ToastVerbosity.All;

    internal int Count => _pending.Count;

    internal void Enqueue(Toast toast)
    {
        if (toast.IsItem && (Verbosity == ToastVerbosity.Off || (Verbosity == ToastVerbosity.Important && !toast.Important))) return;

        var full = _pending.Count >= Capacity;
        if (full && toast.IsItem && !toast.Important) { _collapsed++; return; }
        if (_pending.Count >= HardLimit) { if (toast.IsItem) _collapsed++; return; }
        _pending.Enqueue(toast);
    }

    internal bool TryDequeue(out Toast toast)
    {
        if (_pending.Count > 0)
        {
            toast = _pending.Dequeue();
            return true;
        }

        if (_collapsed > 0)
        {
            toast = new Toast { Text = $"+{_collapsed} more item{(_collapsed == 1 ? "" : "s")}", Color = Color.white, IsItem = false };
            _collapsed = 0;
            return true;
        }

        toast = null;
        return false;
    }
}
