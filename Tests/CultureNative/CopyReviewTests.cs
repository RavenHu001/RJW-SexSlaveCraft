// AI 辅助生成：文案契约及真实故事框架的单段／整组混合回归；不替换原版拼接算法。
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Formats.Tar;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Grammar;

internal static partial class Program
{
    private static string LocalizedLessonFormat(string language)
    {
        if (language == "English") return null;
        // 只读原版语言归档的实际排版规则；不改归档，不把全部原版翻译已加载作为测试结论。
        string file = Directory.GetFiles(Path.Combine(namingGameRoot, "Data/Ideology/Languages"), language + "*.tar").Single();
        using var stream = File.OpenRead(file);
        using var reader = new TarReader(stream);
        while (reader.GetNextEntry() is { } entry)
        {
            if (!entry.Name.Replace('\\', '/').EndsWith("DefInjected/IdeoStoryPatternDef/RulePacks_Ideo_Description.xml")) continue;
            using var text = new StreamReader(entry.DataStream);
            var xml = XDocument.Parse(text.ReadToEnd());
            return xml.Descendants().Where(e => !e.HasElements && e.Value.StartsWith("r_pattern->"))
                .Select(e => e.Value["r_pattern->".Length..]).Single();
        }
        throw new Exception("Missing localized lesson format: " + language);
    }

    private static void CopyReviewTests(string repo, string language)
    {
        foreach (string patternName in new[] { "TheistFounder", "IdeologicalLesson" })
            Check("native authored single-segment and full-group narrative mixing " + patternName + ": " + language, () =>
            {
                var saved = mainMeme.descriptionMaker.rules;
                var pattern = DefDatabase<IdeoStoryPatternDef>.GetNamed(patternName);
                int formatIndex = pattern.rules.Rules.FindIndex(r => r.keyword == "r_pattern");
                Rule savedFormat = formatIndex >= 0 ? pattern.rules.Rules[formatIndex] : null;
                try
                {
                    if (patternName == "IdeologicalLesson" && LocalizedLessonFormat(language) is string format)
                        pattern.rules.Rules[formatIndex] = new Rule_String("r_pattern", format);
                    var structure = namingStructures.First(s => s.descriptionMaker?.patterns?.Any(p => p.def == pattern) == true);
                    int[] indices = patternName == "TheistFounder" ? new[] { 2, 3, 4, 5 } : new[] { 8, 9, 10 };
                    foreach (int[] selected in indices.Select(i => new[] { i }).Append(indices))
                    {
                        // 只在测试内缩小候选到某一正式片段／整组；其余位置必须走实际原版兜底。
                        // 提高候选优先级以验证每个拼接位置，正文、tag 与 uses 保留，不用于生产文件。
                        var strings = new XElement("rulesStrings", selected.Select(i =>
                        {
                            var r = saved.Rules[i];
                            return new XElement("li", r.keyword + "(priority=1000,tag=" + r.tag + ")->" + r.Generate());
                        }));
                        mainMeme.descriptionMaker.rules = DirectXmlToObject.ObjectFromXml<RulePack>(Xml(new XElement("rules", strings)), false);
                        var ideo = NamingIdeo(true, structure); AddNamingTokens(ideo);
                        bool sawAll = false;
                        for (int seed = 0; seed < 16; seed++)
                        {
                            string text = Seeded(seed, () => ((IdeoDescriptionResult)AccessTools.Method(DescriptionUtility, "ResolveDescription")
                                .Invoke(null, new object[] { ideo, pattern, true })).text);
                            ValidGenerated(text); Require(!text.Contains("%%"));
                            Require(!Regex.IsMatch(text, @"\.\.|。\.|\.。|。。"), "Duplicate framework punctuation: " + text);
                            // 固定正文中的最长字面片段确认目标被选中，不能仅靠输出无错误判定混合覆盖。
                            sawAll |= selected.All(i =>
                            {
                                string signature = Regex.Split(saved.Rules[i].Generate(), @"\[[^\]]+\]")
                                    .OrderByDescending(s => s.Length).First().Trim().TrimEnd('.', '。');
                                return text.Contains(signature, StringComparison.OrdinalIgnoreCase);
                            });
                        }
                        Require(sawAll, "Authored segment selection not observed: " + string.Join(",", selected));
                    }
                }
                finally
                {
                    mainMeme.descriptionMaker.rules = saved;
                    if (savedFormat != null) pattern.rules.Rules[formatIndex] = savedFormat;
                }
            });
    }
}
