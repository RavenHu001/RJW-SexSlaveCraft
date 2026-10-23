// AI 辅助生成
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class Recipe_BeautyEnhancement : Recipe_Surgery
    {
        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            var report = base.AvailableReport(thing, part);
            return report.Accepted ? SSCCosmeticUtility.RecipientReport(thing as Pawn, true) : report;
        }

        // 麻醉、材料、完成判定和账单清理由原版负责。
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!SSCCosmeticUtility.IsLiving(pawn)) return;
            try
            {
                var report = SSCCosmeticUtility.RecipientReport(pawn, true);
                if (!report.Accepted)
                {
                    ApplyFailure(pawn, report.Reason);
                    return;
                }
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;
                SSCCosmeticUtility.ApplyResult(pawn, true, SSCCosmeticUtility.SurgerySuccess);
                if (billDoer != null) TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                Messages.Message("SSC_Cosmetic_SurgerySuccess".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex) { Log.Error($"[SSC Cosmetic] Surgery result failed for {pawn}: {ex}"); }
        }

        internal static void ApplyFailure(Pawn patient, string reason = null)
        {
            if (!SSCCosmeticUtility.ApplyResult(patient, false, SSCCosmeticUtility.SurgeryFailure)) return;
            string text = "SSC_Cosmetic_SurgeryFailure".Translate(patient.LabelShort);
            if (!reason.NullOrEmpty()) text += "\n" + reason;
            Messages.Message(text, patient, MessageTypeDefOf.NegativeEvent);
        }
    }
}
