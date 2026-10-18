using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Items;
using Newtonsoft.Json;

namespace HomuraHimeAP.Core;

/// <summary>
/// Everything that belongs to one Archipelago slot of one seed: how many received items have already been granted,
/// which locations are checked, unlocked chapters and the playthrough's progress flags. Stored next to the BepInEx config, one
/// file per slot and seed, so reconnecting never double-grants and a different seed never reuses old state.
/// Saving is deferred a moment and merged (a burst of checks is one write); the file is written atomically.
/// </summary>
internal sealed class SessionState
{
    /// <summary>Version of the file format. 0 = files written before versioning, 2 = progress flags live in the session.</summary>
    internal const int CurrentVersion = 2;

    /// <summary>How long a change may wait before it is written.</summary>
    internal const float SaveDelaySeconds = 0.25f;

    [JsonProperty] public int Version;
    [JsonProperty] public string Slot;
    [JsonProperty] public string Seed;

    /// <summary>Number of received items (in server order) that have been granted in game.</summary>
    [JsonProperty] public int ReceivedIndex;

    [JsonProperty] public HashSet<long> CheckedLocations = new();
    [JsonProperty] public HashSet<string> UnlockedChapters = new();

    /// <summary>The worlds (chapters) whose boss has been beaten, in no particular order.</summary>
    [JsonProperty] public HashSet<string> ClearedWorlds = new();

    /// <summary>Ultimate Charge items received while neither ability was owned (applied when one arrives).</summary>
    [JsonProperty] public int BankedUltimateCharge;

    /// <summary>The Climax prelude (the start of the Chapter 6 world) has been cleared; its hub gate then leads to the world itself.</summary>
    [JsonProperty] public bool PreludeCleared;

    /// <summary>The Level 0 tutorial has been finished in the current playthrough (reset when Level 0 starts again).</summary>
    [JsonProperty] public bool TutorialDone;

    /// <summary>Sub-chapter keys the player has moved on from (see ChapterAccess).</summary>
    [JsonProperty] public HashSet<string> CompletedSubChapters = new();

    /// <summary>Received items the game could not take; tried again after the next load.</summary>
    [JsonProperty] public List<UnresolvedItem> Unresolved = new();

    /// <summary>Shop locations already announced as hints, and those seen but not sent yet (slot-wide: hints belong to the multiworld).</summary>
    [JsonProperty] public HashSet<long> HintedLocations = new();
    [JsonProperty] public HashSet<long> PendingHints = new();

    /// <summary>The game was beaten; reported to the server on every connection until it confirms.</summary>
    [JsonProperty] public bool GoalReached;

    /// <summary>What the last connection told the plugin about the slot; lets a bound save be played without a connection.</summary>
    [JsonProperty] public string SlotDataJson;
    [JsonProperty] public List<long> LocationIds;
    [JsonProperty] public List<ScoutEntry> Scouted;

    /// <summary>Where the files live; tests point this at a temporary folder.</summary>
    internal static string DirectoryOverride;

    /// <summary>The clock used to time deferred saves; tests replace it because Unity's is not available outside the game.</summary>
    internal static Func<float> Clock = () => UnityEngine.Time.realtimeSinceStartup;

    private static readonly HashSet<SessionState> Dirty = new();
    private float _dirtySince;

    private static string Dir => DirectoryOverride ?? System.IO.Path.Combine(Paths.ConfigPath, "HomuraHimeArchipelago");

    private string FilePath => System.IO.Path.Combine(Dir, $"session_{Sanitize(Slot)}_{Sanitize(Seed)}.json");

    internal static SessionState Load(string slot, string seed)
    {
        var fresh = new SessionState { Version = CurrentVersion, Slot = slot, Seed = seed };
        try
        {
            var loaded = Parse(SessionStore.Read(fresh.FilePath, text => Parse(text) != null));
            if (loaded == null || loaded.Slot != slot || loaded.Seed != seed)
            {
                fresh.ImportLegacyProgress();
                return fresh;
            }

            if (loaded.Version < 2) loaded.ImportLegacyProgress();
            loaded.Version = CurrentVersion;
            return loaded;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not read session state, starting fresh: " + e.Message);
            return fresh;
        }
    }

    private static SessionState Parse(string text) => text == null ? null : JsonConvert.DeserializeObject<SessionState>(text);

    /// <summary>Files from before versioning kept the progress flags in the global sidecar file; take them over once.</summary>
    private void ImportLegacyProgress()
    {
        TutorialDone = SaveState.Current.TutorialDone;
        CompletedSubChapters = new HashSet<string>(SaveState.Current.CompletedSubChapters);
    }

    /// <summary>A new game starts with an empty inventory, so everything received so far has to be granted again.</summary>
    internal void ResetForNewGame()
    {
        ApplyPlaythrough(new Playthrough());
        Plugin.Log.LogInfo("New game: received items will be granted again");
    }

    /// <summary>The progress that belongs to the game save currently being played.</summary>
    internal Playthrough CapturePlaythrough() => new()
    {
        ReceivedIndex = ReceivedIndex,
        BankedUltimateCharge = BankedUltimateCharge,
        UnlockedChapters = new HashSet<string>(UnlockedChapters),
        ClearedWorlds = new HashSet<string>(ClearedWorlds),
        TutorialDone = TutorialDone,
        PreludeCleared = PreludeCleared,
        CompletedSubChapters = new HashSet<string>(CompletedSubChapters),
        Unresolved = Unresolved.ToList(),
    };

    /// <summary>Switches to the progress of another game save. Checked locations belong to the slot and are kept.</summary>
    internal void ApplyPlaythrough(Playthrough playthrough)
    {
        ReceivedIndex = playthrough.ReceivedIndex;
        BankedUltimateCharge = playthrough.BankedUltimateCharge;
        UnlockedChapters = new HashSet<string>(playthrough.UnlockedChapters);
        ClearedWorlds = new HashSet<string>(playthrough.ClearedWorlds);
        TutorialDone = playthrough.TutorialDone;
        PreludeCleared = playthrough.PreludeCleared;
        CompletedSubChapters = new HashSet<string>(playthrough.CompletedSubChapters);
        Unresolved = playthrough.Unresolved.ToList();
        Save();
        Items.ItemGranter.Recheck();
    }
    /// <summary>Marks the state as changed; it is written within <see cref="SaveDelaySeconds"/>, or at once by <see cref="Flush"/>.</summary>
    internal void Save()
    {
        if (Dirty.Add(this)) _dirtySince = Clock();
    }

    /// <summary>Writes pending changes now.</summary>
    internal void Flush()
    {
        if (!Dirty.Remove(this)) return;
        try
        {
            SessionStore.WriteAtomic(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not write session state: " + e.Message);
            Dirty.Add(this); // try again on the next tick
        }
    }

    /// <summary>Called every frame: writes states whose changes have waited long enough.</summary>
    internal static void Tick(float now)
    {
        if (Dirty.Count == 0) return;
        foreach (var state in Dirty.Where(s => now - s._dirtySince >= SaveDelaySeconds).ToList()) state.Flush();
    }

    /// <summary>Writes every pending state (disconnect, scene change, quit).</summary>
    internal static void FlushAll()
    {
        foreach (var state in Dirty.ToList()) state.Flush();
    }

    private static string Sanitize(string s) =>
        new string((s ?? "x").Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
