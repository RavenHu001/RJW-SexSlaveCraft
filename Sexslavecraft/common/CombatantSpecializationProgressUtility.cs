using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保留战斗员击杀经验，并提供目前供全部方向复用的受训评分公式。</summary>
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

        /// <summary>仅 SSC 性奴可获得战斗员经验；不附加研究、绑定、锁链或恶堕倍率。</summary>
        public static float TryGainProgress(Pawn pawn, float amount)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || !IsPositiveFinite(amount)) return 0f;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !SSCIdentityUtility.IsSexSlave(pawn) ||
                comp.specializationType != SexSlaveSpecializationType.Combatant ||
                CombatantSpecializationUtility.HasFinalState(pawn)) return 0f;

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

    }
}
