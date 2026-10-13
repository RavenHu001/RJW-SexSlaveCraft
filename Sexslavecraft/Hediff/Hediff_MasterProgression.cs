// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原生严重度保存累计经验；四阶段只提供主人自身加成，不保存绑定关系。</summary>
    public sealed class Hediff_MasterProgression : Hediff
    {
        // 不改共享 Def 阶段；暂停时返回独立中性阶段，保留真实经验和阶段索引。
        private static readonly HediffStage InactiveStage = new HediffStage();

        public bool IsActive => SSCMasterProgressionUtility.IsActive(pawn);
        // 原版零严重度会移除 Hediff，基础阶段必须显式保留。
        public override bool ShouldRemove => false;
        // 原版能力、属性与自然愈合读取 CurStage；返回中性阶段才会真正暂停全部加成。
        // 不改严重度或 CurStageIndex，身份返回后仍能读取原有成长阶段。
        public override HediffStage CurStage => IsActive ? base.CurStage : InactiveStage;
        // 严重度代表经验，不显示疾病百分比；具体数值在下方悬停中提供。
        public override string SeverityLabel => null;

        /// <summary>健康栏仍显示保存的阶段，退出身份或空壳状态另标明加成暂停。</summary>
        public override string LabelInBrackets
        {
            get
            {
                // 显示基础类按经验选出的真实阶段，而不是暂停时的中性阶段标签。
                string stage = base.CurStage?.label ?? string.Empty;
                return IsActive ? stage : "SSC_MasterProgression_InactiveStage".Translate(stage).ToString();
            }
        }

        /// <summary>用经验数值解释严重度，避免把成长显示为疾病百分比。</summary>
        public override string GetTooltip(Pawn pawn, bool showHediffsDebugInfo)
        {
            // 保留原版当前有效属性与调试信息，再补累计经验；上限同样读取当前 Def。
            return base.GetTooltip(pawn, showHediffsDebugInfo) + "\n" +
                "SSC_MasterProgression_Experience".Translate(Severity.ToString("0"), def.maxSeverity.ToString("0"));
        }
    }
}
