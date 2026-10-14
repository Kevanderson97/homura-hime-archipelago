using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HomuraHimeAP.Tests;

/// <summary>
/// Rules learned the hard way (see docs/TESTING.md), checked against the source so a new hook or call cannot break them unnoticed.
/// </summary>
public class SourceLintTests
{
    private static readonly string PluginDirectory = FindPluginDirectory();

    private static string FindPluginDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "plugin", "Patches"))) directory = directory.Parent;
        return directory == null ? throw new InvalidOperationException("plugin folder not found") : Path.Combine(directory.FullName, "plugin");
    }

    private static string[] Sources(string subfolder = "") =>
        Directory.GetFiles(Path.Combine(PluginDirectory, subfolder), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToArray();

    [Fact]
    public void ItemAndLocationNamesAreOnlyReadWhereTheDataPackageMayBlock()
    {
        // Resolving a name can wait for the server, which deadlocks on the game thread. ScoutCache reads them on the pool,
        // ApClient does so inside its own Task.Run (ScoutAndAnnounce).
        var allowed = new[] { "ScoutCache.cs", "ApClient.cs" };
        string[] forbidden = { ".ItemName", ".LocationName", ".LocationDisplayName", ".ItemDisplayName" };

        var offenders = Sources()
            .Where(f => !allowed.Contains(Path.GetFileName(f)))
            .Where(f => forbidden.Any(token => File.ReadAllText(f).Contains(token)))
            .Select(Path.GetFileName);

        Assert.Empty(offenders);
    }

    [Fact]
    public void CallsIntoTheClientLibraryThatCanBlockRunOnThePool()
    {
        string[] blocking = { ".SendDeathLink(", ".CompleteLocationChecks(", ".SetGoalAchieved(", ".ScoutLocationsAsync(", ".TryConnectAndLogin(" };

        var offenders = Sources("Archipelago")
            .Where(f => blocking.Any(call => File.ReadAllText(f).Contains(call)))
            .Where(f => !File.ReadAllText(f).Contains("Task.Run"))
            .Select(Path.GetFileName);

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryHookFileChecksThatTheRandomizerIsActive()
    {
        // These delegate to a guarded class or guard inside the code they call (documented here on purpose).
        var delegating = new[]
        {
            "LocationPatches.cs",   // calls LocationHooks, which checks ApClient.Active
            "DeathPatches.cs",      // DeathLinkHandler only acts when a DeathLink service exists
            "SaveSlotPatches.cs",   // binds saves; works without a connection by design
            "ChapterSelectPatches.cs", // asks WorldProgress/ChapterAccess, which fall back to vanilla themselves
            "MissilePatches.cs",    // bug fixes for the missile gun's own HUD, which only exists when that gun is in use
            "CutGunSoundPatches.cs", // sounds of the cut-content guns, which only fire when the player owns one
            "PortalPatches.cs",     // HubBoards and ChapterAccess check WorldProgress.Active; PostGameHub is about the Chapter Select the plugin exposes
        };

        var offenders = Directory.GetFiles(Path.Combine(PluginDirectory, "Patches"), "*.cs")
            .Where(f => File.ReadAllText(f).Contains("[HarmonyPatch]"))
            .Where(f => !delegating.Contains(Path.GetFileName(f)))
            .Where(f => !File.ReadAllText(f).Contains("ApClient.Active") && !File.ReadAllText(f).Contains("WorldProgress.Active"))
            .Select(Path.GetFileName);

        Assert.Empty(offenders);
    }
}
