// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>继承原版配置接口，使 Hediff_Addiction 仍能识别并显示恢复进度。</summary>
    public sealed class HediffCompProperties_SSCPnaRecovery : HediffCompProperties_SeverityPerDay
    {
        public HediffCompProperties_SSCPnaRecovery() { compClass = typeof(HediffComp_SSCPnaRecovery); }
    }

    /// <summary>沿用原版依赖与进度，忽略旧档保存的无条件恢复速率；不新增时间字段。</summary>
    public sealed class HediffComp_SSCPnaRecovery : HediffComp_SeverityPerDay
    {
        public override float SeverityChangePerDay()
        {
            // 身体无效、健康更新暂停、有有效链或负荷仍高时，恢复速率为零。
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed || Pawn.Discarded || Pawn.Suspended ||
                SSCPnaExposureUtility.IsRooted(Pawn) || SSCPnaExposureUtility.GetLoad(Pawn) > .20f) return 0f;
            // 合法需求存在时必须进入原版戒断类别；没有合法需求则只要求低负荷。
            // 实际速率由规则直接返回，不使用旧组件保存的 severityPerDay 值。
            Need_Chemical need = SSCPnaExposureUtility.GetNeed(Pawn);
            return need == null || need.CurCategory == DrugDesireCategory.Withdrawal ? -.125f : 0f;
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            // 原生健康回调是唯一计时入口；读档、零或负 delta 都不推进 D。
            if (delta <= 0 || Scribe.mode != LoadSaveMode.Inactive) return;
            // 原版 200-tick 固定步长不等同于传入的有效 delta；按实际健康更新时间计算。
            severityAdjustment += SeverityChangePerDay() * (delta / (float)SSCPnaMath.TicksPerDay);
        }

        // 让原版健康流程移除耗尽的依赖；移除补丁负责刷新对应需求的存在性。
        public override bool CompShouldRemove => parent.Severity <= 0f;

        // 悬停只读取恢复条件：显示有效恢复日或暂停原因，不承诺固定日历倒计时。
        public override string CompTipStringExtra => SeverityChangePerDay() < 0f
            ? "SSC_Pna_RecoveryActive".Translate((parent.Severity / .125f).ToString("0.0")).ToString()
            : "SSC_Pna_RecoveryPaused".Translate().ToString();
    }
}
