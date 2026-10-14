using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Core;

/// <summary>When the last scene change happened, so game-changing actions (granting items, applying a DeathLink) wait until a level has settled.</summary>
internal static class SceneClock
{
    private static float _lastChange;

    internal static void Init()
    {
        SceneManager.sceneLoaded += (_, _) => _lastChange = Time.unscaledTime;
        SceneManager.sceneUnloaded += _ => _lastChange = Time.unscaledTime;
    }

    /// <summary>True when no scene has loaded or unloaded for <paramref name="seconds"/>.</summary>
    internal static bool Settled(float now, float seconds) => now - _lastChange >= seconds;
}
