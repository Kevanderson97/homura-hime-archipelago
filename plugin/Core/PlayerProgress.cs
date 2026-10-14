using System.Collections.Generic;
using HomuraHimeAP.Archipelago;

namespace HomuraHimeAP.Core;

/// <summary>
/// The progress flags of the current playthrough: whether the tutorial is finished and which sub-chapters have been left
/// behind. They live in the Archipelago session while a randomizer game is running (so they follow the slot and seed) and in the
/// plugin's own sidecar file otherwise.
/// </summary>
internal static class PlayerProgress
{
    internal static bool TutorialDone
    {
        get => ApClient.State?.TutorialDone ?? SaveState.Current.TutorialDone;
        set
        {
            if (ApClient.State != null) { ApClient.State.TutorialDone = value; ApClient.State.Save(); }
            else { SaveState.Current.TutorialDone = value; SaveState.Current.Save(); }
        }
    }

    internal static ISet<string> CompletedSubChapters => ApClient.State?.CompletedSubChapters ?? SaveState.Current.CompletedSubChapters;

    /// <summary>Records that a sub-chapter was left behind; false when it already was.</summary>
    internal static bool MarkSubChapterCompleted(string key)
    {
        if (!CompletedSubChapters.Add(key)) return false;
        if (ApClient.State != null) ApClient.State.Save();
        else SaveState.Current.Save();
        return true;
    }
}
