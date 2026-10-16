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

        /// <summary>只为仍附着的临时状态累计经过时间，到达扫描间隔后推进一次适应度。</summary>
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            // 成功、失败或外部移除都可能发生在当前组件回调之前，先拒绝失效过程再计时或抽随机数。
            if (!GelatinizationProcessUtility.IsActive(parent)) return;
            // 沿用原有计时规则：累计 delta，每次达到阈值只扫描一次并重置本组件计时。
            ticksSinceLastScan += delta;
            if (ticksSinceLastScan < Props.scanInterval) return;
            ticksSinceLastScan = 0;
            DoScan();
        }

        /// <summary>根据准备程度推进适应度，并在成功阈值处领取唯一的结算资格。</summary>
        private void DoScan()
        {
            // 只处理对应的临时全胶类型；死亡角色保持原有的停止推进语义。
            Hediff_FullGelatinizationTemporary gel = parent as Hediff_FullGelatinizationTemporary;
            if (gel == null || parent.pawn.Dead) return;

            // EN: Utility call: get the adaptation gain profile for the pawn's current full-gel state.
            // CN: 工具调用：根据 Pawn 当前的完全胶化状态，取得适应增长区间。
            FloatRange gainRange = FullGelatinizationUtility.GetAdaptationGainRange(parent.pawn, Props.guaranteedGainRange, Props.baseGainRange, out bool guaranteed);
            if (guaranteed)
            {
                // 满足必定成功条件时，每次扫描都按专用增长区间推进，不再判定概率是否命中。
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

            // 进度保持在百分比范围内；短路判断保证未达标时不移除过程。
            // 先移除临时状态，再执行成功服务，使同一 Hediff 的严重度组件无法随后销毁成功身体。
            gel.adaptation = Mathf.Clamp01(gel.adaptation);
            if (gel.adaptation >= 1f && GelatinizationProcessUtility.TryComplete(parent))
            {
                // EN: Utility call: finish the full-gel conversion and apply the success outcome.
                // CN: 工具调用：完成完全胶化，并施加成功结局。
                FullGelatinizationUtility.CompleteSuccess(parent.pawn);
            }
        }

        /// <summary>仅查询当前适应度供翻译提示显示，临时状态类型缺失时显示零进度。</summary>
        public override string CompTipStringExtra => "SSC_FullGel_AdaptationTip".Translate((parent as Hediff_FullGelatinizationTemporary)?.adaptation.ToStringPercent() ?? "0%");
    }
}
