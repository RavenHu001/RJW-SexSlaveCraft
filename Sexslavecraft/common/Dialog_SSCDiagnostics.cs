using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>同一设置窗口内展示概况和注册列表。两页和剪贴板报告均读取打开或刷新时捕获的快照。</summary>
    internal sealed class Dialog_SSCDiagnostics : Window
    {
        private SSCDiagnostics.Snapshot snapshot;
        private Vector2 scrollPosition;
        private bool copied;
#if SSC_RW17
        private bool showRegistrations;
        private SSCPatchState? filter;
        // 筛选后的行和按钮文字在快照或筛选条件改变时生成，避免绘制时重复枚举全部补丁。
        private string currentFilterLabel;
        private SSCDiagnostics.PatchRow[] rows;
        private float[] rowHeights;
        private float measuredWidth;
        private Vector2 patchScroll;
#endif

        public override Vector2 InitialSize => new Vector2(780f, 680f);

        public Dialog_SSCDiagnostics()
        {
            optionalTitle = "SSC_Diagnostics_Title".Translate();
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            Refresh();
        }

        /// <summary>刷新后保留当前页面和状态筛选。重新开始滚动，避免停留在新列表范围以外。</summary>
        private void Refresh()
        {
            snapshot = SSCDiagnostics.Capture();
            scrollPosition = Vector2.zero;
            copied = false;
#if SSC_RW17
            ApplyFilter(filter);
#endif
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float buttonWidth = (inRect.width - 12f) / 2f;
                if (Widgets.ButtonText(new Rect(0f, 0f, buttonWidth, 32f), "SSC_Diagnostics_Refresh".Translate()))
                    Refresh();
                if (Widgets.ButtonText(new Rect(buttonWidth + 12f, 0f, buttonWidth, 32f),
                    (copied ? "SSC_Diagnostics_Copied" : "SSC_Diagnostics_Copy").Translate()))
                {
                    // 始终复制整份报告。当前页面和筛选条件只影响窗口显示，便于汇报时保留其他注册项。
                    GUIUtility.systemCopyBuffer = snapshot.Report;
                    copied = true;
                }
#if SSC_RW17
                DrawPages(inRect);
#else
                // Reserve the standard close-button footer, even on a screen-clamped window.
                Rect viewport = new Rect(0f, 44f, inRect.width, Mathf.Max(1f, inRect.height - 108f));
                Widgets.LabelScrollable(viewport, snapshot.Report, ref scrollPosition, longLabel: true);
#endif
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
            }
        }

#if SSC_RW17
        private void DrawPages(Rect inRect)
        {
            float tabWidth = (inRect.width - 8f) / 2f;
            DrawTab(new Rect(0f, 42f, tabWidth, 30f), "SSC_Diagnostics_Overview", false);
            DrawTab(new Rect(tabWidth + 8f, 42f, tabWidth, 30f), "SSC_Diagnostics_PatchRegistration", true);
            float top = 82f;
            if (showRegistrations)
            {
                if (Widgets.ButtonText(new Rect(0f, top, Mathf.Min(320f, inRect.width), 30f), currentFilterLabel))
                    OpenFilter();
                top += 38f;
            }
            // 底部留给原版关闭按钮。切换页面分别保留两处滚动位置。
            var viewport = new Rect(0f, top, inRect.width, Mathf.Max(1f, inRect.height - top - 62f));
            if (showRegistrations) DrawRegistrations(viewport);
            else DrawOverview(viewport);
        }

        private void DrawTab(Rect rect, string key, bool registration)
        {
            Color oldColor = GUI.color;
            if (showRegistrations == registration) GUI.color = new Color(0.65f, 0.85f, 1f);
            try
            {
                if (Widgets.ButtonText(rect, key.Translate())) showRegistrations = registration;
            }
            finally { GUI.color = oldColor; }
        }

        private void DrawOverview(Rect viewport)
        {
            // 在实际正文宽度下测量分区，折行后的标题和正文都计入滚动高度。
            float width = Mathf.Max(1f, viewport.width - 20f);
            float textWidth = Mathf.Max(1f, width - 24f);
            float height = 0f;
            foreach (var section in snapshot.Overview)
                height += 46f + Text.CalcHeight(section.Title, textWidth) + Text.CalcHeight(section.Body, textWidth);
            Widgets.BeginScrollView(viewport, ref scrollPosition, new Rect(0f, 0f, width, height));
            try
            {
                float y = 0f;
                foreach (var section in snapshot.Overview)
                {
                    float titleHeight = Text.CalcHeight(section.Title, textWidth);
                    float bodyHeight = Text.CalcHeight(section.Body, textWidth);
                    float cardHeight = titleHeight + bodyHeight + 34f;
                    Widgets.DrawMenuSection(new Rect(0f, y, width, cardHeight));
                    Widgets.Label(new Rect(12f, y + 10f, textWidth, titleHeight), section.Title);
                    Widgets.DrawLineHorizontal(12f, y + titleHeight + 14f, textWidth);
                    Widgets.Label(new Rect(12f, y + titleHeight + 21f, textWidth, bodyHeight), section.Body);
                    y += cardHeight + 12f;
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private string FilterLabel(SSCPatchState? state)
        {
            // 所有选项均根据完整快照统计。切换到一个筛选项后，菜单仍能看到其他状态的数量。
            string label = (state.HasValue ? "SSC_Diagnostics_Patch" + state.Value : "SSC_Diagnostics_PatchAll").Translate();
            int count = state.HasValue ? snapshot.Registrations.Count(row => row.State == state.Value) : snapshot.Registrations.Length;
            return "SSC_Diagnostics_PatchFilter".Translate(label, count);
        }

        private void OpenFilter()
        {
            var options = new List<FloatMenuOption> { new FloatMenuOption(FilterLabel(null), () => ApplyFilter(null)) };
            foreach (SSCPatchState state in Enum.GetValues(typeof(SSCPatchState)))
                options.Add(new FloatMenuOption(FilterLabel(state), () => ApplyFilter(state)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyFilter(SSCPatchState? state)
        {
            filter = state;
            currentFilterLabel = FilterLabel(state);
            rows = state.HasValue ? snapshot.Registrations.Where(row => row.State == state.Value).ToArray() : snapshot.Registrations;
            rowHeights = null;
            patchScroll = Vector2.zero;
        }

        private void DrawRegistrations(Rect viewport)
        {
            if (rows.Length == 0)
            {
                // 只有全表为空才显示等待安装。某个筛选结果为空时，给出该筛选对应的空列表提示。
                Widgets.Label(viewport, (snapshot.Registrations.Length == 0 ?
                    "SSC_Diagnostics_PatchWaiting" : "SSC_Diagnostics_PatchEmpty").Translate());
                return;
            }
            float width = Mathf.Max(1f, viewport.width - 20f);
            float statusWidth = Mathf.Min(150f, width * 0.32f);
            float nameWidth = Mathf.Max(1f, width - statusWidth - 26f);
            float notesWidth = Mathf.Max(1f, width - 24f);
            // 状态与名称并排，跳过理由和目标占用整行。宽度变化时按当前语言重算折行高度。
            if (rowHeights == null || measuredWidth != width)
            {
                measuredWidth = width;
                rowHeights = rows.Select(row => 16f +
                    Mathf.Max(Text.CalcHeight(row.Label, nameWidth), Text.CalcHeight(row.Status, statusWidth)) +
                    (row.Notes.Length == 0 ? 0f : Text.CalcHeight(row.Notes, notesWidth) + 3f)).ToArray();
            }
            Widgets.BeginScrollView(viewport, ref patchScroll, new Rect(0f, 0f, width, rowHeights.Sum()));
            try
            {
                float y = 0f;
                for (int i = 0; i < rows.Length; i++)
                {
                    // 使用缓存高度保持滚动位置，只提交视口内的行给原版绘制。
                    float height = rowHeights[i];
                    if (y > patchScroll.y + viewport.height) break;
                    if (y + height >= patchScroll.y)
                    {
                        var row = rows[i];
                        if (i % 2 == 0) Widgets.DrawLightHighlight(new Rect(0f, y, width, height));
                        float headHeight = Mathf.Max(Text.CalcHeight(row.Label, nameWidth), Text.CalcHeight(row.Status, statusWidth));
                        Widgets.Label(new Rect(12f, y + 7f, nameWidth, headHeight), row.Label);
                        Widgets.Label(new Rect(width - statusWidth - 12f, y + 7f, statusWidth, headHeight), row.Status);
                        if (row.Notes.Length != 0)
                            Widgets.Label(new Rect(12f, y + headHeight + 10f, notesWidth, height - headHeight - 10f), row.Notes);
                    }
                    y += height;
                }
            }
            finally { Widgets.EndScrollView(); }
        }
#endif
    }
}
