using System.Collections.Generic;
using System.Text;

namespace HomuraHimeAP.Core;

/// <summary>
/// The text of a DeathLink death sent from this game: who (or what) dealt the blow that killed the player, in words. The game
/// names its enemies by the names of their objects ("Enemy_CursedDoll_A_Default(Clone)"); <see cref="NameOf"/> turns such a name
/// into something readable. The decisions are free of Unity so they can be tested.
/// </summary>
internal static class DeathCause
{
    /// <summary>Words of an object name that say nothing to a player.</summary>
    private static readonly HashSet<string> Noise = new() { "Enemy", "Default", "Clone", "Prefab", "Variant" };

    /// <summary>Words the game's object names use for what the rest of the project calls something else.</summary>
    private static readonly Dictionary<string, string> Renamed = new() { ["Cursed Doll"] = "Hexed Doll" };

    /// <summary>A readable name for an enemy (or any other source of damage) from the name of its object; null when there is none.</summary>
    internal static string NameOf(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        var words = new List<string>();
        foreach (var part in objectName.Replace("(Clone)", "").Split('_', ' ', '(', ')', '-'))
        {
            if (part.Length == 0 || Noise.Contains(part)) continue;
            words.Add(SplitCamelCase(part));
        }

        var name = string.Join(" ", words).Trim();
        foreach (var pair in Renamed) name = name.Replace(pair.Key, pair.Value);
        return name.Length == 0 ? null : name;
    }

    /// <summary>"CursedDoll" becomes "Cursed Doll"; a single letter or a number stays as it is.</summary>
    private static string SplitCamelCase(string word)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < word.Length; i++)
        {
            var current = word[i];
            if (i > 0 && char.IsUpper(current) && (char.IsLower(word[i - 1]) || (i + 1 < word.Length && char.IsLower(word[i + 1]) && char.IsUpper(word[i - 1]))))
                builder.Append(' ');
            builder.Append(current);
        }

        return builder.ToString();
    }

    /// <summary>The generic causes of death the other players see (the cause of a hit is not used for now); {0} is the player.</summary>
    internal static readonly string[] Generic =
    {
        "{0} should have parried.", "{0} couldn't dodge.", "{0} was excommunicated.", "{0} was exorcised.",
        "{0} was haunted.", "{0} couldn't handle the challenge.", "{0} was killed.", "{0} couldn't escape fate.",
        "{0} was decimated.", "{0} couldn't defeat the Will of the World.",
    };

    /// <summary>One of the generic causes; <paramref name="pick"/> is any number (it wraps around the list).</summary>
    internal static string GenericMessage(string slot, int pick) => string.Format(Generic[((pick % Generic.Length) + Generic.Length) % Generic.Length], slot);

    /// <summary>The cause naming the killer ("Slot was killed by X"), or where it happened when the killer is not known. Not used for now.</summary>
    internal static string Message(string slot, string killer, string scene) =>
        string.IsNullOrWhiteSpace(killer) ? $"{slot} died in {scene}" : $"{slot} was killed by {killer}";

    /// <summary>
    /// The index of the smallest distance that is no longer than <paramref name="maxDistance"/> (the distances are given squared),
    /// or -1 when none is: the shooter of a laser is the enemy next to the place it starts from.
    /// </summary>
    internal static int Nearest(System.Collections.Generic.IReadOnlyList<float> squaredDistances, float maxDistance)
    {
        var best = -1;
        for (var i = 0; i < squaredDistances.Count; i++)
            if (squaredDistances[i] <= maxDistance * maxDistance && (best < 0 || squaredDistances[i] < squaredDistances[best])) best = i;
        return best;
    }

    /// <summary>The last damage dealt to the player counts as the killer when it is not older than <paramref name="window"/> seconds.</summary>
    internal static string Killer(string lastSource, float lastTime, float now, float window) =>
        lastSource != null && now - lastTime <= window ? lastSource : null;
}
