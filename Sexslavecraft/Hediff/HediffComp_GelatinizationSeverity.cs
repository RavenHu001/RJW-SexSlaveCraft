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
        
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            ticksSinceLastScan += delta;
            
            if (ticksSinceLastScan >= Props.scanInterval)
            {
                ticksSinceLastScan = 0;
                DoScan();
            }
        }
        
        private void DoScan()
        {
            if (parent.pawn.Dead) return;
            
            // EN: This path uses the raw failure gain range defined on the comp properties.
            // CN: 这条失败分支直接使用组件属性里定义的严重度增长区间。
            float severityGain = Props.severityGainRange.RandomInRange;
            parent.Severity += severityGain;
            parent.Severity = Mathf.Clamp01(parent.Severity);
            
            if (parent.Severity >= 1f)
            {
                // EN: A filled severity bar resolves the process as irreversible limb loss.
                // CN: 严重度走满后，流程会以不可逆的断肢失败结局收束。
                CompleteFailure();
            }
        }
        
        private void CompleteFailure()
        {
            if (parent.pawn.Dead) return;
            
            BodyPartRecord part = parent.Part;
            if (part == null)
            {
                parent.pawn.health.RemoveHediff(parent);
                return;
            }

            if (parent.pawn.health.hediffSet.PartIsMissing(part))
            {
                parent.pawn.health.RemoveHediff(parent);
                return;
            }
            
            parent.pawn.health.RemoveHediff(parent);
            
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

        public void DebugForceCompleteFailure()
        {
            if (parent?.pawn == null || parent.pawn.Dead)
            {
                return;
            }

            parent.Severity = 1f;
            CompleteFailure();
            parent.pawn.health.Notify_HediffChanged(parent);
        }
        
        public override string CompDebugString()
        {
            return $"Severity: {parent.Severity.ToStringPercent()}";
        }

        public override string CompTipStringExtra
        {
            get
            {
                return $"严重度: {parent.Severity.ToStringPercent()}";
            }
        }
    }
}
