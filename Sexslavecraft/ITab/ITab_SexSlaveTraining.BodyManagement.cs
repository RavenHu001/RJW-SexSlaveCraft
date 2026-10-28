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

        /// <summary>一行的文字、控件状态与测量结果；不跨 GUI 事件缓存。</summary>
        private sealed class BodyManagementRow
        {
            internal string Label, Tip;
            internal AbilityToggleRow Toggle;
#if SSC_RW17
            internal OrgasmSensitivityRow Sensitivity;
#endif
            internal float Height, Indent, GapBefore;
        }

        /// <summary>同一次测量和绘制共享的身体引用；点击前必须再次确认引用仍然有效。</summary>
        private sealed class BodyManagementView
        {
            internal Pawn Pawn;
            internal CompSexSlaveTraining Comp;
            internal string Title, ChangedTip;
            internal readonly List<BodyManagementRow> Rows = new List<BodyManagementRow>();
            internal float Height, TitleHeight;
        }

        /// <summary>只读取得当前身体条目；没有对应 Hediff 时不产生空栏或补建开发状态。</summary>
        private static BodyManagementView BuildBodyManagementView(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            var view = new BodyManagementView { Pawn = pawn, Comp = comp };
            if (pawn?.health?.hediffSet == null || comp == null) return view;

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
                // 所有布尔条目按注册分组／顺序构建；从属信息由定义提供，不按能力 enum 分派。
                var toggles = BuildAbilityToggleRows(pawn, comp, SSCAbilityToggleGroup.Body, innerWidth);
                bool first = true;
                foreach (var toggle in toggles)
                {
                    // 生育禁用时仍优先显示原权威原因，独立项组间距与旧身体栏相同。
                    AddBodyRow(view, innerWidth, toggle.Label, toggle.ChangeReason ?? toggle.Tip,
                        toggle, gapBefore: first ? 0f : BodyGroupGap);
                    first = false;
                    // 充盈度继续作为缩进信息，不新增身体开发进度／收益展示。
                    if (!string.IsNullOrEmpty(toggle.Detail)) AddBodyRow(view, innerWidth, toggle.Detail, indent: BodyChildIndent);
                }
#if SSC_RW17
                // 枚举行独立于 bool 条目，必须追加后再判断整个身体区是否为空。
                AppendOrgasmSensitivityRows(view, innerWidth);
#endif
                // 无任何条目时保留零高度；提前返回也由 finally 归还字体与换行状态。
                if (view.Rows.Count == 0) return view;
                view.Height = SectionPadding * 2f + view.TitleHeight + view.Rows.Sum(row => row.Height + row.GapBefore)
                    + BodyRowGap * Math.Max(0, view.Rows.Count - 1);
            }
            finally { Text.Font = font; Text.WordWrap = wrap; }
            return view;
        }

        /// <summary>按缩进后的实际宽度测量文字，行高与组间留白同时计入视图；复选框另留控件宽度。</summary>
        private static void AddBodyRow(BodyManagementView view, float width, string label, string tip = null,
            AbilityToggleRow toggle = null, float indent = 0f, float gapBefore = 0f)
        {
            view.Rows.Add(new BodyManagementRow
            {
                Label = label, Tip = tip, Toggle = toggle,
                Indent = indent, GapBefore = gapBefore,
                Height = Mathf.Max(toggle == null ? Text.LineHeight : 24f,
                    Text.CalcHeight(label, Mathf.Max(1f, width - indent - (toggle == null ? 0f : 24f))))
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
#if SSC_RW17
                    if (row.Sensitivity != null) DrawOrgasmSensitivityRow(line, row);
                    else
#endif
                    if (row.Toggle == null)
                    {
                        Widgets.Label(line, row.Label);
                        if (!string.IsNullOrEmpty(row.Tip)) TooltipHandler.TipRegion(line, row.Tip);
                    }
                    else
                    {
                        var toggle = row.Toggle;
                        bool current = IsBodyViewCurrent(view, toggle);
                        bool next = SSCUIWidgets.Checkbox(line, row.Label, toggle.Enabled, toggle.CanEdit && current,
                            current ? row.Tip : view.ChangedTip);
                        // 禁用、原身体离体或切换选中角色后均不写回快照值。
                        if (next != toggle.Enabled && enabled && toggle.CanEdit && IsBodyViewCurrent(view, toggle))
                        {
                            // UI 禁用只是展示保护；公共提交重查目标值，并把原生育 setter 拒绝反馈给玩家。
                            if (!TrySetAbilityToggleRow(toggle, next, out string reason))
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
        private bool IsBodyViewCurrent(BodyManagementView view, AbilityToggleRow toggle)
        {
            if (SelPawn != view.Pawn || view.Pawn?.GetComp<CompSexSlaveTraining>() != view.Comp) return false;
            // 原 Comp 相同仍可能已换人格；各行还必须匹配原 token 及原泌乳／子宫 Hediff。
            return SSCAbilityToggleUtility.IsCurrent(view.Pawn, toggle.Id, toggle.Context);
        }
    }
}
