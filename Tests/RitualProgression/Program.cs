using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

// 本测试程序验证“阶段结算 -> 需求刷新”的数值行为，实际逻辑通过 csproj 链接生产源码。
// 它不会启动 RimWorld，也不运行六阶段仪式 Job；不能据此声称动画、转奴或存档已通过实测。
internal static class Program
{
    private static int failures, cases;

    // 每个用例先重置设置，避免零衰减/旧评分等配置污染下一个用例。
    // 单个用例失败后继续运行其余用例，最后统一以非零退出码报告失败，方便脚本比较新旧代码。
    private static void Check(string name, Action test)
    {
        cases++;
        SSCMod.settings = new TestSettings();
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }

    // 容忍 float 加减产生的微小表示误差；1e-5 远小于本测试中的阶段差和进度差，
    // 不会把 0.8 -> 0 或 0.9 -> 0.5 等实际退阶误判为相等。
    private static void Equal(float expected, float actual)
    {
        if (Math.Abs(expected - actual) > 0.00001f)
            throw new Exception($"expected {expected}, got {actual}");
    }

    // 建立独立的最小 Pawn 测试状态。恶堕需求和锁链对象使用生产类，
    // severity < 0 是测试专用的“初始无锁链”标记，不会写入实际锁链严重度。
    // 显式设置严重度允许构造“已有高阶段、当前恶堕不足”等回归场景。
    private static Pawn Subject(float severity, float corruption)
    {
        var pawn = new Pawn();
        var need = new Need_Corruption(pawn);
        pawn.needs.allNeeds.Add(need);
        need.SetCorruption(corruption);
        if (severity >= 0f)
        {
            var chain = Hediff_ChainOfSexSlave.AddToPawn(pawn, new Pawn());
            chain.Severity = severity;
        }
        return pawn;
    }

    // 从新、旧仪式评分共同到达的阶段处理入口开始验证，复用已有主人以避免换主。
    // 此帮助函数不增加仪式恶堕收益：用例传入的是到达此入口时的数值。
    // “先增加恶堕，再处理阶段”的调用顺序在最后一个用例中单独覆盖。
    private static void Ritual(Pawn pawn)
    {
        var master = SSCBondUtility.GetChain(pawn)?.LinkedPawn ?? new Pawn();
        CorruptionProgressionUtility.ProcessCorruptionProgression(master, pawn, true);
    }

    // 装备宿主是替身，但挂载的是实际 CompApparel_CorruptionModifier。
    // GetMinLevel/GetActiveMinLevel 的历史里程碑与阶段区间校验仍运行生产实现。
    private static void Protect(Pawn pawn, float minimum)
    {
        var apparel = new Apparel { Wearer = pawn };
        apparel.comps.Add(new CompApparel_CorruptionModifier
        {
            parent = apparel,
            props = new CompProperties_CorruptionModifier { minCorruptionLevel = minimum }
        });
        pawn.apparel.WornApparel.Add(apparel);
    }

    private static int Main(string[] args)
    {
        // Read the actual shipped stage definitions, rather than duplicating the ladder in the test host.
        // 第一个命令行参数指向模组发布的 Hediff XML，InvariantCulture 保证小数点解析
        // 不受系统语言影响。载入阶段表、初始严重度和上限，不模拟完整的 Def 加载过程。
        var definition = XDocument.Load(args[0]).Root.Elements("HediffDef")
            .Single(e => (string)e.Element("defName") == "Hediff_ChainOfSexSlave");
        SSCDefOf.ChainOfSexSlave = new HediffDef
        {
            defName = "Hediff_ChainOfSexSlave",
            initialSeverity = definition.Element("initialSeverity") == null ? 0.5f :
                float.Parse((string)definition.Element("initialSeverity"), CultureInfo.InvariantCulture),
            maxSeverity = definition.Element("maxSeverity") == null ? float.MaxValue :
                float.Parse((string)definition.Element("maxSeverity"), CultureInfo.InvariantCulture),
            stages = definition.Element("stages").Elements("li").Select(e => new HediffStage
            { minSeverity = float.Parse((string)e.Element("minSeverity"), CultureInfo.InvariantCulture) }).ToList()
        };
        DefDatabase<HediffDef>.AllDefs.Add(SSCDefOf.ChainOfSexSlave);

        // 核心复现：阶段结算时锁链 0.8、恶堕 0.65。当前 XML 未限制最大严重度，旧实现先加到 1.1，
        // 随后的真实 NeedInterval 会把它清零。连续执行三轮也应保留已有 0.8，
        // 防止仅修复第一轮、但重复仪式仍累积到一个无法维持的高阶段。
        Check("repeat ritual below next threshold preserves stage through demand refresh", () =>
        {
            var pawn = Subject(0.8f, 0.65f);
            var need = pawn.needs.TryGetNeed<Need_Corruption>();
            for (int i = 0; i < 3; i++)
            {
                Ritual(pawn);
                need.NeedInterval();
                Equal(0.8f, SSCBondUtility.GetChain(pawn).Severity);
                Equal(0.5f, TrainingOutcomeUtility.GetCorruptionStageFloor(pawn));
            }
        });

        // 元组含义：(仪式前锁链严重度, 阶段结算时当前恶堕, 期望仪式后严重度)。
        // 同时覆盖刚好到达门槛、略低于门槛、预算被截断、满进度和已有进度高于恶堕。
        // 这里仅验证仪式本身不减进度；自然衰减的退阶由下一组用例验证。
        foreach (var sample in new[]
        {
            (0.2f, 0.299f, 0.2f), (0.2f, 0.3f, 0.3f), (0.4f, 0.45f, 0.45f),
            (0.4f, 0.5f, 0.5f), (0.8f, 0.899f, 0.899f), (0.8f, 0.9f, 0.9f),
            (0.8f, 1f, 1f), (0.95f, 0.6f, 0.95f), (0.4f, 0.2f, 0.4f)
        })
        {
            Check($"ritual chain {sample.Item1}, corruption {sample.Item2}", () =>
            {
                var pawn = Subject(sample.Item1, sample.Item2);
                Ritual(pawn);
                Equal(sample.Item3, SSCBondUtility.GetChain(pawn).Severity);
            });
        }

        // 元组含义：(已有严重度, 低于本阶段底线的恶堕, 期望前一级阶段底线)。
        // 特意选取同一阶段内不同严重度，证明结果由阶段决定，而不是减去固定 0.1。
        foreach (var sample in new[] { (1f, 0.89f, 0.5f), (0.95f, 0.89f, 0.5f),
            (0.9f, 0.89f, 0.5f), (0.8f, 0.49f, 0.3f), (0.5f, 0.49f, 0.3f),
            (0.4f, 0.29f, 0.1f), (0.3f, 0.29f, 0.1f), (0.2f, 0.09f, 0f) })
        {
            Check($"natural regression from {sample.Item1} to {sample.Item3}", () =>
            {
                var pawn = Subject(sample.Item1, sample.Item2);
                pawn.needs.TryGetNeed<Need_Corruption>().NeedInterval();
                Equal(sample.Item3, SSCBondUtility.GetChain(pawn).Severity);
            });
        }

        // 最容易暴露顺序错误的边界：恶堕恰好在阶段底线，且已经解锁同值的维持装备。
        // 自然扣减会让它略低于门槛，装备必须在退阶判断之前把它恢复。
        foreach (float threshold in new[] { 0.1f, 0.3f, 0.5f, 0.9f })
        {
            Check($"earned equipment floor protects {threshold} at decay boundary", () =>
            {
                var pawn = Subject(threshold, threshold);
                Protect(pawn, threshold);
                var need = pawn.needs.TryGetNeed<Need_Corruption>();
                need.NeedInterval();
                Equal(threshold, SSCBondUtility.GetChain(pawn).Severity);
                Equal(threshold, need.CurLevel);
            });
        }

        // 防止修复顺序时意外放宽装备条件：未达到过的 100% 底线不能直接赠送进度。
        Check("unearned equipment floor cannot unlock a milestone", () =>
        {
            var pawn = Subject(0.2f, 0.2f);
            Protect(pawn, 1f);
            pawn.needs.TryGetNeed<Need_Corruption>().NeedInterval();
            Equal(0.2f, SSCBondUtility.GetChain(pawn).Severity);
            if (pawn.needs.TryGetNeed<Need_Corruption>().CurLevel > 0.2f) throw new Exception("unearned floor applied");
        });

        // 三种无自然衰减路径分别验证，不能用“关闭开关”的通过代替“滑杆为 0”的验证。
        // 此组断言的是锁链保留；各模式对恶堕底线/上限的原有钳制差异没有被改写。
        foreach (string mode in new[] { "disabled", "zero", "legacy" })
        {
            Check(mode + " decay preserves existing high stage", () =>
            {
                var pawn = Subject(0.95f, 0.6f);
                SSCMod.settings.enableCorruptionDecay = mode != "disabled";
                SSCMod.settings.corruptionDecayPerDay = mode == "zero" ? 0f : 0.02f;
                SSCMod.settings.useOldScoring = mode == "legacy";
                pawn.needs.TryGetNeed<Need_Corruption>().NeedInterval();
                Equal(0.95f, SSCBondUtility.GetChain(pawn).Severity);
            });
        }

        // 兼容性检查：新建绑定仍按原先的 0.2 初始进度开始，不受仪式增量限制反向扣减。
        Check("first binding retains initial severity at low corruption", () =>
        {
            var pawn = Subject(-1f, 0.11f);
            Ritual(pawn);
            Equal(0.2f, SSCBondUtility.GetChain(pawn).Severity);
        });
        // 日常调教与仪式共用入口，但 isRitual=false 时只处理共享绑定，不授予仪式成长。
        Check("daily progression does not grant ritual chain growth", () =>
        {
            var pawn = Subject(0.4f, 0.6f);
            CorruptionProgressionUtility.ProcessCorruptionProgression(
                SSCBondUtility.GetChain(pawn).LinkedPawn, pawn, false);
            Equal(0.4f, SSCBondUtility.GetChain(pawn).Severity);
        });
        // 模拟结算的关键顺序：本次收益先让恶堕跨过 0.5，再判定锁链成长。
        // 若错误地使用加收益前的旧恶堕，后面的阶段断言将失败。
        Check("ritual sees corruption gain before progression", () =>
        {
            var pawn = Subject(0.4f, 0.49f);
            CorruptionUtility.AddCorruption(pawn, 0.1f);
            Ritual(pawn);
            pawn.needs.TryGetNeed<Need_Corruption>().NeedInterval();
            Equal(0.5f, TrainingOutcomeUtility.GetCorruptionStageFloor(pawn));
        });

        // 给人工阅读保留汇总，同时通过退出码让构建脚本能够可靠判断成败。
        Console.WriteLine($"{cases - failures}/{cases} passed");
        return failures == 0 ? 0 : 1;
    }
}
