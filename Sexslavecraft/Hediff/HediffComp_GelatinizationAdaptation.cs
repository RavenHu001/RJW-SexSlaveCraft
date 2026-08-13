using System;
using RimWorld;
using UnityEngine;
using Verse;

// EN: This hediff comp advances limb-level gelatinization adaptation.
// EN: It converts corruption into adaptation growth and, once complete, swaps
// EN: the temporary hediff into a stable gelatinized limb hediff.
// CN: 这个 HediffComp 负责推进肢体级胶化的适应过程。
// CN: 它把腐化度转换为适应增长，并在完成后把临时 Hediff 替换成稳定的胶化肢体 Hediff。
namespace SexSlaveCraft
{
    public class HediffComp_GelatinizationAdaptation : HediffComp
    {
        public HediffCompProperties_GelatinizationAdaptation Props => 
            (HediffCompProperties_GelatinizationAdaptation)props;
        
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
            var gelHediff = parent as Hediff_GelatinizationTemporary;
            if (gelHediff == null || gelHediff.pawn.Dead) return;
            
            float corruptionLevel = gelHediff.CorruptionLevel;
            
            if (corruptionLevel >= 0.7f)
            {
                float adaptGain = Props.adaptGainRange.RandomInRange;
                gelHediff.adaptation += adaptGain;
            }
            else
            {
                // EN: Corruption increases the chance that the limb adapts instead of resisting.
                // CN: 腐化度越高，肢体越容易适应而不是继续抗拒胶化。
                float adaptChance = Props.baseAdaptChance + corruptionLevel * Props.corruptionBonusMultiplier;
                
                if (Rand.Chance(adaptChance))
                {
                    float adaptGain = Props.adaptGainRange.RandomInRange;
                    gelHediff.adaptation += adaptGain;
                }
            }
            
            gelHediff.adaptation = Mathf.Clamp01(gelHediff.adaptation);
            
            if (gelHediff.adaptation >= 1f)
            {
                // EN: Completion replaces the temporary process with a permanent limb outcome.
                // CN: 适应完成后，要把临时过程替换成稳定的肢体结果。
                CompleteSuccessfully();
            }
        }
        
        private void CompleteSuccessfully()
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
            
            // EN: Resolve the permanent result by limb type so arms and legs get different end states.
            // CN: 按肢体类型决定永久结果，让手臂和腿走不同的终态 Hediff。
            string targetHediffDefName = GetGelatinizedHediffDefNameForPart(part);
            var gelDef = DefDatabase<HediffDef>.GetNamedSilentFail(targetHediffDefName);
            
            if (gelDef != null)
            {
                var gelHediff = HediffMaker.MakeHediff(gelDef, parent.pawn, part);
                parent.pawn.health.AddHediff(gelHediff);
            }
            else
            {
                Log.Warning($"[SSC] {targetHediffDefName} not found, using MissingBodyPart as fallback");
                var missingPartDef = DefDatabase<HediffDef>.GetNamedSilentFail("MissingBodyPart");
                if (missingPartDef != null)
                {
                    var missingPart = HediffMaker.MakeHediff(missingPartDef, parent.pawn, part) as Hediff_MissingPart;
                    if (missingPart != null)
                    {
                        missingPart.lastInjury = HediffDefOf.SurgicalCut;
                        missingPart.IsFresh = false;
                        parent.pawn.health.AddHediff(missingPart);
                    }
                }
            }
        }

        public void DebugForceCompleteSuccess()
        {
            var gelHediff = parent as Hediff_GelatinizationTemporary;
            if (gelHediff == null || parent?.pawn == null || parent.pawn.Dead)
            {
                return;
            }

            gelHediff.adaptation = 1f;
            CompleteSuccessfully();
            parent.pawn.health.Notify_HediffChanged(parent);
        }
        
        private string GetGelatinizedHediffDefNameForPart(BodyPartRecord part)
        {
            if (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore))
            {
                return "Hediff_GelatinizedArm";
            }
            else if (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore))
            {
                return "Hediff_GelatinizedLeg";
            }
            
            return "Hediff_GelatinizedArm"; // fallback
        }
        
        public override string CompDebugString()
        {
            var gelHediff = parent as Hediff_GelatinizationTemporary;
            return $"Adaptation: {gelHediff?.adaptation.ToStringPercent()} (Corruption: {gelHediff?.CorruptionLevel.ToStringPercent()})";
        }

        public override string CompTipStringExtra
        {
            get
            {
                var gelHediff = parent as Hediff_GelatinizationTemporary;
                if (gelHediff == null) return null;
                return $"适应度: {gelHediff.adaptation.ToStringPercent()}\n腐化度: {gelHediff.CorruptionLevel.ToStringPercent()}";
            }
        }
    }
}
