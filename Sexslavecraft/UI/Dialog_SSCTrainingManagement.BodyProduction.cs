// AI 辅助生成
using System;
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
        private AbilityTogglePress? abilityTogglePress;

        private const float SettingsColumnGap = 18f;
        private const float SettingsTwoColumnWidth = 560f;

        /// <summary>每列按可用宽度测量一遍，滚动高度和控件位置共用同一批行。</summary>
        private sealed class SettingsColumn
        {
            private const float RowGap = 4f;
            internal readonly float Width;
            internal readonly List<(float Height, Action<Rect> Draw)> Rows = new();
            internal float Indent => Mathf.Min(16f, Width * .12f);
            internal float Height => Rows.Sum(row => row.Height + RowGap);

            /// <summary>固定本次布局宽度，让测量和绘制共用同一基准。</summary>
            internal SettingsColumn(float width) => Width = width;

            /// <summary>按测量结果绘制本列，坐标以全页滚动内容为原点。</summary>
            internal void Draw(float x, float y = 0f)
            {
                foreach (var row in Rows)
                {
                    row.Draw(new Rect(x, y, Width, row.Height));
                    y += row.Height + RowGap;
                }
            }

            /// <summary>用缩进后的文字宽度测量换行，悬停说明跟随同一个文字区域。</summary>
            internal void AddLabel(string label, float indent = 0f, string tooltip = null)
                => Rows.Add((Text.CalcHeight(label, Mathf.Max(1f, Width - indent)), rect =>
                {
                    rect = InsetSettingsRow(rect, indent);
                    Widgets.Label(rect, label);
                    if (!string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(rect, tooltip);
                }));

            /// <summary>仅在本列已有模块时加入横线，双列之间由页面绘制竖线。</summary>
            internal void Divide()
            {
                if (Rows.Count != 0)
                    Rows.Add((8f, rect => Widgets.DrawLineHorizontal(rect.x, rect.y + 3f, rect.width, DividerColor)));
            }
        }

        /// <summary>保存实际按下时的页面和能力凭据，松手不能借用下一帧的新载体。</summary>
        private readonly struct AbilityTogglePress
        {
            internal readonly PageEdit Edit;
            internal readonly string Id;
            internal readonly bool Enabled;
            internal readonly SSCAbilityToggleContext Context;

            /// <summary>复用按下时视图中的保存值和原载体引用。</summary>
            internal AbilityTogglePress(PageEdit edit, string id, SSCAbilityToggleView state)
            {
                Edit = edit;
                Id = id;
                Enabled = state.Enabled;
                Context = state.Context;
            }
        }

        /// <summary>按实际出现的模块布局，宽窗两列共享一个滚动区域。</summary>
        private void DrawBodyAppearance(Rect page)
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
            var edit = new PageEdit(pawn, comp, currentPage, contextRevision);
            var production = new List<(SSCAbilityToggleDefinition Definition, SSCAbilityToggleView State)>();
            // 保留注册顺序及原后端的可见规则。
            foreach (var definition in SSCAbilityToggleUtility.Definitions.OrderBy(item => item.Order))
            {
                if (definition.Group != SSCAbilityToggleGroup.Body ||
                    definition.Id != SSCAbilityToggleUtility.MilkProduction && definition.Id != SSCAbilityToggleUtility.PnaProduction &&
                    definition.Id != SSCCorruptionGelProductionUtility.AbilityId) continue;

                var state = SSCAbilityToggleUtility.GetView(pawn, definition.Id);
                if (state.Visible) production.Add((definition, state));
            }

            // 可见性和控件凭据都从当前载体读取，失去敏感度资格后仍能查看已有记录。
            var preconception = SSCFertilityPreconceptionUtility.Capture(pawn);
            bool missingUterus = preconception.Owner == null && FertilitySpecializationUtility.HasFinalState(pawn);
            var sensitivity = SSCOrgasmSensitivityUtility.Capture(pawn);
            bool showSensitivity = SSCIdentityUtility.IsSexSlave(pawn) || sensitivity.Records.Length != 0;
            bool showFertility = preconception.Owner != null || missingUterus;
            var gelOwner = FullGelAppearanceUtility.GetOwner(pawn);
            var gelRendering = gelOwner == null ? default : SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.GelRendering);
            bool showGel = gelOwner != null && gelRendering.Visible;
            bool showLeft = production.Count != 0 || showGel;
            if (!showLeft && !showFertility && !showSensitivity)
            {
                Widgets.Label(viewport, "SSC_TrainingManagement_BodyEmpty".Translate());
                return;
            }

            // 窄窗共用同一列，只有一侧有模块时也让它占满宽度。
            bool twoColumns = width >= SettingsTwoColumnWidth && showLeft && (showFertility || showSensitivity);
            float leftWidth = twoColumns ? (width - SettingsColumnGap) * .44f : width;
            var left = new SettingsColumn(leftWidth);
            var settings = twoColumns ? new SettingsColumn(width - leftWidth - SettingsColumnGap) : left;
            if (production.Count != 0)
            {
                left.AddLabel("<b>" + "SSC_TrainingManagement_BodyProduction".Translate() + "</b>");
                foreach (var item in production)
                {
                    var definition = item.Definition;
                    string label = definition.LabelKey.Translate();
                    string tip = definition.TipRule == null ? definition.TipKey.Translate().ToString() : definition.TipRule(pawn);
                    AddAbilityToggle(left, edit, definition.Id, item.State, label, tip);
                    string detail = definition.DetailRule?.Invoke(pawn);
                    if (!string.IsNullOrEmpty(detail)) left.AddLabel(detail, left.Indent, AbilityToggleTip(item.State, tip));
                }
            }
            if (showGel)
            {
                left.Divide();
                AddGelAppearanceSection(left, edit, gelOwner, gelRendering);
            }
            if (showFertility)
            {
                settings.Divide();
                AddFertilitySection(settings, edit, preconception);
            }
            if (showSensitivity)
            {
                settings.Divide();
                AddSensitivitySection(settings, edit, sensitivity);
            }

            DrawSettingsColumns(viewport, width, left, settings, ref bodyProductionScroll);
        }

        /// <summary>让单列或双列共用一个滚动区域，内容缩短时收紧滚动范围。</summary>
        private static void DrawSettingsColumns(Rect viewport, float width, SettingsColumn left, SettingsColumn right,
            ref Vector2 scrollPosition)
        {
            float height = Mathf.Max(left.Height, right.Height);
            scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f, Mathf.Max(0f, height - viewport.height));
            Widgets.BeginScrollView(viewport, ref scrollPosition, new Rect(0f, 0f, width, Mathf.Max(height, viewport.height)));
            try
            {
                left.Draw(0f);
                if (!ReferenceEquals(left, right))
                {
                    Widgets.DrawBoxSolid(new Rect(left.Width + SettingsColumnGap / 2f, 0f, 1f, height), DividerColor);
                    right.Draw(left.Width + SettingsColumnGap);
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        /// <summary>使用注册项的标签及提示显示开关。</summary>
        private void AddAbilityToggle(SettingsColumn column, PageEdit edit, SSCAbilityToggleDefinition definition,
            SSCAbilityToggleView state, float indent)
            => AddAbilityToggle(column, edit, definition.Id, state, definition.LabelKey.Translate(),
                definition.TipRule == null ? definition.TipKey.Translate().ToString() : definition.TipRule(edit.Pawn), indent);

        /// <summary>复用能力开关的凭据及说明，行高为原版勾选框预留宽度。</summary>
        private void AddAbilityToggle(SettingsColumn column, PageEdit edit, string id, SSCAbilityToggleView state,
            string label, string tip, float indent = 0f)
        {
            float height = SettingsToggleHeight(label, column.Width - indent);
            column.Rows.Add((height, rect =>
                DrawAbilityToggle(InsetSettingsRow(rect, indent), edit, id, state, label, AbilityToggleTip(state, tip))));
        }

        /// <summary>让组内行缩进，保留与组标题一致的右边界。</summary>
        private static Rect InsetSettingsRow(Rect rect, float inset)
            => new Rect(rect.x + inset, rect.y, Mathf.Max(1f, rect.width - inset), rect.height);

        /// <summary>按扣除勾选框后的文字宽度计算行高。</summary>
        private static float SettingsToggleHeight(string label, float width)
            => Mathf.Max(24f, Text.CalcHeight(label, Mathf.Max(1f, width - 24f)));

        /// <summary>同一份能力视图给出当前锁定或暂不生效的原因。</summary>
        private static string AbilityToggleTip(SSCAbilityToggleView state, string tip)
        {
            string reason = !state.CanChange ? state.ChangeReason : state.Enabled && !state.Active ? state.InactiveReason : null;
            return string.IsNullOrEmpty(reason) ? tip : tip + "\n\n" + reason;
        }

        /// <summary>凝胶外观读取当前身体的保存值，渲染关闭时仍保留配色入口。</summary>
        private void AddGelAppearanceSection(SettingsColumn column, PageEdit edit, HediffWithComps owner,
            SSCAbilityToggleView rendering)
        {
            column.AddLabel("<b>" + "SSC_GelAppearance_Title".Translate() + "</b>");
            float indent = column.Indent;
            AddAbilityToggle(column, edit, SSCAbilityToggleUtility.Find(SSCAbilityToggleUtility.GelRendering), rendering, indent);

            var color = FullGelAppearanceUtility.Read(owner).Color;
            string opacity = "SSC_GelAppearance_Opacity".Translate() + " " + Mathf.RoundToInt(color.a * 100f) + "%";
            // 透明度文字可换行，棋盘色块固定高度，以免窄列或长译文截断百分比。
            const float swatchGap = 8f;
            float swatchWidth = Mathf.Min(54f, (column.Width - indent) * .25f);
            float height = Mathf.Max(24f, Text.CalcHeight(opacity, Mathf.Max(1f, column.Width - indent - swatchWidth - swatchGap)));
            column.Rows.Add((height, rect =>
            {
                rect = InsetSettingsRow(rect, indent);
                SSCUIWidgets.Swatch(new Rect(rect.x, rect.y + (rect.height - 24f) / 2f, swatchWidth, 24f), color);
                Widgets.Label(InsetSettingsRow(rect, swatchWidth + swatchGap), opacity);
            }));

            // 与开关分别提交：按钮按下时固定原身体及其外观组件，渲染状态不参与配色资格。
            var appearance = owner.TryGetComp<HediffComp_FullGelAppearance>();
            const string editKey = "SSC_GelAppearance_Edit";
            string editLabel = editKey.Translate();
            column.Rows.Add((Mathf.Max(32f, Text.LineHeight + 6f), rect =>
            {
                rect = InsetSettingsRow(rect, indent);
                var submit = PreparePageAction(edit, rect, editKey, OpenAppearance);
                if (DrawSettingsControl(rect, editLabel, enabled: appearance != null)) submit?.Invoke();
            }));

            // 这里仍使用按下时的身体；弹窗确认时再检查实例归属和组件引用。
            void OpenAppearance()
            {
                if (!BodyIsCurrent()) return;
                Dialog_FullGelAppearance popup = null;
                popup = new Dialog_FullGelAppearance(edit.Pawn, owner, () => EditPopupIsCurrent(popup, edit) && BodyIsCurrent());
                ShowEditPopup(popup);
            }

            // 仍是同一个 Hediff 时，也不能把旧草稿交给替换后的外观组件。
            bool BodyIsCurrent() => appearance != null && ReferenceEquals(FullGelAppearanceUtility.GetOwner(edit.Pawn), owner) &&
                ReferenceEquals(owner.TryGetComp<HediffComp_FullGelAppearance>(), appearance);
        }

        /// <summary>直接显示生育开关和子宫预设，保持原有资格与锁定说明。</summary>
        private void AddFertilitySection(SettingsColumn column, PageEdit edit, SSCFertilityPreconceptionContext context)
        {
            column.AddLabel("<b>" + "SSC_TrainingManagement_BodyFertility".Translate() + "</b>");
            if (context.Owner == null)
            {
                column.AddLabel("SSC_Preconception_MissingUterus".Translate(), column.Indent, "SSC_Fertility_Uterus".Translate());
                return;
            }

            const string fertility = SSCAbilityToggleUtility.UterusFertility;
            var state = SSCAbilityToggleUtility.GetView(edit.Pawn, fertility);
            if (state.Visible)
            {
                var definition = SSCAbilityToggleUtility.Find(fertility);
                AddAbilityToggle(column, edit, fertility, state, definition.LabelKey.Translate(), definition.TipKey.Translate(), column.Indent);
            }

            // 从捕获的身体读保存值，孕期和阶段锁定只影响编辑资格。
            AddBodySetting(column, edit, "SSC_Preconception_GenderLabel".Translate(), context.Owner.OffspringGender,
                (SSCOffspringGender[])Enum.GetValues(typeof(SSCOffspringGender)),
                value => ITab_SexSlaveTraining.PreconceptionOptionLabel(false, (int)value),
                (SSCOffspringGender value, out string reason) => SSCFertilityPreconceptionUtility.CanSetGender(context, value, out reason),
                (SSCOffspringGender value, out string reason) => SSCFertilityPreconceptionUtility.TrySetGender(context, value, out reason),
                "SSC_Preconception_GenderTip".Translate(), column.Indent);
            AddBodySetting(column, edit, "SSC_Preconception_SpeciesLabel".Translate(), context.Owner.OffspringSpeciesSource,
                (SSCOffspringSpeciesSource[])Enum.GetValues(typeof(SSCOffspringSpeciesSource)),
                value => ITab_SexSlaveTraining.PreconceptionOptionLabel(true, (int)value),
                (SSCOffspringSpeciesSource value, out string reason) => SSCFertilityPreconceptionUtility.CanSetSpeciesSource(context, value, out reason),
                (SSCOffspringSpeciesSource value, out string reason) => SSCFertilityPreconceptionUtility.TrySetSpeciesSource(context, value, out reason),
                "SSC_Preconception_SpeciesTip".Translate(), column.Indent);
            const string imprint = SSCAbilityToggleUtility.OffspringImprint;
            var imprintState = SSCAbilityToggleUtility.GetView(edit.Pawn, imprint);
            if (imprintState.Visible)
            {
                var imprintDefinition = SSCAbilityToggleUtility.Find(imprint);
                AddAbilityToggle(column, edit, imprint, imprintState, imprintDefinition.LabelKey.Translate(),
                    imprintDefinition.TipKey.Translate(), column.Indent);
            }
        }

        /// <summary>下拉呈现保存档位，当前实际效果有差异时补充说明。</summary>
        private void AddSensitivitySection(SettingsColumn column, PageEdit edit, SSCOrgasmSensitivityContext context)
        {
            // 降阶后保存档位和实际档位可不同，显示实际值不会回写保存值。
            var state = SSCOrgasmSensitivityUtility.GetState(edit.Pawn);
            AddBodySetting(column, edit, "<b>" + "SSC_Sensitivity_Title".Translate() + "</b>", state.SavedLevel,
                Enumerable.Range(0, 5), SSCOrgasmSensitivityPresentation.Level,
                (int level, out string reason) => SSCOrgasmSensitivityUtility.CanSet(context, level, out reason),
                (int level, out string reason) => SSCOrgasmSensitivityUtility.TrySet(context, level, out reason),
                optionLabel: SSCOrgasmSensitivityPresentation.OptionLabel);
            if (state.SavedLevel != state.EffectiveLevel)
                column.AddLabel("SSC_Sensitivity_EffectiveLevel".Translate(SSCOrgasmSensitivityPresentation.Level(state.EffectiveLevel)),
                    column.Indent, state.InactiveReason?.Translate().ToString());
        }

        /// <summary>沿用原设置接口的返回值及失败原因，供菜单预检和提交共用。</summary>
        private delegate bool BodySettingRequest<T>(T value, out string reason);

        /// <summary>菜单沿用当前行捕获的提交函数，页面和载体分别复查后才允许写入。</summary>
        private void AddBodySetting<T>(SettingsColumn column, PageEdit edit, string caption, T saved, IEnumerable<T> choices,
            Func<T, string> label, BodySettingRequest<T> canSet, BodySettingRequest<T> trySet,
            string tooltip = null, float indent = 0f, Func<T, string> optionLabel = null)
        {
            float captionWidth = (column.Width - indent) * .45f;
            float controlHeight = Mathf.Max(32f, Text.LineHeight + 6f);
            float height = Mathf.Max(controlHeight, Text.CalcHeight(caption, Mathf.Max(1f, captionWidth - 6f)));
            column.Rows.Add((height, rect =>
            {
                using var scope = new SSCBondOverviewWidgets.GuiScope(false);
                Text.Anchor = TextAnchor.MiddleLeft;
                rect = InsetSettingsRow(rect, indent);
                Widgets.Label(new Rect(rect.x, rect.y, Mathf.Max(1f, captionWidth - 6f), rect.height), caption);
                var control = InsetSettingsRow(rect, captionWidth);
                // 用自然／关闭档位查询菜单能否打开，即使保存档位已失去资格，也可以尝试选更低档。
                bool editable = canSet(default, out string reason);
                string tip = reason == null ? tooltip : tooltip == null ? reason : tooltip + "\n\n" + reason;
                if (!DrawSettingsControl(control, label(saved), dropdown: true, enabled: editable, tooltip: tip)) return;
                // 回调保留打开菜单时的业务凭据，不能从本次选中角色重新取值。
                OpenSettingsMenu(edit, choices, optionLabel ?? label, value =>
                {
                    if (trySet(value, out string rejection)) RefreshAfterEdit();
                    else if (!string.IsNullOrEmpty(rejection))
                        Messages.Message(rejection, edit.Pawn, MessageTypeDefOf.RejectInput, false);
                }, value => canSet(value, out string unavailable) ? null : unavailable);
            }));
        }

        /// <summary>按下时捕获原载体，复选框请求只通过能力框架提交。</summary>
        private void DrawAbilityToggle(Rect rect, PageEdit edit, string id, SSCAbilityToggleView state, string label, string tip)
        {
            var input = Event.current;
            bool canEdit = state.CanChange && GUI.enabled;
            // 原版复选框在松手时才返回新值，按下时先保留本行的原授权。
            if (canEdit && input.type == EventType.mouseDown && input.button == 0 && Mouse.IsOver(rect) &&
                PageEditIsCurrent(edit) && SSCAbilityToggleUtility.IsCurrent(edit.Pawn, id, state.Context))
                abilityTogglePress = new AbilityTogglePress(edit, id, state);

            bool next = SSCUIWidgets.Checkbox(rect, label, state.Enabled, canEdit, tip);
            if (next == state.Enabled || !abilityTogglePress.HasValue) return;
            var press = abilityTogglePress.Value;
            abilityTogglePress = null;
            // 页面先拒绝已切角／切页的输入，再由后端用按下时的凭据复查保存载体和目标值资格。
            if (press.Id != id || press.Enabled != state.Enabled || !PageEditIsCurrent(press.Edit)) return;
            if (!SSCAbilityToggleUtility.TrySet(press.Edit.Pawn, press.Id, next, press.Context, out string reason) &&
                !string.IsNullOrEmpty(reason))
                Messages.Message(reason, press.Edit.Pawn, MessageTypeDefOf.RejectInput, false);
        }
    }
}
