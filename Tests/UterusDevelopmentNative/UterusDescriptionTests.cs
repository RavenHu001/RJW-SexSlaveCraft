// AI 辅助生成：通过真实 Hediff 描述、原版健康悬停和 Scribe 验证三态本地化正文。
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>核对当前语言中的预期正文，不在测试中复制生产三态选择算法。</summary>
    private static void RequireDescription(Hediff_SSCUterusDevelopment record, string suffix)
    {
        string expected = ("SSC_UterusDevelopment_Description" + suffix).Translate();
        Equal(expected, record.Description);
        Require(!record.Description.Contains("SSC_UterusDevelopment_"));
    }

    /// <summary>验证真实身体状态、描述查询只读、语言即时切换和原有存档结构。</summary>
    private static void UterusDescriptionTests(string repo)
    {
        foreach (var state in new[] { (.01f, false, "Developing"), (.3f, false, "Developing"), (.7f, false, "Developing"), (1f, false, "Infertile"), (1f, true, "Fertile") })
            Check("three-state description native progress and toggle: " + state, () =>
            {
                var pawn = SurgeryPawn(); var record = Start(pawn, state.Item1); if (state.Item2) Toggle(pawn, true);
                var stage = record.CurStage; float factor = Factor(pawn); int before = notifications;
                RequireDescription(record, state.Item3); RequireDescription(record, state.Item3);
                Equal(stage, record.CurStage); Near(factor, Factor(pawn)); Equal(before, notifications);
                Near(state.Item1, record.Severity); Equal(state.Item2, record.FertilityEnabled);
            });
        Check("three-state description follows completion toggles and reduced progress", () =>
        {
            var pawn = SurgeryPawn(); var record = Start(pawn, .7f); RequireDescription(record, "Developing");
            record.Severity = 1; RequireDescription(record, "Infertile");
            Toggle(pawn, true); RequireDescription(record, "Fertile");
            Toggle(pawn, false); RequireDescription(record, "Infertile");
            Toggle(pawn, true); record.Severity = .7f; RequireDescription(record, "Developing"); Require(!record.FertilityEnabled);
        });
        Check("three-state description isolates bodies and preserves shared definition text", () =>
        {
            string original = development.Description;
            var first = SurgeryPawn(); var a = Start(first, 1); Toggle(first, true);
            var b = Start(SurgeryPawn(), 1); var c = Start(SurgeryPawn(), .3f);
            RequireDescription(a, "Fertile"); RequireDescription(b, "Infertile"); RequireDescription(c, "Developing");
            Equal(original, development.Description);
        });
        Check("three-state description invalid detached and removed records use definition fallback", () =>
        {
            string original = development.Description;
            var unattached = new Hediff_SSCUterusDevelopment { def = development, pawn = SurgeryPawn() };
            Equal(original, unattached.Description);
            var pawn = SurgeryPawn(); var record = Start(pawn, 1); Toggle(pawn, true);
            var torso = record.Part; record.Part = null; Equal(original, record.Description); record.Part = torso;
            pawn.health.RemoveHediff(record); Equal(original, record.Description);
        });
        Check("three-state description is used by native tooltip with stage and fertility effects intact", () =>
        {
            // 独立宿主未运行语言加载后的原版颜色初始化；提供派系称谓并初始化格式化缓存。
            FactionDefOf.PlayerColony ??= new FactionDef { defName = "TooltipPlayerColony", pawnSingular = "殖民者", pawnsPlural = "殖民者" };
            ColoredText.ResetStaticData();
            var pawn = SurgeryPawn(); var record = Start(pawn, 1);
            foreach (bool enabled in new[] { false, true })
            {
                Toggle(pawn, enabled); string description = record.Description;
                string tip = record.GetTooltip(pawn, false);
                Equal(1, tip.Split(description).Length - 1);
                Require(tip.Contains(record.CurStage.label));
                Require(tip.Contains(enabled ? "×2.5" : "×0"));
                Near(enabled ? 2.5f : 0, Factor(pawn));
            }
        });
        // 不保存正文或第三份模式；由既有 Severity 和开关经过真实 Scribe 回环后重新选择。
        foreach (var state in new[] { (.3f, false, "Developing"), (1f, false, "Infertile"), (1f, true, "Fertile") })
            Check("three-state description derives from real Scribe round-trip: " + state, () =>
            {
                var pawn = SurgeryPawn(); Start(pawn, state.Item1); if (state.Item2) Toggle(pawn, true);
                string path = Save(pawn); string xml = File.ReadAllText(path);
                Require(!xml.Contains("DescriptionDeveloping") && !xml.Contains("DescriptionInfertile") && !xml.Contains("DescriptionFertile"));
                var loaded = Load(path); RequireDescription(SSCUterusDevelopmentUtility.Find(loaded), state.Item3);
            });
        Check("three-state description old save missing toggle uses completed infertility text", () =>
        {
            var pawn = SurgeryPawn(); Start(pawn, 1); Toggle(pawn, true); string path = Save(pawn);
            Edit(path, doc => doc.Descendants("sscUterusFertilityEnabled").Remove());
            RequireDescription(SSCUterusDevelopmentUtility.Find(Load(path)), "Infertile");
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("three-state description uses current language without caching: " + language, () =>
            {
                var pawn = SurgeryPawn(); var record = Start(pawn, .7f);
                LoadBodyManagementLanguage(repo, language);
                // 四语目前使用同一套中文占位；给真实语言容器附不同标记，检测硬编码和旧语言缓存。
                foreach (string suffix in new[] { "Developing", "Infertile", "Fertile" })
                    LanguageDatabase.activeLanguage.keyedReplacements["SSC_UterusDevelopment_Description" + suffix].value += "（语言检查：" + language + "）";
                RequireDescription(record, "Developing"); Require(record.Description.Contains(language));
                record.Severity = 1; RequireDescription(record, "Infertile"); Toggle(pawn, true); RequireDescription(record, "Fertile");
                LoadBodyManagementLanguage(repo, "ChineseSimplified"); Require(!record.Description.Contains("语言检查"));
            });
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
    }
}
