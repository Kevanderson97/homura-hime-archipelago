using System;
using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Core;
using Newtonsoft.Json.Linq;

namespace HomuraHimeAP.Archipelago;

/// <summary>
/// Options and generation results the AP world sends to the client (see apworld fill_slot_data). Parsing is tolerant: a
/// missing or odd value falls back to a default and is reported in <see cref="Warnings"/>; a seed this plugin cannot play
/// correctly (an unsupported world version) sets <see cref="Problem"/> and the connection is refused with that message.
/// </summary>
internal sealed class SlotData
{
    /// <summary>
    /// The world versions this plugin understands (major.minor); patch versions never change the slot data. Worlds
    /// before 0.3 have no Konpeito items in the pool, while this plugin takes the vanilla Konpeito payouts away.
    /// </summary>
    internal static readonly (int Major, int Minor) OldestWorld = (0, 4), NewestWorld = (0, 4);

    internal string WorldVersion;
    /// <summary>Chapter keys in play order (vanilla or shuffled), e.g. "Level1", "Level3", ...</summary>
    internal List<string> LevelOrder = new();
    internal string LevelOrderMode = "vanilla";
    internal UnlockMode UnlockMode = UnlockMode.Off;
    internal bool LevelUnlockItems => UnlockMode != UnlockMode.Off;
    /// <summary>The world order is shuffled ("balanced" or "full"; "randomized" is what worlds before the balanced option sent).</summary>
    internal bool IsRandomized => LevelOrderMode is "balanced" or "full" or "randomized";
    internal int DangoCount;

    /// <summary>How many Puffer Swarm Traps the seed asked for; the enemies they need are only prepared when this is above 0.</summary>
    internal int PufferSwarmTraps, HexedDollTrioTraps;
    internal bool IncludeCutContent;
    internal bool EncounterRewards;

    /// <summary>The logic asks for no dash skill: the gap in Level 1 is crossed by parrying a laser (worlds before 0.3.2 do not send it: off).</summary>
    internal bool AdvancedMovementRequired;
    internal bool DeathLink;
    internal bool DeathLinkSkipRevives;

    /// <summary>Seeing an item in a shop tells the players concerned (off when the option is missing).</summary>
    internal bool ShopHints;

    /// <summary>Why this slot data cannot be played with this plugin; null when it can.</summary>
    internal string Problem;

    /// <summary>Things that were odd but not fatal (a default was used).</summary>
    internal readonly List<string> Warnings = new();

    /// <summary>Parses the slot data as it was stored by <see cref="ToJson"/> (offline play).</summary>
    internal static SlotData FromJson(string json) =>
        Parse(string.IsNullOrEmpty(json) ? null : Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json));

    internal static string ToJson(IDictionary<string, object> raw) => Newtonsoft.Json.JsonConvert.SerializeObject(raw);

    internal static SlotData Parse(IDictionary<string, object> raw)
    {
        var d = new SlotData();
        if (raw == null)
        {
            d.Problem = "The server sent no slot data";
            return d;
        }

        d.WorldVersion = d.Text(raw, "world_version");
        d.LevelOrderMode = d.Text(raw, "level_order_mode") ?? "vanilla";
        d.ReadUnlockMode(raw);
        d.DangoCount = (int)d.Number(raw, "dango_count");
        d.PufferSwarmTraps = (int)d.Number(raw, "puffer_swarm_traps");
        d.HexedDollTrioTraps = (int)d.Number(raw, "hexed_doll_trio_traps");
        d.IncludeCutContent = d.Flag(raw, "include_cut_content");
        d.EncounterRewards = d.Flag(raw, "encounter_rewards");
        d.AdvancedMovementRequired = d.Flag(raw, "advanced_movement_required");
        d.DeathLink = d.Flag(raw, "death_link");
        d.DeathLinkSkipRevives = d.Flag(raw, "death_link_skip_revives");
        d.ShopHints = d.Flag(raw, "shop_hints");
        d.ReadLevelOrder(raw);
        d.CheckVersion();
        return d;
    }

    /// <summary>One line per option, for the log (the other clients do the same, it makes bug reports readable).</summary>
    internal IEnumerable<string> Describe() => new[]
    {
        $"world {WorldVersion}, level order {LevelOrderMode} [{string.Join(", ", LevelOrder)}]",
        $"level keys {UnlockMode}, dango {DangoCount}, puffer swarm traps {PufferSwarmTraps}, hexed doll trio traps {HexedDollTrioTraps}, cut content {IncludeCutContent}",
        $"advanced movement {AdvancedMovementRequired}, encounter rewards {EncounterRewards}, death link {DeathLink} (skip revives {DeathLinkSkipRevives}), shop hints {ShopHints}",
    };

    private void CheckVersion()
    {
        if (!Version.TryParse(WorldVersion, out var version))
        {
            Problem = $"The seed has no usable world version (\"{WorldVersion}\"); generate it again with a current Homura-Hime world";
            return;
        }

        var tooOld = (version.Major, version.Minor).CompareTo(OldestWorld) < 0;
        var tooNew = (version.Major, version.Minor).CompareTo(NewestWorld) > 0;
        if (tooOld) Problem = $"The seed was generated by an old Homura-Hime world ({WorldVersion}); generate it again";
        else if (tooNew) Problem = $"The seed was generated by a newer Homura-Hime world ({WorldVersion}); update the plugin";
    }

    /// <summary>"off", "linear_progression" or "freeform_progression"; anything else is off, with a warning.</summary>
    private void ReadUnlockMode(IDictionary<string, object> raw)
    {
        switch (Text(raw, "level_unlock_items"))
        {
            case null:
            case "off": UnlockMode = UnlockMode.Off; break;
            case "linear_progression": UnlockMode = UnlockMode.Linear; break;
            case "freeform_progression": UnlockMode = UnlockMode.Freeform; break;
            default:
                Warnings.Add($"level_unlock_items \"{Text(raw, "level_unlock_items")}\" is not known; playing without level keys");
                break;
        }
    }

    /// <summary>The level order must name exactly the nine worlds; anything else falls back to the story order.</summary>
    private void ReadLevelOrder(IDictionary<string, object> raw)
    {
        if (!raw.TryGetValue("level_order", out var order) || order == null) return;

        List<string> read;
        try
        {
            var array = order as JArray ?? JArray.FromObject(order);
            read = array.Select(t => t.ToString()).ToList();
        }
        catch (Exception e)
        {
            Warnings.Add($"level_order is not a list ({e.Message}); using the story order");
            return;
        }

        var known = WorldRules.VanillaOrder.ToList();
        if (read.Count != known.Count || read.Distinct().Count() != read.Count || read.Except(known).Any())
        {
            Warnings.Add($"level_order [{string.Join(", ", read)}] is not the nine worlds; using the story order");
            LevelOrderMode = "vanilla";
            return;
        }

        LevelOrder = read;
    }

    private string Text(IDictionary<string, object> raw, string key) =>
        raw.TryGetValue(key, out var v) && v != null ? v.ToString() : null;

    private long Number(IDictionary<string, object> raw, string key)
    {
        if (!raw.TryGetValue(key, out var v) || v == null) return 0;
        try
        {
            return Convert.ToInt64(v is bool b ? (b ? 1 : 0) : v);
        }
        catch (Exception)
        {
            Warnings.Add($"{key} = \"{v}\" is not a number; using 0");
            return 0;
        }
    }

    private bool Flag(IDictionary<string, object> raw, string key) => Number(raw, key) != 0;
}
