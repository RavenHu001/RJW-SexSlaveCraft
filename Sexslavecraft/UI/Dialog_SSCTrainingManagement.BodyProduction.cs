// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private Vector2 bodyProductionScroll;
        private BodyProductionPress? bodyProductionPress;

        /// <summary>保存实际按下时的页面和能力凭据，松手不能借用下一帧的新载体。</summary>
        private readonly struct BodyProductionPress
        {
            internal readonly PageEdit Edit;
            internal readonly string Id;
            internal readonly bool Enabled;
            internal readonly SSCAbilityToggleContext Context;

            /// <summary>复用按下时视图中的保存值和原载体引用。</summary>
            internal BodyProductionPress(PageEdit edit, string id, SSCAbilityToggleView state)
            {
                Edit = edit;
                Id = id;
                Enabled = state.Enabled;
                Context = state.Context;
            }
        }

        /// <summary>从注册表读取本批的生产项；各自的可见、保存值和可编辑条件由原后端决定。</summary>
        private void DrawBodyProduction(Rect page)
        {
            Widgets.DrawBoxSolid(page, PanelColor);
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            var pawn = source;
            if (pawn == null || comp == null)
            {
                Widgets.Label(viewport, "SSC_TrainingManagement_SelectPawn".Translate());
                return;
            }

            float width = Mathf.Max(1f, viewport.width - 16f);
            float indent = Mathf.Min(16f, width * .12f);
            float innerWidth = Mathf.Max(1f, width - indent);
            const float gap = 6f;
            var edit = new PageEdit(pawn, comp, currentPage, contextRevision);
            var rows = new List<(string Id, SSCAbilityToggleView State, string Label, string Tip, string Detail, float Height, float DetailHeight)>();
            // 仅筛选本批已接入的生产项，按注册顺序布局。每项是否出现仍由原定义决定。
            foreach (var definition in SSCAbilityToggleUtility.Definitions.OrderBy(item => item.Order))
            {
                if (definition.Group != SSCAbilityToggleGroup.Body ||
                    definition.Id != SSCAbilityToggleUtility.MilkProduction && definition.Id != SSCAbilityToggleUtility.PnaProduction &&
                    definition.Id != SSCCorruptionGelProductionUtility.AbilityId) continue;

                var state = SSCAbilityToggleUtility.GetView(pawn, definition.Id);
                if (!state.Visible) continue;
                string label = definition.LabelKey.Translate();
                string tip = definition.TipRule == null ? definition.TipKey.Translate().ToString() : definition.TipRule(pawn);
                // 复选框显示保存选择；暂不生效或不可编辑的原因从同一份能力视图读取。
                string reason = !state.CanChange ? state.ChangeReason : state.Enabled && !state.Active ? state.InactiveReason : null;
                if (!string.IsNullOrEmpty(reason)) tip += "\n\n" + reason;
                string detail = definition.DetailRule?.Invoke(pawn);
                float height = Mathf.Max(24f, Text.CalcHeight(label, Mathf.Max(1f, width - 24f)));
                float detailHeight = string.IsNullOrEmpty(detail) ? 0f : Text.CalcHeight(detail, innerWidth);
                rows.Add((definition.Id, state, label, tip, detail, height, detailHeight));
            }

            if (rows.Count == 0)
            {
                Widgets.Label(viewport, "SSC_TrainingManagement_BodyProductionEmpty".Translate());
                return;
            }

            string title = "<b>" + "SSC_TrainingManagement_BodyProduction".Translate() + "</b>";
            float titleHeight = Text.CalcHeight(title, width);
            float heightNeeded = titleHeight + gap + rows.Sum(row => row.Height + row.DetailHeight + gap);
            // 测量和绘制使用同一批行，载体移除后先约束滚动范围，再绘制剩余条目。
            bodyProductionScroll.y = Mathf.Clamp(bodyProductionScroll.y, 0f, Mathf.Max(0f, heightNeeded - viewport.height));
            Widgets.BeginScrollView(viewport, ref bodyProductionScroll, new Rect(0f, 0f, width, Mathf.Max(heightNeeded, viewport.height)));
            try
            {
                Widgets.Label(new Rect(0f, 0f, width, titleHeight), title);
                float y = titleHeight + gap;
                foreach (var row in rows)
                {
                    DrawBodyProductionToggle(new Rect(0f, y, width, row.Height), edit, row.Id, row.State, row.Label, row.Tip);
                    y += row.Height;
                    if (row.DetailHeight > 0f)
                    {
                        var detailRect = new Rect(indent, y, innerWidth, row.DetailHeight);
                        Widgets.Label(detailRect, row.Detail);
                        TooltipHandler.TipRegion(detailRect, row.Tip);
                        y += row.DetailHeight;
                    }
                    y += gap;
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        /// <summary>按下时捕获原载体，复选框请求只通过能力框架提交。</summary>
        private void DrawBodyProductionToggle(Rect rect, PageEdit edit, string id, SSCAbilityToggleView state, string label, string tip)
        {
            var input = Event.current;
            bool canEdit = state.CanChange && GUI.enabled;
            // 原版复选框在松手时才返回新值，按下时先保留本行的原授权。
            if (canEdit && input.type == EventType.mouseDown && input.button == 0 && Mouse.IsOver(rect) &&
                PageEditIsCurrent(edit) && SSCAbilityToggleUtility.IsCurrent(edit.Pawn, id, state.Context))
                bodyProductionPress = new BodyProductionPress(edit, id, state);

            bool next = SSCUIWidgets.Checkbox(rect, label, state.Enabled, canEdit, tip);
            if (next == state.Enabled || !bodyProductionPress.HasValue) return;
            var press = bodyProductionPress.Value;
            bodyProductionPress = null;
            // 即便复选框的原生控件 ID 恰好被新行复用，也只接受按下时的行和值。
            if (press.Id != id || press.Enabled != state.Enabled || !PageEditIsCurrent(press.Edit)) return;
            if (!SSCAbilityToggleUtility.TrySet(press.Edit.Pawn, press.Id, next, press.Context, out string reason) &&
                !string.IsNullOrEmpty(reason))
                Messages.Message(reason, press.Edit.Pawn, MessageTypeDefOf.RejectInput, false);
        }
    }
}
