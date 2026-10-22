// AI 辅助生成：动态背景使用原版请求、片段解析、姓名别名及模板刷新，不替换生成算法。
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Grammar;

internal static partial class Program
{
    private static void DynamicDescriptionTests(string language)
    {
        foreach (Gender gender in new[] { Gender.Male, Gender.Female })
        {
            Check("native SSC dynamic myth names and template refresh " + gender + ": " + language, () =>
            {
                var ideo = NamingIdeo(true); AddNamingTokens(ideo);
                var foundation = (IdeoFoundation_Deity)ideo.foundation;
                foundation.SetDeities(new() { new() { name = "SSCFIRSTDEITY", type = "god", gender = gender },
                    new() { name = "SSCSECONDDEITY", type = "god", gender = gender } });
                var pattern = DefDatabase<IdeoStoryPatternDef>.GetNamed("TheistMyth");
                var evaluator = DescriptionRequest(ideo, pattern);
                foreach (string key in new[] { "creation", "episode" })
                {
                    string segment = (string)AccessTools.Method(evaluator.GetType(), "NextSegment").Invoke(evaluator,
                        new object[] { key, "meme_SSC_MasterSlaveOrder", false });
                    ValidGenerated(segment); Require(segment.Contains("SSCFIRSTDEITY")); Require(!segment.Contains("SSCSECONDDEITY"));
                }
                // 不强制正常故事选中 SSC；先找到确有该段的真实模板，再检查神名变动后的非强制刷新。
                IdeoDescriptionResult result = null;
                for (int seed = 0; seed < 32; seed++)
                {
                    Seeded(seed, () =>
                    {
                        result = (IdeoDescriptionResult)AccessTools.Method(DescriptionUtility, "ResolveDescription")
                            .Invoke(null, new object[] { ideo, pattern, true });
                        return result.text;
                    });
                    if (result.text.Contains("SSCFIRSTDEITY")) break;
                }
                Require(result.text.Contains("SSCFIRSTDEITY"));
                ideo.descriptionTemplate = result.template;
                Require(ideo.descriptionTemplate.Contains("%%deity0_name%%"));
                foundation.SetDeities(new() { new() { name = "SSCRENAMEDDEITY", type = "god", gender = gender } });
                var refreshed = (IdeoDescriptionResult)AccessTools.Method(DescriptionUtility, "ResolveDescription")
                    .Invoke(null, new object[] { ideo, pattern, false });
                ValidGenerated(refreshed.text); Require(refreshed.text.Contains("SSCRENAMEDDEITY"));
                Require(!refreshed.text.Contains("SSCFIRSTDEITY") && !refreshed.text.Contains("%%"));
            });
            Check("native SSC founder aliases stay consistent " + gender + ": " + language, () =>
            {
                var ideo = NamingIdeo(true);
                var evaluator = DescriptionRequest(ideo, DefDatabase<IdeoStoryPatternDef>.GetNamed("IdeologicalFounder"));
                var field = AccessTools.Field(evaluator.GetType(), "request");
                var request = (GrammarRequest)field.GetValue(evaluator);
                // 只固定请求中的人物数据以便精确断言，姓名别名首次全名／后续简称仍由原版规则处理。
                request.Rules.RemoveAll(r => new[] { "founder_nameFull", "founder_label", "founder_pronoun" }.Contains(r.keyword));
                request.Rules.Add(new Rule_String("founder_nameFull", "SSCFULLFOUNDER"));
                request.Rules.Add(new Rule_String("founder_label", "SSCSHORTFOUNDER"));
                request.Rules.Add(new Rule_String("founder_pronoun", gender.GetPronoun()));
                request.Constants.SetOrAdd("founder_gender", gender.ToString()); field.SetValue(evaluator, request);
                string setup = (string)AccessTools.Method(evaluator.GetType(), "NextSegment").Invoke(evaluator,
                    new object[] { "setup", "meme_SSC_MasterSlaveOrder", false });
                string story = (string)AccessTools.Method(evaluator.GetType(), "NextSegment").Invoke(evaluator,
                    new object[] { "story", "meme_SSC_MasterSlaveOrder", false });
                ValidGenerated(setup); ValidGenerated(story);
                Require(setup.Contains("SSCFULLFOUNDER"), setup); Require(story.Contains("SSCSHORTFOUNDER"), story);
                Require(!story.Contains("SSCFULLFOUNDER"), story);
            });
        }
    }
}
