using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HomuraHimeAP.Patches;

/// <summary>
/// The shotgun's and Ann's Board's bullets did not break the large boxes with Konpeito (the small crates broke). A bullet's damage
/// is done by <c>ColliderHitDamage.OnTriggerEnter</c>, and Unity only raises a trigger event when one of the two colliders is a
/// trigger. The crates' damage colliders are triggers; the Konpeito boxes' are solid, and so are the damage boxes of these two
/// bullets (the standard bullet gets around it with its own swept hit check, the missile's box is a trigger). Solid against solid
/// raises nothing, so the bullets flew through the big boxes. Their damage boxes are made triggers when the bullet is shot (the
/// bullets are pooled, so it is done once per pooled bullet). Checked in the game: the shotgun's hit box laid over a large
/// Konpeito box did nothing while solid, and broke the box as soon as it was a trigger.
/// </summary>
[HarmonyPatch]
internal static class CutGunBreakBoxPatch
{
    private static readonly Type HitDamage = AccessTools.TypeByName("ColliderHitDamage");
    private const string BreakableTag = "CanDamageObject"; // the tag of the boxes, crates and pots

    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HonoHimeShotGun"), "ShotBullet");

    private static bool Prepare() => TargetMethod() != null && HitDamage != null;

    private static void Postfix(object __instance, GameObject __result)
    {
        if (__result == null || __instance.GetType().Name is not ("ShotGun" or "AnnGun")) return;

        try
        {
            MakeDamageBoxesTriggers(__result);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not make the bullet's damage boxes triggers: " + e.Message);
        }
    }

    /// <summary>Every damage box of the bullet that is meant to hurt breakable objects and is solid becomes a trigger.</summary>
    private static void MakeDamageBoxesTriggers(GameObject bullet)
    {
        foreach (var hit in bullet.GetComponentsInChildren(HitDamage, true))
        {
            if (!HurtsBreakables(hit)) continue;
            foreach (var collider in hit.GetComponents<Collider>())
                if (!collider.isTrigger) collider.isTrigger = true;
        }
    }

    private static bool HurtsBreakables(Component hit)
    {
        var info = Traverse.Create(hit).Field("damageInfo").GetValue();
        return info != null && info.GetType().GetMethod("GetCanDamageTag")?.Invoke(info, null) is List<string> tags && tags.Contains(BreakableTag);
    }
}
