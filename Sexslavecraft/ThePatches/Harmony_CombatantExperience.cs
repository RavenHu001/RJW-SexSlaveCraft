using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) })]
    public static class Harmony_CombatantDeath
    {
        public static void Prefix(Pawn __instance, DamageInfo? __0, out CombatantKillUtility.DeathEvent __state)
            => __state = CombatantKillUtility.BeginDeath(__instance, __0);

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(CombatantKillUtility.DeathEvent __state)
        {
            try { __state?.Complete(); }
            catch (Exception error)
            {
                Log.ErrorOnce("[SSC Combatant] Could not apply kill experience: " + error, 194706022);
            }
        }

        // Harmony finalizer 在正常及异常退出时均运行；不吞掉原死亡流程的异常。
        public static void Finalizer(CombatantKillUtility.DeathEvent __state) => __state?.Dispose();
    }

    [HarmonyPatch(typeof(Fire), "DoFireDamage")]
    public static class Harmony_CombatantFireDamage
    {
        public static void Prefix(out IDisposable __state) => __state = CombatantKillUtility.BeginIndirectDamage();
        public static void Finalizer(IDisposable __state) => __state?.Dispose();
    }

    [HarmonyPatch(typeof(Explosion), "AffectCell")]
    public static class Harmony_CombatantExplosionDamage
    {
        public static void Prefix(Explosion __instance, out IDisposable __state)
        {
            // 原版射弹爆炸保留 projectile；武器来源保留 weapon。油桶、弹药堆及
            // 爆炸组件的连锁引爆没有这两项，可能仍保留原攻击者，不能据此发奖。
            // 在每个实际伤害格结算时检查，覆盖跨 tick 传播和读档后的爆炸。
            __state = __instance.projectile == null && __instance.weapon == null
                ? CombatantKillUtility.BeginIndirectDamage() : null;
        }
        public static void Finalizer(IDisposable __state) => __state?.Dispose();
    }
}
