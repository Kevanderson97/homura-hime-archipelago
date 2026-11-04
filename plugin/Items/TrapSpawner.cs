using System;
using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace HomuraHimeAP.Items;

/// <summary>
/// The traps put enemies around the player. The game keeps no prefab of these enemies (they exist only inside the scenes that
/// use them), so a copy of each is made once per game session, in a one-time setup: a scene that holds them is loaded next to
/// the current one, the enemies are copied into a hidden holder that survives scene changes, and the scene is unloaded again.
/// A trap then copies from the holder, in any scene. The setup runs at the title screen, once a connection shows that the
/// seed has traps: nothing big is loaded then, the screen is covered (see <see cref="SetupOverlay"/>) and the sound is off.
/// </summary>
internal static class TrapSpawner
{
    /// <summary>One trap: the enemies it needs, where they are copied from, and how many of each it spawns.</summary>
    private sealed class Kind
    {
        internal string Item;                  // the item's game key (data/items.json)
        internal string Scene;                 // a scene that holds every enemy of the trap
        internal string ComponentType;         // the enemy component the templates are found by
        internal (string Template, int Count)[] Enemies;
        internal bool Flying;                  // placed in the air around the player; otherwise a group on the ground
        internal bool NeedsGround;             // waits until the player stands on solid ground (not in the air, on a rail or in a boost)
        internal Func<SlotData, int> InSeed;   // how many of this trap the seed asked for
    }

    private static readonly Kind[] Kinds =
    {
        new()
        {
            Item = "PufferSwarmTrap", Scene = "Level_4_Area_1_Main", ComponentType = "Standalone_SummonObject",
            Enemies = new[] { ("SummonObject_Fugu", 4), ("SummonObject_BombFugu", 4) }, // the spiky and the bomb puffers
            Flying = true, NeedsGround = true, InSeed = slot => slot.PufferSwarmTraps,
        },
        new()
        {
            Item = "HexedDollTrioTrap", Scene = "Level_3_Area_6_Main", ComponentType = "HonoHime.CharacterSystem.Enemy",
            Enemies = new[] { ("Enemy_CursedDoll_A_Default", 1), ("Enemy_CursedDoll_B_Default", 1), ("Enemy_CursedDoll_C_Default", 1) },
            Flying = false, NeedsGround = true, InSeed = slot => slot.HexedDollTrioTraps,
        },
    };

    private const string SetupMessage = "Preparing AP World Settings";

    /// <summary>The title screen has to be quiet for this long before the setup starts; the game, before a trap goes off.</summary>
    private const float TitleSettleSeconds = 1.5f, GameSettleSeconds = 6f;

    /// <summary>A ground trap goes off once the player has stood on solid ground for this long (not on the frame of a landing).</summary>
    private const float LandedSeconds = 0.5f;

    /// <summary>A source scene that is not loaded after this long is given up on.</summary>
    private const float LoadTimeoutSeconds = 90f;

    /// <summary>After the last unload the game runs smoothly for this long (the cleanup has finished) before the screen fades back in.</summary>
    private const float SettleBeforeFadeInSeconds = 0.5f;

    private static readonly Type HomingBomb = HarmonyLib.AccessTools.TypeByName("HomingAttackSummonObject");

    private static readonly Random Dice = new();
    private static readonly Dictionary<Kind, int> Pending = new();
    private static readonly HashSet<Kind> Failed = new();
    private static readonly Queue<string> Todo = new(); // the source scenes still to load (one scene may serve several traps)

    private enum Phase { Idle, FadingOut, Loading, Unloading, Settling, FadingIn }

    private static Phase _phase;
    private static GameObject _holder;
    private static AsyncOperation _operation;
    private static string _currentScene;
    private static float _phaseStarted;
    private static float _groundedSince = float.MaxValue;
    private static bool _setupHinted;
    private static bool _wasMuted;
    private static UnityEngine.EventSystems.EventSystem _blockedInput;

    /// <summary>A trap item was received; it goes off as soon as the player can deal with it.</summary>
    internal static void Queue(string game)
    {
        var kind = Array.Find(Kinds, k => k.Item == game);
        if (kind == null)
        {
            Plugin.Log.LogWarning($"Unknown trap {game}");
            return;
        }

        Pending[kind] = Pending.GetValueOrDefault(kind) + 1;
        Plugin.Log.LogInfo($"Trap queued: {game} ({Pending[kind]} waiting)");
    }

    internal static void Tick(float time)
    {
        SetupTick(time);
        if (_phase == Phase.Idle) TrapTick(time);
    }

    // ---------------------------------------------------------------- the one-time setup

    private static bool IsCached(Kind kind)
    {
        if (_holder == null) return false;
        foreach (var (template, _) in kind.Enemies)
            if (_holder.transform.Find(template) == null) return false;
        return true;
    }

    /// <summary>
    /// True while the setup is running, and in the moment between connecting at the title screen and the setup starting: no save may
    /// be loaded and no game started then (the screen is about to go black, and a scene change in the middle of the loads would
    /// break them). See <see cref="Patches.SetupLoadBlockPatch"/>.
    /// </summary>
    internal static bool BlocksLoading =>
        _phase != Phase.Idle || (SaveBinding.AtTitle && ApClient.IsConnected && Missing().Any());

    /// <summary>The traps of this seed whose enemies have not been copied yet.</summary>
    private static IEnumerable<Kind> Missing() =>
        Array.FindAll(Kinds, kind => !Failed.Contains(kind) && !IsCached(kind) && kind.InSeed(ApClient.Slot) > 0);

    /// <summary>The source scenes the missing enemies are in, each once.</summary>
    private static void CollectMissing()
    {
        foreach (var kind in Missing())
            if (!Todo.Contains(kind.Scene)) Todo.Enqueue(kind.Scene);
    }

    private static void SetupTick(float time)
    {
        switch (_phase)
        {
            case Phase.Idle:
                if (!SaveBinding.AtTitle || !ApClient.IsConnected || !SceneClock.Settled(time, TitleSettleSeconds)) return;
                CollectMissing();
                if (Todo.Count == 0) return;

                Plugin.Log.LogInfo($"Trap setup starts ({Todo.Count} source scene(s))");
                ConnectionPanel.Open = false;
                SetupOverlay.Show(SetupMessage);
                BlockInput(true);
                _phase = Phase.FadingOut;
                break;

            case Phase.FadingOut:
                if (!SetupOverlay.Opaque) return; // the screen has to be black before anything is loaded
                SetMuted(true);
                LoadNext(time);
                break;

            case Phase.Loading:
                if (_operation.isDone) CopyAndUnload(time);
                else if (time - _phaseStarted > LoadTimeoutSeconds) GiveUpOnLoad(time);
                break;

            case Phase.Unloading:
                if (_operation == null || _operation.isDone) LoadNext(time);
                break;

            case Phase.Settling:
                // the cleanup of the unloaded scenes freezes the game for a moment; a fade that starts inside that freeze is over
                // in its first frame, so it only starts once the cleanup is done and the frames are smooth again
                if (_operation != null && !_operation.isDone)
                {
                    _phaseStarted = time;
                    return;
                }

                if (time - _phaseStarted < SettleBeforeFadeInSeconds) return;
                SetupOverlay.Hide();
                _operation = null;
                _phase = Phase.FadingIn;
                break;

            case Phase.FadingIn:
                if (SetupOverlay.Visible) return;
                BlockInput(false);
                _phase = Phase.Idle;
                break;
        }
    }

    /// <summary>Loads the next source scene, or ends the setup when there is none left.</summary>
    private static void LoadNext(float time)
    {
        if (Todo.Count == 0)
        {
            Finish(time);
            return;
        }

        _currentScene = Todo.Dequeue();
        _operation = SceneManager.LoadSceneAsync(_currentScene, LoadSceneMode.Additive);
        _phaseStarted = time;
        if (_operation == null)
        {
            FailScene(_currentScene, "the scene with the enemies could not be loaded");
            LoadNext(time);
            return;
        }

        Plugin.Log.LogInfo($"Trap setup: loading {_currentScene}");
        _phase = Phase.Loading;
    }

    private static void CopyAndUnload(float time)
    {
        var sceneName = _currentScene;
        try
        {
            if (_holder == null)
            {
                _holder = new GameObject("HHAP_TrapCache");
                UnityEngine.Object.DontDestroyOnLoad(_holder);
                _holder.SetActive(false);
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            foreach (var kind in Array.FindAll(Kinds, k => k.Scene == sceneName && !Failed.Contains(k)))
            {
                var found = 0;
                foreach (var template in Templates(kind, scene))
                {
                    if (_holder.transform.Find(template.name) != null) continue;
                    var copy = UnityEngine.Object.Instantiate(template, _holder.transform);
                    copy.name = template.name;
                    found++;
                }

                if (!IsCached(kind)) Fail(kind, $"not every enemy was found in {sceneName}");
                else Plugin.Log.LogInfo($"Enemies of {kind.Item} are ready ({found} copied)");
            }
        }
        catch (Exception e)
        {
            FailScene(sceneName, e.Message);
        }

        UnloadCurrent();
    }

    private static void GiveUpOnLoad(float time)
    {
        FailScene(_currentScene, $"{_currentScene} was not loaded after {LoadTimeoutSeconds:0} seconds");
        UnloadCurrent();
    }

    // one source scene at a time, and each is gone again before the next is loaded (small PCs)
    private static void UnloadCurrent()
    {
        _operation = SceneManager.GetSceneByName(_currentScene).isLoaded ? SceneManager.UnloadSceneAsync(_currentScene) : null;
        _phase = Phase.Unloading;
    }

    private static void Finish(float time)
    {
        _operation = Resources.UnloadUnusedAssets(); // what the source scenes used is freed now rather than at some later load
        _phaseStarted = time;
        SetMuted(false); // the sound comes back while the screen is still black
        Plugin.Log.LogInfo("Trap setup done");
        _phase = Phase.Settling;
    }

    /// <summary>The whole game's sound (the master bus), put back to what it was when the setup is over.</summary>
    private static void SetMuted(bool muted)
    {
        try
        {
            var master = FMODUnity.RuntimeManager.GetBus("bus:/");
            if (muted) master.getMute(out _wasMuted);
            master.setMute(muted || _wasMuted);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not change the sound during the trap setup: " + e.Message);
        }
    }

    /// <summary>No clicks or keys reach the title menu behind the cover (a save could be started in the middle of the setup).</summary>
    private static void BlockInput(bool blocked)
    {
        try
        {
            if (blocked)
            {
                _blockedInput = UnityEngine.EventSystems.EventSystem.current;
                if (_blockedInput != null) _blockedInput.enabled = false;
            }
            else if (_blockedInput != null)
            {
                _blockedInput.enabled = true;
                _blockedInput = null;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not block the menu during the trap setup: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- the traps

    private static void TrapTick(float time)
    {
        if (!ApClient.CanReceive || !PlayerApi.InGame || !SceneClock.Settled(time, GameSettleSeconds)) return;

        // the hub (Mihare Shrine) is no place for them: the enemies behaved badly there, so the traps wait for a level
        if (!TrapPlacement.IsLevel(SceneManager.GetActiveScene().name))
        {
            _groundedSince = float.MaxValue;
            return;
        }

        _groundedSince = PlayerApi.IsOnSolidGround() ? Math.Min(_groundedSince, time) : float.MaxValue;

        foreach (var kind in Kinds)
        {
            var waiting = Pending.GetValueOrDefault(kind);
            if (waiting == 0 || Failed.Contains(kind)) continue;

            if (!IsCached(kind))
            {
                if (!_setupHinted)
                {
                    _setupHinted = true;
                    Toasts.Show("Traps need a one-time setup: go back to the title screen while connected", Toasts.Info);
                    Plugin.Log.LogInfo("A trap is waiting for the setup, which runs at the title screen");
                }

                continue;
            }

            var grounded = !kind.NeedsGround || time - _groundedSince >= LandedSeconds;
            if (grounded && PlayerApi.IsPlayerAlive() && PlayerApi.HasControl() && PlayerApi.Position is { } at)
            {
                Spawn(kind, at);
                Pending[kind] = waiting - 1;
                return; // one trap per tick
            }
        }
    }

    /// <summary>One object of each template name from a scene (they are switched off in it, so inactive objects are searched).</summary>
    private static IEnumerable<GameObject> Templates(Kind kind, Scene scene)
    {
        var type = HarmonyLib.AccessTools.TypeByName(kind.ComponentType);
        var wanted = new HashSet<string>(Array.ConvertAll(kind.Enemies, e => e.Template));
        foreach (var candidate in Resources.FindObjectsOfTypeAll(type))
        {
            if (candidate is not Component component || component.gameObject.scene != scene) continue;
            if (wanted.Remove(component.gameObject.name)) yield return component.gameObject;
        }
    }

    private static void Fail(Kind kind, string reason)
    {
        Failed.Add(kind);
        Plugin.Log.LogError($"{kind.Item} cannot run: {reason}");
        Toasts.Show("This trap is not available in this game build", Toasts.Error);
    }

    /// <summary>Every trap that needs the scene fails with it.</summary>
    private static void FailScene(string scene, string reason)
    {
        foreach (var kind in Array.FindAll(Kinds, k => k.Scene == scene && !Failed.Contains(k))) Fail(kind, reason);
    }

    // ---------------------------------------------------------------- the trap

    private static void Spawn(Kind kind, Vector3 around)
    {
        var scene = SceneManager.GetActiveScene();
        var spawned = 0;

        // a group of ground enemies stands together; flying ones are placed one by one
        var group = kind.Flying ? null : TrapPlacement.Group(Dice, TotalEnemies(kind));
        foreach (var (template, count) in kind.Enemies)
        {
            var source = _holder.transform.Find(template);
            for (var i = 0; i < count; i++)
            {
                var enemy = UnityEngine.Object.Instantiate(source.gameObject);
                enemy.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(enemy, scene);
                enemy.name = "Trap_" + template;
                enemy.transform.position = kind.Flying ? AirSpot(around) : GroundSpot(around, group[spawned]);
                Aim(enemy);
                enemy.SetActive(true);
                ShowHud(enemy);
                spawned++;
            }
        }

        Plugin.Log.LogInfo($"{kind.Item}: {spawned} enemies around the player");
    }

    private static int TotalEnemies(Kind kind)
    {
        var total = 0;
        foreach (var (_, count) in kind.Enemies) total += count;
        return total;
    }

    /// <summary>
    /// An enemy's health and stagger bars are a child object named EnemyHUD (a canvas with the bars and its controller, which hooks
    /// into the enemy's damage events when the enemy wakes up and fades the bars in on the first hit). In the scenes the enemy is
    /// waiting with that child switched off, and it was copied that way: the controller was set up but its bars were never on screen.
    /// It is switched on once the copy is awake.
    /// </summary>
    private static void ShowHud(GameObject enemy)
    {
        var hud = enemy.transform.Find("EnemyHUD");
        if (hud != null && !hud.gameObject.activeSelf) hud.gameObject.SetActive(true);
    }

    /// <summary>
    /// The bomb puffers move only when they have a target (<c>HomingAttackSummonObject.Update</c> does nothing without one): in the
    /// levels something hands them the player when they are summoned, and a copy has none, so it hung in the air for ever. They
    /// are aimed at the player here, and told to fly straight at them (<c>isTrack</c>; otherwise, from more than 10 m away, they
    /// steer by a ray-sensor result that nothing fills in for a copy). The spiky puffers find the player themselves.
    /// </summary>
    private static void Aim(GameObject enemy)
    {
        if (HomingBomb == null || PlayerApi.PlayerTransform is not { } player || enemy.GetComponent(HomingBomb) is not { } bomb) return;

        var fields = HarmonyLib.Traverse.Create(bomb);
        fields.Field("target").SetValue(player);
        fields.Field("isTrack").SetValue(true);
    }

    /// <summary>A free point in the air at a random distance of 6 to 20 m; after a few tries the last point is used anyway.</summary>
    private static Vector3 AirSpot(Vector3 around)
    {
        var point = around;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var (x, y, z) = TrapPlacement.Offset(Dice);
            point = around + new Vector3(x, y, z);
            if (!Physics.CheckSphere(point, 0.8f, ~0, QueryTriggerInteraction.Ignore)) return point;
        }

        return point;
    }

    /// <summary>The floor below (or just above) a point beside the player; the player's own height when there is none.</summary>
    private static Vector3 GroundSpot(Vector3 around, (float X, float Z) offset)
    {
        var point = new Vector3(around.x + offset.X, around.y, around.z + offset.Z);
        if (Physics.Raycast(point + Vector3.up * 6f, Vector3.down, out var hit, 14f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.1f;
        return point;
    }
}
