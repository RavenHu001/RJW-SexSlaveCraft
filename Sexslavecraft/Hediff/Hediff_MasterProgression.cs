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
        // 严重度只用于保存经验，不在健康栏显示百分比；经验与进度由主人面板展示。
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

        // 悬停直接沿用原版设定描述、阶段短句和有效属性，不额外追加成长经验。
    }
}
