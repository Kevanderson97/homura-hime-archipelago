using System.Collections.Generic;

namespace HomuraHimeAP.Core;

/// <summary>
/// The lines the connection form shows from the server (command results, chat, hints) and the form's own notes. A fixed number of
/// the newest lines is kept. Only the game thread touches it.
/// </summary>
internal sealed class ConsoleLog
{
    internal const int Capacity = 200;

    private readonly List<string> _lines = new();

    /// <summary>Changes with every line added or cleared, so a view knows when to redraw.</summary>
    internal int Version { get; private set; }

    internal int Count => _lines.Count;

    internal void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        foreach (var part in line.Replace("\r", "").Split('\n'))
            if (part.Trim().Length > 0) _lines.Add(part.Trim());

        if (_lines.Count > Capacity) _lines.RemoveRange(0, _lines.Count - Capacity);
        Version++;
    }

    internal void Clear()
    {
        _lines.Clear();
        Version++;
    }

    /// <summary>The newest <paramref name="count"/> lines, oldest first, one per line of text.</summary>
    internal string Tail(int count)
    {
        var from = System.Math.Max(0, _lines.Count - count);
        return string.Join("\n", _lines.GetRange(from, _lines.Count - from));
    }
}
