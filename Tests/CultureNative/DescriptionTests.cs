// AI 辅助生成：六种原版故事框架的实际请求、段落解析与描述生命周期。
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Grammar;

internal static partial class Program
{
    private static object DescriptionRequest(Ideo ideo, IdeoStoryPatternDef pattern)
    {
        // 原版接口为 internal，反射仅跨越可见性；创始者／信徒／地点／神祇规则均由实际方法构建。
        return AccessTools.Method(DescriptionUtility, "BuildGrammarRequest").Invoke(null, new object[] { ideo, pattern, new Dictionary<string, string>() });
    }

    private static void DescriptionTests(string language)
    {
        Check("native final localized copy resolves all 17 segments: " + language, () =>
        {
            // 先解析交付的正式段落，确认文字能进入真实生成器；随后才安装只用于隔离判定的内存探针。
            var ideo = NamingIdeo(true);
            var evaluator = DescriptionRequest(ideo, DefDatabase<IdeoStoryPatternDef>.GetNamed("TheistMyth"));
            for (int i = 0; i < DescriptionKeywords.Length; i++)
            {
                string text = (string)AccessTools.Method(evaluator.GetType(), "NextSegment").Invoke(evaluator,
                    new object[] { DescriptionKeywords[i], "meme_SSC_MasterSlaveOrder", false });
                ValidGenerated(text); RequireFinalCopy(text);
                Require(string.Equals(mainMeme.descriptionMaker.rules.Rules[i].Generate(), text, StringComparison.OrdinalIgnoreCase), text);
            }
        });
        foreach (string name in StoryNames)
            Check("native full framework resolves final localized text " + name + ": " + language, () =>
            {
                // 实际正式长文与原版框架也须整体解析；来源隔离和 uses=1 另由探针给出无歧义判定。
                var pattern = DefDatabase<IdeoStoryPatternDef>.GetNamed(name);
                var structure = namingStructures.First(s => s.descriptionMaker?.patterns?.Any(p => p.def == pattern) == true);
                foreach (bool with in new[] { true, false })
                {
                    var ideo = NamingIdeo(with, structure); AddNamingTokens(ideo);
                    string text = Seeded(7, () => ((IdeoDescriptionResult)AccessTools.Method(DescriptionUtility, "ResolveDescription")
                        .Invoke(null, new object[] { ideo, pattern, true })).text);
                    ValidGenerated(text); RequireFinalCopy(text); Require(!text.Contains("%%"));
                }
            });

        // 替换只发生在测试对象，保留每条规则的实际关键词、tag 和 uses；finally 恢复正式规则。
        // 探针在有模因的正例中必须出现，因此无模因／移除后的“不出现”不可能因文案换掉而空泛通过。
        var saved = mainMeme.descriptionMaker.rules;
        try
        {
            var rules = new XElement("rulesStrings", saved.Rules.Select((r, i) => new XElement("li",
                r.keyword + "(" + (r.usesLimit.HasValue ? $"uses={r.usesLimit.Value}," : "") + $"tag={r.tag})->SSCTESTDESC{i + 1:00}")));
            mainMeme.descriptionMaker.rules = DirectXmlToObject.ObjectFromXml<RulePack>(Xml(new XElement("rules", rules)), false);
            DescriptionProbeTests(language);
        }
        finally { mainMeme.descriptionMaker.rules = saved; }
    }

    private static void DescriptionProbeTests(string language)
    {
        Check("native description requests gate all 17 SSC rules by current meme: " + language, () =>
        {
            // 同进程有／无模因交替，检查请求实际包含的规则，避免随机没选中被误当成隔离成功。
            for (int pass = 0; pass < 4; pass++)
            {
                bool with = pass % 2 == 0; var ideo = NamingIdeo(with);
                var pattern = DefDatabase<IdeoStoryPatternDef>.GetNamed("TheistMyth");
                object evaluator = DescriptionRequest(ideo, pattern);
                var request = (GrammarRequest)AccessTools.Field(evaluator.GetType(), "request").GetValue(evaluator);
                Equal(with, request.IncludesBare.Any(p => p == mainMeme.descriptionMaker.rules));
                if (with)
                {
                    // 明确指定原版分组偏好，只验证每类可被解析，不强迫正常混合描述的每段都使用 SSC。
                    for (int i = 0; i < DescriptionKeywords.Length; i++)
                    {
                        string text = (string)AccessTools.Method(evaluator.GetType(), "NextSegment").Invoke(evaluator,
                            new object[] { DescriptionKeywords[i], "meme_SSC_MasterSlaveOrder", false });
                        Equal($"SSCTESTDESC{i + 1:00}", text);
                    }
                }
            }
        });
        foreach (string name in StoryNames)
            Check("native full description framework and generic fallback " + name + ": " + language, () =>
            {
                // 使用实际框架 segments 和规则，补足真实职位／仪式 token；不手工串接段落结果。
                var pattern = DefDatabase<IdeoStoryPatternDef>.GetNamed(name);
                var structure = namingStructures.First(s => s.descriptionMaker?.patterns?.Any(p => p.def == pattern) == true);
                foreach (bool with in new[] { true, false })
                {
                    var ideo = NamingIdeo(with, structure); AddNamingTokens(ideo);
                    bool sawSSC = false;
                    for (int seed = 0; seed < 8; seed++)
                    {
                        // ResolveDescription 执行框架及解析；GetNewDescription 另测结构的真实框架选择。
                        string text = Seeded(seed, () =>
                        {
                            var result = (IdeoDescriptionResult)AccessTools.Method(DescriptionUtility, "ResolveDescription").Invoke(null, new object[] { ideo, pattern, true });
                            return result.text;
                        });
                        ValidGenerated(text); Require(!text.Contains("%%"));
                        sawSSC |= text.Contains("SSCTESTDESC");
                        if (!with) Require(!text.Contains("SSCTESTDESC"));
                        // 重复位置必须遵守单条 uses=1，并允许框架的通用／其他模因内容补足。
                        foreach (string marker in new[] { "SSCTESTDESC02", "SSCTESTDESC08" })
                            Require(text.Split(marker).Length <= 2, text);
                    }
                    if (with) Require(sawSSC);
                    ideo.RegenerateDescription(true); ValidGenerated(ideo.description);
                }
            });
        Check("native description locks, templates, Scribe and meme removal: " + language, () =>
        {
            var ideo = NamingIdeo(true); AddNamingTokens(ideo);
            // 原版允许混合其他来源；固定种子中选一个确有 SSC 段落的正例，再验证锁定与移除后的消退。
            for (int seed = 0; seed < 8; seed++)
            {
                Seeded(seed, () => { ideo.RegenerateDescription(true); return ideo.description; });
                if (ideo.description.Contains("SSCTESTDESC")) break;
            }
            string generated = ideo.description, template = ideo.descriptionTemplate;
            Require(!string.IsNullOrEmpty(template));
            Require(generated.Contains("SSCTESTDESC"));
            // 未重新生成时保留历史文字；移除模因不清洗已保存模板。
            ideo.memes.Remove(mainMeme); Equal(generated, ideo.description);
            ideo.descriptionLocked = true; ideo.RegenerateDescription(true); Equal(generated, ideo.description);
            // XML 保存加载会规范化 CRLF／LF；只规范换行后比较全文，不能 trim 或删除内容掩盖变化。
            var loaded = NamingRoundtrip(ideo);
            Equal(generated.Replace("\r\n", "\n"), loaded.description.Replace("\r\n", "\n"));
            Equal(template.Replace("\r\n", "\n"), loaded.descriptionTemplate.Replace("\r\n", "\n")); Require(loaded.descriptionLocked);
            loaded.RegenerateDescription(true); Equal(generated.Replace("\r\n", "\n"), loaded.description.Replace("\r\n", "\n"));
            // force 指重新抽取原版模板，不能越过锁；明确解锁后才能检查新候选已隔离。
            loaded.descriptionLocked = false; loaded.RegenerateDescription(true);
            ValidGenerated(loaded.description); Require(!loaded.description.Contains("SSCTESTDESC"));
        });
    }

    private static void AddNamingTokens(Ideo ideo)
    {
        // 实际职位和仪式对象提供原版 token，称谓由当前规则生成。遗物／动物缺失时保留原版分支回退。
        ideo.foundation.GenerateLeaderTitle();
        var leader = Role(ideo, leaderDef); Set(leader, "name", ideo.leaderTitleMale);
        var moralist = Role(ideo, moralistDef); moralist.RegenerateName();
        var ritual = Ritual(ideo, bindingPattern); ritual.RegenerateName();
    }
}
