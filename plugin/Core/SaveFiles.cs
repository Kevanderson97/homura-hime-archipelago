using System;
using System.Collections.Generic;
using HarmonyLib;
using Opsive.Shared.Utility;
using Opsive.UltimateInventorySystem.SaveSystem;

namespace HomuraHimeAP.Core;

/// <summary>
/// Reads the Archipelago record out of the game's save files without loading them. The main menu starts the ending again
/// (once a save has beaten the game) without loading any save, so which save that belongs to can only be told from the files:
/// the one whose record names the connected slot and seed.
/// </summary>
internal static class SaveFiles
{
    private const int SaveSlots = 3; // what the title screen lists
    private const string SaverKey = "ApSaver"; // part of the key the framework files the record under

    /// <summary>
    /// True when one of the game's saves was made for this slot and seed and has come as far as the ending (a save of the game
    /// being played that is still early in the story cannot stand before the choice, whatever other saves have done).
    /// </summary>
    internal static bool AnySaveReachedFinale(string slot, string seed, int worldCount)
    {
        foreach (var record in Records())
            if (SaveBinding.Decide(slot, seed, record) == BindingDecision.Continue && record.Progress.ReachedFinale(worldCount)) return true;
        return false;
    }

    private static List<SaveEntry> Records()
    {
        var records = new List<SaveEntry>();
        try
        {
            var manager = AccessTools.Property(typeof(SaveSystemManager), "Instance")?.GetValue(null);
            var read = manager == null ? null : AccessTools.Method(manager.GetType(), "ReadSaveDataFromDisk", new[] { typeof(int) });
            if (read == null) return records;

            for (var index = 0; index < SaveSlots; index++)
            {
                if (read.Invoke(manager, new object[] { index }) is not SaveData data) continue;
                foreach (var key in data.SaveDataKeys)
                {
                    if (!key.Contains(SaverKey) || !data.TryGetValue(key, out var serialization)) continue;
                    if (serialization.DeserializeFields(MemberVisibility.All) is ApSaveHolder holder && SaveBinding.Deserialize(holder.Json) is { } record)
                        records.Add(record);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not read the Archipelago records of the save files: " + e.Message);
        }

        return records;
    }
}
