// AI 辅助生成：真实 LoadedLanguage / Translate 路径，局部装载生产键值，不扫描玩家安装的语言目录。
using System.Xml.Linq;
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>为独立 CLR 提供真实语言容器和生产 XML 键值，保留原版查键及格式化逻辑。</summary>
    private static void LoadBodyManagementLanguage(string repo, string language)
    {
        var loaded = Bare<LoadedLanguage>(); loaded.folderName = language;
        AccessTools.Field(typeof(LoadedLanguage), "legacyFolderName").SetValue(loaded, language);
        loaded.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
        loaded.stringFiles = new(); loaded.keyedReplacements = new(); loaded.loadErrors = new();
        foreach (string file in new[] { "SSC_Keys.xml", "SSC_BodyManagement.xml", "SSC_UterusDevelopment.xml", "SSC_Cosmetic.xml", "SSC_HediffFertility.xml" })
            foreach (var entry in XDocument.Load(Path.Combine(repo, "1.7/Languages", language, "Keyed", file)).Root.Elements())
                loaded.keyedReplacements.Add(entry.Name.LocalName, new LoadedLanguage.KeyedReplacement
                    { key = entry.Name.LocalName, value = entry.Value.Replace("\\n", "\n") });
        // 本专项只装载上述键，不触发完整模组语言扫描或 Def 注入；实际 Translate 不被替换。
        AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(loaded, true);
        AccessTools.Field(typeof(LoadedLanguage), "wordInfo").SetValue(loaded, new LanguageWordInfo());
        LanguageDatabase.activeLanguage = LanguageDatabase.defaultLanguage = loaded;
    }

    /// <summary>四语均通过原版翻译器取得精简界面标题、开关和拒绝原因，不显示代码键。</summary>
    private static void BodyManagementLanguageTests(string repo)
    {
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("body controls native keyed lookup: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                string title = "SSC_BodyManagement_Title".Translate();
                string toggle = "SSC_BodyManagement_FertilityToggle".Translate();
                var entries = XDocument.Load(Path.Combine(repo, "1.7/Languages", language, "Keyed/SSC_BodyManagement.xml")).Root;
                Equal(entries.Element("SSC_BodyManagement_Title").Value, title);
                Equal(entries.Element("SSC_BodyManagement_FertilityToggle").Value, toggle);
                Require(!title.Contains("SSC_") && !toggle.Contains("SSC_"));
                Require(!SSCUterusDevelopmentUtility.CanSetFertilityEnabled(NewPawn(), true, out string reason));
                Equal("SSC_BodyManagement_NotStarted".Translate().ToString(), reason); Require(!reason.Contains("SSC_"));
                string milk = Strings.ITab_MilkProductionToggle;
                Equal("SSC_ITab_MilkProductionToggle".Translate().ToString(), milk); Require(!milk.Contains("SSC_"));
            });
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
    }
}
