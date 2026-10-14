namespace HomuraHimeAP.UI;

/// <summary>The wording of the item notifications (the colour and the icon are chosen where they are shown).</summary>
internal static class ItemMessages
{
    /// <summary>An item found at one of this slot's locations that belongs to another player's world.</summary>
    internal static string Sent(string item, string player) => $"{item} sent to {player}";

    /// <summary>An item of this game that another player found for this slot.</summary>
    internal static string Received(string item, string player) => $"{item} from {player}";

    /// <summary>An item found at one of this slot's locations that is this slot's own.</summary>
    internal static string Found(string item) => $"Found {item}";

    /// <summary>An item this slot found for itself and receives as well, or one the server gave it.</summary>
    internal static string ReceivedFromSelf(string item) => $"Received {item}";
}
