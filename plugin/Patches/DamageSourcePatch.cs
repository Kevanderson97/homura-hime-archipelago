using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Remembers who dealt the last damage to the player, so a DeathLink death can say what killed them. The game hands every hit that
/// goes through to <c>Player.OnDamageValid</c> in a <c>DamageInfo</c>, which names the attacker (<c>damageOwner</c>; for a bullet
/// the one who fired it) and the object that did the hitting (<c>Sender</c>).
/// </summary>
internal static class DamageSource
{
    /// <summary>A death counts the last hit as its cause when the hit was no longer ago than this (the death animation takes a few seconds).</summary>
    internal const float Window = 10f;

    /// <summary>The enemy that fired a laser, noted when the laser starts: a laser that is not part of an enemy's object (it is spawned
    /// at the root of the scene) says nothing of who fired it, its owner is itself.</summary>
    internal sealed class Shooter { internal string Raw, Codex; }

    internal static readonly ConditionalWeakTable<GameObject, Shooter> Shooters = new();

    private static string _raw, _readable;
    private static float _time = float.MinValue;

    /// <summary>The source of a hit: the name of its object, and the name the game gives it in its codex when it has one.</summary>
    internal static void Record(string rawName, string codexName, float time) => (_raw, _readable, _time) = (rawName, codexName, time);

    /// <summary>The readable name of the source of the last hit when it is recent enough, else null; the raw object name is logged.</summary>
    internal static string Killer(float now)
    {
        var killer = DeathCause.Killer(_raw, _time, now, Window);
        Plugin.Log.LogInfo($"DeathLink: the last hit came from \"{_raw}\" {now - _time:0.0} s ago; killer: {killer ?? "unknown"}");
        return killer == null ? null : _readable ?? DeathCause.NameOf(killer);
    }
}

[HarmonyPatch]
internal static class DamageSourcePatch
{
    private static readonly System.Type DamageInfoType = AccessTools.TypeByName("DamageInfo");
    internal static readonly System.Type EnemyType = AccessTools.TypeByName("HonoHime.CharacterSystem.Enemy");

    private static MethodBase TargetMethod() =>
        DamageInfoType == null ? null : AccessTools.Method(AccessTools.TypeByName("HonoHime.CharacterSystem.Player"), "OnDamageValid", new[] { DamageInfoType });

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(object __0)
    {
        if (!ApClient.Active || __0 == null) return;

        var traverse = Traverse.Create(__0);
        var owner = traverse.Field("damageOwner").GetValue<GameObject>() ?? traverse.Field("Sender").GetValue<GameObject>();
        if (owner == null) return;

        // the enemy is the object with the Enemy component; a bullet or a hit box is named after the enemy that owns it
        var enemy = EnemyType == null ? null : owner.GetComponentInParent(EnemyType);
        if (enemy != null) DamageSource.Record(enemy.gameObject.name, CodexName(enemy), Time.unscaledTime);
        else if (DamageSource.Shooters.TryGetValue(owner, out var shooter)) DamageSource.Record(shooter.Raw, shooter.Codex, Time.unscaledTime);
        else DamageSource.Record(owner.name, null, Time.unscaledTime);
    }

    private static readonly Dictionary<string, string> CodexNames = new();

    /// <summary>
    /// The name the game's codex gives an enemy: its data (<c>enemyDataHandler.enemyData</c>) names a codex item whose name is the
    /// entry of the HH_Codex string table ("Gumi", "Longwraith", "Spikepuffer"). Null for an enemy the codex does not list.
    /// </summary>
    internal static string CodexName(Component enemy)
    {
        var handler = Traverse.Create(enemy).Field("enemyDataHandler").GetValue();
        var data = handler == null ? null : Traverse.Create(handler).Field("enemyData").GetValue();
        var definition = data == null ? null : Traverse.Create(data).Property("codexItemDefinition").GetValue<UnityEngine.Object>();
        if (definition == null) return null;

        if (CodexNames.TryGetValue(definition.name, out var known)) return known;
        if (!LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone) return null;

        var table = LocalizationSettings.StringDatabase.GetTableAsync("HH_Codex").WaitForCompletion();
        var text = table?.GetEntry(definition.name)?.LocalizedValue;
        var name = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        CodexNames[definition.name] = name;
        Plugin.Log.LogInfo($"DeathLink: codex entry {definition.name} is called \"{name ?? "(no name in HH_Codex)"}\"");
        return name;
    }
}

// The hook below is switched off while the cause of a death is not used (the DeathLink messages are generic for now).
// It notes the shooter of a laser when the laser starts; kept for when the cause comes back.
//
// /// <summary>
// /// Notes who fires a laser when it starts. Enemy lasers that are spawned at the root of the scene (the "Laser_Normal" the game
// /// hands out) have no owner (<c>damageOwner</c> is null, so the damage names the laser itself); the shooter is the enemy standing
// /// next to the place the laser starts from. Lasers that belong to an enemy's own object, and the player's lasers (they have an
// /// owner), need nothing.
// /// </summary>
// [HarmonyPatch]
// internal static class LaserShooterPatch
// {
//     /// <summary>A laser starts at the shooter, who is at most this far from where the laser object is placed.</summary>
//     private const float MaxDistance = 25f;
//
//     private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("LaserController"), "Use");
//
//     private static bool Prepare() => TargetMethod() != null && DamageSourcePatch.EnemyType != null;
//
//     private static void Postfix(Component __instance)
//     {
//         if (!ApClient.Active) return;
//
//         var laser = __instance.gameObject;
//         if (Traverse.Create(__instance).Field("damageOwner").GetValue<GameObject>() != null) return; // the player's laser
//         if (laser.GetComponentInParent(DamageSourcePatch.EnemyType) != null) return; // named after its enemy anyway
//
//         var enemies = Object.FindObjectsByType(DamageSourcePatch.EnemyType, FindObjectsSortMode.None);
//         var squared = new List<float>(enemies.Length);
//         var candidates = new List<Component>(enemies.Length);
//         foreach (var candidate in enemies)
//         {
//             if (candidate is not Component enemy || !enemy.gameObject.activeInHierarchy) continue;
//             candidates.Add(enemy);
//             squared.Add((enemy.transform.position - laser.transform.position).sqrMagnitude);
//         }
//
//         var nearest = DeathCause.Nearest(squared, MaxDistance);
//         DamageSource.Shooters.Remove(laser);
//         if (nearest < 0) return;
//
//         var shooter = candidates[nearest];
//         DamageSource.Shooters.Add(laser, new DamageSource.Shooter { Raw = shooter.gameObject.name, Codex = DamageSourcePatch.CodexName(shooter) });
//     }
// }
