using UnityEngine;
using Verse;

// EN: This hediff comp advances the failure side of full gelatinization.
// EN: It increases the destructive severity track until the pawn fully fails
// EN: the conversion and is removed by the utility layer.
// CN: 这个 HediffComp 负责推进完全胶化的失败进度。
// CN: 它不断增加破坏性严重度，直到转换彻底失败，并由工具层执行销毁结局。
namespace SexSlaveCraft
{
    public class HediffComp_FullGelatinizationSeverity : HediffComp
    {
        private int ticksSinceLastScan;

        public HediffCompProperties_FullGelatinizationSeverity Props => (HediffCompProperties_FullGelatinizationSeverity)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            ticksSinceLastScan += delta;
            if (ticksSinceLastScan < Props.scanInterval) return;
            ticksSinceLastScan = 0;
            DoScan();
        }

        private void DoScan()
        {
            if (parent.pawn.Dead) return;

            // EN: Utility call: get the current failure-side severity gain range.
            // CN: 工具调用：取得当前失败侧的严重度增长区间。
            FloatRange gainRange = FullGelatinizationUtility.GetFailureSeverityGainRange(parent.pawn, Props.severityGainRange);
            parent.Severity += gainRange.RandomInRange;
            parent.Severity = Mathf.Clamp01(parent.Severity);

            if (parent.Severity >= 1f)
            {
                parent.pawn.health.RemoveHediff(parent);
                // EN: Utility call: execute the terminal failure outcome.
                // CN: 工具调用：执行完全失败的终局处理。
                FullGelatinizationUtility.DestroyPawnCompletely(parent.pawn);
            }
        }

        public override string CompTipStringExtra => "SSC_FullGel_SeverityTip".Translate(parent.Severity.ToStringPercent());
    }
}
