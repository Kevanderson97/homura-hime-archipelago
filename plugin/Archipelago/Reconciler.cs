using System.Collections.Generic;
using System.Linq;

namespace HomuraHimeAP.Archipelago;

/// <summary>What the two sides know about which locations are checked, and what each is missing.</summary>
internal readonly struct CheckDifference
{
    /// <summary>Checked on the server but not here (found by another client, `!collect`, an earlier session).</summary>
    internal readonly List<long> ToAdopt;

    /// <summary>Checked here but not on the server (made offline, or lost when the connection dropped).</summary>
    internal readonly List<long> ToSend;

    internal CheckDifference(List<long> toAdopt, List<long> toSend)
    {
        ToAdopt = toAdopt;
        ToSend = toSend;
    }
}

/// <summary>
/// Brings the local record of checked locations and the server's together, in both directions. Runs after every connect and
/// whenever the server reports newly checked locations. The comparison itself is a pure function so it can be tested alone.
/// </summary>
internal static class Reconciler
{
    internal static CheckDifference Compare(ICollection<long> local, ICollection<long> server) =>
        new(server.Where(id => !local.Contains(id)).ToList(), local.Where(id => !server.Contains(id)).ToList());
}
