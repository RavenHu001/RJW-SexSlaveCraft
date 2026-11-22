// AI 辅助生成
using System;
using rjw;
using rjw.Modules.Interactions;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>子宫独立成长：只更新当前身体已有记录，完成认领由各场景原有出口负责。</summary>
    public static class SSCUterusDevelopmentGrowthUtility
    {
        public const float ExperienceFactor = 0.5f;
        public const float MaximumGain = 0.10f;

        /// <summary>统一换算评分与旧部位倍率；上限是进度百分点，不是当前进度的百分比。</summary>
        public static float CalculateGain(float score, float corruptionMultiplier, float chainMultiplier)
            => Math.Min(MaximumGain, TrainingExpUtility.CalculateExperienceBudget(score, ExperienceFactor,
                corruptionMultiplier, chainMultiplier));

        /// <summary>一次完成的局部计划；保留捕获时的身体记录与该场景的成长资格。</summary>
        internal sealed class Plan
        {
            private readonly Pawn target;
            private readonly Hediff_SSCUterusDevelopment record;
            private readonly Func<bool> canGrow;
            private readonly float gain;

            /// <summary>只持有捕获时已存在的记录，后来新做手术或更换记录不能继承本场奖励。</summary>
            internal Plan(Pawn target, Hediff_SSCUterusDevelopment record,
                float gain, Func<bool> canGrow)
            {
                this.target = target; this.record = record; this.gain = gain; this.canGrow = canGrow;
            }

            /// <summary>在原完成认领内执行一次，并复查当前身体、研究和本场资格。</summary>
            internal void Apply(Func<bool> isCurrent)
            {
                if (Scribe.mode != LoadSaveMode.Inactive || isCurrent?.Invoke() != true ||
                    target == null || target.Dead || target.Destroyed || target.Discarded ||
                    SSCUterusDevelopmentUtility.Find(target) != record || record.IsComplete ||
                    !HasBodyPartTraining() || !canGrow()) return;
                // 已满部分不补到其他部位，也不创建记录；Severity 原生 setter 负责阶段与健康缓存通知。
                float actual = Math.Min(gain, 1f - record.Severity);
                if (actual > 0f && isCurrent()) record.Severity += actual;
            }
        }

        /// <summary>日常与完整仪式复用阴道开发的动作分类和当前身体部位判断。</summary>
        internal static Plan CaptureTraining(Pawn target, xxx.rjwSextype sexType, float score)
            => Capture(target, score, () => TrainingExpUtility.CanTrainGenitals(target, sexType));

        private static Plan Capture(Pawn target, float score, Func<bool> canGrow)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || target == null || target.Dead || target.Destroyed || target.Discarded)
                return null;
            var record = SSCUterusDevelopmentUtility.Find(target);
            if (record == null || record.IsComplete || !HasBodyPartTraining() || !canGrow()) return null;
            float gain = CalculateGain(score, TrainingExpUtility.GetCorruptionStageMultiplier(target),
                TrainingOutcomeUtility.GetChainStageMult(target));
            return gain > 0f ? new Plan(target, record, gain, canGrow) : null;
        }

        /// <summary>欢愉侵蚀使用已捕获的阴道开发候选与同一次评分。</summary>
        internal static Plan CaptureInfestation(Pawn target, SSCDesireInfestationBodyUtility.Plan bodyPlan,
            Func<float> score)
        {
            try
            {
                if (bodyPlan == null) return null;
                var genitalDef = SSCOrdinarySexPartsUtility.GetExperienceDef(LewdablePartFamily.Vagina);
                return Capture(target, score(), () => bodyPlan.HasAttachedPart(genitalDef));
            }
            catch (Exception ex)
            {
                Log.Warning("[SSC UterusGrowth] Cannot prepare optional development: " + ex.GetType().Name);
                return null;
            }
        }

        private static bool HasBodyPartTraining()
            => ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BodyPartTraining);
    }
}
