using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;

namespace HomuraHimeAP.Core;

/// <summary>
/// A scene that is loaded next to the game's own starts the sounds of its place (the ambience of the area, wind, the sea) and
/// does not stop all of them when it is unloaded again: they would go on playing in the title screen for ever. The trap setup
/// notes what is playing before it loads its source scenes and stops the area ambience and the sound effects that have started
/// since. Music is left alone.
/// </summary>
internal static class SceneSounds
{
    private static readonly HashSet<IntPtr> Known = new();

    /// <summary>Notes the events that are playing now; they are not touched by <see cref="StopNew"/>.</summary>
    internal static void Remember()
    {
        Known.Clear();
        try
        {
            foreach (var (instance, _) in Playing()) Known.Add(instance.handle);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not list the playing sounds: " + e.Message);
        }
    }

    /// <summary>Stops the ambience and sound effect events that were not playing when <see cref="Remember"/> was called.</summary>
    internal static void StopNew()
    {
        var stopped = new List<string>();
        try
        {
            foreach (var (instance, path) in Playing())
            {
                if (Known.Contains(instance.handle) || !IsScenePath(path)) continue;
                instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                stopped.Add(path);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not stop the sounds of a source scene: " + e.Message);
        }

        if (stopped.Count > 0) Plugin.Log.LogInfo("Sounds started by a source scene were stopped: " + string.Join(", ", stopped));
    }

    private static bool IsScenePath(string path) => path.StartsWith("event:/Ambient/") || path.StartsWith("event:/SFX/");

    private static IEnumerable<(EventInstance Instance, string Path)> Playing()
    {
        RuntimeManager.StudioSystem.getBankList(out var banks);
        foreach (var bank in banks)
        {
            if (bank.getEventList(out var descriptions) != FMOD.RESULT.OK) continue;
            foreach (var description in descriptions)
            {
                if (description.getInstanceList(out var instances) != FMOD.RESULT.OK || instances.Length == 0) continue;
                description.getPath(out var path);
                foreach (var instance in instances)
                {
                    instance.getPlaybackState(out var state);
                    if (state is PLAYBACK_STATE.PLAYING or PLAYBACK_STATE.STARTING or PLAYBACK_STATE.SUSTAINING) yield return (instance, path);
                }
            }
        }
    }
}
