using System.Collections.Generic;
using RimWorld;
using Verse;

// EN: This recipe starts partial gelatinization on a selected limb.
// EN: It blocks the surgery on fully gelatinized bodies, then applies the `半凝胶化处理中` state to a valid arm or leg.
// CN: 这个配方用于在指定肢体上启动局部胶化流程。
// CN: 它会阻止对已完全胶化的躯体继续使用，并把“半凝胶化处理中”状态施加到合法的手臂或腿部上。
namespace SexSlaveCraft
{
    public class Recipe_ApplyGelatinization : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part)) return false;
            Pawn pawn = thing as Pawn;
            // EN: A pawn already in `全凝胶化完成` should never re-enter the partial gelatinization branch.
            // CN: 已经处于“全凝胶化完成”的 Pawn 不应该再回到局部胶化分支里。
            return pawn == null || pawn.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.SSC_FullGelatinizedBody) == null;
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            var notMissingParts = pawn.health.hediffSet.GetNotMissingParts();
            foreach (var part in notMissingParts)
            {
                if (IsValidLimb(part))
                {
                    yield return part;
                }
            }
        }
        
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn?.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.SSC_FullGelatinizedBody) != null)
            {
                Messages.Message("SSC_FullGel_BlockSemiGel".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }
            
            // EN: Compatibility path: the recipe still resolves `Hediff_GelatinizationTemporary` by defName to match the current XML setup.
            // CN: 兼容分支：这个配方仍然通过 defName 查找 `Hediff_GelatinizationTemporary`，以匹配当前 XML 配置方式。
            var gelDef = DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_GelatinizationTemporary");
            if (gelDef != null)
            {
                var gelHediff = HediffMaker.MakeHediff(gelDef, pawn, part) as Hediff_GelatinizationTemporary;
                if (gelHediff != null)
                {
                    pawn.health.AddHediff(gelHediff);
                    
                    if (recipe.addsHediff.initialSeverity > 0f)
                    {
                        gelHediff.Severity = recipe.addsHediff.initialSeverity;
                    }
                }
            }
            else
            {
                Log.Error("[SSC] Hediff_GelatinizationTemporary not found!");
            }
        }
        
        private bool IsValidLimb(BodyPartRecord part)
        {
            return part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore) ||
                   part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore);
        }
    }
}
