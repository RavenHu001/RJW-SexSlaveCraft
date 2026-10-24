// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        private const float AbilityToggleRowGap = 12f;
        // 测量与绘制共用本次 GUI 快照；保存原 Pawn/组件/token，提交时拒绝迟到点击。
        private sealed class AbilityToggleRow
        {
            public Pawn Pawn;
            public CompSexSlaveTraining Comp;
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
            public object PersonalityToken;
#endif
            public string Id, Label, Tip;
            public bool Enabled, CanEdit;
            public float Height;
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
            public SSCAbilityToggleContext Context;
            public string Detail, ChangeReason;
#endif
        }

        /// <summary>按展示分组为主人、性奴、身体和外观区域形成当前 GUI 事件的只读行，不授予编辑资格。</summary>
        private static List<AbilityToggleRow> BuildAbilityToggleRows(Pawn pawn, CompSexSlaveTraining comp,
            SSCAbilityToggleGroup group, float innerWidth)
        {
            var rows = new List<AbilityToggleRow>();
            GameFont oldFont = Text.Font;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                // 按组复用同一行模板，并给复选框留宽度；只读测量不创建成长或默认配置。
                foreach (SSCAbilityToggleDefinition definition in SSCAbilityToggleUtility.Definitions
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
                    // 稳定排序保留同序项的注册顺序，分伤与恩赐继续使用各自定义的显示位置。
                    .OrderBy(d => d.Order)
#endif
                    )
                {
                    if (definition.Group != group) continue;
                    string label = definition.LabelKey.Translate();
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
                    var state = SSCAbilityToggleUtility.GetView(pawn, definition.Id);
                    if (!state.Visible) continue;
                    // 不能编辑时优先解释拒绝原因；允许编辑但已开启未生效时解释暂停条件。
                    string tip = definition.TipRule == null ? definition.TipKey.Translate().ToString() : definition.TipRule(pawn);
                    string reason = !state.CanChange ? state.ChangeReason : state.Enabled && !state.Active ? state.InactiveReason : null;
                    if (!string.IsNullOrEmpty(reason)) tip += "\n\n" + reason;
                    // 同一次状态、提示与从属文字供各区域测量／绘制；提交仍只携带原捕获上下文。
                    rows.Add(new AbilityToggleRow
                    {
                        Pawn = pawn, Comp = comp, Id = definition.Id, Label = label, Tip = tip,
                        Enabled = state.Enabled, CanEdit = state.CanChange, Context = state.Context,
                        Detail = definition.DetailRule?.Invoke(pawn), ChangeReason = state.ChangeReason,
                        Height = Mathf.Max(24f, Text.CalcHeight(label, Mathf.Max(1f, innerWidth - 24f)))
                    });
#else
                    rows.Add(new AbilityToggleRow
                    {
                        Pawn = pawn, Comp = comp, PersonalityToken = SSCPersonalityRuntimeUtility.Capture(pawn),
                        Id = definition.Id, Label = label, Tip = definition.TipKey.Translate(),
                        Enabled = SSCAbilityToggleUtility.GetEnabled(pawn, definition.Id),
                        CanEdit = SSCAbilityToggleUtility.CanEdit(pawn, definition.Id),
                        Height = Mathf.Max(24f, Text.CalcHeight(label, Mathf.Max(1f, innerWidth - 24f)))
                    });
#endif
                }
            }
            finally { Text.Font = oldFont; Text.WordWrap = oldWrap; }
            return rows;
        }

        /// <summary>由同一次测量所得行高累计区块高度，空分组不占空间或生成空框。</summary>
        private static float AbilityToggleSectionHeight(List<AbilityToggleRow> rows)
        {
            // 当前没有 Slave 能力时高度为零，保留原性奴面板的布局与滚动范围。
            if (rows == null || rows.Count == 0) return 0f;
            float height = SectionPadding * 2f;
            foreach (AbilityToggleRow row in rows) height += AbilityToggleRowGap + row.Height;
            return height;
        }

        /// <summary>绘制独立能力区块并复用共用行，正常与异常路径均恢复进入时的全局 GUI 状态。</summary>
        private void DrawAbilityToggleSection(Rect rect, List<AbilityToggleRow> rows)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap, oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Widgets.DrawMenuSection(rect);
                Rect inner = rect.ContractedBy(SectionPadding);
                float y = inner.y;
                DrawAbilityToggleRows(inner, ref y, rows, oldEnabled);
            }
            finally
            {
                // 公共绘制器必须归还全部 GUI 状态，避免后续原面板继承禁用、颜色或换行设置。
                Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap;
                GUI.enabled = oldEnabled; GUI.color = oldColor;
            }
        }

        /// <summary>只按快照绘制并提交真实切换请求；框架复查归属与权限，绘制器不直接写任何保存载体。</summary>
        private void DrawAbilityToggleRows(Rect inner, ref float y, List<AbilityToggleRow> rows, bool outerEnabled)
        {
            if (rows == null) return;
            foreach (AbilityToggleRow row in rows)
            {
                y += AbilityToggleRowGap;
                Rect rect = new Rect(inner.x, y, inner.width, row.Height);
                bool enabled = row.Enabled;
                // 外层禁用与行资格同时约束实际 Checkbox 输入，不只改变视觉样式。
                GUI.enabled = outerEnabled && row.CanEdit;
                Widgets.CheckboxLabeled(rect, row.Label, ref enabled, !GUI.enabled);
                TooltipHandler.TipRegion(rect, row.Tip);
                // 行快照仅用于显示；真正提交交给通用入口再查当前权限与人格归属。
                if (GUI.enabled && enabled != row.Enabled && SelPawn == row.Pawn)
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
                    TrySetAbilityToggleRow(row, enabled, out _);
#else
                    SSCAbilityToggleUtility.TrySetEnabled(row.Pawn, row.Id, enabled, row.Comp, row.PersonalityToken);
#endif
                y += row.Height;
            }
        }
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
        /// <summary>所有显示区域共用提交；角色选择变化后不把旧行请求交给新选中角色。</summary>
        private bool TrySetAbilityToggleRow(AbilityToggleRow row, bool enabled, out string reason)
        {
            // 显示区域不重新捕获身体或人格；切换选中对象后直接拒绝这次旧行请求。
            if (SelPawn != row.Pawn)
            { reason = SSCAbilityToggleUtility.Reason("Changed"); return false; }
            return SSCAbilityToggleUtility.TrySet(row.Pawn, row.Id, enabled, row.Context, out reason);
        }
#endif
    }
}
