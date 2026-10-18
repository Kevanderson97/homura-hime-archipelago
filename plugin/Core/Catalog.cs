using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using global::Archipelago.MultiClient.Net.Enums;
using Newtonsoft.Json;

namespace HomuraHimeAP.Core;

/// <summary>An item of the AP world as described in data/items.json.</summary>
internal sealed class ItemRecord
{
    [JsonProperty("id")] public long Id;
    [JsonProperty("name")] public string Name;
    [JsonProperty("type")] public string Type;
    [JsonProperty("classification")] public string Classification;

    /// <summary>
    /// How this game's own item is shown (colour of its name, pop-ups). The server marks every Konpeito piece as progression
    /// because the logic of the shop prices counts them, which is not what the player should see.
    /// </summary>
    internal ItemFlags OwnFlags => Classification switch
    {
        "progression" => ItemFlags.Advancement,
        "useful" => ItemFlags.NeverExclude,
        _ => ItemFlags.None,
    };
    /// <summary>Game-side name: item definition, currency name, or a utility keyword.</summary>
    [JsonProperty("game")] public string Game;
    [JsonProperty("collection")] public string Collection;
    [JsonProperty("count")] public int PoolCount;
    [JsonProperty("chapter")] public string Chapter;

    /// <summary>Quantity granted per received item: the leading number of the name ("500 Konpeito"), else 1.</summary>
    public int Amount
    {
        get
        {
            var digits = 0;
            while (digits < Name.Length && char.IsDigit(Name[digits])) digits++;
            return digits > 0 && int.TryParse(Name.Substring(0, digits), out var n) ? n : 1;
        }
    }
}

/// <summary>A location of the AP world as described in data/locations.json.</summary>
internal sealed class LocationRecord
{
    [JsonProperty("id")] public long Id;
    [JsonProperty("key")] public string Key;
    /// <summary>The readable name players see in Archipelago; the key is the stable internal identifier.</summary>
    [JsonProperty("name")] public string Name;
    [JsonProperty("category")] public string Category;
    [JsonProperty("region")] public string Region;
    [JsonProperty("scene")] public string Scene;
    [JsonProperty("vanilla")] public string Vanilla;
    [JsonProperty("pos")] public float[] Pos;
    /// <summary>Encounter locations: the object name and up to four parent names, joined by " &lt; ".</summary>
    [JsonProperty("chain")] public string Chain;
    /// <summary>Senbei locations: "map" (lying around) or "settlement" (handed out when an encounter ends).</summary>
    [JsonProperty("senbei_type")] public string SenbeiType;
}

internal static class Catalog
{
    private static readonly Dictionary<long, ItemRecord> ItemsById = new();
    private static readonly Dictionary<string, ItemRecord> ItemsByName = new();
    private static readonly Dictionary<long, LocationRecord> LocationsById = new();
    private static readonly Dictionary<string, LocationRecord> LocationsByKey = new();

    internal static void Load()
    {
        foreach (var item in Read<List<ItemRecord>>("items.json"))
        {
            ItemsById[item.Id] = item;
            ItemsByName[item.Name] = item;
        }

        foreach (var loc in Read<List<LocationRecord>>("locations.json"))
        {
            LocationsById[loc.Id] = loc;
            LocationsByKey[loc.Key] = loc;
        }

        Plugin.Log.LogInfo($"Catalog: {ItemsById.Count} items, {LocationsById.Count} locations");
    }

    internal static IEnumerable<ItemRecord> AllItems => ItemsById.Values;
    internal static IEnumerable<LocationRecord> AllLocations => LocationsById.Values;

    internal static ItemRecord Item(long id) => ItemsById.TryGetValue(id, out var r) ? r : null;
    internal static ItemRecord Item(string name) => name != null && ItemsByName.TryGetValue(name, out var r) ? r : null;
    internal static LocationRecord Location(long id) => LocationsById.TryGetValue(id, out var r) ? r : null;
    internal static LocationRecord Location(string key) =>
        key != null && LocationsByKey.TryGetValue(key, out var r) && IsActive(r) ? r : null;

    // The slot only contains the locations its options switch on (e.g. no encounter clears unless enabled). Hooks must
    // leave everything else vanilla, so searches only consider the locations the connected slot has.
    private static HashSet<long> _slotLocations;

    internal static void SetSlotLocations(IEnumerable<long> ids) => _slotLocations = ids == null ? null : new HashSet<long>(ids);

    private static bool IsActive(LocationRecord loc) => _slotLocations == null || _slotLocations.Contains(loc.Id);

    private static IEnumerable<LocationRecord> Active() => LocationsById.Values.Where(IsActive);

    /// <summary>The location of one of the given categories in a scene whose recorded position is nearest to pos (within tolerance).</summary>
    internal static LocationRecord Nearest(string scene, UnityEngine.Vector3 pos, float tolerance, params string[] categories)
    {
        LocationRecord best = null;
        var bestDistance = tolerance;
        foreach (var loc in Active())
        {
            if (loc.Scene != scene || loc.Pos == null || loc.Pos.Length < 3 || System.Array.IndexOf(categories, loc.Category) < 0) continue;
            var distance = UnityEngine.Vector3.Distance(pos, new UnityEngine.Vector3(loc.Pos[0], loc.Pos[1], loc.Pos[2]));
            if (distance > bestDistance) continue;
            best = loc;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// The location whose vanilla reward contains the given item definition: in the given scene if there is one,
    /// otherwise anywhere as long as the definition identifies a single location.
    /// </summary>
    internal static LocationRecord ByVanillaItem(string scene, string definition)
    {
        LocationRecord inScene = null, only = null;
        var matches = 0;
        foreach (var loc in Active())
        {
            if (!VanillaContains(loc, definition)) continue;
            matches++;
            only = loc;
            if (loc.Scene == scene) inScene = loc;
        }

        return inScene ?? (matches == 1 ? only : null);
    }

    /// <summary>
    /// The locations of one reward screen: a screen that hands out several things has one location per thing (keys "trial/FC01",
    /// "trial/FC01#2", ...). A location that is alone returns just itself. Only locations of this slot are listed.
    /// </summary>
    internal static IEnumerable<LocationRecord> Bundle(LocationRecord loc)
    {
        var baseKey = BundleKey(loc.Key);
        return Active().Where(other => other.Category == loc.Category && BundleKey(other.Key) == baseKey).ToList();
    }

    /// <summary>The key without the part number: "trial/FC01#2" -> "trial/FC01".</summary>
    internal static string BundleKey(string key) => key.Split(new[] { (char)35 }, 2)[0];

    private static bool VanillaContains(LocationRecord loc, string definition) =>
        loc.Vanilla != null && loc.Vanilla.Split(';').Select(part => part.Trim()).Contains(definition);

    /// <summary>The encounter location of the battle round with this scene and parent-name chain, or null.</summary>
    internal static LocationRecord Encounter(string scene, string chain)
    {
        foreach (var loc in Active())
            if (loc.Category == "encounter" && loc.Scene == scene && loc.Chain == chain)
                return loc;
        return null;
    }

    /// <summary>The Senbei that an encounter in this scene hands out when it ends.</summary>
    internal static IEnumerable<LocationRecord> Settlement(string scene) =>
        Active().Where(loc => loc.Category == "senbei" && loc.SenbeiType == "settlement" && loc.Scene == scene);

    /// <summary>Locations of the given categories in a scene that have no fixed position (rewards that spawn at the end of an encounter).</summary>
    internal static IEnumerable<LocationRecord> Unplaced(string scene, params string[] categories)
    {
        foreach (var loc in Active())
            if (loc.Scene == scene && loc.Pos == null && System.Array.IndexOf(categories, loc.Category) >= 0)
                yield return loc;
    }

    private static T Read<T>(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException("Missing embedded resource " + resource);
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
    }
}
