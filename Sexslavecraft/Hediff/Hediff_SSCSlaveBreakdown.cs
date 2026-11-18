// AI 辅助生成：用同一 Hediff 截止点驱动本场发作和健康页倒计时。
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存本场发作的实际截止 tick，并提供原版健康页标签和当前属性阶段。</summary>
    public sealed class Hediff_SSCSlaveBreakdown : HediffWithComps
    {
        internal const string DefName = "SSC_Hediff_SlaveBreakdown";
        // 唯一运行时发作期限。这里保存绝对游戏 tick，界面刷新和暂停不会推进计时。
        internal long ExpiresAt = -1L;
        private static readonly HediffStage NeutralStage = new HediffStage();

        // 读档解析引用和状态启动期间保留空阶段。到期时先停止属性，实例留给精神状态完成结算。
        private bool IsActiveAt(int now) => Scribe.mode == LoadSaveMode.Inactive && now >= 0 && now < ExpiresAt &&
            MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.EffectIsActive(this) == true;

        public override HediffStage CurStage => IsActiveAt(SSCSlaveBreakdown.Now) ? base.CurStage : NeutralStage;

        public override string LabelInBrackets
        {
            get
            {
                int now = SSCSlaveBreakdown.Now;
                if (!IsActiveAt(now)) return base.LabelInBrackets;
                // 沿用原版限时 Hediff 的短格式，剩余不足一秒时仍显示一秒。
                int remaining = (int)Math.Clamp(ExpiresAt - now, 60L, int.MaxValue);
                return remaining < GenDate.TicksPerHour ? remaining.ToStringSecondsFromTicks("F0") :
                    remaining.ToStringTicksToPeriod(shortForm: true, canUseDecimalsShortForm: true);
            }
        }

        // 到期仍由持有者移除，使健康先刷新时也能用原期限结算整小时。健康刷新保留原版移除条件，
        // 另兜底清掉失去持有者的实例。读档期间等待交叉引用解析。
        public override bool ShouldRemove => base.ShouldRemove || (Scribe.mode == LoadSaveMode.Inactive &&
            MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.OwnsEffect(this) != true);

        // 禁止原版按 Def 合并，保证 Scribe 引用和退出清理一直指向同一个实例。
        public override bool TryMergeWith(Hediff other) => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ExpiresAt, "sscBreakdownEffectExpiresAt", -1L);
        }
    }

    /// <summary>单独保存发作后的绝对截止时间，包括角色离图期间。</summary>
    public sealed class Hediff_SSCSlaveBreakdownCooldown : Hediff
    {
        internal const string DefName = "SSC_Hediff_SlaveBreakdownCooldown";
        internal long ExpiresAt;

        public override bool ShouldRemove => base.ShouldRemove ||
            (Scribe.mode == LoadSaveMode.Inactive && SSCSlaveBreakdown.Now >= ExpiresAt);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ExpiresAt, "sscBreakdownCooldownExpiresAt", 0L);
        }
    }
}
