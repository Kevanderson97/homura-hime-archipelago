using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace HomuraHimeAP.Core;

/// <summary>A name and description written by hand for an item the game has no text for (data/custom_texts.json).</summary>
internal sealed class CustomText
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("description")] public string Description;

    /// <summary>Written over the game's own entry too (for names the game shows wrongly, such as Ann's Board showing as Ann's Support).</summary>
    [JsonProperty("override")] public bool Override;
}

/// <summary>The hand-written texts, keyed by the game's item definition name. Used where the game''s string tables have none.</summary>
internal static class CustomTexts
{
    private static Dictionary<string, CustomText> _texts;

    internal static IEnumerable<KeyValuePair<string, CustomText>> All => _texts ??= Load();

    internal static CustomText Find(string definition)
    {
        _texts ??= Load();
        return definition != null && _texts.TryGetValue(definition, out var text) ? text : null;
    }

    private static Dictionary<string, CustomText> Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("custom_texts.json");
            if (stream == null) return new Dictionary<string, CustomText>();
            using var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<Dictionary<string, CustomText>>(reader.ReadToEnd()) ?? new Dictionary<string, CustomText>();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not read the custom texts: " + e.Message);
            return new Dictionary<string, CustomText>();
        }
    }

    private const float CheckSeconds = 1f;

    private static float _nextCheck;
    private static bool _everWritten;

    /// <summary>Writes the value into the table; false when it was already there.</summary>
    private static bool Write(UnityEngine.Localization.Tables.StringTable table, string key, string value)
    {
        if (table.GetEntry(key) is { } entry)
        {
            if (entry.Value == value) return false;
            entry.Value = value;
        }
        else
        {
            table.AddEntry(key, value);
        }

        return true;
    }

    /// <summary>
    /// Keeps the entries marked "override" written into the game's string tables, so every place that shows the item (inventory,
    /// shop, pop-ups) uses the name. It is checked every second, not done once: the localization system drops a table when
    /// nothing uses it any more (the scenes the trap setup loads and unloads at the title screen were enough) and loads it fresh
    /// from the game's data later, which brought back the game's own wrong name (Ann's Board shown as Ann's Support). A table
    /// that is not loaded is left alone, never loaded for this.
    /// </summary>
    internal static void ApplyOverrides()
    {
        if (!UnityEngine.Localization.Settings.LocalizationSettings.HasSettings ||
            !UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation.IsDone) return;

        var now = UnityEngine.Time.unscaledTime;
        if (now < _nextCheck) return;
        _nextCheck = now + CheckSeconds;

        var rewritten = false;
        foreach (var tableName in new[] { "HH_InventoryItemsCollection", "HH_Codex" })
        {
            var load = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(tableName);
            if (!load.IsDone || load.Result is not { } table) continue;

            foreach (var (definition, text) in All)
            {
                if (!text.Override) continue;
                // an entry the game's table lacks (the Transformation ability has no item text at all) is added
                if (text.Name != null) rewritten |= Write(table, definition, text.Name);
                if (text.Description != null) rewritten |= Write(table, definition + "_Description", text.Description);
            }
        }

        if (rewritten && _everWritten) Plugin.Log.LogInfo("The game reloaded a text table: the item names were written again");
        _everWritten |= rewritten;
    }
}
