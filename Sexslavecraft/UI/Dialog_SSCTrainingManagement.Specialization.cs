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

        /// <summary>分开呈现当前培养方向和已取得成果；测量与绘制使用同一批行。</summary>
        private void DrawSpecialization(Rect page)
        {
            Widgets.DrawBoxSolid(page, PanelColor);
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            float width = Mathf.Max(1f, viewport.width - 16f);
            float indent = Mathf.Min(16f, width * .12f);
            var edit = new PageEdit(source, comp, currentPage, contextRevision);
            var direction = edit.Comp.specializationType;
            var rows = new List<(float Height, Action<Rect> Draw)>();

            // 当前方向和进度一起读取，空方向不会借用历史成果生成进度。
            AddLabel("<b>" + "SSC_TrainingManagement_CurrentCultivation".Translate() + "</b>");
            string directionLabel = ITab_SexSlaveTraining.GetSpecializationLabel(edit.Pawn, edit.Comp);
            rows.Add((Mathf.Max(34f, Text.LineHeight + 8f), rect =>
            {
                var control = new Rect(rect.x + indent, rect.y, Mathf.Max(1f, rect.width - indent), rect.height);
                if (DrawSettingsControl(control, directionLabel, dropdown: true)) OpenSpecializationMenu(edit, direction);
            }));
            if (direction != SexSlaveSpecializationType.None)
            {
                string progress = ITab_SexSlaveTraining.GetSpecializationProgressText(edit.Pawn, edit.Comp);
                AddLabel(Strings.ITab_SpecializationProgress(progress), indent,
                    ITab_SexSlaveTraining.GetSpecializationProgressTip(edit.Pawn, edit.Comp));
                string trainer = ITab_SexSlaveTraining.GetOrdinaryTrainerStatusText(edit.Pawn);
                if (trainer != null) AddLabel(trainer, indent, "SSC_TrainerIdentity_RestrictionTip".Translate());
            }

            // 即使当前未选方向，已有成果也来自实际终极标记并保留独立悬停说明。
            var finals = ITab_SexSlaveTraining.GetFinalSpecializationRows(edit.Pawn);
            if (finals.Count > 0)
            {
                rows.Add((8f, rect => Widgets.DrawLineHorizontal(rect.x, rect.y + 3f, rect.width, DividerColor)));
                AddLabel("<b>" + "SSC_TrainingManagement_FinalAchievements".Translate() + "</b>");
                AddFinalRows(finals);
            }

            // 在成果减少或换窄布局后先约束滚动位置，避免视口停在内容末尾以外。
            const float rowGap = 4f;
            float total = 0f;
            foreach (var row in rows) total += row.Height + rowGap;
            specializationScroll.y = Mathf.Clamp(specializationScroll.y, 0f, Mathf.Max(0f, total - viewport.height));
            Widgets.BeginScrollView(viewport, ref specializationScroll, new Rect(0f, 0f, width, Mathf.Max(total, viewport.height)));
            try
            {
                float y = 0f;
                foreach (var row in rows)
                {
                    row.Draw(new Rect(0f, y, width, row.Height));
                    y += row.Height + rowGap;
                }
            }
            finally { Widgets.EndScrollView(); }

            // 说明按当前字体换行并保留整行悬停区域。
            void AddLabel(string label, float offset = 0f, string tip = null)
            {
                float textWidth = Mathf.Max(1f, width - offset);
                rows.Add((Text.CalcHeight(label, textWidth), rect =>
                {
                    var line = new Rect(rect.x + offset, rect.y, textWidth, rect.height);
                    Widgets.Label(line, label);
                    if (tip != null) TooltipHandler.TipRegion(line, tip);
                }));
            }

            // 每行以最高的译名决定行高，宽窗最多三列，窄窗按可用宽度换行。
            void AddFinalRows(List<(SexSlaveSpecializationType Type, string Text, string Tip)> entries)
            {
                const float columnGap = 8f;
                float available = width - indent;
                int columns = Math.Min(entries.Count, Math.Max(1, Math.Min(3, (int)((available + columnGap) / 158f))));
                float cellWidth = Mathf.Max(1f, (available - (columns - 1) * columnGap) / columns);
                for (int first = 0; first < entries.Count; first += columns)
                {
                    int start = first;
                    int count = Math.Min(columns, entries.Count - start);
                    float height = Text.LineHeight;
                    for (int i = 0; i < count; i++)
                        height = Mathf.Max(height, Text.CalcHeight(entries[start + i].Text, cellWidth));

                    rows.Add((height, rect =>
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
