// AI 辅助生成：仅1.7的终极成果只读网格与特化区紧凑测高。
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        private const float SpecializationListingSpacing = 2f;
        private const float FinalGridPadding = 6f;
        private const float FinalGridColumnGap = 8f;
        private const float FinalGridRowGap = 2f;
        private const float FinalGridTopGap = 6f;

        private static readonly SexSlaveSpecializationType[] FinalSpecializationDisplayOrder =
        {
            SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.Cow,
            SexSlaveSpecializationType.Combatant, SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog, SexSlaveSpecializationType.PetRabbit,
            SexSlaveSpecializationType.TrainerOfficer, SexSlaveSpecializationType.Fertility
        };

        /// <summary>按各方向现有终极记录生成名称及悬停说明；不把普通完成、历史进度或当前方向当成终极化。</summary>
        private static List<(SexSlaveSpecializationType Type, string Text, string Tip)> GetFinalSpecializationRows(Pawn pawn)
        {
            var rows = new List<(SexSlaveSpecializationType, string, string)>();
            foreach (SexSlaveSpecializationType type in FinalSpecializationDisplayOrder)
            {
                if (!IsTypeFinalized(pawn, type)) continue;
                // 仅复用已有生效／禁用规则；列表不为其他方向增设新的禁用门槛。
                bool active = true;
                string tip = null;
                if (type == SexSlaveSpecializationType.TrainerOfficer)
                {
                    active = TrainerSpecializationUtility.HasActiveFinalEffect(pawn);
                    tip = active ? "SSC_TrainerIdentity_RestrictionTip".Translate().ToString()
                        : GetTrainerFinalDisabledReason(pawn);
                }
                else if (type == SexSlaveSpecializationType.Fertility)
                {
                    active = FertilitySpecializationUtility.MeetsConditions(pawn, out string reason);
                    tip = active ? "SSC_Fertility_Tip".Translate().ToString() : reason;
                }
                string status = (active ? "SSC_SpecializationStatus_Finalized"
                    : "SSC_SpecializationStatus_FinalDisabled").Translate();
                rows.Add((type, GetSpecializationName(type), string.IsNullOrEmpty(tip) ? status : status + "\n\n" + tip));
            }
            return rows;
        }

        /// <summary>按实际内容计入特化标题、选择按钮、进度及可见附加项，取消固定底部余量。</summary>
        private static float GetCompactSpecializationSectionHeight(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            GameFont previousFont = Text.Font;
            bool previousWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                float innerWidth = Mathf.Max(1f, width - SectionPadding * 2f);
                float height = SectionPadding * 2f + SectionTitleHeight + 30f + SpecializationListingSpacing + 6f;
                height += Text.CalcHeight(Strings.ITab_SpecializationProgress(GetSpecializationProgressText(pawn, comp)), innerWidth)
                    + SpecializationListingSpacing;
                string trainer = GetOrdinaryTrainerStatusText(pawn);
                if (trainer != null) height += Text.CalcHeight(trainer, innerWidth) + SpecializationListingSpacing;
                if (comp.IsPetRabbitSpecialized || PetSpecializationUtility.HasAnyPetState(pawn, SexSlaveSpecializationType.PetRabbit))
                {
                    string rabbit = Strings.ITab_RabbitReproductionMode(GetRabbitReproductionModeLabel(comp.rabbitReproductionMode))
                        + " " + Strings.ITab_SpecializationUnfinishedSuffix;
                    height += Text.CalcHeight(rabbit, innerWidth) + SpecializationListingSpacing;
                }
                return height + BuildFinalSpecializationGrid(GetFinalSpecializationRows(pawn), innerWidth).Height;
            }
            finally { Text.Font = previousFont; Text.WordWrap = previousWrap; }
        }

        /// <summary>普通训导官保留原任职状态行，终极成果只进入下方名单，绘制与测高共用此查询。</summary>
        private static string GetOrdinaryTrainerStatusText(Pawn pawn)
        {
            var state = TrainerSpecializationUtility.GetDisplayState(pawn);
            return state == TrainerSpecializationDisplayState.None || TrainerSpecializationUtility.HasFinalRecord(pawn)
                ? null : "SSC_TrainerIdentity_StatusLine".Translate(("SSC_TrainerIdentity_Status_" + state).Translate()).ToString();
        }

        /// <summary>返回标题及名单框的总高和各名称相对矩形；绘制与滚动测高使用同一份1至3列布局。</summary>
        private static (float Height, float HeaderHeight, List<Rect> Cells) BuildFinalSpecializationGrid(
            List<(SexSlaveSpecializationType Type, string Text, string Tip)> rows, float width)
        {
            var cells = new List<Rect>();
            if (rows.Count == 0) return (0f, 0f, cells);
            GameFont previousFont = Text.Font;
            bool previousWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                float contentWidth = Mathf.Max(1f, width - FinalGridPadding * 2f);
                int columns = Mathf.Min(rows.Count, Mathf.Clamp(Mathf.FloorToInt((contentWidth + FinalGridColumnGap) / 88f), 1, 3));
                float cellWidth = (contentWidth - (columns - 1) * FinalGridColumnGap) / columns;
                float headerHeight = Mathf.Max(26f, Text.CalcHeight("SSC_SpecializationStatus_Header".Translate(), Mathf.Max(1f, width)) + 4f);
                float y = FinalGridTopGap + headerHeight + FinalGridPadding;
                for (int i = 0; i < rows.Count; i += columns)
                {
                    float rowHeight = Text.LineHeight;
                    for (int c = 0; c < columns && i + c < rows.Count; c++)
                        rowHeight = Mathf.Max(rowHeight, Text.CalcHeight(rows[i + c].Text, cellWidth));
                    for (int c = 0; c < columns && i + c < rows.Count; c++)
                        cells.Add(new Rect(FinalGridPadding + c * (cellWidth + FinalGridColumnGap), y, cellWidth, rowHeight));
                    y += rowHeight + FinalGridRowGap;
                }
                return (y - FinalGridRowGap + FinalGridPadding, headerHeight, cells);
            }
            finally { Text.Font = previousFont; Text.WordWrap = previousWrap; }
        }

        /// <summary>居中标题栏下的框内只排特化名称；每项保留独立悬停，退出时恢复所有改动的GUI状态。</summary>
        private static void DrawFinalSpecializationStates(Listing_Standard listing, Pawn pawn)
        {
            var rows = GetFinalSpecializationRows(pawn);
            var layout = BuildFinalSpecializationGrid(rows, listing.ColumnWidth);
            if (layout.Height <= 0f) return;
            Rect block = listing.GetRect(layout.Height);
            var header = new Rect(block.x, block.y + FinalGridTopGap, block.width, layout.HeaderHeight);
            var body = new Rect(block.x, header.yMax, block.width, block.yMax - header.yMax);
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.DrawMenuSection(header);
                Widgets.Label(header, "SSC_SpecializationStatus_Header".Translate());
                Widgets.DrawMenuSection(body);
                for (int i = 0; i < rows.Count; i++)
                {
                    Rect cell = layout.Cells[i];
                    cell.x += block.x; cell.y += block.y;
                    Widgets.Label(cell, rows[i].Text);
                    TooltipHandler.TipRegion(cell, rows[i].Tip);
                }
            }
            finally { Text.Font = previousFont; Text.Anchor = previousAnchor; Text.WordWrap = previousWrap; }
        }
    }
}
