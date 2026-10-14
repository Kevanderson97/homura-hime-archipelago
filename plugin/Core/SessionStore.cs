using System;
using System.IO;

namespace HomuraHimeAP.Core;

/// <summary>
/// Reads and writes the plugin's JSON files so a crash or a full disk never leaves a half-written file behind: the new content
/// goes to a temporary file first and replaces the old one in one step, which becomes the backup. A file that cannot be read
/// falls back to that backup.
/// </summary>
internal static class SessionStore
{
    private const string Temporary = ".tmp";
    private const string Backup = ".bak";

    /// <summary>Writes the content so that either the old or the new file exists in full, never a mixture.</summary>
    internal static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + Temporary;
        try
        {
            File.WriteAllText(temporary, content);
            if (File.Exists(path)) File.Replace(temporary, path, path + Backup);
            else File.Move(temporary, path);
        }
        catch
        {
            try { File.Delete(temporary); } catch { /* nothing more to clean up */ }
            throw;
        }
    }

    /// <summary>
    /// The content of the file, or of its backup when the file is missing or <paramref name="isValid"/> rejects it. Null when
    /// neither exists or neither is valid.
    /// </summary>
    internal static string Read(string path, Func<string, bool> isValid)
    {
        foreach (var candidate in new[] { path, path + Backup })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                var content = File.ReadAllText(candidate);
                if (!isValid(content)) throw new InvalidDataException("content rejected");
                if (candidate != path) Plugin.Log.LogWarning($"{Path.GetFileName(path)} was unreadable, restored from its backup");
                return content;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read {Path.GetFileName(candidate)}: {e.Message}");
            }
        }

        return null;
    }
}
