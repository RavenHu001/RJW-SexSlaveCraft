// AI 辅助生成
using System;
using System.Collections.Generic;
using rjw;
using rjw.Modules.Interactions;
using Verse;
using Verse.AI.Group;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 只有当前 bindingRitualLord 已完成阶段的接触事实；随仪式结束清理，不是身体进度副本。
        internal List<SSCOrdinarySexPartsSnapshot.Part> stomachRitualEvidence;
        internal List<SSCOrdinarySexPartsSnapshot.Part> ritualBodyEvidence;

        /// <summary>保存跨阶段的原器官引用；旧档缺字段为空，不根据阶段号推补合格接触。</summary>
        private void ExposeStomachRitualEvidence()
        {
            Scribe_Collections.Look(ref stomachRitualEvidence, "stomachRitualEvidence", LookMode.Deep);
            Scribe_Collections.Look(ref ritualBodyEvidence, "ritualBodyEvidence", LookMode.Deep);
        }

        internal void RecordRitualBodyPhase(Lord owner, int phase, SSCOrdinarySexPartsSnapshot parts, SexProps props)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || owner == null || bindingRitualLord != owner ||
                !isRitualTraining || ritualPhase != phase + 1 || parts?.Matches(props) != true ||
                props._resolved?.Penetrations == null) return;
            foreach (var evidence in parts.Parts)
            {
                if (evidence.Owner != parent || !SSCRitualInteraction.IsTargetPart(phase, evidence.Family) || !evidence.IsAttached()) continue;
                if ((phase is 4 or 5) && props._resolved.Penetrations.Exists(pair =>
                    SSCOrdinarySexPartsUtility.DescribePart(pair.Item1)?.Same(evidence) == true)) continue;
                ritualBodyEvidence ??= new List<SSCOrdinarySexPartsSnapshot.Part>();
                if (!ritualBodyEvidence.Exists(evidence.Same)) ritualBodyEvidence.Add(evidence);
            }
        }

        internal bool HasRitualGenitalEvidence(Lord owner) => owner != null && bindingRitualLord == owner &&
            ritualBodyEvidence?.Exists(part => part != null && part.Owner == parent &&
                part.Family == LewdablePartFamily.Vagina && part.IsAttached()) == true;

        internal void ApplyRitualBodyExperience(Lord owner, float quality, Func<bool> isCurrent)
        {
            if (owner == null || owner != bindingRitualLord || ritualBodyEvidence == null || !(parent is Pawn target)) return;
            float amount = TrainingExpUtility.CalculateExperienceBudget(quality * 1000f, 0.5f,
                TrainingExpUtility.GetCorruptionStageMultiplier(target), TrainingOutcomeUtility.GetChainStageMult(target));
            var awarded = new HashSet<(BodyPartRecord, HediffDef)>();
            foreach (var part in ritualBodyEvidence)
            {
                if (bindingRitualLord != owner || !isCurrent()) return;
                if (part == null || part.Owner != target || !part.IsAttached()) continue;
                var def = SSCOrdinarySexPartsUtility.GetExperienceDef(part.Family);
                if (def == null || !awarded.Add((part.BodyPart, def))) continue;
                TrainingExpUtility.TryApplyExperienceToPart(target, part.BodyPart, def, amount,
                    () => bindingRitualLord == owner && isCurrent() && part.IsAttached());
            }
        }

        /// <summary>阶段成功推进后记录被插入条目；最后阶段必须在完成 memo 同步派发结果之前调用。</summary>
        internal void RecordStomachRitualPhase(Lord owner, SSCOrdinarySexPartsSnapshot parts, SexProps props)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || owner == null || bindingRitualLord != owner ||
                !isRitualTraining || parts?.Matches(props) != true) return;
            Pawn target = parent as Pawn;
            foreach (var evidence in parts.Parts)
            {
                if (!SSCStomachDevelopmentGrowthUtility.IsEvidenceFor(evidence, target) ||
                    !SSCOrdinarySexPartsUtility.IsCurrentReceivingPenileOral(parts, props, evidence)) continue;
                stomachRitualEvidence ??= new List<SSCOrdinarySexPartsSnapshot.Part>();
                if (!stomachRitualEvidence.Exists(evidence.Same)) stomachRitualEvidence.Add(evidence);
            }
        }
    }
}
