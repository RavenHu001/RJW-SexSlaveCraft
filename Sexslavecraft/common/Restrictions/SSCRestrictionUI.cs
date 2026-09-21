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
        /// <summary>绘制默认模板和全局特化开关；无存档时仍可编辑模板，批量应用必须有当前游戏。</summary>
        public static void DrawSettings(Listing_Standard listing)
        {
            SSCSettings settings = SSCMod.settings;
            if (settings == null) return;
            listing.Label("SSC_Restrictions_DefaultsTitle".Translate());
            listing.Label("SSC_Restrictions_DefaultsTip".Translate());
            bool enabled = settings.enableSpecializationRestrictionOverrides;
            listing.CheckboxLabeled("SSC_Restrictions_ProfileSwitch".Translate(), ref enabled,
                "SSC_Restrictions_ProfileSwitchTip".Translate());
            if (enabled != settings.enableSpecializationRestrictionOverrides)
            {
                settings.enableSpecializationRestrictionOverrides = enabled;
                SSCRestrictionGameComponent.SettingsChanged();
                settings.Write();
            }
            SSCRestrictionRules defaults = settings.restrictionDefaults;
            if (defaults?.IsValid() != true) listing.Label("SSC_Restrictions_InvalidDefaults".Translate());
            else
                foreach (SSCRestrictionRule rule in SSCRestrictionRules.All)
                {
                    DrawGroup(listing, rule);
                    DrawChoice(listing, rule, defaults.Get(rule), true, value =>
                    {
                        settings.restrictionDefaults.Set(rule, value);
                        settings.Write();
                    });
                }
            bool previous = GUI.enabled;
            try
            {
                GUI.enabled = previous && Current.Game != null && defaults?.IsValid() == true;
                if (listing.ButtonText("SSC_Restrictions_ApplyAll".Translate()))
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
            finally { GUI.enabled = previous; }
        }

        /// <summary>绘制角色配置、实时强制来源及全局状态；不适用角色已有配置只读，缺失配置可显式重试。</summary>
        public static void DrawPawn(Listing_Standard listing, Pawn pawn)
        {
            listing.Label(pawn?.LabelShort ?? "SSC_Restrictions_None".Translate().ToString());
            listing.Label("SSC_Restrictions_TestNotice".Translate());
            listing.Label("SSC_Restrictions_OwnerNotice".Translate());
            listing.Label("SSC_Restrictions_GlobalState".Translate(
                ValueLabel(SSCMod.settings?.enableSexSlaveProtectionRules ?? true),
                ValueLabel(SSCMod.settings?.enableSpecializationRestrictionOverrides ?? true)));
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
            listing.Label("SSC_Restrictions_SaveNotice".Translate());
            foreach (SSCRestrictionRule rule in SSCRestrictionRules.All)
            {
                DrawGroup(listing, rule);
                DrawChoice(listing, rule, config.rules.Get(rule), applicable, value =>
                {
                    if (!SSCRestrictionEditor.TrySet(pawn, rule, value))
                        Messages.Message("SSC_Restrictions_EditFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                });
                SSCRestrictionResolution entry = SSCRestrictionResolver.Resolve(pawn, config.rules, rule);
                listing.Label("SSC_Restrictions_EffectiveValue".Translate(entry.Valid ? ValueLabel(entry.Value) :
                    "SSC_Restrictions_InvalidValue".Translate().ToString(), SourceLabel(entry)));
            }
            Pawn forced = SSCRestrictionLifecycle.GetForcedTrainer(pawn);
            if (forced != null) listing.Label("SSC_Restrictions_TrainerLocked".Translate(forced.LabelShort));
        }

        /// <summary>在主动、被动、调教的首项绘制分组标题，避免六项规则成为没有分类的长列表。</summary>
        private static void DrawGroup(Listing_Standard listing, SSCRestrictionRule rule)
        {
            if (rule != SSCRestrictionRule.Masturbation && rule != SSCRestrictionRule.ReceiveConsensual &&
                rule != SSCRestrictionRule.ReceiveTraining) return;
            listing.GapLine();
            listing.Label(("SSC_Restrictions_Group_" + rule).Translate());
        }

        /// <summary>为规则提供合法值菜单；捕获当前角色回调，切换选中对象不会把选择写给另一个角色。</summary>
        private static void DrawChoice(Listing_Standard listing, SSCRestrictionRule rule, SSCRestrictionValue saved,
            bool editable, Action<SSCRestrictionValue> apply)
        {
            listing.Label(("SSC_Restrictions_Rule_" + rule).Translate());
            bool previous = GUI.enabled;
            try
            {
                GUI.enabled = previous && editable;
                if (!listing.ButtonText("SSC_Restrictions_SavedValue".Translate(ValueLabel(saved)))) return;
                var options = new List<FloatMenuOption>();
                foreach (SSCRestrictionValue value in Enum.GetValues(typeof(SSCRestrictionValue)))
                {
                    if (!SSCRestrictionRules.IsValid(rule, value)) continue;
                    SSCRestrictionValue choice = value;
                    options.Add(new FloatMenuOption(ValueLabel(choice), () => apply(choice)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            finally { GUI.enabled = previous; }
        }

        /// <summary>显示两个全局开关的本地化开启或关闭状态，不把它们误当作保存值。</summary>
        private static string ValueLabel(bool value) => (value ? "SSC_Restrictions_Enabled" : "SSC_Restrictions_Disabled").Translate();

        /// <summary>显示条目保存值或有效值的本地化名称。</summary>
        private static string ValueLabel(SSCRestrictionValue value) => ("SSC_Restrictions_Value_" + value).Translate();

        /// <summary>展示来源及定义名，便于核对同层合成和非法默认的具体出处。</summary>
        private static string SourceLabel(SSCRestrictionResolution entry)
        {
            string label = ("SSC_Restrictions_Source_" + entry.Source).Translate();
            return entry.SourceDef == null ? label : label + " (" + entry.SourceDef + ")";
        }
    }
}
