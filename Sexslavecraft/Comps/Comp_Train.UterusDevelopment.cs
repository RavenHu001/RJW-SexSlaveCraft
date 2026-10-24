// AI 辅助生成
using System.Collections.Generic;
using rjw;
using Verse;
using Verse.AI.Group;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 只有当前 bindingRitualLord 已完成阶段的接触事实；随仪式结束清理，不是身体进度副本。
        internal List<SSCOrdinarySexPartsSnapshot.Part> uterusRitualEvidence;

        /// <summary>保存跨阶段的原器官引用；旧档缺字段为空，不根据阶段号推补合格接触。</summary>
        private void ExposeUterusRitualEvidence()
            => Scribe_Collections.Look(ref uterusRitualEvidence, "uterusRitualEvidence", LookMode.Deep);

        /// <summary>阶段成功推进后记录被插入条目；最后阶段必须在完成 memo 同步派发结果之前调用。</summary>
        internal void RecordUterusRitualPhase(Lord owner, SSCOrdinarySexPartsSnapshot parts, SexProps props)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || owner == null || bindingRitualLord != owner ||
                !isRitualTraining || parts?.Matches(props) != true) return;
            Pawn target = parent as Pawn;
            foreach (var evidence in parts.Parts)
            {
                if (!SSCUterusDevelopmentGrowthUtility.IsEvidenceFor(evidence, target) ||
                    !SSCOrdinarySexPartsUtility.IsCurrentReceivingVagina(parts, props, evidence)) continue;
                uterusRitualEvidence ??= new List<SSCOrdinarySexPartsSnapshot.Part>();
                if (!uterusRitualEvidence.Exists(evidence.Same)) uterusRitualEvidence.Add(evidence);
            }
        }
    }
}
