using RimWorld;
using UnityEngine;
using Verse;

// EN: This hediff comp advances full-gel adaptation over time.
// EN: It reads the current success profile from FullGelatinizationUtility and
// EN: converts that into adaptation progress until the process succeeds.
// CN: 这个 HediffComp 负责推进完全胶化的适应进度。
// CN: 它从 FullGelatinizationUtility 读取当前成功权重，并把它转换成适应值，直到完全成功。
namespace SexSlaveCraft
{
    public class HediffComp_FullGelatinizationAdaptation : HediffComp
    {
        private int ticksSinceLastScan;

        public HediffCompProperties_FullGelatinizationAdaptation Props => (HediffCompProperties_FullGelatinizationAdaptation)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            ticksSinceLastScan += delta;
            if (ticksSinceLastScan < Props.scanInterval) return;
            ticksSinceLastScan = 0;
            DoScan();
        }

        private void DoScan()
        {
            Hediff_FullGelatinizationTemporary gel = parent as Hediff_FullGelatinizationTemporary;
            if (gel == null || parent.pawn.Dead) return;

            // EN: Utility call: get the adaptation gain profile for the pawn's current full-gel state.
            // CN: 工具调用：根据 Pawn 当前的完全胶化状态，取得适应增长区间。
            FloatRange gainRange = FullGelatinizationUtility.GetAdaptationGainRange(parent.pawn, Props.guaranteedGainRange, Props.baseGainRange, out bool guaranteed);
            if (guaranteed)
            {
                gel.adaptation += gainRange.RandomInRange;
            }
            else
            {
                // EN: Utility call: reuse the smoothed success weight as the adaptation chance driver.
                // CN: 工具调用：复用平滑后的成功权重作为适应增长概率依据。
                float smooth = FullGelatinizationUtility.GetSmoothedSuccessWeight(parent.pawn);
                float adaptChance = Mathf.Lerp(0.15f, 0.95f, smooth);
                if (Rand.Chance(adaptChance))
                {
                    gel.adaptation += gainRange.min;
                }
            }

            gel.adaptation = Mathf.Clamp01(gel.adaptation);
            if (gel.adaptation >= 1f)
            {
                parent.pawn.health.RemoveHediff(parent);
                // EN: Utility call: finish the full-gel conversion and apply the success outcome.
                // CN: 工具调用：完成完全胶化，并施加成功结局。
                FullGelatinizationUtility.CompleteSuccess(parent.pawn);
            }
        }

        public override string CompTipStringExtra => "SSC_FullGel_AdaptationTip".Translate((parent as Hediff_FullGelatinizationTemporary)?.adaptation.ToStringPercent() ?? "0%");
    }
}
