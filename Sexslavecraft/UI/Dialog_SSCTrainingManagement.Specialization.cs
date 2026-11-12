// AI 辅助生成
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        // 每个页面维护自己的滚动位置，换角色时才将这里归顶。
        private Vector2 specializationScroll;

        /// <summary>当前培养在上，已有终极成果在下，两块面板共用一个滚动区。</summary>
        private void DrawSpecialization(Rect page)
        {
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            float width = Mathf.Max(1f, viewport.width - 16f);
            var edit = new PageEdit(source, comp, currentPage, contextRevision);
            var direction = edit.Comp.specializationType;
            var cultivation = new SettingsColumn(width, panel: true);

            // 当前方向和进度一起读取，空方向不会借用历史成果生成进度。
            cultivation.AddLabel("<b>" + "SSC_TrainingManagement_CurrentCultivation".Translate() + "</b>");
            string directionLabel = ITab_SexSlaveTraining.GetSpecializationLabel(edit.Pawn, edit.Comp);
            cultivation.Rows.Add((Mathf.Max(34f, Text.LineHeight + 8f), rect =>
            {
                var control = InsetSettingsRow(rect, cultivation.Indent);
                if (DrawSettingsControl(control, directionLabel, dropdown: true)) OpenSpecializationMenu(edit, direction);
            }));
            if (direction != SexSlaveSpecializationType.None)
            {
                string progress = ITab_SexSlaveTraining.GetSpecializationProgressText(edit.Pawn, edit.Comp);
                cultivation.AddLabel(Strings.ITab_SpecializationProgress(progress), cultivation.Indent,
                    ITab_SexSlaveTraining.GetSpecializationProgressTip(edit.Pawn, edit.Comp));
            }

            // 即使当前未选方向，已有成果也来自实际终极标记并保留独立悬停说明。
            var panels = new List<(float Height, Action<float> Draw)> { LayoutSettingsPanelRow(cultivation) };
            var finals = ITab_SexSlaveTraining.GetFinalSpecializationRows(edit.Pawn);
            if (finals.Count > 0)
            {
                var achievements = new SettingsColumn(width, panel: true);
                achievements.AddLabel("<b>" + "SSC_TrainingManagement_FinalAchievements".Translate() + "</b>");
                AddFinalRows(achievements, finals);
                panels.Add(LayoutSettingsPanelRow(achievements));
            }
            DrawSettingsPanels(viewport, width, panels, ref specializationScroll);
        }

        /// <summary>成果格按面板内宽排布，每行取最长译名的高度并保留各自的悬停。</summary>
        private static void AddFinalRows(SettingsColumn column, List<(SexSlaveSpecializationType Type, string Text, string Tip)> entries)
        {
            const float columnGap = 8f;
            float indent = column.Indent;
            float available = column.Width - indent;
            int columns = Math.Min(entries.Count, Math.Max(1, Math.Min(3, (int)((available + columnGap) / 158f))));
            float cellWidth = Mathf.Max(1f, (available - (columns - 1) * columnGap) / columns);
            for (int first = 0; first < entries.Count; first += columns)
            {
                int start = first;
                int count = Math.Min(columns, entries.Count - start);
                float height = Text.LineHeight;
                for (int i = 0; i < count; i++)
                    height = Mathf.Max(height, Text.CalcHeight(entries[start + i].Text, cellWidth));

                column.Rows.Add((height, rect =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var entry = entries[start + i];
                        var cell = new Rect(rect.x + indent + i * (cellWidth + columnGap), rect.y, cellWidth, rect.height);
                        Widgets.Label(cell, entry.Text);
                        TooltipHandler.TipRegion(cell, entry.Tip);
                    }
                }));
            }
        }

        /// <summary>由原选择逻辑复查目标方向，窗口额外保证菜单仍属于原角色、人格和方向。</summary>
        private void OpenSpecializationMenu(PageEdit edit, SexSlaveSpecializationType direction)
        {
            if (!PageEditIsCurrent(edit) || edit.Comp.specializationType != direction) return;
            // 选项捕获这个弹窗和打开时的方向，菜单被替换或外部改方向后原回调即失效。
            FloatMenu menu = null;
            var options = ITab_SexSlaveTraining.BuildSpecializationOptions(edit.Pawn, edit.Comp,
                () => EditPopupIsCurrent(menu, edit) && edit.Comp.specializationType == direction, RefreshAfterEdit);
            menu = new FloatMenu(options);
            ShowEditPopup(menu);
        }
    }
}
