using System;
using HomuraHimeAP.Archipelago;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Core;

/// <summary>
/// The tutorial (Level_0_Main) has rewards and cannot be replayed from Chapter Select, so the pause-menu Chapter Select
/// stays unavailable until it is finished. Finished = the hub loads: the game starts a new game in Level 0 and only
/// leaves it for the hub, and a save is first written at the end of the tutorial (the hub is where it is loaded into).
/// Starting Level 0 again (new game) clears the flag.
/// </summary>
internal static class TutorialTracker
{
    private const string TutorialScene = "Level_0_Main";
    private const string HubScene = "HubWorld_Main";

    /// <summary>True while the hub is the active scene (a Unity call, so the tests replace it).</summary>
    internal static Func<bool> InHub = () => SceneManager.GetActiveScene().name == HubScene;

    internal static bool Done => PlayerProgress.TutorialDone;

    internal static void Init()
    {
        SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name == TutorialScene)
            {
                SaveBinding.NewGame();
                Set(false);
                ApClient.State?.ResetForNewGame();
            }
            else if (scene.name == HubScene)
            {
                Set(true);
                SkipTutorialOption.Arrived();
            }
        };
    }

    /// <summary>The tutorial counts as finished from now on (the Skip Tutorial button).</summary>
    internal static void Complete() => Set(true);

    /// <summary>
    /// A save's stored progress was just applied. The save the game writes when the tutorial ends was written before the
    /// hub loaded, so its record still says "not finished"; the game loads it into the hub right after, over the flag
    /// that was just set. A save that is being played in the hub is past the tutorial.
    /// </summary>
    internal static void SaveApplied()
    {
        if (InHub() || GameApi.HubProgress > 0) Set(true);
    }

    private static void Set(bool done)
    {
        if (PlayerProgress.TutorialDone == done) return;
        PlayerProgress.TutorialDone = done;
        Plugin.Log.LogInfo("Tutorial " + (done ? "finished" : "restarted"));
    }
}
