using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The cut-content missile gun (Fortunate Misfortune) leaves its red targeting boxes on screen. MissileHUD spawns one box per
/// target and returns them to the pool only when the aim stops; when the aim is started again first (fire pressed again, a gun
/// switch, a cancelled charge) it forgets the boxes without returning them, so they are never updated or hidden again.
/// These two patches return the boxes before the list is forgotten and when the HUD is switched off.
/// </summary>
internal static class MissileHud
{
    internal static readonly System.Type Type = AccessTools.TypeByName("MissileHUD");

    /// <summary>Returns every box of the HUD to the pool and empties its list.</summary>
    internal static void DespawnAll(object hud)
    {
        var boxes = Traverse.Create(hud).Field("missileLockUIs").GetValue<IList>();
        var despawn = AccessTools.Method(AccessTools.TypeByName("SimplePool"), "Despawn", new[] { typeof(GameObject) });
        if (boxes == null || despawn == null) return;

        foreach (var box in boxes)
            if (box is Component component && component != null) despawn.Invoke(null, new object[] { component.gameObject });
        boxes.Clear();
    }
}

/// <summary>
/// A missile that never hits anything flies on for ever with its flame and trail effects. The gun gives every bullet a release
/// time (3 s), and the bullets that use it count it down from <c>Update</c> with <c>TickReleaseTimer</c>; the missile's own
/// <c>Update</c> only steers and moves it, so the timer stays where it was set. A missile chasing a target it cannot reach
/// (it circles the lock point) was seen still burning long after the shot. This counts the timer down, so the missile is
/// released (explodes and is destroyed) when its time is up, like the bullets that were finished.
/// </summary>
[HarmonyPatch]
internal static class MissileReleaseTimerPatch
{
    private static readonly System.Type Missile = AccessTools.TypeByName("HonoHimeBullet_Missile");
    private static readonly MethodInfo Tick = AccessTools.Method(AccessTools.TypeByName("HonoHimeBullet"), "TickReleaseTimer");

    private static MethodBase TargetMethod() => AccessTools.Method(Missile, "Update");

    private static bool Prepare() => TargetMethod() != null && Tick != null;

    private static void Postfix(object __instance) => Tick.Invoke(__instance, null);
}

/// <summary>
/// A missile that hits (or runs out of time) is released: its collider goes off and the hit explosion plays, but the missile
/// object itself stays for two more seconds (<c>Destroy(gameObject, 2f)</c>), and so do its flame, trail and launch particles,
/// hanging in the air where it hit. The flight effects are stopped and cleared at the release; the explosion (the hit feedback)
/// is a separate child and plays on.
/// </summary>
[HarmonyPatch]
internal static class MissileReleaseEffectsPatch
{
    // the in-flight flame and trails, and the burst of the launch
    private static readonly string[] FlightEffects = { "Model", "MSL_Burst" };

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHimeBullet_Missile"), "OnRelease");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(Component __instance)
    {
        try
        {
            foreach (var name in FlightEffects)
            {
                var effect = __instance.transform.Find(name);
                if (effect == null) continue;
                foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("Could not clear the missile's flight effects: " + e.Message);
        }
    }
}

/// <summary>
/// A missile steers to the lock point of its target and only hits what its small hit box (0.3 m) overlaps: the enemy's hurtbox
/// (the body is on a layer the bullets ignore). For most enemies the lock point is inside the hurtbox. The spiky puffer's hurtbox
/// is a box shifted 0.8 m upwards, its bottom face 0.21 m above the lock point, so a missile reached the lock point, stopped 6 cm
/// short of the hurtbox and circled there until its timer ran out, then exploded. When a missile has reached the lock point
/// without having hit, it hits the target's nearest hurtbox through the game's own hit code (damage, hit effect and release
/// as for any hit), and is released where it is when that did not release it.
/// </summary>
[HarmonyPatch]
internal static class MissileArrivalPatch
{
    private const float ArriveDistance = 0.3f;

    private static readonly System.Type Missile = AccessTools.TypeByName("HonoHimeBullet_Missile");
    private static readonly System.Type CharacterType = AccessTools.TypeByName("HonoHime.CharacterSystem.Character");
    private static readonly FieldInfo Released = AccessTools.Field(Missile, "isReleaseBullet");
    private static readonly FieldInfo Target = AccessTools.Field(Missile, "target");
    private static readonly FieldInfo HitDamage = AccessTools.Field(Missile, "colliderHitDamage");
    private static readonly MethodInfo TryHit = AccessTools.Method(AccessTools.TypeByName("ColliderHitDamage"), "TryHit");
    private static readonly MethodInfo Release = AccessTools.Method(AccessTools.TypeByName("HonoHimeBullet"), "ReleaseBullet");

    private static MethodBase TargetMethod() => AccessTools.Method(Missile, "Update");

    private static bool Prepare() =>
        TargetMethod() != null && CharacterType != null && Released != null && Target != null && HitDamage != null && TryHit != null && Release != null;

    private static void Postfix(Component __instance)
    {
        if (Released.GetValue(__instance) is true || Target.GetValue(__instance) is not Transform { } target || target == null) return;
        if ((target.position - __instance.transform.position).sqrMagnitude > ArriveDistance * ArriveDistance) return;

        try
        {
            if (NearestHurtBox(__instance.transform.position, target) is { } hurtBox && HitDamage.GetValue(__instance) is { } hitDamage)
                TryHit.Invoke(hitDamage, new object[] { hurtBox.gameObject });
            if (Released.GetValue(__instance) is not true) Release.Invoke(__instance, null);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("A missile that reached its target could not be made to hit: " + e.Message);
        }
    }

    /// <summary>The hurtbox of the enemy the lock point belongs to that is nearest to the point.</summary>
    private static Collider NearestHurtBox(Vector3 from, Transform lockPoint)
    {
        var owner = lockPoint.GetComponentInParent(CharacterType);
        if (owner == null) return null;

        var layer = LayerMask.NameToLayer("EnemyHurtBox");
        Collider best = null;
        var bestDistance = float.MaxValue;
        foreach (var collider in owner.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.gameObject.layer != layer || !collider.gameObject.CompareTag("HurtBox")) continue;
            var distance = (collider.ClosestPoint(from) - from).sqrMagnitude;
            if (distance < bestDistance) (best, bestDistance) = (collider, distance);
        }

        return best;
    }
}

/// <summary>
/// Inside a domain (the Shadowspecter's arena and the like) the game stops its own timer (<c>HHTimer.deltaTime</c> is 0, see
/// <c>SpecialArenaController.UpdateHandle</c>) and runs only the player on the unscaled clock. The missiles that circle the
/// player while the gun charges are moved with the game timer, so there they stayed where they were created and the missiles
/// were shot from that spot, wherever the player had gone. The same movement is done here with the player's own clock.
/// </summary>
[HarmonyPatch]
internal static class MissileReadyDomainTimePatch
{
    private static readonly System.Type Controller = AccessTools.TypeByName("MissileReadyBulletController");
    private static readonly PropertyInfo IsDomain = AccessTools.Property(AccessTools.TypeByName("SpecialArenaController"), "IsDomain");
    private static readonly System.Type PlayerType = AccessTools.TypeByName("HonoHime.CharacterSystem.Player");
    private static readonly FieldInfo PlayerInstance = AccessTools.Field(PlayerType, "ins");
    private static readonly PropertyInfo PlayerDelta = AccessTools.Property(PlayerType, "deltaTime");

    private static MethodBase TargetMethod() => AccessTools.Method(Controller, "Update");

    private static bool Prepare() => TargetMethod() != null && IsDomain != null && PlayerInstance != null && PlayerDelta != null;

    /// <summary>The seconds that passed for the player, only while a domain is on (null otherwise: the game's own code runs).</summary>
    private static float? DomainDelta()
    {
        if (IsDomain.GetValue(null) is not true) return null;
        var player = PlayerInstance.GetValue(null);
        return player == null ? null : (float?)PlayerDelta.GetValue(player);
    }

    private static bool Prefix(object __instance)
    {
        if (DomainDelta() is not { } delta) return true;

        // the game's Update, with the clock of the player
        var self = Traverse.Create(__instance);
        if (!self.Field("isUse").GetValue<bool>()) return false;

        var turnTimer = self.Field("turnTimer").GetValue<float>() + delta;
        self.Field("curTime").SetValue(self.Field("curTime").GetValue<float>() + delta);
        self.Field("turnTimer").SetValue(turnTimer);
        var turnAmount = turnTimer * self.Field("turnSpeed").GetValue<float>();
        self.Field("turnAmount").SetValue(turnAmount);

        var center = self.Field("center").GetValue<Transform>();
        var bullets = self.Property("curMissileReadyBullets").GetValue<IList>();
        if (center == null || bullets == null) return false;

        var distance = self.Field("dis").GetValue<float>();
        var smooth = self.Field("smoothSpeed").GetValue<float>();
        for (var i = 0; i < bullets.Count; i++)
        {
            if (bullets[i] is not Component bullet || bullet == null) continue;
            var angle = 360 / bullets.Count * i + turnAmount;
            var target = center.position + Quaternion.AngleAxis(angle, center.forward) * center.up * distance;
            bullet.transform.position = Vector3.Lerp(bullet.transform.position, target, delta * smooth);
        }

        return false;
    }
}

/// <summary>
/// The aim assist keeps the lock points it found while fire was held, also when the enemy behind one is destroyed meanwhile (a
/// monster that was not spawned yet, a cutscene, a boost ring). Releasing the shot reads <c>.transform</c> of the first targets;
/// a destroyed one throws on every frame, so the gun never releases, never lifts its red boxes and never finishes: charging
/// again only works after switching guns. Destroyed targets are dropped before the gun looks at them.
/// </summary>
[HarmonyPatch]
internal static class MissileDeadTargetPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("MissileGun"), "OnUpdate");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __instance)
    {
        var targets = Traverse.Create(__instance).Field("missileAssistAim").Field("curScanTargets").GetValue();
        if (targets is not IEnumerable all) return;

        List<object> destroyed = null;
        foreach (var target in all)
            if (target is Object unityObject && unityObject == null) (destroyed ??= new List<object>()).Add(target);
        if (destroyed == null) return;

        var remove = targets.GetType().GetMethod("Remove");
        foreach (var target in destroyed) remove.Invoke(targets, new[] { target });
        Plugin.Log.LogInfo($"Dropped {destroyed.Count} destroyed missile target(s) before the shot");
    }
}

/// <summary>
/// Charging the missile gun (fire held) spawns a missile per target that circle the player until the shot is released. Switching
/// to another weapon in that time stops the gun (<c>StopFire</c> and <c>Finish</c>) before it released them; its Update then
/// no longer runs, so the missiles stay frozen where they were and the aim stays on. The cleanup is harmless when the gun has
/// already released them (the end of a shot calls <c>Finish</c> after that), and it also frees a gun that was stuck releasing
/// (see <see cref="MissileDeadTargetPatch"/>).
/// </summary>
[HarmonyPatch]
internal static class MissileChargeCancelPatch
{
    private static readonly System.Type Gun = AccessTools.TypeByName("MissileGun");

    // MissileGun does not declare Finish: this is the base method of every gun (HonoHimeShotGun.Finish), so the patch sees
    // all of them and must leave the others alone. (For the other guns it threw on the missile-only fields, which aborted
    // Finish and left the gun marked as firing for ever: it could not be started again.)
    private static MethodBase TargetMethod() => AccessTools.Method(Gun, "Finish");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __instance)
    {
        if (!Gun.IsInstanceOfType(__instance)) return;
        try
        {
            CancelCharge(__instance);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("Could not clean up the missile charge: " + e.Message); // never let this stop the gun from finishing
        }
    }

    private static void CancelCharge(object __instance)
    {
        var gun = Traverse.Create(__instance);
        if (gun.Field("state").GetValue()?.ToString() == "None") return;

        gun.Field("MissileReadyBulletController").Method("Release").GetValue();
        gun.Field("missileAssistAim").Method("Stop").GetValue();
        gun.Field("state").SetValue(System.Enum.ToObject(gun.Field("state").GetValue().GetType(), 0)); // None
    }
}

[HarmonyPatch]
internal static class MissileAimStartPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(MissileHud.Type, "MissileAssistAim_OnUseEvent");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __instance) => MissileHud.DespawnAll(__instance);
}

/// <summary>
/// The HUD returns the boxes when the aim stops but keeps them in its list, so a second stop (a shot released after a cancelled
/// charge) would return them again and the pool would hand the same box out twice. They are returned and forgotten here; the
/// game's own loop then has nothing left to do.
/// </summary>
[HarmonyPatch]
internal static class MissileAimStopPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(MissileHud.Type, "MissileAssistAim_OnStopEvent");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __instance) => MissileHud.DespawnAll(__instance);
}

[HarmonyPatch]
internal static class MissileHudDisabledPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(MissileHud.Type, "OnDisable");

    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object __instance) => MissileHud.DespawnAll(__instance);
}
