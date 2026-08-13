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
    public class SSCSettings : ModSettings
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
        public bool enableRimTalkSexDialogue = true;
        public int rimTalkMaxGenerationSpeed = 0;
        public string rimTalkTrainingInterruptTemplate = "";
        public bool enableCorruptionDecay = true;
        public float corruptionDecayPerDay = 0.02f;
        public bool useOldScoring = false;

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

        public SSCMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<SSCSettings>();
        }

        public override string SettingsCategory()
        {
            return Strings.Setting_Category;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

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
            // EN: RimTalk requests can consume an external AI call, so players always get an explicit opt-out.
            // CN: RimTalk 请求可能消耗外部 AI 调用，因此这里始终提供明确的关闭开关。
            listingStandard.CheckboxLabeled(
                "SSC_Setting_EnableRimTalkSexDialogue".Translate(),
                ref settings.enableRimTalkSexDialogue,
                "SSC_Setting_EnableRimTalkSexDialogue_Desc".Translate()
            );

            if (settings.enableRimTalkSexDialogue)
            {
                string speedValue = settings.rimTalkMaxGenerationSpeed <= 0
                    ? (string)"SSC_Setting_RimTalkMaxSpeed_Follow".Translate()
                    : (string)"SSC_Setting_RimTalkMaxSpeed_Value".Translate(settings.rimTalkMaxGenerationSpeed);
                listingStandard.Label(
                    "SSC_Setting_RimTalkMaxSpeed".Translate(speedValue),
                    -1f,
                    "SSC_Setting_RimTalkMaxSpeed_Desc".Translate());
                settings.rimTalkMaxGenerationSpeed = Mathf.RoundToInt(
                    listingStandard.Slider(settings.rimTalkMaxGenerationSpeed, 0f, 4f));

                listingStandard.Label(
                    "SSC_Setting_RimTalkInterruptTemplate".Translate(),
                    -1f,
                    "SSC_Setting_RimTalkInterruptTemplate_Desc".Translate());

                string displayedTemplate = string.IsNullOrWhiteSpace(settings.rimTalkTrainingInterruptTemplate)
                    ? (string)"SSC_RimTalk_DefaultInterrupt".Translate()
                    : settings.rimTalkTrainingInterruptTemplate;
                string editedTemplate = listingStandard.TextEntry(displayedTemplate, 2);
                if (!string.Equals(editedTemplate, displayedTemplate, StringComparison.Ordinal) ||
                    !string.IsNullOrWhiteSpace(settings.rimTalkTrainingInterruptTemplate))
                {
                    settings.rimTalkTrainingInterruptTemplate = editedTemplate;
                }
            }

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

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
