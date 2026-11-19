using System;
using System.Collections.Generic;

namespace HomuraHimeAP.Core;

/// <summary>
/// Which kinds of received items must stay in the player's inventory once they were granted. If one is missing although the
/// plugin counted it as granted (a save written before the grant, an inventory reset, a burst of items the game did not
/// keep), it is given again. Consumable-like items (currency, shards, healing, upgrades, traps) are left out: they are
/// used up, so a missing one proves nothing.
/// </summary>
internal static class OwnedItems
{
    /// <summary>
    /// Equipment, and the key items and Soul Cores that open the way through the game: the game only checks that the player
    /// has them (a door, an NPC, a boss fight), it does not take them away, and without one the player is stuck.
    /// </summary>
    internal static bool IsPermanent(string type) =>
        type is "ability" or "dash_skill" or "skill" or "combo" or "omamori" or "gun" or "key" or "soul_core";

    /// <summary>
    /// Of the items an NPC asks for, those the player does not have although the
    /// multiworld has sent them. They are given before the NPC checks, so a quest does not wait for an item that was received.
    /// </summary>
    internal static List<string> ReceivedButNotOwned(IEnumerable<string> asked, Func<string, bool> owns, Func<string, bool> received)
    {
        var missing = new List<string>();
        foreach (var definition in asked)
            if (!owns(definition) && received(definition) && !missing.Contains(definition)) missing.Add(definition);
        return missing;
    }

    /// <summary>
    /// The items the plugin counted as granted that must be repaired: kept kinds that the game has a definition for and the
    /// player does not own. Each item is named once (the same item received twice is checked once), in the order received.
    /// </summary>
    internal static List<ItemRecord> Missing(IEnumerable<ItemRecord> granted, Func<string, bool> definitionExists, Func<string, bool> owns)
    {
        var missing = new List<ItemRecord>();
        var seen = new HashSet<string>();
        foreach (var record in granted)
        {
            if (record == null || !IsPermanent(record.Type) || !seen.Add(record.Game)) continue;
            if (definitionExists(record.Game) && !owns(record.Game)) missing.Add(record);
        }

        return missing;
    }
}
