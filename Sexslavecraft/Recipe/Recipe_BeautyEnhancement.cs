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
            // 原版可用性优先；仅向医疗清单补充受塑资格与拒绝原因。
            var report = base.AvailableReport(thing, part);
            return report.Accepted ? SSCCosmeticUtility.RecipientReport(thing as Pawn, true) : report;
        }

        // 麻醉、材料、完成判定和账单清理由原版负责。
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!SSCCosmeticUtility.IsLiving(pawn)) return;
            try
            {
                // 旧账单执行期间资格可能变化，此时按确定失败处理，不再抽取正常手术成败。
                var report = SSCCosmeticUtility.RecipientReport(pawn, true);
                if (!report.Accepted)
                {
                    ApplyFailure(pawn, report.Reason);
                    return;
                }
                // 医疗质量和随机判定沿用原版；失败效果已由配方的专用 Outcome 执行。
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;
                // 仅通过判定后升档并加轻度反应；此处不再麻醉或消费材料。
                SSCCosmeticUtility.ApplyResult(pawn, true, SSCCosmeticUtility.SurgerySuccess);
                if (billDoer != null) TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                Messages.Message("SSC_Cosmetic_SurgerySuccess".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent);
            }
            // 技术异常只记录红字，不重试、不退款，也不撤销已经完成的身体或特质修改。
            catch (Exception ex) { Log.Error($"[SSC Cosmetic] Surgery result failed for {pawn}: {ex}"); }
        }

        internal static void ApplyFailure(Pawn patient, string reason = null)
        {
            // 随机失败与最终资格失效共用此入口：不升档，只加失败反应并报告原因。
            if (!SSCCosmeticUtility.ApplyResult(patient, false, SSCCosmeticUtility.SurgeryFailure)) return;
            string text = "SSC_Cosmetic_SurgeryFailure".Translate(patient.LabelShort);
            if (!reason.NullOrEmpty()) text += "\n" + reason;
            Messages.Message(text, patient, MessageTypeDefOf.NegativeEvent);
        }
    }
}
