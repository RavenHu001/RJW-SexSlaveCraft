// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>侵染独有的目标准入与恶堕分量；个人偏好和人格凭据仍由通用框架管理。</summary>
    public static class SSCDesireInfestationUtility
    {
        public static bool CanAffect(Pawn source, Pawn target)
        {
            // 来源必须实际可用；目标保留自己的身份、需求与身体，不创建需求或主从绑定。
            return source != target && SSCPnaExposureUtility.IsValidTarget(source) &&
                SSCAbilityToggleUtility.IsActive(source, SSCAbilityToggleUtility.MasterDesireInfestation) &&
                SSCPnaExposureUtility.IsValidTarget(target) && target.RaceProps.Humanlike &&
                target.TryGetComp<CompSexSlaveTraining>() != null && !SSCIdentityUtility.IsMaster(target) &&
                !SSCMasterProgressionUtility.IsHollow(target) && !SSCPersonalityRuntimeUtility.IsMutating(target) &&
                target.needs?.TryGetNeed<Need_Corruption>() != null;
        }

        public static void ApplyCorruption(Pawn source, Pawn target, Need_Corruption expectedNeed, Func<bool> isCurrent)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || !isCurrent() || !CanAffect(source, target) || expectedNeed == null ||
                target.needs?.TryGetNeed<Need_Corruption>() != expectedNeed) return;
            // 当前数值和阶段上限在写入前重读；超限只停止增加，不能在侵染入口倒扣原进度。
            float current = expectedNeed.CurLevel;
            float cap = SSCMod.settings?.useOldScoring == true ? expectedNeed.MaxLevel :
                Math.Min(expectedNeed.MaxLevel, TrainingOutcomeUtility.GetChainCap(target));
            if (!SSCPnaMath.IsFinite(current) || !SSCPnaMath.IsFinite(cap)) return;
            float gain = Math.Max(0f, Math.Min(.01f, cap - current));
            if (gain > 0f && isCurrent()) CorruptionUtility.AddCorruption(target, gain);
        }
    }
}
