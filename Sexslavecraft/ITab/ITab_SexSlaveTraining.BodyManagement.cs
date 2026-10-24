// AI 辅助生成：当前身体的泌乳与子宫开发管理，独立于 SSC 身份及训练研究。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        private const float BodyRowGap = 6f;
        private const float BodyChildIndent = 16f;
        private const float BodyGroupGap = 10f;
        private enum BodyControl { None, Milk, Fertility }

        /// <summary>一行的文字、控件状态与测量结果；不跨 GUI 事件缓存。</summary>
        private sealed class BodyManagementRow
        {
            internal string Label, Tip;
            internal BodyControl Control;
            internal bool Enabled, CanEdit;
            internal float Height, Indent, GapBefore;
        }

        /// <summary>同一次测量和绘制共享的身体引用；点击前必须再次确认引用仍然有效。</summary>
        private sealed class BodyManagementView
        {
            internal Pawn Pawn;
            internal CompSexSlaveTraining Comp;
            internal Hediff Milk;
            internal Hediff_SSCUterusDevelopment Uterus;
            internal string Title, ChangedTip;
            internal readonly List<BodyManagementRow> Rows = new List<BodyManagementRow>();
            internal float Height, TitleHeight;
        }

        /// <summary>只读取得当前身体条目；没有对应 Hediff 时不产生空栏或补建开发状态。</summary>
        private static BodyManagementView BuildBodyManagementView(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            var view = new BodyManagementView { Pawn = pawn, Comp = comp };
            if (pawn?.health?.hediffSet == null || comp == null) return view;
            view.Milk = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState);
            view.Uterus = SSCUterusDevelopmentUtility.Find(pawn);
            if (view.Milk == null && view.Uterus == null) return view;

            // 测量时和绘制时使用同样的字体、换行及复选框预留宽度。
            GameFont font = Text.Font;
            bool wrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                float innerWidth = Mathf.Max(1f, width - SectionPadding * 2f);
                // 标题及提示与行文案一起翻译并保存在本次视图，测量和绘制不会读取不同文本。
                view.Title = "SSC_BodyManagement_Title".Translate();
                view.ChangedTip = "SSC_BodyManagement_BodyChanged".Translate();
                view.TitleHeight = Mathf.Max(SectionTitleHeight, Text.CalcHeight(view.Title, innerWidth));
                if (view.Milk != null)
                {
                    // 泌乳操作沿用已有四语翻译键；只为新增说明建立占位键。
                    AddBodyRow(view, innerWidth, Strings.ITab_MilkProductionToggle, "SSC_BodyManagement_MilkTip".Translate(),
                        BodyControl.Milk, comp.milkProductionEnabled, true);
                    string progress = view.Milk.TryGetComp<HediffComp_PermanentLactating>()?.CompLabelInBracketsExtra;
                    // 充盈度是泌乳开关的从属信息，缩进显示，不与下一项独立开关混排。
                    if (!string.IsNullOrEmpty(progress)) AddBodyRow(view, innerWidth, progress, indent: BodyChildIndent);
                }
                if (view.Uterus != null)
                {
                    // 身体管理只提供开关；名称、开发进度、阶段、收益和生育状态由健康页展示。
                    bool enabled = view.Uterus.FertilityEnabled;
                    bool available = SSCUterusDevelopmentUtility.CanSetFertilityEnabled(pawn, !enabled, out string reason);
                    AddBodyRow(view, innerWidth, "SSC_BodyManagement_FertilityToggle".Translate(), reason ?? "SSC_BodyManagement_FertilityTip".Translate(),
                        BodyControl.Fertility, enabled, available, gapBefore: view.Milk != null ? BodyGroupGap : 0f);
                }
                view.Height = SectionPadding * 2f + view.TitleHeight + view.Rows.Sum(row => row.Height + row.GapBefore)
                    + BodyRowGap * Math.Max(0, view.Rows.Count - 1);
            }
            finally { Text.Font = font; Text.WordWrap = wrap; }
            return view;
        }

        /// <summary>按缩进后的实际宽度测量文字，行高与组间留白同时计入视图；复选框另留控件宽度。</summary>
        private static void AddBodyRow(BodyManagementView view, float width, string label, string tip = null,
            BodyControl control = BodyControl.None, bool enabled = false, bool canEdit = false, float indent = 0f, float gapBefore = 0f)
        {
            view.Rows.Add(new BodyManagementRow
            {
                Label = label, Tip = tip, Control = control, Enabled = enabled, CanEdit = canEdit,
                Indent = indent, GapBefore = gapBefore,
                Height = Mathf.Max(control == BodyControl.None ? Text.LineHeight : 24f,
                    Text.CalcHeight(label, Mathf.Max(1f, width - indent - (control == BodyControl.None ? 0f : 24f))))
            });
        }

        /// <summary>绘制一次共享栏并归还全部 GUI 状态；栏内所有项目沿外层滚动布局排列。</summary>
        private void DrawBodyManagementSection(Rect rect, BodyManagementView view)
        {
            if (view.Height <= 0f) return;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            bool wrap = Text.WordWrap, enabled = GUI.enabled;
            Color color = GUI.color;
            try
            {
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Widgets.DrawMenuSection(rect);
                Rect inner = rect.ContractedBy(SectionPadding);
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, view.TitleHeight), view.Title);
                float y = inner.y + view.TitleHeight;
                foreach (var row in view.Rows)
                {
                    // 独立开关之间增加组间留白，充盈度的缩进与测量保持同一宽度。
                    y += row.GapBefore;
                    Rect line = new Rect(inner.x + row.Indent, y, inner.width - row.Indent, row.Height);
                    if (row.Control == BodyControl.None)
                    {
                        Widgets.Label(line, row.Label);
                        if (!string.IsNullOrEmpty(row.Tip)) TooltipHandler.TipRegion(line, row.Tip);
                    }
                    else
                    {
                        bool current = IsBodyViewCurrent(view, row.Control);
                        bool next = SSCUIWidgets.Checkbox(line, row.Label, row.Enabled, row.CanEdit && current,
                            current ? row.Tip : view.ChangedTip);
                        // 禁用、原身体离体或切换选中角色后均不写回快照值。
                        if (next != row.Enabled && enabled && row.CanEdit && IsBodyViewCurrent(view, row.Control))
                        {
                            if (row.Control == BodyControl.Milk) view.Comp.milkProductionEnabled = next;
                            else if (!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(view.Pawn, next, out string reason))
                                Messages.Message(reason, view.Pawn, MessageTypeDefOf.RejectInput, false);
                        }
                    }
                    y += row.Height + BodyRowGap;
                }
            }
            finally
            {
                Text.Font = font; Text.Anchor = anchor; Text.WordWrap = wrap;
                GUI.enabled = enabled; GUI.color = color;
            }
        }

        /// <summary>只允许本次选中身体及原组件／Hediff 接受点击，不重新进行身份或绑定准入。</summary>
        private bool IsBodyViewCurrent(BodyManagementView view, BodyControl control)
        {
            if (SelPawn != view.Pawn || view.Pawn?.GetComp<CompSexSlaveTraining>() != view.Comp) return false;
            if (control == BodyControl.Fertility) return SSCUterusDevelopmentUtility.Find(view.Pawn) == view.Uterus;
            return view.Milk != null && view.Pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState) == view.Milk;
        }
    }
}
