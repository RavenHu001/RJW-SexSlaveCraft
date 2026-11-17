// AI 辅助生成：原版身体属性仅由当前发作持有的临时实例提供。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>给原版 StatWorker 提供发作属性。精神状态负责时限、身份和累计严重度。</summary>
    public sealed class Hediff_SSCSlaveBreakdown : HediffWithComps
    {
        internal const string DefName = "SSC_Hediff_SlaveBreakdown";
        // 单独保存同一个截止 tick，防止状态被直接丢弃后残留的 Hediff 继续生效。
        internal long ExpiresAt;
        private static readonly HediffStage NeutralStage = new HediffStage();

        // Scribe 解析引用时暂用空阶段。只有当前发作已经确认且持有本实例，才允许 RJW 读取倍率与偏移。
        public override HediffStage CurStage => Scribe.mode == LoadSaveMode.Inactive && SSCSlaveBreakdown.Now < ExpiresAt &&
            MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.EffectIsActive(this) == true ? base.CurStage : NeutralStage;

        // 读档期保留待解析的实例。运行中由原版健康刷新移除到期、离开所有者或被错误复制的残留。
        public override bool ShouldRemove => base.ShouldRemove || (Scribe.mode == LoadSaveMode.Inactive &&
            (SSCSlaveBreakdown.Now >= ExpiresAt || MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.OwnsEffect(this) != true));

        // 禁止原版按 Def 合并，保证 Scribe 引用和退出清理一直指向同一个实例。
        public override bool TryMergeWith(Hediff other) => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ExpiresAt, "sscBreakdownEffectExpiresAt", -1L);
        }
    }
}
