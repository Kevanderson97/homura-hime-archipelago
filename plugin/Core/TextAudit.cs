using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine.Localization.Settings;

namespace HomuraHimeAP.Core;

/// <summary>
/// A one-time report of the texts the game has and lacks, written next to the session files when the game has loaded its
/// strings (delete the files to run it again):
/// <list type="bullet">
/// <item><c>journal_names.tsv</c>: the in-game name of every journal entry (<c>Codex_Note_*</c>), to name the journal locations.</item>
/// <item><c>text_audit.txt</c>: every item of the randomizer whose name or description is in none of the item tables, so a custom entry
/// can be written for it (data/custom_texts.json).</item>
/// </list>
/// </summary>
internal static class TextAudit
{
    private static readonly string[] Tables = { "HH_InventoryItemsCollection", "HH_Codex" };
    private static bool _done;

    private static string Folder => Path.Combine(Paths.ConfigPath, "HomuraHimeArchipelago");

    /// <summary>Called every frame; does nothing once the report exists.</summary>
    internal static void Tick()
    {
        if (_done || !Plugin.CfgTextAudit.Value) return;
        _done = true; // one attempt per run, successful or not
        if (File.Exists(Path.Combine(Folder, "text_audit.txt"))) return;
        if (!LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone) { _done = false; return; }

        try
        {
            Directory.CreateDirectory(Folder);
            var collections = Tables.ToDictionary(name => name, name => LocalizationSettings.StringDatabase.GetTableAsync(name).WaitForCompletion());
            WriteJournalNames(collections["HH_Codex"]);
            WriteAudit(collections);
            Plugin.Log.LogInfo("Text audit written to " + Folder);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Text audit failed: " + e.Message);
        }
    }

    private static void WriteJournalNames(UnityEngine.Localization.Tables.StringTable codex)
    {
        var lines = new List<string>();
        foreach (var entry in codex.Values.OrderBy(e => e.Key))
            if (entry.Key.StartsWith("Codex_Note_") && !entry.Key.EndsWith("_Description"))
                lines.Add(entry.Key + "\t" + (entry.LocalizedValue ?? "").Replace("\n", " ").Replace("\t", " "));
        File.WriteAllLines(Path.Combine(Folder, "journal_names.tsv"), lines, new UTF8Encoding(false));
    }

    private static void WriteAudit(Dictionary<string, UnityEngine.Localization.Tables.StringTable> tables)
    {
        bool Has(string key) => tables.Values.Any(table => !string.IsNullOrEmpty(table.GetEntry(key)?.LocalizedValue));

        var lines = new List<string> { "# item definitions the randomizer can show whose text is in no item table (name / description)" };
        foreach (var item in Catalog.AllItems)
        {
            if (item.Type is "currency" or "unlock" or "utility") continue; // these have texts of the plugin
            if (!Has(item.Game)) lines.Add($"NAME\t{item.Game}\t{item.Name}");
            if (!Has(item.Game + "_Description")) lines.Add($"DESCRIPTION\t{item.Game}\t{item.Name}");
        }

        File.WriteAllLines(Path.Combine(Folder, "text_audit.txt"), lines, new UTF8Encoding(false));
    }
}
