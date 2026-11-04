using System.Reflection;
using FMODUnity;
using HarmonyLib;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Sounds for the cut-content guns, which the game never finished: the missile's charge, launch and explosion, the shotgun's shot
/// and the board's wind (a placeholder sample). They are the game's own sounds (see <see cref="GameSamples"/>).
/// </summary>
internal static class CutGunSounds
{
    // pieces of the sample names in the bank (some names are stored in a mangled encoding, so only the ASCII part is matched)
    /// <summary>The samples are the game's raw sound files and much louder than its sound events, which are mixed: 50 % sounds like the other guns.</summary>
    internal const float SampleVolume = 0.5f;
    internal const string Shotgun = "audiostock_226672";

    /// <summary>The missile's charge uses the shotgun sample, quietly: it plays for every orb that appears.</summary>
    internal const float MissileChargeVolume = 0.2f;
    internal const string Board = "SND122929"; // placeholder: a laser sample, a real wind sound is still wanted

    internal const string NormalShot = "event:/SFX/Player/Shot/Basebullet_Emit";
    internal const string MissileExplosion = "event:/SFX/Player/Skill/DoubleCharm/DoubleCharm_Explosion";

    /// <summary>
    /// Opens the sound bank and finds the gun samples now: the first time one was played, the lookup froze the game for a moment
    /// (the missile gun's first charge). The lookup reads the name table in the bank's header now (see <see cref="Fsb5Names"/>),
    /// which takes milliseconds, so all three are prepared together.
    /// </summary>
    internal static void PreloadSamples()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        GameSamples.Preload(Shotgun, Board);
        Plugin.Log.LogInfo($"Gun sound samples prepared in {timer.ElapsedMilliseconds} ms");
    }

    /// <summary>Loads the sample data of the sound events the guns play, so the first shot or explosion does not load it.</summary>
    internal static void PreloadEvents()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        foreach (var path in new[] { NormalShot, MissileExplosion })
        {
            try { RuntimeManager.GetEventDescription(path).loadSampleData(); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"Game sound event {path} could not be loaded ahead: {e.Message}"); }
        }

        Plugin.Log.LogInfo($"Gun sound events prepared in {timer.ElapsedMilliseconds} ms");
    }

    internal static void PlayEvent(string path)
    {
        try { RuntimeManager.PlayOneShot(path); }
        catch (System.Exception e) { Plugin.Log.LogWarning($"Game sound event {path} failed: {e.Message}"); }
    }
}

/// <summary>Every bullet a gun shoots goes through HonoHimeShotGun.ShotBullet.</summary>
[HarmonyPatch]
internal static class CutGunShotPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHimeShotGun"), "ShotBullet");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(object __instance)
    {
        switch (__instance.GetType().Name)
        {
            case "ShotGun": GameSamples.Play(CutGunSounds.Shotgun, volume: CutGunSounds.SampleVolume); break;
            // (the board is not here: its shot only summons the board, the attack comes when the button is released, see BoardAttackPatch)
            case "MissileGun": CutGunSounds.PlayEvent(CutGunSounds.NormalShot); break;
        }
    }
}

/// <summary>
/// Holding fire charges the missile gun: it readies one missile per target found (the first at once, then one every 0.2 s), and
/// every one of them plays the short charge sound (a missile charges in about 0.3 s).
/// </summary>
[HarmonyPatch]
internal static class MissileChargePatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("MissileReadyBulletController"), "Add", System.Type.EmptyTypes);

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix() =>
        GameSamples.Play(CutGunSounds.Shotgun, volume: CutGunSounds.MissileChargeVolume);
}

/// <summary>
/// Ann's Board is a two-part attack: holding fire summons the board (<c>AnnGun.Use</c>, which shoots the bullet and waits in the
/// "Ready" state), and releasing fire makes it attack (<c>AnnGun.OnUpdate</c> calls <c>HonoHimeBullet_Ann.UseAttack</c>). The
/// sound belongs to the attack.
/// </summary>
[HarmonyPatch]
internal static class BoardAttackPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHimeBullet_Ann"), "UseAttack");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix() => GameSamples.Play(CutGunSounds.Board, volume: CutGunSounds.SampleVolume);
}

/// <summary>A missile that hits explodes.</summary>
[HarmonyPatch]
internal static class MissileHitPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHimeBullet_Missile"), "OnHit");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix() => CutGunSounds.PlayEvent(CutGunSounds.MissileExplosion);
}

/// <summary>
/// Prepares the gun sounds ahead of play so nothing stalls the first time a gun is used: the samples once the game has been
/// quiet for a moment (the title screen, normally), the sound events once a save is loaded and settled (their banks may not be
/// ready before that).
/// </summary>
internal static class GunSoundPreload
{
    private const float QuietSeconds = 2f;

    private static bool _samplesDone, _eventsDone;

    internal static void Tick(float now)
    {
        if (!SceneClock.Settled(now, QuietSeconds)) return;

        if (!_samplesDone)
        {
            _samplesDone = true;
            CutGunSounds.PreloadSamples();
        }
        else if (!_eventsDone && PlayerApi.InGame)
        {
            _eventsDone = true;
            CutGunSounds.PreloadEvents();
        }
    }
}
