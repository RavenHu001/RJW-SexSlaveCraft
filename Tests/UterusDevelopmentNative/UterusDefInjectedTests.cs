// AI 辅助生成：用目标游戏的语言注入器验证 v2 四语 Def 文案，包含自定义扩展阶段。
using System.Xml.Linq;
using RimWorld;
using RimWorld.IO;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>仅适配工作区文件读取；XML 解析、路径解析及字段写入全部由原版语言注入器执行。</summary>
    private sealed class TranslationFile(string path) : VirtualFile
    {
        public override string Name => Path.GetFileName(path);
        public override string FullPath => path;
        public override bool Exists => File.Exists(path);
        public override long Length => new FileInfo(path).Length;
        public override Stream CreateReadStream() => File.OpenRead(path);
        public override byte[] ReadAllBytes() => File.ReadAllBytes(path);
        public override string[] ReadAllLines() => File.ReadAllLines(path);
        public override string ReadAllText() => File.ReadAllText(path);
    }

    /// <summary>加载真实 DefInjected XML 并执行原版注入，拒绝解析错误、无效路径和未注入条目。</summary>
    private static Dictionary<string, string> InjectUterusText(string repo, string language, Type type)
    {
        string path = Path.Combine(repo, "1.7/Languages", language, "DefInjected", type.Name, "SSC_UterusDevelopment.xml");
        var file = new TranslationFile(path);
        var package = new DefInjectionPackage(type);
        package.AddDataFromFile(file, out bool parseError, file.ReadAllText());
        Require(!parseError && package.loadErrors.Count == 0, string.Join("; ", package.loadErrors));
        package.InjectIntoDefs(true);
        Require(package.loadErrors.Count == 0, string.Join("; ", package.loadErrors));
        Require(package.injections.Values.All(entry => entry.injected));
        return XDocument.Load(path).Root.Elements().ToDictionary(entry => entry.Name.LocalName, entry => entry.Value);
    }

    /// <summary>同一批真实 Def 连续切换四语再回到简中，验证名称、正文、五个阶段与实际健康悬停。</summary>
    private static void UterusDefInjectedTests(string repo)
    {
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian", "ChineseSimplified" })
            Check("v2 native DefInjected research surgery stages and tooltip: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                var research = InjectUterusText(repo, language, typeof(ResearchProjectDef));
                Equal(research[uterusResearch.defName + ".label"], uterusResearch.label);
                Equal(research[uterusResearch.defName + ".description"], uterusResearch.description);
                Require("SSC_UterusDevelopment_ResearchRequired".Translate().ToString().Contains(uterusResearch.label));
                var surgery = InjectUterusText(repo, language, typeof(RecipeDef));
                Equal(surgery[uterusSurgery.defName + ".label"], uterusSurgery.label);
                Equal(surgery[uterusSurgery.defName + ".description"], uterusSurgery.description);
                Equal(surgery[uterusSurgery.defName + ".jobString"], uterusSurgery.jobString);
                var hediff = InjectUterusText(repo, language, typeof(HediffDef));
                Equal(hediff[development.defName + ".label"], development.label);
                Equal(hediff[development.defName + ".description"], development.description);
                for (int i = 0; i < 4; i++) Equal(hediff[$"{development.defName}.stages.{i}.label"], development.stages[i].label);
                string fertile = hediff[development.defName + ".modExtensions.0.fertileStage.label"];
                Equal(fertile, development.GetModExtension<SSCUterusDevelopmentExtension>().fertileStage.label);

                // 健康页读取同一批已注入的真实实例；只切换开关，不重建或手动替换 CurStage。
                var pawn = SurgeryPawn(); var record = Start(pawn, 1);
                foreach (bool enabled in new[] { false, true })
                {
                    Toggle(pawn, enabled);
                    Equal(enabled ? fertile : development.stages[3].label, record.CurStage.label);
                    string tip = record.GetTooltip(pawn, false);
                    Require(tip.Contains(record.CurStage.label) && tip.Contains(record.Description));
                    Require(!tip.Contains("SSC_UterusDevelopment_") && !tip.Contains("正式文案待定"));
                    Near(enabled ? 2.5f : 0, Factor(pawn));
                }
                Near(2000, uterusResearch.baseCost); Near(4000, uterusSurgery.workAmount);
                Near(0, uterusSurgery.deathOnFailedSurgeryChance);
            });
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
    }
}
