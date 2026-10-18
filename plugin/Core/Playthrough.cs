using System.Collections.Generic;
using Newtonsoft.Json;

namespace HomuraHimeAP.Core;

/// <summary>
/// The part of a session that belongs to one game save rather than to the Archipelago slot: what this save has received,
/// which worlds it has cleared, where it is in the tutorial. It is kept next to the save (see <see cref="SaveBinding"/>) and
/// written whenever the game writes the save, so loading an older save also brings back the older progress, and items that
/// arrived after that point are granted again.
/// </summary>
internal sealed class Playthrough
{
    [JsonProperty] public int ReceivedIndex;
    [JsonProperty] public int BankedUltimateCharge;
    [JsonProperty] public HashSet<string> UnlockedChapters = new();
    [JsonProperty] public HashSet<string> ClearedWorlds = new();
    [JsonProperty] public bool TutorialDone;
    [JsonProperty] public bool PreludeCleared;
    [JsonProperty] public HashSet<string> CompletedSubChapters = new();

    /// <summary>Received items the game could not take (see ItemGranter); tried again after the next load.</summary>
    [JsonProperty] public List<UnresolvedItem> Unresolved = new();
}

/// <summary>A received item that could not be granted, and why.</summary>
internal sealed class UnresolvedItem
{
    [JsonProperty] public long ItemId;
    [JsonProperty] public string Name;
    [JsonProperty] public string Reason;
}
