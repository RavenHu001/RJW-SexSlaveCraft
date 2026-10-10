using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

// 定义数值、镜像与四语文案用例：只读取生产资源，不复制数值表。
internal static partial class Program
{
    /// <summary>四语目录，与工程发布语言一致。</summary>
    private static readonly string[] Languages =
    {
        "ChineseSimplified", "ChineseTraditional", "English", "Russian"
    };

    /// <summary>弱套 XML 中应存在的 8 个 Def 名称。</summary>
    private static readonly string[] TrainerDefNames =
    {
        "SSC_TrainerTraining_MoodDynamic",
        "SSC_TrainerTraining_OpinionDynamic",
        "SSC_TrainerTraining_Mood_Lvl1",
        "SSC_TrainerTraining_Mood_Lvl2",
        "SSC_TrainerTraining_Mood_Lvl3",
        "SSC_TrainerTraining_Social_Lvl1",
        "SSC_TrainerTraining_Social_Lvl2",
        "SSC_TrainerTraining_Social_Lvl3"
    };

    /// <summary>弱套 XML 与主人原套按 K 互相校核，并确认原套数值未被改写。</summary>
    private static void DefinitionAlignment()
    {
        // 新模式心情：四档弱值为主人基准的整数弱值。
        AssertWeakMood(SSCDefOf.SSC_Training_MoodDynamic, SSCDefOf.SSC_TrainerTraining_MoodDynamic, 4);
        // 旧模式心情：单阶段，-10／+2／+15 的整数弱值。
        AssertWeakMood(SSCDefOf.SSC_Training_Mood_Lvl1, SSCDefOf.SSC_TrainerTraining_Mood_Lvl1, 1);
        AssertWeakMood(SSCDefOf.SSC_Training_Mood_Lvl2, SSCDefOf.SSC_TrainerTraining_Mood_Lvl2, 1);
        AssertWeakMood(SSCDefOf.SSC_Training_Mood_Lvl3, SSCDefOf.SSC_TrainerTraining_Mood_Lvl3, 1);

        // 旧模式社交：主人原套的运行值由启动修正确定，弱套直接写入整数弱值。
        AssertWeakSocial(SSCDefOf.SSC_Training_Social_Lvl1, SSCDefOf.SSC_TrainerTraining_Social_Lvl1);
        AssertWeakSocial(SSCDefOf.SSC_Training_Social_Lvl2, SSCDefOf.SSC_TrainerTraining_Social_Lvl2);
        AssertWeakSocial(SSCDefOf.SSC_Training_Social_Lvl3, SSCDefOf.SSC_TrainerTraining_Social_Lvl3);

        // 主人原套的启动修正必须保持原值，弱套不得被同一次修正改写。
        Equal(5, (int)SSCDefOf.SSC_Training_Social_Lvl2.stages[0].baseOpinionOffset, "主人等级 2 运行值");
        Equal(15, (int)SSCDefOf.SSC_Training_Social_Lvl3.stages[0].baseOpinionOffset, "主人等级 3 运行值");
        Equal(3, (int)SSCDefOf.SSC_TrainerTraining_Social_Lvl2.stages[0].baseOpinionOffset, "弱等级 2 运行值");
        Equal(8, (int)SSCDefOf.SSC_TrainerTraining_Social_Lvl3.stages[0].baseOpinionOffset, "弱等级 3 运行值");

        // 弱套必须与主人原套使用不同的 Def 实例，避免共存时互相覆盖。
        foreach (ThoughtDef def in TrainerDefNames.Select(name => DefDatabase<ThoughtDef>.GetNamedSilentFail(name)))
        {
            Assert(def != null, "弱套 Def 必须已加载");
        }

        var trainerDefs = TrainerDefNames.Select(DefDatabase<ThoughtDef>.GetNamedSilentFail).ToList();
        var masterDefs = new[]
        {
            SSCDefOf.SSC_Training_MoodDynamic, SSCDefOf.SSC_Training_OpinionDynamic,
            SSCDefOf.SSC_Training_Mood_Lvl1, SSCDefOf.SSC_Training_Mood_Lvl2, SSCDefOf.SSC_Training_Mood_Lvl3,
            SSCDefOf.SSC_Training_Social_Lvl1, SSCDefOf.SSC_Training_Social_Lvl2, SSCDefOf.SSC_Training_Social_Lvl3
        };
        Assert(!trainerDefs.Intersect(masterDefs).Any(), "弱套不得复用主人原套 Def");

        // 容量的初稿口径：动态心情 1 天／3，动态社交 15 天／40，与原套一致。
        Equal(SSCDefOf.SSC_Training_MoodDynamic.durationDays.ToString(), SSCDefOf.SSC_TrainerTraining_MoodDynamic.durationDays.ToString(), "动态心情时长");
        Equal(SSCDefOf.SSC_Training_MoodDynamic.stackLimit, SSCDefOf.SSC_TrainerTraining_MoodDynamic.stackLimit, "动态心情容量");
        Assert(SSCDefOf.SSC_TrainerTraining_OpinionDynamic.showBubble, "动态社交保留气泡");

        // 弱社交与弱动态社交必须使用正确的实例类。
        Equal("Thought_MemorySocial", SSCDefOf.SSC_TrainerTraining_Social_Lvl1.thoughtClass.Name, "弱固定社交类");
        Equal("Thought_MemoryTrainerTraining", SSCDefOf.SSC_TrainerTraining_OpinionDynamic.thoughtClass.Name, "弱动态社交类");
        Equal("Thought_Memory", SSCDefOf.SSC_TrainerTraining_Mood_Lvl1.thoughtClass.Name, "弱固定心情类");
    }

    /// <summary>逐阶段比较主人与弱心情，弱值必须是主人基准的整数弱值。</summary>
    private static void AssertWeakMood(ThoughtDef master, ThoughtDef weak, int stageCount)
    {
        Equal(stageCount, master.stages.Count, master.defName + " 主人阶段数");
        Equal(stageCount, weak.stages.Count, weak.defName + " 弱套阶段数");
        for (int i = 0; i < stageCount; i++)
        {
            int baseline = (int)master.stages[i].baseMoodEffect;
            int expected = TrainerTrainingMemoryUtility.WeakValue(baseline);
            Equal(expected, (int)weak.stages[i].baseMoodEffect, $"{weak.defName} 阶段 {i} 弱心情");
            Assert(Math.Abs(weak.stages[i].baseMoodEffect) <= Math.Abs(master.stages[i].baseMoodEffect)
                || baseline == 0, $"{weak.defName} 阶段 {i} 必须弱于主人");
            Assert(weak.stages[i].baseMoodEffect == Math.Truncate(weak.stages[i].baseMoodEffect), "弱心情必须为整数");
        }

        Equal(master.durationDays.ToString(), weak.durationDays.ToString(), weak.defName + " 时长沿用");
        Equal(master.stackLimit, weak.stackLimit, weak.defName + " 容量沿用");
    }

    /// <summary>比较单阶段主人与弱社交的运行值与容量字段。</summary>
    private static void AssertWeakSocial(ThoughtDef master, ThoughtDef weak)
    {
        int baseline = (int)master.stages[0].baseOpinionOffset;
        Equal(TrainerTrainingMemoryUtility.WeakValue(baseline), (int)weak.stages[0].baseOpinionOffset,
            weak.defName + " 弱社交运行值");
        Assert(Math.Abs(weak.stages[0].baseOpinionOffset) < Math.Abs(baseline), weak.defName + " 必须弱于主人");
        Equal(master.durationDays.ToString(), weak.durationDays.ToString(), weak.defName + " 时长沿用");
        Equal(master.stackLimit, weak.stackLimit, weak.defName + " 容量沿用");
        Equal(master.stackLimitForSameOtherPawn, weak.stackLimitForSameOtherPawn, weak.defName + " 同对象容量沿用");
    }

    /// <summary>根目录 Def 与源码镜像、四语 DefInjected 与 Keyed 的键集与占位符一致。</summary>
    private static void LocalizationParity()
    {
        // Def 文件镜像必须逐字节一致，避免发布包与源码目录出现两套数值。
        string rootDef = Path.Combine(repoRoot, "Defs/ThoughtDefs/SSC_TrainerTrainingMemories.xml");
        string mirrorDef = Path.Combine(repoRoot, "Sexslavecraft/Defs/ThoughtDefs/SSC_TrainerTrainingMemories.xml");
        Assert(File.Exists(mirrorDef), "源码目录必须保留 Def 镜像");
        Equal(File.ReadAllText(rootDef), File.ReadAllText(mirrorDef), "Def 镜像内容");

        // 四语 DefInjected：键集一致、无重复，且动态社交的占位符与阶段数匹配。
        var reference = LoadKeys(repoRoot, "Languages/ChineseSimplified/DefInjected/ThoughtDef/SSC_TrainerTrainingMemories.xml");
        foreach (string language in Languages)
        {
            string relative = $"Languages/{language}/DefInjected/ThoughtDef/SSC_TrainerTrainingMemories.xml";
            var keys = LoadKeys(repoRoot, relative);
            Assert(keys.SequenceEqual(reference), $"{language} 的 DefInjected 键集必须一致");
            Assert(keys.Distinct().Count() == keys.Count, $"{language} 的 DefInjected 键不得重复");
            foreach (var entry in LoadEntries(repoRoot, relative))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value), $"{language} 的 {entry.Key} 不得为空");
                if (entry.Key.EndsWith(".description", StringComparison.Ordinal)
                    && entry.Key.StartsWith("SSC_TrainerTraining_OpinionDynamic", StringComparison.Ordinal))
                {
                    // 动态社交描述需要执行者与偏移两个占位符。
                    Assert(entry.Value.Contains("{0}"), $"{language} 的 {entry.Key} 必须含执行者占位符");
                    Assert(entry.Value.Contains("{1}"), $"{language} 的 {entry.Key} 必须含偏移占位符");
                }
                else if (entry.Key.EndsWith(".description", StringComparison.Ordinal))
                {
                    Assert(!entry.Value.Contains("{0}"), $"{language} 的 {entry.Key} 不应含占位符");
                }
            }

            // 镜像目录同步。
            string mirrorRelative = $"Sexslavecraft/{relative}";
            Assert(File.Exists(Path.Combine(repoRoot, mirrorRelative.Replace('/', Path.DirectorySeparatorChar))),
                $"{language} 的 DefInjected 必须存在源码镜像");
            Equal(File.ReadAllText(Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar))),
                File.ReadAllText(Path.Combine(repoRoot, mirrorRelative.Replace('/', Path.DirectorySeparatorChar))),
                $"{language} 的 DefInjected 镜像内容");
        }

        // 四语 Keyed：动态社交运行时描述的 4 标签与 4 描述键集一致且占位符正确。
        var keyedReference = LoadKeys(repoRoot, "Languages/ChineseSimplified/Keyed/SSC_TrainerTrainingMemory.xml");
        Equal(8, keyedReference.Count, "Keyed 键数量");
        foreach (string language in Languages)
        {
            string relative = $"Languages/{language}/Keyed/SSC_TrainerTrainingMemory.xml";
            var keys = LoadKeys(repoRoot, relative);
            Assert(keys.SequenceEqual(keyedReference), $"{language} 的 Keyed 键集必须一致");
            Assert(keys.Distinct().Count() == keys.Count, $"{language} 的 Keyed 键不得重复");
            foreach (var entry in LoadEntries(repoRoot, relative))
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value), $"{language} 的 {entry.Key} 不得为空");
                if (entry.Key.EndsWith("Desc", StringComparison.Ordinal))
                {
                    // 运行时描述必须含执行者与偏移两个占位符，并能被真实格式化。
                    Assert(entry.Value.Contains("{0}") && entry.Value.Contains("{1}"),
                        $"{language} 的 {entry.Key} 必须含两个占位符");
                    string formatted = string.Format(entry.Value, "测试", "+6");
                    Assert(!formatted.Contains("{0}") && !formatted.Contains("{1}"),
                        $"{language} 的 {entry.Key} 必须可格式化");
                }
                else
                {
                    Assert(!entry.Value.Contains("{0}") && !entry.Value.Contains("{1}"),
                        $"{language} 的 {entry.Key} 标签不应含占位符");
                }
            }

            string mirrorRelative = $"Sexslavecraft/{relative}";
            Assert(File.Exists(Path.Combine(repoRoot, mirrorRelative.Replace('/', Path.DirectorySeparatorChar))),
                $"{language} 的 Keyed 必须存在源码镜像");
            Equal(File.ReadAllText(Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar))),
                File.ReadAllText(Path.Combine(repoRoot, mirrorRelative.Replace('/', Path.DirectorySeparatorChar))),
                $"{language} 的 Keyed 镜像内容");
        }

        // 真实记忆类必须能从 Keyed 文件取到全部四个档位的标签与描述。
        foreach (string stage in new[] { "Resentment", "Wavering", "Adaptation", "Submission" })
        {
            Assert(Verse.Extensions.Translations.ContainsKey("SSC_TrainerTrainingOpinionMemory_" + stage),
                "缺少标签键：" + stage);
            Assert(Verse.Extensions.Translations.ContainsKey("SSC_TrainerTrainingOpinionMemory_" + stage + "Desc"),
                "缺少描述键：" + stage);
        }
    }

    private static List<string> LoadKeys(string repoRootPath, string relative)
        => LoadEntries(repoRootPath, relative).Select(e => e.Key).ToList();

    /// <summary>保持 XML 文档顺序读取键值，便于比较键集。</summary>
    private static List<KeyValuePair<string, string>> LoadEntries(string repoRootPath, string relative)
    {
        var document = XDocument.Load(Path.Combine(repoRootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        return document.Root.Elements()
            .Select(e => new KeyValuePair<string, string>(e.Name.LocalName, e.Value))
            .ToList();
    }
}
