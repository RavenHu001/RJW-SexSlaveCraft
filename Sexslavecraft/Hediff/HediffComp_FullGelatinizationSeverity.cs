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

        /// <summary>仅为仍在进行的全胶过程计时；同轮已经成功的过程不能再推进失败进度。</summary>
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            // 实际游戏会继续遍历已被移除 Hediff 的剩余组件，必须在本入口主动识别已结束状态。
            if (!GelatinizationProcessUtility.IsActive(parent)) return;
            // delta 是本次实际经过的 tick 数；到达配置间隔时沿用一次扫描、计时归零的规则。
            ticksSinceLastScan += delta;
            if (ticksSinceLastScan < Props.scanInterval) return;
            ticksSinceLastScan = 0;
            DoScan();
        }

        /// <summary>按当前准备程度抽取失败进度，仅取得终结资格后才执行身体销毁。</summary>
        private void DoScan()
        {
            if (parent.pawn.Dead) return;

            // EN: Utility call: get the current failure-side severity gain range.
            // CN: 工具调用：取得当前失败侧的严重度增长区间。
            FloatRange gainRange = FullGelatinizationUtility.GetFailureSeverityGainRange(parent.pawn, Props.severityGainRange);
            // 增长区间由原工具服务决定，保持既有风险计算；限制到 0..1 供结果判断及提示显示。
            parent.Severity += gainRange.RandomInRange;
            parent.Severity = Mathf.Clamp01(parent.Severity);

            // 先移除临时状态，再进入不可逆的失败结果；失败优先发生时，后续适应回调同样会被阻止。
            if (parent.Severity >= 1f && GelatinizationProcessUtility.TryComplete(parent))
            {
                // EN: Utility call: execute the terminal failure outcome.
                // CN: 工具调用：执行完全失败的终局处理。
                FullGelatinizationUtility.DestroyPawnCompletely(parent.pawn);
            }
        }

        /// <summary>只读显示失败进度，查看健康提示不会推进或重新结算过程。</summary>
        public override string CompTipStringExtra => "SSC_FullGel_SeverityTip".Translate(parent.Severity.ToStringPercent());
    }
}
