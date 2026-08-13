using System.Collections.Generic;
using RimWorld;
using Verse;

// EN: This recipe starts full gelatinization on a hollow pawn.
// EN: It checks the personality-excretion prerequisite, adds the `全凝胶化处理中` state, and then reports whether this surgery is guaranteed or risky.
// CN: 这个配方负责在空壳 Pawn 身上启动完全胶化流程。
// CN: 它会检查人格排泄前置条件、施加“全凝胶化处理中”状态，并告知这次手术是必成还是高风险。
namespace SexSlaveCraft
{
    public class Recipe_ApplyFullGelatinization : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part)) return false;
            return thing is Pawn pawn && pawn.RaceProps.Humanlike;
        }

        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part)) return false;
            if (!(thing is Pawn pawn) || !pawn.RaceProps.Humanlike) return false;

            if (pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_FullGelatinizedBody) != null)
            {
                return "已完成全凝胶化";
            }

            if (pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_FullGelatinizationTemporary) != null)
            {
                return "已在全凝胶化处理中";
            }

            if (!FullGelatinizationUtility.HasPersonalityExcretionDone(pawn))
            {
                return "未进行排泄";
            }

            return true;
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn == null) return;

            // EN: Re-check the hollow-pawn prerequisite at execution time in case the target changed after the bill was queued.
            // CN: 执行时再次确认“空壳 Pawn”前置条件，避免 bill 排队后目标状态发生变化。
            if (!FullGelatinizationUtility.HasPersonalityExcretionDone(pawn))
            {
                Messages.Message("SSC_FullGel_MissingPE".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            if (pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_FullGelatinizationTemporary) != null)
            {
                Messages.Message("SSC_FullGel_AlreadyRunning".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Hediff fullGel = HediffMaker.MakeHediff(SSCDefOf.SSC_FullGelatinizationTemporary, pawn);
            pawn.health.AddHediff(fullGel);

            // EN: Show either the guaranteed-success message or the high-risk race warning for `全凝胶化处理中`.
            // CN: 这里决定显示“必定成功”提示，还是“全凝胶化处理中”的高风险竞赛警告。
            if (FullGelatinizationUtility.IsGuaranteedSuccess(pawn, out _))
            {
                Messages.Message("SSC_FullGel_Guaranteed".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                // EN: Build the detailed full-gel risk report so the player can see which body-part training tracks are still missing.
                // CN: 生成详细的完全胶化风险报告，让玩家知道还缺哪些部位训练条目。
                Messages.Message("SSC_FullGel_RiskWarning".Translate(pawn.LabelShort, FullGelatinizationUtility.BuildRiskReport(pawn)), pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
