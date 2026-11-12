using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>模组设置、角色侧栏及测试窗口共用的规则控件；绘制只读，按钮回调才写入配置。</summary>
    internal static class SSCRestrictionUI
    {
        /// <summary>设置主页只显示特化总开关和默认配置入口，规则模板及批量操作收纳到独立窗口。</summary>
        public static void DrawSettings(Listing_Standard listing)
        {
            SSCSettings settings = SSCMod.settings;
            if (settings == null) return;
            bool enabled = settings.enableSpecializationRestrictionOverrides;
            listing.CheckboxLabeled("SSC_Restrictions_ProfileSwitch".Translate(), ref enabled,
                "SSC_Restrictions_ProfileSwitchTip".Translate());
            if (enabled != settings.enableSpecializationRestrictionOverrides)
            {
                settings.enableSpecializationRestrictionOverrides = enabled;
                SSCRestrictionGameComponent.SettingsChanged();
                settings.Write();
            }
            Rect defaultsButton = listing.GetRect(30f);
            if (Widgets.ButtonText(defaultsButton, "SSC_Restrictions_DefaultsOpen".Translate()))
                Find.WindowStack.Add(new Dialog_SSCRestrictionDefaults());
            TooltipHandler.TipRegion(defaultsButton, "SSC_Restrictions_DefaultsTip".Translate());
        }

        /// <summary>在默认配置子窗口中编辑模板；无存档仍能保存模板，批量应用需有效模板及当前游戏。</summary>
        public static void DrawDefaults(Listing_Standard listing)
        {
            SSCSettings settings = SSCMod.settings;
            if (settings == null) return;
            listing.Label("SSC_Restrictions_DefaultsShort".Translate(), -1f, "SSC_Restrictions_DefaultsTip".Translate());
            SSCRestrictionRules defaults = settings.restrictionDefaults;
            if (defaults?.IsValid() != true) listing.Label("SSC_Restrictions_InvalidDefaults".Translate());
            else
                foreach (SSCRestrictionRule rule in SSCRestrictionRules.All)
                {
                    DrawGroup(listing, rule);
                    DrawChoice(listing, rule, defaults, true, null, value =>
                    {
                        settings.restrictionDefaults.Set(rule, value);
                        settings.Write();
                    });
                }
            listing.GapLine();
            DrawApplyDefaults(listing, defaults);
        }

        /// <summary>在模板子页底部绘制醒目的批量覆盖按钮；需有效模板及已载入存档，并在确认后才写入角色。</summary>
        private static void DrawApplyDefaults(Listing_Standard listing, SSCRestrictionRules defaults)
        {
            string label = "SSC_Restrictions_ApplyAll".Translate();
            Rect button = listing.GetRect(Mathf.Max(32f, Text.CalcHeight(label, Mathf.Max(1f, listing.ColumnWidth - 16f)) + 12f));
            bool previous = GUI.enabled;
            Color previousColor = GUI.color;
            try
            {
                GUI.enabled = previous && Current.Game != null && defaults?.IsValid() == true;
                // 橙色与文字警示同时标识覆盖操作；即使无法分辨颜色，也能从按钮文字识别风险。
                GUI.color = previousColor * (GUI.enabled ? new Color(1f, 0.65f, 0.4f) : new Color(0.55f, 0.55f, 0.55f));
                if (Widgets.ButtonText(button, label) && GUI.enabled)
                {
                    // 快照在确认时仍属于同一个存档；确认前改变模板或选中角色不会改变本次操作。
                    Game game = Current.Game;
                    SSCRestrictionRules template = defaults.Copy();
                    List<Pawn> targets = SSCRestrictionGameComponent.AllPawns();
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("SSC_Restrictions_ApplyConfirm".Translate(), () =>
                    {
                        if (Current.Game != game) return;
                        SSCRestrictionBatchResult result = SSCRestrictionLifecycle.ApplyDefaults(targets, template);
                        Messages.Message("SSC_Restrictions_ApplyResult".Translate(result.Applied, result.Skipped, result.Failed),
                            MessageTypeDefOf.NeutralEvent, false);
                    }));
                }
            }
            finally { GUI.enabled = previous; GUI.color = previousColor; }
            TooltipHandler.TipRegion(button, "SSC_Restrictions_ApplyAllTip".Translate());
        }

        /// <summary>绘制角色配置、实时强制来源及全局状态；不适用角色已有配置只读，缺失配置可显式重试。</summary>
        public static void DrawPawn(Listing_Standard listing, Pawn pawn)
            => DrawPawn(listing, pawn, false);

        /// <summary>双板块界面省去重复角色标题和常驻说明，保留子组间横线；其他入口及规则／保存逻辑不变。</summary>
        public static void DrawPawn(Listing_Standard listing, Pawn pawn, bool compact)
            => DrawPawn(listing, pawn, compact, null);

        /// <summary>在调教窗口中由外层保留原按压请求，旧入口继续使用即时提交。</summary>
        internal static void DrawPawn(Listing_Standard listing, Pawn pawn, bool compact,
            Func<Rect, object, Action, Action> prepare)
        {
            if (!compact)
            {
                listing.Label("SSC_Restrictions_PawnTitle".Translate(pawn?.LabelShort ?? "SSC_Restrictions_None".Translate().ToString()));
                listing.Label("SSC_Restrictions_CompactNotice".Translate(), -1f, "SSC_Restrictions_SaveNotice".Translate());
            }
            if (!(SSCMod.settings?.enableSexSlaveProtectionRules ?? true))
                listing.Label("SSC_Restrictions_Paused".Translate());
            bool applicable = SSCRestrictionResolver.IsApplicable(pawn);
            bool selfApplicable = SSCSelfTrainingEligibility.IsEligible(pawn);
            if (!applicable && !selfApplicable) listing.Label("SSC_Restrictions_NotApplicable".Translate());
            SSCRestrictionConfig config = pawn?.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            if (config == null)
            {
                listing.Label("SSC_Restrictions_Uninitialized".Translate());
                if (applicable || selfApplicable)
                {
                    if (prepare == null)
                    {
                        if (listing.ButtonText("SSC_Restrictions_Initialize".Translate())) Initialize();
                    }
                    else
                    {
                        string label = "SSC_Restrictions_Initialize".Translate();
                        Rect button = listing.GetRect(Mathf.Max(30f, Text.CalcHeight(label, Mathf.Max(1f, listing.ColumnWidth - 12f)) + 8f));
                        var submit = prepare(button, "RestrictionInitialize", Initialize);
                        if (Widgets.ButtonText(button, label) && GUI.enabled) submit?.Invoke();
                    }
                }
                return;

                void Initialize()
                {
                    if (!SSCRestrictionEditor.TryInitialize(pawn, out SSCRestrictionResolution error))
                        Messages.Message(error == null ? "SSC_Restrictions_EditFailed".Translate() :
                            "SSC_Restrictions_InitializeFailed".Translate(("SSC_Restrictions_Rule_" + error.Rule).Translate(), SourceLabel(error)),
                            MessageTypeDefOf.RejectInput, false);
                }
            }
            if (!config.IsValid()) { listing.Label("SSC_Restrictions_InvalidConfig".Translate(config.version)); return; }
            foreach (SSCRestrictionRule rule in SSCRestrictionRules.All)
            {
                DrawGroup(listing, rule, !compact || rule != SSCRestrictionRule.Masturbation, prepare != null);
                // 未绑定角色只展示保留的个人选择，不把当前装备/特化标成正在生效的强制来源。
                bool rowApplicable = rule == SSCRestrictionRule.SelfTraining ? selfApplicable : applicable;
                SSCRestrictionResolution entry = rowApplicable ? SSCRestrictionResolver.Resolve(pawn, config.rules, rule) : null;
                string extraTip = null;
                if (rule == SSCRestrictionRule.ReceiveTraining)
                {
                    Pawn forced = SSCRestrictionLifecycle.GetForcedTrainer(pawn);
                    if (forced != null) extraTip = "SSC_Restrictions_TrainerLocked".Translate(forced.LabelShort);
                }
                var rules = config.rules;
                var saved = rules.Get(rule);
                var savedTarget = rules.ConsensualTarget;
                // 提交闭包保留原保存值及对象范围，切换开关时沿用按下时记住的范围。
                DrawChoice(listing, rule, rules, rowApplicable, entry, value =>
                {
                    bool changed = prepare == null ? SSCRestrictionEditor.TrySet(pawn, rule, value) :
                        SSCRestrictionEditor.TrySetFromView(pawn, rule, value, config, rules, saved, savedTarget);
                    if (!changed)
                        Messages.Message("SSC_Restrictions_EditFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                }, extraTip, prepare);
            }
        }

        /// <summary>子标题居中并统一下横线的绘制颜色；恢复 GUI 状态，避免影响后续开关及禁用样式。</summary>
        private static void DrawSubtitle(Listing_Standard listing, string label, bool embedded)
        {
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            try
            {
                Text.Anchor = embedded ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                GUI.color = Color.white;
#if SSC_RW17
                using var tint = new SSCTrainingWindowTheme.TextTint(embedded ? SSCTrainingWindowTheme.SecondaryText : GUI.contentColor);
#endif
                listing.Label(label);
                if (embedded)
                {
                    var line = listing.GetRect(6f);
#if SSC_RW17
                    Widgets.DrawBoxSolid(new Rect(line.x, line.y + 2f, line.width, 1f), SSCTrainingWindowTheme.DividerColor);
#else
                    Widgets.DrawBoxSolid(new Rect(line.x, line.y + 2f, line.width, 1f), new Color(.36f, .43f, .45f));
#endif
                }
                else listing.GapLine(6f);
            }
            finally { Text.Anchor = previousAnchor; GUI.color = previousColor; }
        }

        /// <summary>主动、被动及调教子标题上下均有横线；紧凑首组复用外层主标题下横线。</summary>
        private static void DrawGroup(Listing_Standard listing, SSCRestrictionRule rule, bool drawDivider = true, bool embedded = false)
        {
            if (rule != SSCRestrictionRule.Masturbation && rule != SSCRestrictionRule.ReceiveConsensual &&
                rule != SSCRestrictionRule.ReceiveTraining) return;
            if (drawDivider && !embedded) listing.GapLine();
            else listing.Gap(6f);
            DrawSubtitle(listing, ("SSC_Restrictions_Group_" + rule).Translate(), embedded);
        }

        /// <summary>绘制左文右勾叉；受强制影响时显示并锁住有效值，保存选择只用于无强制时的编辑及悬停说明。</summary>
        private static void DrawChoice(Listing_Standard listing, SSCRestrictionRule rule, SSCRestrictionRules rules,
            bool editable, SSCRestrictionResolution entry, Action<SSCRestrictionValue> apply, string extraTip = null,
            Func<Rect, object, Action, Action> prepare = null)
        {
            SSCRestrictionValue saved = rules.Get(rule);
            bool forced = entry != null && (entry.Source == SSCRestrictionSource.Equipment ||
                entry.Source == SSCRestrictionSource.Specialization);
            // 只锁定本次解析实际命中的强制条目；关闭总限制或特化强制后由解析结果自然解除锁定。
            // 不将显示值写回角色，装备脱下或特化失效后仍恢复原先保存的个人选择。
            bool canEdit = editable && !forced && (entry?.Valid ?? true);
            SSCRestrictionValue displayed = forced && entry.Valid ? entry.Value : saved;
            bool allowed = displayed != SSCRestrictionValue.Deny;
            string label = ("SSC_Restrictions_Option_" + rule).Translate();
            string badge = OverrideBadge(entry);
            string tip = ("SSC_Restrictions_Tip_" + rule).Translate();
            if (entry != null && (!entry.Valid || entry.Source != SSCRestrictionSource.Saved))
                tip += "\n\n" + "SSC_Restrictions_OverrideTip".Translate(ValueLabel(saved),
                    entry.Valid ? ValueLabel(entry.Value) : "SSC_Restrictions_InvalidValue".Translate().ToString(), SourceLabel(entry));
            if (forced) tip += "\n" + "SSC_Restrictions_ForcedLocked".Translate();
            if (!string.IsNullOrEmpty(extraTip)) tip += "\n" + extraTip;

            // 给右侧勾叉和可选来源标记独立留位。长译文按实际文本高度换行，不遮住操作区。
            float badgeWidth = badge == null ? 0f : Mathf.Min(listing.ColumnWidth * 0.36f, Text.CalcSize(badge).x + 8f);
            float labelWidth = Mathf.Max(1f, listing.ColumnWidth - 32f - badgeWidth);
            float height = Mathf.Max(prepare != null ? 34f : 28f, Text.CalcHeight(label, labelWidth) + 4f);
            if (badge != null) height = Mathf.Max(height, Text.CalcHeight(badge, Mathf.Max(1f, badgeWidth - 4f)) + 4f);
            Rect row = listing.GetRect(height);
            Rect labelRect = new Rect(row.x, row.y + 2f, labelWidth, height - 4f);
            bool previous = GUI.enabled;
            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            bool changed = allowed;
            var next = allowed ? SSCRestrictionValue.Deny :
                rule == SSCRestrictionRule.ConsensualInitiation ? rules.ConsensualTarget : SSCRestrictionValue.Allow;
            Action submit = () => apply(next);
            if (prepare != null && previous && canEdit) submit = prepare(row, rule, submit);
            try
            {
                GUI.enabled = previous && canEdit;
#if SSC_RW17
                if (!GUI.enabled && prepare == null) GUI.color = previousColor * new Color(0.55f, 0.55f, 0.55f, 1f);
#else
                if (!GUI.enabled) GUI.color = previousColor * new Color(0.55f, 0.55f, 0.55f, 1f);
#endif
                if (prepare != null) Text.Anchor = TextAnchor.MiddleLeft;
#if SSC_RW17
                using (new SSCTrainingWindowTheme.TextTint(prepare != null && !GUI.enabled ? SSCTrainingWindowTheme.DisabledText : GUI.contentColor))
                    Widgets.Label(labelRect, label);
#else
                Widgets.Label(labelRect, label);
#endif
                float iconX = row.xMax - 24f;
                float iconY = row.y + (height - 24f) / 2f;
                if (prepare != null)
                {
                    // 原生 Checkbox 读取帧级 Input。调教窗口整行使用同一个 IMGUI 按钮与按压凭据配对。
                    Widgets.CheckboxDraw(iconX, iconY, allowed, !GUI.enabled);
                    if (GUI.enabled && Widgets.ButtonInvisible(row)) changed = !changed;
                }
                else
                {
                    Widgets.Checkbox(iconX, iconY, ref changed, 24f, !GUI.enabled);
                    if (GUI.enabled && Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width - 28f, height)))
                        changed = !changed;
                }
            }
            finally { GUI.enabled = previous; GUI.color = previousColor; Text.Anchor = previousAnchor; }
            if (badge != null)
            {
                Color oldColor = GUI.color;
                try
                {
                    GUI.color = entry.Valid ? new Color(1f, 0.8f, 0.4f) : Color.red;
                    Widgets.Label(new Rect(row.xMax - 32f - badgeWidth, row.y + 2f, badgeWidth - 4f, height - 4f), badge);
                }
                finally { GUI.color = oldColor; }
            }
            TooltipHandler.TipRegion(row, tip);
            if (previous && canEdit && changed != allowed)
                submit?.Invoke();
            if (rule == SSCRestrictionRule.ConsensualInitiation)
                DrawTargetChoices(listing, rules, displayed, canEdit, apply, prepare);
        }

        /// <summary>对象单选跟随当前显示值；强制时同步锁定，个人关闭时仍显示记住的范围，窄栏分两行。</summary>
        private static void DrawTargetChoices(Listing_Standard listing, SSCRestrictionRules rules,
            SSCRestrictionValue displayed, bool editable, Action<SSCRestrictionValue> apply, Func<Rect, object, Action, Action> prepare)
        {
            const float indent = 18f;
            string owner = "SSC_Restrictions_Value_OwnerOnly".Translate();
            string anyone = "SSC_Restrictions_AnyTarget".Translate();
            float width = Mathf.Max(1f, listing.ColumnWidth - indent);
            bool stacked = Text.CalcSize(owner).x + Text.CalcSize(anyone).x + 72f > width;
            float cellWidth = stacked ? width : (width - 8f) / 2f;
            float height = Mathf.Max(28f, Mathf.Max(Text.CalcHeight(owner, Mathf.Max(1f, cellWidth - 28f)),
                Text.CalcHeight(anyone, Mathf.Max(1f, cellWidth - 28f))) + 4f);
            Rect space = listing.GetRect(stacked ? height * 2f : height);
            Rect first = new Rect(space.x + indent, space.y, cellWidth, height);
            Rect second = new Rect(stacked ? first.x : first.xMax + 8f, stacked ? first.yMax : first.y, cellWidth, height);
            bool previous = GUI.enabled;
            Color previousColor = GUI.color;
            try
            {
                GUI.enabled = previous && editable && displayed != SSCRestrictionValue.Deny;
                if (!GUI.enabled) GUI.color = previousColor * new Color(0.55f, 0.55f, 0.55f, 1f);
                SSCRestrictionValue selected = displayed == SSCRestrictionValue.Deny ? rules.ConsensualTarget : displayed;
                DrawTarget(first, owner, SSCRestrictionValue.OwnerOnly);
                DrawTarget(second, anyone, SSCRestrictionValue.Allow);

                void DrawTarget(Rect rect, string label, SSCRestrictionValue value)
                {
                    bool current = selected == value;
                    Action submit = () => apply(value);
                    // 各对象选项使用独立键，松手时只允许响应原来按下的那个选项。
                    if (prepare != null && GUI.enabled && !current)
                        submit = prepare(rect, (SSCRestrictionRule.ConsensualInitiation, value), submit);
                    if (Widgets.RadioButtonLabeled(rect, label, current, !GUI.enabled) && GUI.enabled && !current)
                        submit?.Invoke();
                }
            }
            finally { GUI.enabled = previous; GUI.color = previousColor; }
            TooltipHandler.TipRegion(first, "SSC_Restrictions_TargetOwnerTip".Translate());
            TooltipHandler.TipRegion(second, "SSC_Restrictions_TargetAnyTip".Translate());
        }

        /// <summary>仅为装备/特化强制或无效条目生成短标记；普通保存值和全局停用不重复占用每行空间。</summary>
        private static string OverrideBadge(SSCRestrictionResolution entry)
        {
            if (entry == null) return null;
            if (!entry.Valid) return "SSC_Restrictions_InvalidValue".Translate();
            string key = entry.Source == SSCRestrictionSource.Equipment ? "SSC_Restrictions_EquipmentBadge" :
                entry.Source == SSCRestrictionSource.Specialization ? "SSC_Restrictions_ProfileBadge" : null;
            return key == null ? null : key.Translate(ValueLabel(entry.Value)).ToString();
        }

        /// <summary>显示条目保存值或有效值的本地化名称。</summary>
        private static string ValueLabel(SSCRestrictionValue value) => ("SSC_Restrictions_Value_" + value).Translate();

        /// <summary>悬停优先显示装备名称或特化名称；定义缺失时保留原标识，便于定位异常来源。</summary>
        private static string SourceLabel(SSCRestrictionResolution entry)
        {
            string label = ("SSC_Restrictions_Source_" + entry.Source).Translate();
            if (entry.SourceDef == null) return label;
            string name = entry.SourceDef;
            if (entry.Source == SSCRestrictionSource.Equipment)
                name = DefDatabase<ThingDef>.GetNamedSilentFail(entry.SourceDef)?.LabelCap.ToString() ?? name;
            else if (entry.Source == SSCRestrictionSource.Specialization || entry.Source == SSCRestrictionSource.SpecializationDefault)
            {
                SSCRestrictionProfileDef profile = DefDatabase<SSCRestrictionProfileDef>.GetNamedSilentFail(entry.SourceDef);
                if (profile?.specialization == SexSlaveSpecializationType.Bus) name = Strings.ITab_SpecializationBus;
                else if (profile?.specialization == SexSlaveSpecializationType.TrainerOfficer)
                    name = "SSC_TrainerIdentity_SourceName".Translate();
                else if (!string.IsNullOrEmpty(profile?.label)) name = profile.LabelCap.ToString();
            }
            return label + " (" + name + ")";
        }
    }
}
