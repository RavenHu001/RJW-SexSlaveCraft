using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

// EN: This file stores SSC's mod settings and draws the settings window.
// EN: It groups the switches for sex-slave protection, logging, eligibility, full-gel visuals, and Binding Ritual conversion.
// CN: 这个文件负责保存 SSC 的模组设置，并绘制设置窗口。
// CN: 它把性奴保护、日志、资格判定、完全胶化外观和绑定仪式转奴这些开关统一放在这里。
namespace SexSlaveCraft
{
    public partial class SSCSettings : ModSettings
    {
        // EN: Strict protection stays off only when the player explicitly allows rape on chained sex slaves.
        // CN: 只有玩家明确允许时，锁链性奴才会退出严格保护模式。
        public bool allowSexSlaveRape = false;
        public bool enableSexSlaveProtectionRules = true;
        public bool protectBusAggressorRape = true;
        public bool protectChainedAggressorRape = true;
        public bool protectNonRapeOwnerOnly = true;
        public bool enableSSCLogs = true;
        public bool enableSSCVerboseLogs = false;
        public bool enableSSCImportantLogs = true;
        public bool useRJWOriginalEligibility = true;
        public bool enableDebugGizmos = false;
        public bool enableFullGelatinizedBodyTint = true;
        public bool enableRitualEnslavement = true;
        // EN: Legacy preferences are retained for migration only; the RimTalk runtime is archived.
        // CN: 以下旧设置只保留读写供未来迁移，RimTalk 运行模块已归档，不参与功能判定。
        public bool enableRimTalkSexDialogue = true;
        public int rimTalkMaxGenerationSpeed = 0;
        public string rimTalkTrainingInterruptTemplate = "";
        public bool enableCorruptionDecay = true;
        public float corruptionDecayPerDay = 0.02f;
        public bool useOldScoring = false;

        /// <summary>读写新限制配置及既有全局设置，并在加载结束后将数值选项限制在支持范围内。</summary>
        public override void ExposeData()
        {
            // EN: Save every toggle explicitly so old saves keep the same SSC behavior after updates.
            // CN: 每个开关都要显式保存，保证旧存档在更新后仍保持相同的 SSC 行为。
            Scribe_Values.Look(ref allowSexSlaveRape, "allowSexSlaveRape", false);
            Scribe_Values.Look(ref enableSexSlaveProtectionRules, "enableSexSlaveProtectionRules", true);
            Scribe_Values.Look(ref protectBusAggressorRape, "protectBusAggressorRape", true);
            Scribe_Values.Look(ref protectChainedAggressorRape, "protectChainedAggressorRape", true);
            Scribe_Values.Look(ref protectNonRapeOwnerOnly, "protectNonRapeOwnerOnly", true);
            Scribe_Values.Look(ref enableSSCLogs, "enableSSCLogs", true);
            Scribe_Values.Look(ref enableSSCVerboseLogs, "enableSSCVerboseLogs", false);
            Scribe_Values.Look(ref enableSSCImportantLogs, "enableSSCImportantLogs", true);
            Scribe_Values.Look(ref useRJWOriginalEligibility, "useRJWOriginalEligibility", true);
            Scribe_Values.Look(ref enableDebugGizmos, "enableDebugGizmos", false);
            Scribe_Values.Look(ref enableFullGelatinizedBodyTint, "enableFullGelatinizedBodyTint", true);
            Scribe_Values.Look(ref enableRitualEnslavement, "enableRitualEnslavement", true);
            Scribe_Values.Look(ref enableRimTalkSexDialogue, "enableRimTalkSexDialogue", true);
            Scribe_Values.Look(ref rimTalkMaxGenerationSpeed, "rimTalkMaxGenerationSpeed", 0);
            Scribe_Values.Look(ref rimTalkTrainingInterruptTemplate, "rimTalkTrainingInterruptTemplate", "");
            Scribe_Values.Look(ref enableCorruptionDecay, "enableCorruptionDecay", true);
            Scribe_Values.Look(ref corruptionDecayPerDay, "corruptionDecayPerDay", 0.02f);
            Scribe_Values.Look(ref useOldScoring, "useOldScoring", false);
            ExposeRestrictionSettings();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                corruptionDecayPerDay = Mathf.Clamp(corruptionDecayPerDay, 0f, 0.20f);
                rimTalkMaxGenerationSpeed = Mathf.Clamp(rimTalkMaxGenerationSpeed, 0, 4);
            }

            base.ExposeData();
        }
    }

    public class SSCMod : Mod
    {
        public static SSCSettings settings;
        private Vector2 settingsScrollPosition;
        private float settingsContentHeight = 1000f;

        /// <summary>加载当前模组的持久化设置，供规则查询和设置窗口共同使用。</summary>
        public SSCMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<SSCSettings>();
        }

        /// <summary>返回游戏模组设置列表中显示的本地化分类名称。</summary>
        public override string SettingsCategory()
        {
            return Strings.Setting_Category;
        }

        /// <summary>绘制可滚动设置区与底部诊断入口，并按本次内容高度更新滚动范围。</summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Keep the secondary diagnostics entry in a separate bottom-right footer.
            Rect viewport = new Rect(inRect.x, inRect.y, inRect.width, Mathf.Max(1f, inRect.height - 38f));
            Rect content = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 20f),
                Mathf.Max(viewport.height, settingsContentHeight));
            Widgets.BeginScrollView(viewport, ref settingsScrollPosition, content);
            try
            {
                var listing = new Listing_Standard { maxOneColumn = true };
                listing.Begin(content);
                try { DrawSettings(listing); }
                finally
                {
                    settingsContentHeight = listing.CurHeight + 12f;
                    listing.End();
                }
            }
            finally { Widgets.EndScrollView(); }

            string diagnosticsLabel = "SSC_Diagnostics_Title".Translate();
            float buttonWidth = Mathf.Min(inRect.width, Mathf.Max(140f, Text.CalcSize(diagnosticsLabel).x + 24f));
            if (Widgets.ButtonText(new Rect(inRect.xMax - buttonWidth, inRect.yMax - 28f, buttonWidth, 28f),
                diagnosticsLabel))
                Find.WindowStack.Add(new Dialog_SSCDiagnostics());

            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>在设置页提供测试入口；优先使用选中角色，否则选择当前地图首个可配置角色，主菜单下禁用。</summary>
        private static void DrawRestrictionTestEntry(Listing_Standard listing)
        {
            Map map = Current.Game != null ? Find.CurrentMap : null;
            Pawn pawn = map != null ? Find.Selector?.SingleSelectedThing as Pawn : null;
            if (pawn?.RaceProps?.Humanlike != true || pawn.TryGetComp<CompSexSlaveTraining>() == null)
                pawn = map?.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.RaceProps.Humanlike && p.TryGetComp<CompSexSlaveTraining>() != null);
            bool oldEnabled = GUI.enabled;
            bool clicked;
            try
            {
                GUI.enabled = oldEnabled && pawn != null;
                clicked = listing.ButtonText("SSC_Restrictions_Open".Translate());
            }
            finally { GUI.enabled = oldEnabled; }
            if (clicked) Find.WindowStack.Add(new Dialog_SSCRestrictions(pawn));
            listing.Label((pawn == null ? "SSC_Restrictions_SettingsUnavailable" : "SSC_Restrictions_SettingsTip").Translate());
        }

        /// <summary>绘制测试入口及按功能分组的设置控件，将玩家的勾选与数值调整写入全局设置对象。</summary>
        private static void DrawSettings(Listing_Standard listingStandard)
        {
            bool restrictionsWereEnabled = settings.enableSexSlaveProtectionRules;
            // EN: Step 1: draw the sex-slave protection rules first, because they change how Harmony guards sex jobs.
            // CN: 步骤 1：先画出“性奴保护规则”，因为它们会直接改变 Harmony 对性行为 Job 的拦截方式。
            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableSexSlaveProtectionRules".Translate(),
                ref settings.enableSexSlaveProtectionRules,
                "SSC_Setting_EnableSexSlaveProtectionRules_Desc".Translate()
            );

            listingStandard.CheckboxLabeled(
                Strings.Setting_AllowSexSlaveRape,
                ref settings.allowSexSlaveRape,
                Strings.Setting_AllowSexSlaveRape_Desc
            );

            listingStandard.CheckboxLabeled(
                "SSC_Setting_ProtectBusAggressorRape".Translate(),
                ref settings.protectBusAggressorRape,
                "SSC_Setting_ProtectBusAggressorRape_Desc".Translate()
            );

            listingStandard.CheckboxLabeled(
                "SSC_Setting_ProtectChainedAggressorRape".Translate(),
                ref settings.protectChainedAggressorRape,
                "SSC_Setting_ProtectChainedAggressorRape_Desc".Translate()
            );

            listingStandard.CheckboxLabeled(
                "SSC_Setting_ProtectNonRapeOwnerOnly".Translate(),
                ref settings.protectNonRapeOwnerOnly,
                "SSC_Setting_ProtectNonRapeOwnerOnly_Desc".Translate()
            );

            listingStandard.Gap(8f);
            DrawRestrictionTestEntry(listingStandard);
            listingStandard.GapLine();
            SSCRestrictionUI.DrawSettings(listingStandard);
            if (restrictionsWereEnabled != settings.enableSexSlaveProtectionRules)
                SSCRestrictionGameComponent.SettingsChanged();
            listingStandard.GapLine();
            // EN: Step 2: draw SSC log controls together so debug verbosity can be tuned from one block.
            // CN: 步骤 2：把 SSC 日志开关放在一起，方便在一个区域里调整调试输出等级。
            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableLogs".Translate(),
                ref settings.enableSSCLogs,
                "SSC_Setting_EnableLogs_Desc".Translate()
            );

            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableVerboseLogs".Translate(),
                ref settings.enableSSCVerboseLogs,
                "SSC_Setting_EnableVerboseLogs_Desc".Translate()
            );

            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableImportantLogs".Translate(),
                ref settings.enableSSCImportantLogs,
                "SSC_Setting_EnableImportantLogs_Desc".Translate()
            );

            listingStandard.GapLine();
            // EN: Step 3: expose the training target filter mode, because it changes who counts as a valid training target.
            // CN: 步骤 3：显示调教目标筛选模式，因为它会改变谁能成为合法调教目标。
            listingStandard.CheckboxLabeled(
                Strings.Setting_UseRJWOriginalEligibility,
                ref settings.useRJWOriginalEligibility,
                Strings.Setting_UseRJWOriginalEligibility_Desc
            );

            listingStandard.GapLine();
            // EN: Step 4: expose the SSC-specific debug gizmo switch, which stays off unless the player opts in.
            // CN: 步骤 4：显示 SSC 自己的调试 gizmo 开关，默认保持关闭，只有玩家主动开启才会出现。
            listingStandard.CheckboxLabeled(
                Strings.Setting_EnableDebugGizmos,
                ref settings.enableDebugGizmos,
                Strings.Setting_EnableDebugGizmos_Desc
            );

            listingStandard.GapLine();
            // EN: Step 5: expose the full-gel body tint option for the `全凝胶化完成` visual style.
            // CN: 步骤 5：显示“全凝胶化完成”的身体染色选项，用来控制完全胶化后的外观风格。
            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableFullGelTint".Translate(),
                ref settings.enableFullGelatinizedBodyTint,
                "SSC_Setting_EnableFullGelTint_Desc".Translate()
            );

            listingStandard.GapLine();
            // EN: Step 6: expose whether the Binding Ritual may convert the target into a vanilla slave.
            // CN: 步骤 6：显示绑定仪式是否允许把目标正式转化为原版奴隶。
            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableRitualEnslavement".Translate(),
                ref settings.enableRitualEnslavement,
                "SSC_Setting_EnableRitualEnslavement_Desc".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("SSC_RimTalk_Paused".Translate());

            listingStandard.GapLine();
            // EN: Corruption decay is optional, and its base daily rate can be tuned independently of all dynamic factors.
            // CN: 恶堕衰减可以整体关闭；每日基础值也可以独立于动态系数进行调整。
            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableCorruptionDecay".Translate(),
                ref settings.enableCorruptionDecay,
                "SSC_Setting_EnableCorruptionDecay_Desc".Translate()
            );

            listingStandard.Label(
                "SSC_Setting_CorruptionDecayPerDay".Translate((settings.corruptionDecayPerDay * 100f).ToString("F1")),
                -1f,
                "SSC_Setting_CorruptionDecayPerDay_Desc".Translate()
            );
            float decaySliderValue = listingStandard.Slider(settings.corruptionDecayPerDay, 0f, 0.20f);
            settings.corruptionDecayPerDay = Mathf.Round(decaySliderValue * 200f) / 200f;

            listingStandard.GapLine();
            listingStandard.CheckboxLabeled(
                "SSC_Setting_UseOldScoring".Translate(),
                ref settings.useOldScoring,
                "SSC_Setting_UseOldScoring_Desc".Translate()
            );
        }
    }
}
