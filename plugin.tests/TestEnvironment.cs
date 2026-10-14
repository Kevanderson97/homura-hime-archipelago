using System;
using System.IO;
using BepInEx.Logging;
using HomuraHimeAP.Core;
using Xunit;

// The plugin keeps its state in statics (catalog filter, session folder), so the tests run one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HomuraHimeAP.Tests;

/// <summary>Sets up what the plugin normally gets from BepInEx: a log source and writable folders.</summary>
internal static class TestEnvironment
{
    internal static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "homura-hime-ap-tests-" + Guid.NewGuid().ToString("N"));

    private static bool _ready;

    internal static void Ensure()
    {
        if (_ready) return;
        _ready = true;
        Directory.CreateDirectory(TempDirectory);
        Plugin.Log = new ManualLogSource("tests");
        SessionState.DirectoryOverride = TempDirectory;
        SaveState.DirectoryOverride = TempDirectory;
        SaveBinding.DirectoryOverride = TempDirectory;
        SessionState.Clock = () => 0f;
        TutorialTracker.InHub = () => false;
        Catalog.Load();
    }
}
