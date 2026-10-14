using Newtonsoft.Json;

namespace HomuraHimeAP.Archipelago;

/// <summary>What the server placed at one location of the slot, in the form that is stored for offline play.</summary>
internal sealed class ScoutEntry
{
    [JsonProperty] public long LocationId;
    [JsonProperty] public long ItemId;
    [JsonProperty] public string ItemName;
    [JsonProperty] public string PlayerName;
    [JsonProperty] public int Flags;
    [JsonProperty] public bool Own;
}
