// AI 辅助生成
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // 只监听会改变开关标记或空壳资格的健康事件；普通伤病不触发全套同步。
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    internal static class SSCAbilityToggleHealthAdded
    {
        public static void Postfix(Hediff hediff) => SSCAbilityToggleHealthRemoved.Notify(hediff);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class SSCAbilityToggleHealthRemoved
    {
        public static void Postfix(Hediff hediff) => Notify(hediff);
        internal static void Notify(Hediff hediff)
        {
            if (hediff != null && (SSCAbilityToggleUtility.IsMarkerDef(hediff.def) ||
                hediff.def == SSCDefOf.SSC_PersonalityExcreted_Done))
                SSCAbilityToggleUtility.SyncMarkers(hediff.pawn);
        }
    }

    // 死亡/复活后按最终身体状态协调；失败复活不重建，保存偏好始终保留。
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class SSCAbilityToggleDeath
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance.Dead) SSCAbilityToggleUtility.SyncMarkers(__instance);
        }
    }

    [HarmonyPatch(typeof(ResurrectionUtility), nameof(ResurrectionUtility.TryResurrect))]
    internal static class SSCAbilityToggleResurrection
    {
        public static void Postfix(Pawn pawn, bool __result)
        {
            if (__result) SSCAbilityToggleUtility.SyncMarkers(pawn);
        }
    }
}
