// AI 辅助生成：六种原版故事框架的实际请求、段落解析与描述生命周期。
using System.Reflection;
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
                        Equal($"SSCDESCTODO{i + 1:00}", text);
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
                        sawSSC |= text.Contains("SSCDESCTODO");
                        if (!with) Require(!text.Contains("SSCDESCTODO"));
                        // 重复位置必须遵守单条 uses=1，并允许框架的通用／其他模因内容补足。
                        foreach (string marker in new[] { "SSCDESCTODO02", "SSCDESCTODO08" })
                            Require(text.Split(marker).Length <= 2, text);
                    }
                    if (with) Require(sawSSC);
                    ideo.RegenerateDescription(true); ValidGenerated(ideo.description);
                }
            });
        Check("native description locks, templates, Scribe and meme removal: " + language, () =>
        {
            var ideo = NamingIdeo(true); AddNamingTokens(ideo); ideo.RegenerateDescription(true);
            string generated = ideo.description, template = ideo.descriptionTemplate;
            Require(!string.IsNullOrEmpty(template));
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
            ValidGenerated(loaded.description); Require(!loaded.description.Contains("SSCDESCTODO"));
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
