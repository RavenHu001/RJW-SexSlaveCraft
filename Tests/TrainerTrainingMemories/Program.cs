using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using UnityEngine;

// 训导官弱化套回归：直接运行生产套别分流、记忆发放、动态社交实例类与人格链路。
internal static partial class Program
{
    private static int passed;
    private static int failed;
    private static string repoRoot;

    private static int Main(string[] args)
    {
        repoRoot = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        LoadProductionDefs();
        LoadTranslations();

        Run("弱值换算：四舍五入到整数并对非零基准保底 ±1", WeakValueRule);
        Run("弱值换算：新模式好感变化 D 的整数基准", WeakOpinionOffsetRule);
        Run("套别分流：有效训导官 / 主人 / 失格性奴 / 空对象", MemorySetResolution);
        Run("套别分流：同一角色先后合法改变身份时各自归类", MemorySetFollowsCurrentLegality);
        Run("新模式主人路径：原套 Def、原值与零变化不发", MasterDynamicDispensing);
        Run("新模式训导官路径：单发弱套并按未弱化 D 定档", OfficerDynamicDispensing);
        Run("失格执行者：本次不发任何套且不回退强套", InvalidOfficerDispensing);
        Run("旧模式：三档按等级发原套或弱套", LegacyLevelDispensing);
        Run("旧模式仪式：不新增弱套且不发社交记忆", LegacyRitualDispensing);
        Run("动态社交记忆：按保存阶段显示与分组", DynamicSocialBehaviour);
        Run("动态社交记忆：同档刷新、异档共存且对象隔离", DynamicSocialStacking);
        Run("人格链路：弱套定义、阶段与整数偏移完整恢复", PersonalityRoundTrip);
        Run("定义数值：弱套 XML 与主人原套按 K 互相校核", DefinitionAlignment);
        Run("定义镜像与四语文案：键集与占位符一致", LocalizationParity);

        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>加载生产 Def XML 并运行生产常量入口，使 SSCDefOf 与发布时走同一路径。</summary>
    private static void LoadProductionDefs()
    {
        DefDatabase<ThoughtDef>.Clear();
        foreach (string path in TrainerTrainingMemories.ThoughtDefLoader.MasterDefsRelativePaths)
        {
            TrainerTrainingMemories.ThoughtDefLoader.LoadFile(repoRoot, path);
        }

        TrainerTrainingMemories.ThoughtDefLoader.LoadFile(repoRoot, TrainerTrainingMemories.ThoughtDefLoader.TrainerDefsRelativePath);
        SSCDefOf.Initialize();
        // 直接触发生产启动构造入口，使主人原套的社交运行值（+5／+15）与本机游戏一致，
        // 并同时验证弱套旧社交不会被二次改写。
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(SSCThoughtBootstrap).TypeHandle);
    }

    /// <summary>运行时文案只装载这一语言，避免四语同键互相覆盖而掩盖真实显示路径。</summary>
    private const string ReferenceLanguage = "ChineseSimplified";

    /// <summary>加载四语关键文案，使真实记忆类的显示路径可以运行。</summary>
    private static void LoadTranslations()
    {
        Verse.Extensions.Translations.Clear();
        foreach (string language in new[] { ReferenceLanguage })
        {
            foreach (string relative in new[]
                     {
                         $"Languages/{language}/Keyed/SSC_TrainerTrainingMemory.xml",
                         $"Languages/{language}/DefInjected/ThoughtDef/SSC_TrainerTrainingMemories.xml"
                     })
            {
                var document = XDocument.Load(Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                foreach (XElement entry in document.Root.Elements())
                {
                    Verse.Extensions.Translations[entry.Name.LocalName] = entry.Value;
                }
            }
        }
    }

    /// <summary>建立带 SSC 训练组件的模型角色；短名逐一带序号以便区分不同对象。</summary>
    private static Pawn NewPawn(PawnIdentity identity, bool trainerQualification = false, bool trainerEnabled = false,
        bool trainerIdentityInitialized = false)
    {
        var pawn = new Pawn { LabelShort = "角色" + (++pawnSerial) };
        var comp = new CompSexSlaveTraining
        {
            pawnIdentity = identity,
            slaveTrainerEnabled = trainerEnabled,
            HasTrainerQualification = trainerQualification,
            trainerIdentityInitialized = trainerIdentityInitialized || trainerEnabled
        };
        pawn.AddComp(comp);
        return pawn;
    }

    private static int pawnSerial;

    /// <summary>清空角色记忆，保证每个用例从空状态开始。</summary>
    private static void ClearMemories(Pawn pawn) => pawn.needs.mood.thoughts.memories.Memories.Clear();

    private static List<Thought_Memory> MemoriesOf(Pawn pawn) => pawn.needs.mood.thoughts.memories.Memories;

    /// <summary>
    /// 按定义取记忆。
    /// 动态心情与动态社交共用同一 Def 家族，档位通过阶段表达，因此这里额外用实例类型区分，
    /// 避免把同 Tie 的心情记忆计入社交断言。
    /// </summary>
    private static List<Thought_Memory> MemoriesWithDef(Pawn pawn, ThoughtDef def)
        => MemoriesOf(pawn)
            .Where(m => ReferenceEquals(m.def, def) && m.GetType() == def.thoughtClass)
            .ToList();

    /// <summary>运行独立场景并在失败后继续报告其余场景。</summary>
    private static void Run(string name, Action test)
    {
        SSCMod.settings = new Settings();
        Rand.RangeValue = 0f;
        Rand.ChanceResult = false;
        Scribe_Values.Reset();
        try
        {
            test();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine("FAIL " + name + ": " + ex.Message);
        }
    }

    /// <summary>要求场景符合预期，否则报告调用位置。</summary>
    private static void Assert(bool condition, string message = "断言失败")
    {
        if (!condition) throw new Exception(message);
    }

    private static void Equal(int expected, int actual, string message)
        => Assert(expected == actual, $"{message}：期望 {expected}，实际 {actual}");

    private static void Equal(string expected, string actual, string message)
        => Assert(expected == actual, $"{message}：期望 {expected}，实际 {actual}");
}
