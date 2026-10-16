// AI 辅助生成
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>CE 直接删除健康列表后只调用此通知；用 SSC 自身的离体维护补齐缺失的绑定清理。</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged))]
    internal static class SSCBondHealthChanged
    {
        public static void Postfix(Hediff hediff)
        {
            // 仅处理本模组性奴锁链；正常严重度通知由离体检查快速返回，不改动其他健康机制。
            // 不引用 CE 类型，因此 CE 未安装时仍使用原版 PostRemoved，且无需新增模组依赖。
            if (hediff is Hediff_ChainOfSexSlave chain) chain.ReconcileRemoval();
        }
    }
}
