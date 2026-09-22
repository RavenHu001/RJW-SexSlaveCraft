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
        /// <summary>设置主页只显示特化总开关和默认配置入口，六项模板及批量操作收纳到独立窗口。</summary>
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
        {
            listing.Label("SSC_Restrictions_PawnTitle".Translate(pawn?.LabelShort ?? "SSC_Restrictions_None".Translate().ToString()));
            // 日常面板只常驻一条操作说明；保存机制放在悬停中，接管清单留给许可测试页。
            listing.Label("SSC_Restrictions_CompactNotice".Translate(), -1f, "SSC_Restrictions_SaveNotice".Translate());
            if (!(SSCMod.settings?.enableSexSlaveProtectionRules ?? true))
                listing.Label("SSC_Restrictions_Paused".Translate());
            bool applicable = SSCRestrictionResolver.IsApplicable(pawn);
            if (!applicable) listing.Label("SSC_Restrictions_NotApplicable".Translate());
            SSCRestrictionConfig config = pawn?.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            if (config == null)
            {
                listing.Label("SSC_Restrictions_Uninitialized".Translate());
                if (applicable && listing.ButtonText("SSC_Restrictions_Initialize".Translate()))
                {
                    if (!SSCRestrictionEditor.TryInitialize(pawn, out SSCRestrictionResolution error))
                        Messages.Message(error == null ? "SSC_Restrictions_EditFailed".Translate() :
                            "SSC_Restrictions_InitializeFailed".Translate(("SSC_Restrictions_Rule_" + error.Rule).Translate(), SourceLabel(error)),
                            MessageTypeDefOf.RejectInput, false);
                }
                return;
            }
            if (!config.IsValid()) { listing.Label("SSC_Restrictions_InvalidConfig".Translate(config.version)); return; }
            foreach (SSCRestrictionRule rule in SSCRestrictionRules.All)
            {
                DrawGroup(listing, rule);
                // 未绑定角色只展示保留的个人选择，不把当前装备/特化标成正在生效的强制来源。
                SSCRestrictionResolution entry = applicable ? SSCRestrictionResolver.Resolve(pawn, config.rules, rule) : null;
                string extraTip = null;
                if (rule == SSCRestrictionRule.ReceiveTraining)
                {
                    Pawn forced = SSCRestrictionLifecycle.GetForcedTrainer(pawn);
                    if (forced != null) extraTip = "SSC_Restrictions_TrainerLocked".Translate(forced.LabelShort);
                }
                DrawChoice(listing, rule, config.rules, applicable, entry, value =>
                {
                    if (!SSCRestrictionEditor.TrySet(pawn, rule, value))
                        Messages.Message("SSC_Restrictions_EditFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                }, extraTip);
            }
        }

        /// <summary>在主动、被动、调教的首项绘制分组标题，避免六项规则成为没有分类的长列表。</summary>
        private static void DrawGroup(Listing_Standard listing, SSCRestrictionRule rule)
        {
            if (rule != SSCRestrictionRule.Masturbation && rule != SSCRestrictionRule.ReceiveConsensual &&
                rule != SSCRestrictionRule.ReceiveTraining) return;
            listing.GapLine();
            listing.Label(("SSC_Restrictions_Group_" + rule).Translate());
        }

        /// <summary>绘制左文右勾叉；受强制影响时显示并锁住有效值，保存选择只用于无强制时的编辑及悬停说明。</summary>
        private static void DrawChoice(Listing_Standard listing, SSCRestrictionRule rule, SSCRestrictionRules rules,
            bool editable, SSCRestrictionResolution entry, Action<SSCRestrictionValue> apply, string extraTip = null)
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
            float height = Mathf.Max(28f, Text.CalcHeight(label, labelWidth) + 4f);
            if (badge != null) height = Mathf.Max(height, Text.CalcHeight(badge, Mathf.Max(1f, badgeWidth - 4f)) + 4f);
            Rect row = listing.GetRect(height);
            Rect labelRect = new Rect(row.x, row.y + 2f, labelWidth, height - 4f);
            bool previous = GUI.enabled;
            Color previousColor = GUI.color;
            bool changed = allowed;
            try
            {
                GUI.enabled = previous && canEdit;
                if (!GUI.enabled) GUI.color = previousColor * new Color(0.55f, 0.55f, 0.55f, 1f);
                Widgets.Label(labelRect, label);
                // 使用游戏本身的 Checkbox 贴图，即与“是调教员”相同的绿勾/红叉，不绘制方框。
                Widgets.Checkbox(row.xMax - 24f, row.y + (height - 24f) / 2f, ref changed, 24f, !GUI.enabled);
                // 标签区与图标热区互不重叠，整行可点且不会因同一次点击翻转两次。
                if (GUI.enabled && Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width - 28f, height)))
                    changed = !changed;
            }
            finally { GUI.enabled = previous; GUI.color = previousColor; }
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
                apply(changed ? (rule == SSCRestrictionRule.ConsensualInitiation ? rules.ConsensualTarget : SSCRestrictionValue.Allow)
                    : SSCRestrictionValue.Deny);
            if (rule == SSCRestrictionRule.ConsensualInitiation)
                DrawTargetChoices(listing, rules, displayed, canEdit, apply);
        }

        /// <summary>对象单选跟随当前显示值；强制时同步锁定，个人关闭时仍显示记住的范围，窄栏分两行。</summary>
        private static void DrawTargetChoices(Listing_Standard listing, SSCRestrictionRules rules,
            SSCRestrictionValue displayed, bool editable, Action<SSCRestrictionValue> apply)
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
                if (Widgets.RadioButtonLabeled(first, owner, selected == SSCRestrictionValue.OwnerOnly, !GUI.enabled) && GUI.enabled && selected != SSCRestrictionValue.OwnerOnly)
                    apply(SSCRestrictionValue.OwnerOnly);
                if (Widgets.RadioButtonLabeled(second, anyone, selected == SSCRestrictionValue.Allow, !GUI.enabled) && GUI.enabled && selected != SSCRestrictionValue.Allow)
                    apply(SSCRestrictionValue.Allow);
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
                else if (!string.IsNullOrEmpty(profile?.label)) name = profile.LabelCap.ToString();
            }
            return label + " (" + name + ")";
        }
    }
}
