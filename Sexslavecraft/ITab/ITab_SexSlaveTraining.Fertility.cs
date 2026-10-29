// AI 辅助生成
using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>菜单捕获原组件与人格，提交时复查，不使旧菜单授权换入的人格。</summary>
        private static FloatMenuOption BuildFertilitySpecializationOption(Pawn pawn, CompSexSlaveTraining comp)
        {
            bool available = FertilitySpecializationUtility.CanSelect(pawn, out string reason);
            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            string label = "SSC_Fertility_Label".Translate();
            if (!available && !string.IsNullOrEmpty(reason)) label += " (" + reason + ")";
            return new FloatMenuOption(label, available ? (Action)(() =>
            {
                if (pawn.TryGetComp<CompSexSlaveTraining>() != comp || !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) ||
                    !FertilitySpecializationUtility.CanSelect(pawn, out _)) return;
                comp.SetSpecialization(SexSlaveSpecializationType.Fertility);
            }) : null);
        }
        /// <summary>绘制与测高共用同一状态文字；终极在其他方向也单独展示。</summary>
        private static string GetFertilityStatusText(Pawn pawn, CompSexSlaveTraining comp)
        {
            bool final = FertilitySpecializationUtility.HasFinalState(pawn);
            if (!final && comp.specializationType != SexSlaveSpecializationType.Fertility) return null;
            return FertilitySpecializationUtility.MeetsConditions(pawn, out string reason)
                ? "SSC_Fertility_Status".Translate(final ? "SSC_Fertility_Finalized".Translate() : comp.specializationProgress.ToString("P1"),
                    FertilitySpecializationUtility.CareFactor(pawn).ToString("0.00")).ToString()
                : "SSC_Fertility_Paused".Translate(reason).ToString();
        }

        /// <summary>按本次栏宽和实际译文预留状态行高度，防止滚到下一列或覆盖下一区域。</summary>
        private static float GetFertilityStatusHeight(Pawn pawn, CompSexSlaveTraining comp, float innerWidth)
        {
            string text = GetFertilityStatusText(pawn, comp);
            if (text == null) return 0f;
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                // 额外4像素覆盖Listing默认行间距；长度测量与实际Label使用同一正文。
                return Mathf.Max(Text.LineHeight, Text.CalcHeight(text, Mathf.Max(1f, innerWidth))) + 4f;
            }
            finally { Text.Font = previousFont; }
        }

        /// <summary>只读呈现收益或暂停原因；查询不创建健康状态或推进培养。</summary>
        private static void DrawFertilityState(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            string text = GetFertilityStatusText(pawn, comp);
            if (text != null) listing.Label((TaggedString)text, -1f, "SSC_Fertility_Tip".Translate().ToString());
        }
    }
}
