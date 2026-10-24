// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using rjw;
using rjw.Modules.Interactions;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>胃部独立成长：只更新当前身体已有记录，完成认领由各场景原有出口负责。</summary>
    public static class SSCStomachDevelopmentGrowthUtility
    {
        public const float ExperienceFactor = 0.5f;
        public const float MaximumGain = 0.10f;

        /// <summary>统一换算评分与旧部位倍率；上限是进度百分点，不是当前进度的百分比。</summary>
        public static float CalculateGain(float score, float corruptionMultiplier, float chainMultiplier)
            => Math.Min(MaximumGain, TrainingExpUtility.CalculateExperienceBudget(score, ExperienceFactor,
                corruptionMultiplier, chainMultiplier));

        /// <summary>一次完成的局部计划；保存原记录及器官引用，不存档另一份进度或领取状态。</summary>
        internal sealed class Plan
        {
            private readonly Pawn target;
            private readonly Hediff_SSCStomachDevelopment record;
            private readonly List<SSCOrdinarySexPartsSnapshot.Part> evidence;
            private readonly Func<SSCOrdinarySexPartsSnapshot.Part, bool> evidenceCurrent;
            private readonly float gain;

            /// <summary>只持有捕获时已存在的记录，后来新做手术或更换记录不能继承本场奖励。</summary>
            internal Plan(Pawn target, Hediff_SSCStomachDevelopment record,
                List<SSCOrdinarySexPartsSnapshot.Part> evidence, float gain,
                Func<SSCOrdinarySexPartsSnapshot.Part, bool> evidenceCurrent)
            {
                this.target = target; this.record = record; this.evidence = evidence;
                this.gain = gain; this.evidenceCurrent = evidenceCurrent;
            }

            /// <summary>在原完成认领内执行一次；回调后只允许原场景、原记录和仍附着的原器官。</summary>
            internal void Apply(Func<bool> isCurrent)
            {
                if (Scribe.mode != LoadSaveMode.Inactive || isCurrent?.Invoke() != true ||
                    target == null || target.Dead || target.Destroyed || target.Discarded ||
                    SSCStomachDevelopmentUtility.Find(target) != record || record.IsComplete ||
                    !evidence.Exists(p => IsEvidenceFor(p, target) && (evidenceCurrent == null || evidenceCurrent(p)))) return;
                // 已满部分不补到其他部位，也不创建记录；Severity 原生 setter 负责阶段与健康缓存通知。
                float actual = Math.Min(gain, 1f - record.Severity);
                if (actual > 0f && isCurrent()) record.Severity += actual;
            }
        }

        /// <summary>对当前 Job 的部位快照建立计划；已有解析时检查方向，读档时使用保存的原凭据。</summary>
        internal static Plan Capture(Pawn target, SSCOrdinarySexPartsSnapshot parts, SexProps props, float score)
            => Capture(target, parts?.Parts, score,
                p => SSCOrdinarySexPartsUtility.IsCurrentReceivingPenileOral(parts, props, p));

        /// <summary>整场仪式使用已完成阶段的器官凭据；不依赖最后一个阶段的交互类型。</summary>
        internal static Plan Capture(Pawn target, IEnumerable<SSCOrdinarySexPartsSnapshot.Part> parts, float score,
            Func<SSCOrdinarySexPartsSnapshot.Part, bool> evidenceCurrent = null)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || target == null || target.Dead || target.Destroyed || target.Discarded)
                return null;
            var record = SSCStomachDevelopmentUtility.Find(target);
            if (record == null || record.IsComplete || parts == null) return null;
            var evidence = parts.Where(p => IsEvidenceFor(p, target) &&
                (evidenceCurrent == null || evidenceCurrent(p))).ToList();
            if (evidence.Count == 0) return null;
            float gain = CalculateGain(score, TrainingExpUtility.GetCorruptionStageMultiplier(target),
                TrainingOutcomeUtility.GetChainStageMult(target));
            return gain > 0f ? new Plan(target, record, evidence, gain, evidenceCurrent) : null;
        }

        /// <summary>欢愉侵蚀使用原方向资格与评分载体；缺少新项目证据时不影响其他侵蚀分量。</summary>
        internal static Plan CaptureInfestation(Pawn source, Pawn target, SSCOrdinarySexPartsSnapshot parts,
            Func<float> score)
        {
            try
            {
                if (!SSCDesireInfestationUtility.CanAffect(source, target) || source.skills == null ||
                    target.relations == null || parts?.Parts == null ||
                    !parts.Parts.Exists(p => IsEvidenceFor(p, target))) return null;
                var record = SSCStomachDevelopmentUtility.Find(target);
                if (record == null || record.IsComplete) return null;
                // 方向与本场缓存由 Context/partsCurrent 持有；写入时还需复查该口腔仍为被插入端。
                return Capture(target, parts.Parts, score(), parts.IsCurrentReceivingPenileOral);
            }
            catch (Exception ex)
            {
                Log.Warning("[SSC StomachGrowth] Cannot prepare optional development: " + ex.GetType().Name);
                return null;
            }
        }

        /// <summary>身体条目必须同时证明持有者、口腔被插入事实以及原部位/器官仍然存在。</summary>
        internal static bool IsEvidenceFor(SSCOrdinarySexPartsSnapshot.Part evidence, Pawn target)
            => evidence != null && evidence.Owner == target && evidence.ReceivedPenileOralPenetration &&
                evidence.Family == LewdablePartFamily.Mouth && evidence.IsAttached();
    }
}
