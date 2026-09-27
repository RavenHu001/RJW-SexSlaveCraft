using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>事件层确认完成及一次性认领；此处统一计算并提交战斗员普通培养进度。</summary>
    public static class CombatantSpecializationProgressUtility
    {
        public const float TrainingScoreDivisor = 2000f;
        public const float MaxTrainingProgress = 0.04f;
        public const float BoundMasterMultiplier = 1.5f;
        public const float KillProgressPerBodySize = 0.01f;
        public const float MaxKillProgress = 0.03f;

        public static float TrainingProgress(float score, bool byBoundMaster)
        {
            if (!IsPositiveFinite(score)) return 0f;
            return Math.Min(score / TrainingScoreDivisor, MaxTrainingProgress)
                * (byBoundMaster ? BoundMasterMultiplier : 1f);
        }

        public static float KillProgress(float bodySize)
        {
            return IsPositiveFinite(bodySize) ? Math.Min(bodySize * KillProgressPerBodySize, MaxKillProgress) : 0f;
        }

        internal static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>不附加身份、研究、绑定、锁链或恶堕倍率；组件是唯一经验来源。</summary>
        public static float TryGainProgress(Pawn pawn, float amount)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || !IsPositiveFinite(amount)) return 0f;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.specializationType != SexSlaveSpecializationType.Combatant) return 0f;

            float before = CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress);
            comp.specializationProgress = before;
            if (before >= CompSexSlaveTraining.SpecializationCompletionProgress)
            {
                CombatantSpecializationUtility.Sync(pawn);
                return 0f;
            }

            float after = comp.AddSpecializationProgress(amount);
            CombatantSpecializationUtility.Sync(pawn);
            return after - before;
        }

        /// <summary>只供完整日常结算调用；非主人同样有基础收益，绑定关系仅决定额外倍率。</summary>
        public static float NotifyDailyTrainingCompleted(Pawn trainer, Pawn receiver, float score)
        {
            if (trainer == null || receiver == null || trainer == receiver || trainer.Dead || trainer.Destroyed) return 0f;
            bool byBoundMaster = SSCBondUtility.GetBoundMaster(receiver) == trainer;
            return TryGainProgress(receiver, TrainingProgress(score, byBoundMaster));
        }
    }
}
