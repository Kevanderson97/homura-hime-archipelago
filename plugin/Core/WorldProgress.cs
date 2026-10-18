using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Data;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.Core;

/// <summary>
/// Progress through the eight worlds of a randomizer game. The game itself tracks progress as one number ("hub
/// progress", odd = a chapter entered, even = a chapter cleared) that is tied to the story order. With Archipelago the
/// worlds can be played in any order, so the plugin keeps its own set of cleared worlds and writes the hub progress as
/// "number of cleared worlds" (2 per world, +1 while inside an unfinished one). Shop tiers and hub objects then follow
/// how far the player has got, not which worlds they happened to clear. The rules themselves are in
/// <see cref="WorldRules"/>; this class connects them to the session and the game.
/// </summary>
internal static class WorldProgress
{
    /// <summary>True while a randomizer session is running (connected, state loaded).</summary>
    internal static bool Active => ApClient.Active && ApClient.State != null;

    private static SessionState State => ApClient.State;

    private static WorldRules Rules
    {
        get
        {
            var slot = ApClient.Slot;
            var order = slot.LevelOrder.Count > 0 ? slot.LevelOrder : new System.Collections.Generic.List<string>(WorldRules.VanillaOrder);
            return new WorldRules(order, slot.LevelUnlockItems, slot.IsRandomized);
        }
    }

    internal static void Init()
    {
        SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name == "HubWorld_Main" && Active) SyncHub();
        };
    }

    /// <summary>Where a hub board for <paramref name="chapter"/> should lead instead (see <see cref="WorldRules.RedirectBoard"/>), or null.</summary>
    internal static string BoardTarget(Chapter chapter) => Rules.RedirectBoard(chapter, State.ClearedWorlds, State.PreludeCleared);

    internal static bool IsCleared(Chapter chapter) => State.ClearedWorlds.Contains(WorldRules.WorldOf(chapter));

    /// <summary>Whether a chapter of the Chapter Select may be listed.</summary>
    internal static bool IsAvailable(Chapter chapter) => Rules.IsAvailable(chapter, State.ClearedWorlds, State.UnlockedChapters);

    /// <summary>Replaces the game's own hub-progress writes while a randomizer session is running.</summary>
    internal static void HandleHubWrite(int value)
    {
        var rules = Rules;
        var write = rules.Decide(value, State.ClearedWorlds, State.PreludeCleared);

        if (write.NewWorld != null)
        {
            State.ClearedWorlds.Add(write.NewWorld);
            State.Save();
            PlayerProgress.MarkSubChapterCompleted(write.Chapter.SubChapters[write.Chapter.SubChapters.Length - 1]);
            Plugin.Log.LogInfo($"World cleared: {write.Chapter.Name} ({State.ClearedWorlds.Count}/{rules.WorldCount})");
        }

        if (write.PreludeCleared)
        {
            State.PreludeCleared = true;
            State.Save();
            Plugin.Log.LogInfo("The Climax prelude was cleared");
        }

        if (write.HubValue.HasValue) WriteHub(write.HubValue.Value);
        if (write.Goal) ApClient.CompleteGoal();
    }

    /// <summary>Makes the stored hub progress match the cleared worlds (and the cleared climax prelude), e.g. after loading a save.</summary>
    internal static void SyncHub()
    {
        var current = GameApi.HubProgress;
        if (WorldRules.HubCorrection(current, State.ClearedWorlds, State.PreludeCleared) is not { } target) return;
        Plugin.Log.LogInfo($"Hub progress {current} -> {target} (cleared worlds: {State.ClearedWorlds.Count}, climax prelude: {State.PreludeCleared})");
        GameApi.SetHubProgressUnchecked(target);
    }

    private static void WriteHub(int target)
    {
        if (GameApi.HubProgress != target) GameApi.SetHubProgressUnchecked(target);
    }
}
