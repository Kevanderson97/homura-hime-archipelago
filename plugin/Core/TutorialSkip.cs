using System.Collections.Generic;
using HomuraHimeAP.Archipelago;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Core;

/// <summary>
/// The game's own tutorials after the first one (equipping an Omamori and a gun, Transformation, the first-time pop-ups) are
/// scripts that wait for what the player is expected to do next. In a randomizer game the player may already have an Omamori
/// equipped, or none to equip, and the script then waits for good (with the menu locked). They are switched off; the
/// tutorial of Level 0 stays.
/// </summary>
internal static class TutorialSkip
{
    internal const string InitialTutorial = "Level_0_Main";

    /// <summary>The PlayMaker actions that make a script a tutorial (see the patches in TutorialSkipPatches.cs).</summary>
    private static readonly HashSet<string> ActionNames = new()
    {
        "UseTutorialAction", "MainMenuHandler_TutorialEventAction", "MainMenuHandler_OmamoriTutorialInputLockAction",
    };

    internal static bool IsTutorialAction(string typeName) => typeName != null && ActionNames.Contains(typeName);

    /// <summary>Tutorials are skipped in a randomizer game, everywhere but in the first tutorial.</summary>
    internal static bool Applies(bool randomizerActive, string scene) => randomizerActive && scene != InitialTutorial;

    internal static bool Applies() => Applies(ApClient.Active, SceneManager.GetActiveScene().name);
}
