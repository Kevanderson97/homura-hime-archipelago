using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace HomuraHimeAP.Core;

/// <summary>
/// Progress flags for games that are not a randomizer session, kept in a sidecar file next to the BepInEx config. While a
/// session is active (see <see cref="PlayerProgress"/>) its own copy is used, so the flags follow the slot and seed.
/// </summary>
internal sealed class SaveState
{
    /// <summary>Where the file lives; tests point this at a temporary folder.</summary>
    internal static string DirectoryOverride;

    private static string FilePath => Path.Combine(DirectoryOverride ?? Path.Combine(Paths.ConfigPath, "HomuraHimeArchipelago"), "state.json");

    internal static SaveState Current { get; private set; } = new();

    /// <summary>Sub-chapter keys the player has moved on from (see ChapterAccess).</summary>
    [JsonProperty] public HashSet<string> CompletedSubChapters = new();

    /// <summary>The Level 0 tutorial has been finished in the current playthrough (reset when Level 0 starts again).</summary>
    [JsonProperty] public bool TutorialDone;

    internal static void Load()
    {
        try
        {
            var text = SessionStore.Read(FilePath, t => JsonConvert.DeserializeObject<SaveState>(t) != null);
            Current = text == null ? new SaveState() : JsonConvert.DeserializeObject<SaveState>(text);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not read state file, starting fresh: " + e.Message);
            Current = new SaveState();
        }
    }

    internal void Save()
    {
        try
        {
            SessionStore.WriteAtomic(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not write state file: " + e.Message);
        }
    }
}
