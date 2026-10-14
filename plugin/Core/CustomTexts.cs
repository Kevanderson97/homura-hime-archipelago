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

    private static bool _applied;

    /// <summary>
    /// Writes the entries marked "override" into the game's string tables once the strings are loaded, so every place that shows
    /// the item (inventory, shop, pop-ups) uses the name. Called every frame until it has run.
    /// </summary>
    internal static void ApplyOverrides()
    {
        if (_applied || !UnityEngine.Localization.Settings.LocalizationSettings.HasSettings ||
            !UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation.IsDone) return;
        _applied = true;

        foreach (var (definition, text) in All)
        {
            if (!text.Override) continue;
            foreach (var tableName in new[] { "HH_InventoryItemsCollection", "HH_Codex" })
            {
                var table = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(tableName).WaitForCompletion();
                if (text.Name != null && table?.GetEntry(definition) is { } name) name.Value = text.Name;
                if (text.Description != null && table?.GetEntry(definition + "_Description") is { } description) description.Value = text.Description;
            }
        }
    }
}
