using System.Reflection;
using FMODUnity;
using HarmonyLib;
using HomuraHimeAP.Core;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Sounds for the cut-content guns, which the game never finished: the missile's charge, launch and explosion, the shotgun's shot
/// and the board's wind (a placeholder sample). They are the game's own sounds (see <see cref="GameSamples"/>).
/// </summary>
internal static class CutGunSounds
{
    // pieces of the sample names in the bank (some names are stored in a mangled encoding, so only the ASCII part is matched)
    internal const string MissileCharge = "HH_SE_ShinkanP1_VLaser"; // the first 0.43 s
    internal const string Shotgun = "audiostock_226672";
    internal const string Board = "SND122929"; // placeholder: a laser sample, a real wind sound is still wanted

    internal const string NormalShot = "event:/SFX/Player/Shot/Basebullet_Emit";
    internal const string MissileExplosion = "event:/SFX/Player/Skill/DoubleCharm/DoubleCharm_Explosion";

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
            case "ShotGun": GameSamples.Play(CutGunSounds.Shotgun); break;
            case "AnnGun": GameSamples.Play(CutGunSounds.Board); break;
            case "MissileGun": CutGunSounds.PlayEvent(CutGunSounds.NormalShot); break;
        }
    }
}

/// <summary>Holding fire charges the missile gun: its charge sound plays once the charge starts.</summary>
[HarmonyPatch]
internal static class MissileChargePatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("MissileGun"), "Fire");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix() => GameSamples.Play(CutGunSounds.MissileCharge, 0, 430);
}

/// <summary>A missile that hits explodes.</summary>
[HarmonyPatch]
internal static class MissileHitPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHimeBullet_Missile"), "OnHit");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix() => CutGunSounds.PlayEvent(CutGunSounds.MissileExplosion);
}
