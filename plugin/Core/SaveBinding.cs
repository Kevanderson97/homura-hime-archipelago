using System;
using System.Collections.Generic;
using BepInEx;
using Newtonsoft.Json;

namespace HomuraHimeAP.Core;

/// <summary>What a game save was bound to, and how far that save had come when it was written.</summary>
internal sealed class SaveEntry
{
    [JsonProperty] public int Version = 1;
    [JsonProperty] public string Slot;
    [JsonProperty] public string Seed;
    [JsonProperty] public Playthrough Progress = new();
}

/// <summary>How the plugin treats the game save that is being played.</summary>
internal enum BindingDecision
{
    /// <summary>The save has no record and a slot is connected: it becomes that slot's save, starting from nothing received.</summary>
    Adopt,

    /// <summary>The save belongs to the connected slot and seed: carry on from what it had received.</summary>
    Continue,

    /// <summary>The save belongs to another slot or seed: the randomizer stays off for it.</summary>
    Refuse,

    /// <summary>No connection, but the save was bound to a slot while connected: keep playing it from the stored data.</summary>
    Offline,

    /// <summary>No connection and no record: an ordinary, unrandomized game.</summary>
    Vanilla,
}

/// <summary>
/// Knows which Archipelago slot and seed the played game save belongs to (the Hollow Knight client refuses a save with
/// another seed in the same way). The record travels inside the game's own save file, written and read by <see cref="ApSaver"/>,
/// so it always matches the save's contents, also for a copied or restored save. Saves from before that (the record was kept
/// in <c>saves.json</c> next to the session files) are still understood and move into their save the next time the game saves.
/// </summary>
internal static class SaveBinding
{
    private const int FileVersion = 1;
    private const string TitleScene = "StartScreen";

    /// <summary>The game save slot being played; null before a save was loaded or created.</summary>
    internal static int? CurrentSaveSlot { get; set; }

    /// <summary>
    /// True from the moment the title screen loads until a save is loaded or a new game starts. No save is played then, so a
    /// connection made there is not judged against the save that was played before.
    /// </summary>
    internal static bool AtTitle { get; private set; }

    /// <summary>Where the file lives; tests point this at a temporary folder.</summary>
    internal static string DirectoryOverride;

    private static Dictionary<int, SaveEntry> _entries;

    private sealed class FileContent
    {
        [JsonProperty] public int Version = FileVersion;
        [JsonProperty] public Dictionary<int, SaveEntry> Saves = new();
    }

    private static string FilePath => System.IO.Path.Combine(DirectoryOverride ?? System.IO.Path.Combine(Paths.ConfigPath, "HomuraHimeArchipelago"), "saves.json");

    private static Dictionary<int, SaveEntry> Entries => _entries ??= Read();

    /// <summary>The record that came with the save being played; null when the save has none.</summary>
    internal static SaveEntry LoadedRecord { get; set; }

    /// <summary>The record of the save being played (its own, else one from the old <c>saves.json</c>), or null when there is none.</summary>
    internal static SaveEntry CurrentEntry => LoadedRecord ?? LegacyEntry;

    private static SaveEntry LegacyEntry => CurrentSaveSlot is { } slot && Entries.TryGetValue(slot, out var entry) ? entry : null;

    private static bool _saverLoaded;

    /// <summary>The save's saver is loading (called before the record, if any, is read).</summary>
    internal static void LoadStarted()
    {
        _saverLoaded = true;
        LoadedRecord = null;
    }

    /// <summary>The saver found a record in the save being loaded.</summary>
    internal static void RecordLoaded(string json) => LoadedRecord = Deserialize(json);

    /// <summary>The record as it is stored in the save.</summary>
    internal static string Serialize(SessionState state) =>
        JsonConvert.SerializeObject(new SaveEntry { Slot = state.Slot, Seed = state.Seed, Progress = state.CapturePlaythrough() });

    internal static SaveEntry Deserialize(string json)
    {
        try { return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<SaveEntry>(json); }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("The record in the save could not be read, the save is treated as unbound: " + e.Message);
            return null;
        }
    }

    /// <summary>The decision table. <paramref name="connectedSlot"/> is null when there is no connection.</summary>
    internal static BindingDecision Decide(string connectedSlot, string connectedSeed, SaveEntry entry)
    {
        if (entry == null) return connectedSlot != null ? BindingDecision.Adopt : BindingDecision.Vanilla;
        if (connectedSlot == null) return BindingDecision.Offline;
        return entry.Slot == connectedSlot && entry.Seed == connectedSeed ? BindingDecision.Continue : BindingDecision.Refuse;
    }

    /// <summary>
    /// Whether the main menu may load a save: a save that belongs to an Archipelago slot only loads while connected to that
    /// slot and seed (what the plugin prepares at the title screen, and what a save needs from the moment it loads, cannot be
    /// added by connecting afterwards). A save without a record loads as before.
    /// </summary>
    internal static bool MayLoad(string connectedSlot, string connectedSeed, SaveEntry entry) =>
        entry == null || (connectedSlot != null && entry.Slot == connectedSlot && entry.Seed == connectedSeed);

    /// <summary>What the player is told when <see cref="MayLoad"/> says no.</summary>
    internal static string LoadBlockedMessage(string connectedSlot, SaveEntry entry)
    {
        if (connectedSlot == null) return $"This save belongs to Archipelago slot {entry.Slot}. Connect to it first (F10).";
        if (entry.Slot == connectedSlot) return $"This save belongs to another seed of {entry.Slot}. Connect to that seed first (F10).";
        return $"This save belongs to Archipelago slot {entry.Slot}, but you are connected as {connectedSlot}. Connect to {entry.Slot} first (F10).";
    }

    /// <summary>The line on a save of the load panel that says which Archipelago slot it belongs to (the seed stands for the server).</summary>
    internal static string AssociationLabel(SaveEntry entry) =>
        $"Associated with AP: {entry.Slot} (seed {(entry.Seed != null && entry.Seed.Length > 8 ? entry.Seed.Substring(0, 8) : entry.Seed)})";

    /// <summary>The note shown while the Continue panel has greyed-out saves (the slots they belong to, each once).</summary>
    internal static string GreyedNotice(IEnumerable<string> savedSlots, string connectedSlot)
    {
        var names = string.Join(", ", new SortedSet<string>(savedSlots));
        return connectedSlot == null
            ? $"Greyed-out saves belong to Archipelago slot(s): {names}. Connect to the right slot (F10) to load them."
            : $"Greyed-out saves belong to other Archipelago slots or seeds: {names}. You are connected as {connectedSlot}; F10 changes the connection.";
    }

    /// <summary>The record of a game save slot before it is loaded: the one stored in the save file, else one from the old <c>saves.json</c>.</summary>
    internal static SaveEntry RecordOfSlot(int saveSlot) =>
        SaveFiles.RecordOf(saveSlot) ?? (Entries.TryGetValue(saveSlot, out var legacy) ? legacy : null);

    // ------------------------------------------------------------------ what the game does with its saves

    internal static void Init()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name == TitleScene) TitleScreen();
        };
    }

    /// <summary>The title screen is up: whichever save was played is left, and the next load or new game picks the next one.</summary>
    internal static void TitleScreen()
    {
        (AtTitle, CurrentSaveSlot, LoadedRecord) = (true, null, null);
        Archipelago.ApClient.Rebind(reload: true);
    }

    /// <summary>The game loaded this save slot.</summary>
    internal static void Loaded(int saveSlot)
    {
        AtTitle = false;
        CurrentSaveSlot = saveSlot;
        if (!_saverLoaded) LoadedRecord = null; // the saver did not take part in this load, so nothing of an earlier save applies
        _saverLoaded = false;
        Plugin.Log.LogInfo($"Save {saveSlot} loaded" + (CurrentEntry is { } entry ? $" (bound to {entry.Slot})" : " (not bound to a slot)"));
        Archipelago.ApClient.Rebind(reload: true);
    }

    /// <summary>A new game starts: it has no save slot until the game first writes it.</summary>
    internal static void NewGame()
    {
        (AtTitle, CurrentSaveSlot, LoadedRecord) = (false, null, null);
        Archipelago.ApClient.Rebind(reload: true);
    }

    /// <summary>The game is writing this save slot (the saver writes the record itself, see <see cref="ApSaver"/>).</summary>
    internal static void Saving(int saveSlot)
    {
        CurrentSaveSlot = saveSlot;
        var state = Archipelago.ApClient.State;
        if (state != null) LoadedRecord = Deserialize(Serialize(state));
        else if (Archipelago.ApClient.Refusal == null) LoadedRecord = null; // an unrandomized game overwrites the save
        else return; // the save belongs to another slot and was only loaded, never changed by this session

        if (Entries.Remove(saveSlot)) Write(); // a record kept in saves.json now lives in the save
    }
    /// <summary>The game deleted this save slot.</summary>
    internal static void Deleted(int saveSlot)
    {
        if (Entries.Remove(saveSlot)) Write();
        if (CurrentSaveSlot == saveSlot) (CurrentSaveSlot, LoadedRecord) = (null, null);
    }

    // ------------------------------------------------------------------ storage

    private static Dictionary<int, SaveEntry> Read()
    {
        try
        {
            var text = SessionStore.Read(FilePath, t => JsonConvert.DeserializeObject<FileContent>(t) != null);
            return text == null ? new Dictionary<int, SaveEntry>() : JsonConvert.DeserializeObject<FileContent>(text).Saves ?? new Dictionary<int, SaveEntry>();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not read the save records, treating all saves as unbound: " + e.Message);
            return new Dictionary<int, SaveEntry>();
        }
    }

    private static void Write()
    {
        try
        {
            SessionStore.WriteAtomic(FilePath, JsonConvert.SerializeObject(new FileContent { Saves = Entries }, Formatting.Indented));
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not write the save records: " + e.Message);
        }
    }

    /// <summary>Forgets everything held in memory (tests, and a reload of the file).</summary>
    internal static void Reset()
    {
        _entries = null;
        (AtTitle, CurrentSaveSlot) = (false, null);
    }
}
