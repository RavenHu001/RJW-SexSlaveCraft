using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

// EN: This temporary hediff tracks limb-level gelatinization in progress.
// EN: It stores adaptation progress, validates that the affected part is still a
// EN: valid limb, and exposes developer shortcuts for forced success or failure.
// CN: 这个临时 Hediff 用来追踪肢体级胶化的进行状态。
// CN: 它保存适应进度、校验受影响部位仍然是合法肢体，并提供开发者快捷按钮测试成功或失败结局。
namespace SexSlaveCraft
{
    public class Hediff_GelatinizationTemporary : HediffWithComps
    {
        public float adaptation = 0f;
        
        public float CorruptionLevel
        {
            get
            {
                // EN: This older gelatinization path still reads the corruption need by defName.
                // CN: 旧版局部胶化流程仍然通过 defName 方式读取腐化 Need。
                var corNeed = pawn.needs?.AllNeeds?.FirstOrDefault(n => n.def.defName == "Corruption");
                if (corNeed == null) return 0f;
                return corNeed.CurLevelPercentage;
            }
        }
        
        public bool IsGuaranteedSuccess => CorruptionLevel >= 0.7f;
        
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            
            if (Part == null || !IsValidLimb(Part))
            {
                pawn.health.RemoveHediff(this);
                return;
            }
        }

        public override void PostTick()
        {
            base.PostTick();

            if (Part == null || pawn.health.hediffSet.PartIsMissing(Part))
            {
                pawn.health.RemoveHediff(this);
            }
        }
        
        private bool IsValidLimb(BodyPartRecord part)
        {
            return part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore) ||
                   part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore);
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref adaptation, "adaptation", 0f);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!Prefs.DevMode || pawn == null || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Adapt -> 100%",
                defaultDesc = "Force adaptation to 100% and resolve gelatinization as success.",
                action = delegate
                {
                    var adaptationComp = comps?.OfType<HediffComp_GelatinizationAdaptation>().FirstOrDefault();
                    if (adaptationComp != null)
                    {
                        // EN: Dev helper: force the adaptation-side success path.
                        // CN: 开发辅助：强制走适应成功分支。
                        adaptationComp.DebugForceCompleteSuccess();
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Severity -> 100%",
                defaultDesc = "Force severity to 100% and resolve gelatinization as failure.",
                action = delegate
                {
                    var severityComp = comps?.OfType<HediffComp_GelatinizationSeverity>().FirstOrDefault();
                    if (severityComp != null)
                    {
                        // EN: Dev helper: force the severity-side failure path.
                        // CN: 开发辅助：强制走严重度失败分支。
                        severityComp.DebugForceCompleteFailure();
                    }
                }
            };
        }
    }
}
