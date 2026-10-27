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
        /// <summary>健康添加完成后只转发相关事件，普通伤病不触发标记重评估。</summary>
        public static void Postfix(Hediff hediff) => SSCAbilityToggleHealthRemoved.Notify(hediff);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class SSCAbilityToggleHealthRemoved
    {
        /// <summary>原版 RemoveHediff 完成后协调已离体的 marker／空壳实例。</summary>
        public static void Postfix(Hediff hediff) => Notify(hediff);
        /// <summary>仅处理已注册／通用标记及空壳；身体开关直接查询原载体，不因此新增全图或逐帧同步。</summary>
        internal static void Notify(Hediff hediff)
        {
#if SSC_RW17
            // 外部直接进入空壳也代表旧目标人格失效；重复健康通知不重新签发或反复旋转凭据。
            if (hediff != null && hediff.def == SSCDefOf.SSC_PersonalityExcreted_Done)
                SSCRomanceLifecycle.NotifyHollowChanged(hediff.pawn);
#endif
            if (hediff != null && (SSCAbilityToggleUtility.IsMarkerDef(hediff.def) ||
                hediff.def == SSCDefOf.SSC_PersonalityExcreted_Done))
                SSCAbilityToggleUtility.SyncMarkers(hediff.pawn);
        }
    }

    // 死亡/复活后按最终身体状态协调；失败复活不重建，保存偏好始终保留。
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class SSCAbilityToggleDeath
    {
        /// <summary>实际死亡后协调人格派生标记，保留原偏好与身体／关系保存数据。</summary>
        public static void Postfix(Pawn __instance)
        {
            if (__instance.Dead) SSCAbilityToggleUtility.SyncMarkers(__instance);
        }
    }

    [HarmonyPatch(typeof(ResurrectionUtility), nameof(ResurrectionUtility.TryResurrect))]
    internal static class SSCAbilityToggleResurrection
    {
        /// <summary>仅成功复活触发最终状态协调，失败结果不伪造恢复。</summary>
        public static void Postfix(Pawn pawn, bool __result)
        {
            if (__result) SSCAbilityToggleUtility.SyncMarkers(pawn);
        }
    }
}
