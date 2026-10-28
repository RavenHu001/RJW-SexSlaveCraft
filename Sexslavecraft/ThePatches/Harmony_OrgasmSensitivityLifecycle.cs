// AI 辅助生成：局部健康通知，不改写原版属性值或第三方高潮处理。
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>兼容正常阶段通知及外部直接改健康列表后发送的通知。</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged))]
    internal static class Harmony_OrgasmSensitivityLifecycle
    {
        /// <summary>保存档位不变也可能升降阶或暂停；只刷新相关角色的健康／思想缓存。</summary>
        public static void Postfix(Hediff hediff)
        {
            if (hediff is Hediff_ChainOfSexSlave || hediff?.def?.defName == SSCOrgasmSensitivityUtility.DefName ||
                (hediff != null && hediff.def == SSCDefOf.SSC_PersonalityExcreted_Done))
                SSCOrgasmSensitivityUtility.NotifyStateChanged(hediff.pawn);
        }
    }
}
