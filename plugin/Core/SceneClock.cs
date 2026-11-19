using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Core;

/// <summary>When the last scene change happened, so game-changing actions (granting items, applying a DeathLink) wait until a level has settled.</summary>
internal static class SceneClock
{
    private static float _lastChange;

    /// <summary>Counts every scene that loaded or unloaded, so something that holds objects of a scene can tell that they may be gone.</summary>
    internal static int Changes { get; private set; }

    internal static void Init()
    {
        SceneManager.sceneLoaded += (_, _) => (_lastChange, Changes) = (Time.unscaledTime, Changes + 1);
        SceneManager.sceneUnloaded += _ => (_lastChange, Changes) = (Time.unscaledTime, Changes + 1);
    }

    /// <summary>True when no scene has loaded or unloaded for <paramref name="seconds"/>.</summary>
    internal static bool Settled(float now, float seconds) => now - _lastChange >= seconds;
}
