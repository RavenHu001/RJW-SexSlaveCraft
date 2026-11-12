// AI 辅助生成
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static SexSlaveCraft.SSCTrainingWindowTheme;

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
            // 普通训导官的说明跟随方向控件，终极记录继续由成果卡片解释。
            string directionTip = direction == SexSlaveSpecializationType.TrainerOfficer &&
                !TrainerSpecializationUtility.HasFinalRecord(edit.Pawn)
                    ? "SSC_TrainerIdentity_RestrictionTip".Translate().ToString() : null;
            cultivation.Rows.Add((Mathf.Max(34f, Text.LineHeight + 8f), rect =>
            {
                var control = InsetSettingsRow(rect, cultivation.Indent);
                if (DrawSettingsControl(control, directionLabel, dropdown: true, tooltip: directionTip)) OpenSpecializationMenu(edit, direction);
            }));
            if (direction != SexSlaveSpecializationType.None)
            {
                string progress = ITab_SexSlaveTraining.GetSpecializationProgressText(edit.Pawn, edit.Comp);
                cultivation.AddLabel(Strings.ITab_SpecializationProgress(progress), cultivation.Indent,
                    ITab_SexSlaveTraining.GetSpecializationProgressTip(edit.Pawn, edit.Comp));
            }

            // 即使当前未选方向，已有成果也来自实际终极标记并保留独立悬停说明。
            var panels = new List<(float Height, Action<float> Draw)> { LayoutSettingsPanelRow(cultivation) };
            var finals = ITab_SexSlaveTraining.GetFinalSpecializationCards(edit.Pawn);
            if (finals.Count > 0)
            {
                var achievements = new SettingsColumn(width, panel: true);
                achievements.AddLabel("<b>" + "SSC_TrainingManagement_FinalAchievements".Translate() + "</b>");
                AddFinalCards(achievements, finals);
                panels.Add(LayoutSettingsPanelRow(achievements));
            }
            DrawSettingsPanels(viewport, width, panels, ref specializationScroll);
        }

        /// <summary>每项成果独占一张只读卡。按可用宽度换行，卡右侧预留独立开关位。</summary>
        private static void AddFinalCards(SettingsColumn column,
            List<(SexSlaveSpecializationType Type, string Name, string Status, bool Active, string Tip)> entries)
        {
            const float columnGap = 8f;
            const float padding = 8f;
            float available = column.Width;
            int columns = Math.Max(1, Math.Min(3, (int)((available + columnGap) / 178f)));
            float cellWidth = Mathf.Max(1f, (available - (columns - 1) * columnGap) / columns);
            // 给未来开关留右侧空间，窄卡按比例缩减留白。现阶段整卡只有悬浮提示。
            float controlSpace = Mathf.Min(32f, cellWidth * .2f);
            float textWidth = Mathf.Max(1f, cellWidth - 2f * padding - controlSpace);
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            GameFont nameFont = Text.Font;
            Text.WordWrap = true;
            for (int first = 0; first < entries.Count; first += columns)
            {
                int start = first;
                int count = Math.Min(columns, entries.Count - start);
                // 名称和状态各用实际字号测量。同一行按最高的卡片分配高度。
                float height = 0f;
                for (int i = 0; i < count; i++)
                {
                    var entry = entries[start + i];
                    Text.Font = nameFont;
                    float nameHeight = Text.CalcHeight(entry.Name, textWidth);
                    Text.Font = GameFont.Small;
                    height = Mathf.Max(height, 2f * padding + nameHeight + Text.CalcHeight(entry.Status, textWidth));
                }

                column.Rows.Add((height, rect =>
                {
                    using var drawScope = new SSCBondOverviewWidgets.GuiScope(false);
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.WordWrap = true;
                    for (int i = 0; i < count; i++)
                    {
                        var entry = entries[start + i];
                        var cell = new Rect(rect.x + i * (cellWidth + columnGap), rect.y, cellWidth, rect.height);
                        Color accent = entry.Active ? CopperColor : DividerColor;
                        Widgets.DrawBoxSolid(cell, ControlColor);
                        Widgets.DrawBoxSolid(new Rect(cell.x, cell.y, Mathf.Min(2f, cell.width), cell.height), accent);
                        Text.Font = nameFont;
                        float nameHeight = Text.CalcHeight(entry.Name, textWidth);
                        Widgets.Label(new Rect(cell.x + padding, cell.y + padding, textWidth, nameHeight), entry.Name);
                        Text.Font = GameFont.Small;
                        using (new TextTint(entry.Active ? CopperColor : SecondaryText))
                            Widgets.Label(new Rect(cell.x + padding, cell.y + padding + nameHeight, textWidth,
                                cell.height - 2f * padding - nameHeight), entry.Status);
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
