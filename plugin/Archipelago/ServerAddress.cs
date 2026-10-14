using System;

namespace HomuraHimeAP.Archipelago;

/// <summary>
/// Turns what the player typed ("host", "host:port" or a full ws:// / wss:// URL) into the address the client library
/// connects to. Without a scheme, hosts that cannot have TLS (localhost, IPs, single-label LAN names) use plain ws://
/// directly, because the library's wss-first probe times out against them.
/// </summary>
internal static class ServerAddress
{
    internal const int DefaultPort = 38281;

    /// <summary>The address to connect to, or null with an <paramref name="error"/> the player can read.</summary>
    internal static string Normalize(string input, out string error)
    {
        error = null;
        var address = (input ?? "").Trim();
        if (address.Length == 0)
        {
            error = "Enter the server address (host:port)";
            return null;
        }

        if (address.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            return address;

        var host = address;
        var port = DefaultPort;
        var colon = address.LastIndexOf(':');
        if (colon >= 0)
        {
            host = address.Substring(0, colon);
            if (host.Length == 0 || !int.TryParse(address.Substring(colon + 1), out port) || port < 1 || port > 65535)
            {
                error = $"Invalid server address \"{address}\": the port must be a number from 1 to 65535";
                return null;
            }
        }

        var plain = host == "localhost" || !host.Contains(".") || System.Net.IPAddress.TryParse(host, out _);
        return plain ? $"ws://{host}:{port}" : $"{host}:{port}";
    }
}
