using RimWorld;
using UnityEngine;
using Verse;

// EN: This hediff comp advances the failure track of limb gelatinization.
// EN: If severity fills first, the affected limb is removed instead of becoming
// EN: a stable gelatinized limb.
// CN: 这个 HediffComp 负责推进肢体胶化的失败进度条。
// CN: 如果严重度先走满，受影响肢体就会被直接移除，而不是转成稳定胶化肢体。
namespace SexSlaveCraft
{
    public class HediffComp_GelatinizationSeverity : HediffComp
    {
        public HediffCompProperties_GelatinizationSeverity Props => 
            (HediffCompProperties_GelatinizationSeverity)props;
        
        private int ticksSinceLastScan = 0;
        
        /// <summary>为有效的局部胶化过程累计扫描时间，已移除过程不计时也不抽随机数。</summary>
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            // 适应组件排在前面；它可能刚刚成功移除 parent，而当前组件仍被游戏调用。
            if (!GelatinizationProcessUtility.IsActive(parent)) return;
            ticksSinceLastScan += delta;
            
            if (ticksSinceLastScan >= Props.scanInterval)
            {
                ticksSinceLastScan = 0;
                DoScan();
            }
        }
        
        /// <summary>按 XML 区间增加严重度，达到满值后请求一次失败结算。</summary>
        private void DoScan()
        {
            if (parent.pawn.Dead) return;
            
            // EN: This path uses the raw failure gain range defined on the comp properties.
            // CN: 这条失败分支直接使用组件属性里定义的严重度增长区间。
            float severityGain = Props.severityGainRange.RandomInRange;
            // 保留原有随机增长与百分比上限；失败结果由下方独立的受保护入口执行。
            parent.Severity += severityGain;
            parent.Severity = Mathf.Clamp01(parent.Severity);
            
            if (parent.Severity >= 1f)
            {
                // EN: A filled severity bar resolves the process as irreversible limb loss.
                // CN: 严重度走满后，流程会以不可逆的断肢失败结局收束。
                CompleteFailure();
            }
        }
        
        /// <summary>结束临时过程，仅对仍存在的有效部位添加一次稳定缺肢状态。</summary>
        private void CompleteFailure()
        {
            // 自然失败与开发按钮共用终结检查，避免成功之后再断肢或重复添加缺肢记录。
            if (!GelatinizationProcessUtility.TryComplete(parent)) return;
            
            // 缺少部位或部位已经丢失时只清理临时过程；不会对其他肢体补做失败结果。
            BodyPartRecord part = parent.Part;
            if (part == null)
            {
                return;
            }

            if (parent.pawn.health.hediffSet.PartIsMissing(part))
            {
                return;
            }
            
            // 沿用原版缺肢 Hediff：记录手术切口来源，但将新鲜标记关闭以避免持续流血。
            var missingPartDef = DefDatabase<HediffDef>.GetNamedSilentFail("MissingBodyPart");
            if (missingPartDef != null)
            {
                var missingPart = HediffMaker.MakeHediff(missingPartDef, parent.pawn, part) as Hediff_MissingPart;
                if (missingPart != null)
                {
                    missingPart.lastInjury = HediffDefOf.SurgicalCut;
                    // 失败后直接视为稳定缺失，避免出现持续流血状态
                    missingPart.IsFresh = false;
                    parent.pawn.health.AddHediff(missingPart);
                }
            }
        }

        /// <summary>开发模式强制失败；旧按钮不能影响已结束过程或后来新增的同类过程。</summary>
        public void DebugForceCompleteFailure()
        {
            if (!GelatinizationProcessUtility.IsActive(parent))
            {
                return;
            }

            parent.Severity = 1f;
            CompleteFailure();
            // 统一通过原版通知刷新健康显示，执行次序仍是结束过程、添加结果、通知界面。
            parent.pawn.health.Notify_HediffChanged(parent);
        }
        
        /// <summary>仅显示当前严重度；查看调试信息不触发扫描。</summary>
        public override string CompDebugString()
        {
            return $"Severity: {parent.Severity.ToStringPercent()}";
        }

        /// <summary>普通健康提示沿用百分比显示，与过程的结算逻辑分离。</summary>
        public override string CompTipStringExtra
        {
            get
            {
                return $"严重度: {parent.Severity.ToStringPercent()}";
            }
        }
    }
}
