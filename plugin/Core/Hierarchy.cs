using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HomuraHimeAP.Core;

/// <summary>Scene-hierarchy helpers shared by the hooks that identify game objects by their names.</summary>
internal static class Hierarchy
{
    /// <summary>The object's own name followed by up to depth - 1 parent names, nearest first.</summary>
    internal static List<string> Names(Transform transform, int depth)
    {
        var names = new List<string>();
        for (var t = transform; t != null && names.Count < depth; t = t.parent) names.Add(t.name);
        return names;
    }

    /// <summary>The names joined with " &lt; ", the notation used by the recon data.</summary>
    internal static string Chain(Transform transform, int depth) => string.Join(" < ", Names(transform, depth));

    internal static bool HasAncestorNamed(Transform transform, string name, int depth = 12) =>
        Names(transform, depth).Any(n => n == name);
}
