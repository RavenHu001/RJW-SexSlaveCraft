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
        if (!SscTestProfile.ShouldRun(name)) return;
        cases++;
        SSCMod.settings = new TestSettings();
        SSCRestrictionGameComponent.AfterBind = null;
        SSCIdentityUtility.OnSetIdentity = null;
        WillReductionUtility.OnCalculateEnslaveChance = null;
        SSCRestrictionTrainingUtility.AuthorizeRitual = () => true;
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
        // 仪式阶段验证独立于需求刷新。
        foreach (var sample in new[]
        {
            (0.2f, 0.299f, 0.2f), (0.2f, 0.3f, 0.3f), (0.4f, 0.45f, 0.45f),
            (0.4f, 0.5f, 0.5f), (0.8f, 0.899f, 0.899f), (0.8f, 0.9f, 0.9f),
            (0.8f, 1f, 0.9f), (0.9f, 1f, 1f), (0.95f, 0.6f, 0.95f), (0.4f, 0.2f, 0.4f),
            (0.29f, 0.9f, 0.3f), (0.09f, 0.9f, 0.1f)
        })
        {
            Check($"ritual chain {sample.Item1}, corruption {sample.Item2}", () =>
            {
                var pawn = Subject(sample.Item1, sample.Item2);
                Ritual(pawn);
                Equal(sample.Item3, SSCBondUtility.GetChain(pawn).Severity);
            });
        }

        foreach (var (severity, floor) in new[] { (0.2f, 0.1f), (0.4f, 0.3f), (0.8f, 0.5f), (0.95f, 0.9f) })
        {
            Check($"natural decrement lands on {floor} and keeps exact severity {severity}", () =>
            {
                var pawn = Subject(severity, floor + 0.000001f);
                var need = pawn.needs.TryGetNeed<Need_Corruption>();
                need.NeedInterval();
                if (need.CurLevel != floor) throw new Exception("natural decay must hit the exact floor");
                need.NeedInterval();
                if (need.CurLevel != floor) throw new Exception("natural decay did not stop at the floor");
                Equal(severity, SSCBondUtility.GetChain(pawn).Severity);
            });
        }

        Check("maintenance preserves the original scoring-specific stage eligibility", () =>
        {
            var pawn = Subject(0.2f, 0.2f);
            var need = pawn.needs.TryGetNeed<Need_Corruption>();
            need.RestoreCorruption(0.2f, 0.9f);
            Protect(pawn, 0.25f);
            Protect(pawn, 0.5f);
            need.NeedInterval();
            Equal(0.25f, need.CurLevel);
            SSCMod.settings.useOldScoring = true;
            need.NeedInterval();
            Equal(0.5f, need.CurLevel);
            Equal(0.9f, need.HighestCorruptionLevel);
            Equal(0.2f, SSCBondUtility.GetChain(pawn).Severity);
        });

        // 防止修复顺序时意外放宽装备条件：未达到过的 100% 底线不能直接赠送进度。
        Check("unearned equipment floor cannot unlock a milestone", () =>
        {
            var pawn = Subject(0.2f, 0.2f);
            Protect(pawn, 1f);
            pawn.needs.TryGetNeed<Need_Corruption>().NeedInterval();
            Equal(0.2f, SSCBondUtility.GetChain(pawn).Severity);
            if (pawn.needs.TryGetNeed<Need_Corruption>().CurLevel > 0.2f) throw new Exception("unearned floor applied");
        });

        foreach (string mode in new[] { "active", "disabled", "zero", "legacy" })
        {
            Check(mode + " refresh applies only hard floors and the global maximum", () =>
            {
                var pawn = Subject(0.95f, 0.6f);
                SSCMod.settings.enableCorruptionDecay = mode != "disabled";
                SSCMod.settings.corruptionDecayPerDay = mode == "zero" ? 0f : 0.02f;
                SSCMod.settings.useOldScoring = mode == "legacy";
                var need = pawn.needs.TryGetNeed<Need_Corruption>();
                need.NeedInterval();
                Equal(0.95f, SSCBondUtility.GetChain(pawn).Severity);
                Equal(0.9f, need.CurLevel);
                foreach (float severity in new[] { -1f, 0.2f })
                {
                    var target = Subject(severity, 0.94f);
                    var current = target.needs.TryGetNeed<Need_Corruption>();
                    CorruptionUtility.AddCorruption(target, 0.12f);
                    Equal(1f, current.CurLevel);
                    current.NeedInterval();
                    if (mode == "active" && !(current.CurLevel < 1f && current.CurLevel > 0.94f))
                        throw new Exception("natural decay should apply without the old stage cap");
                    if (mode != "active") Equal(1f, current.CurLevel);
                    Equal(1f, current.HighestCorruptionLevel);
                }
            });
        }

        // 兼容性检查：新建绑定仍按原先的 0.2 初始进度开始，不受仪式增量限制反向扣减。
        Check("first binding retains initial severity at low corruption", () =>
        {
            var pawn = Subject(-1f, 0.11f);
            Ritual(pawn);
            Equal(0.2f, SSCBondUtility.GetChain(pawn).Severity);
        });
        Check("first binding consumes the stage allowance at high corruption", () =>
        {
            var pawn = Subject(-1f, 1f);
            Ritual(pawn);
            Equal(0.2f, SSCBondUtility.GetChain(pawn).Severity);
        });
        Check("first binding does not take a chain created by an identity callback", () =>
        {
            var pawn = Subject(-1f, 0.9f);
            var host = new Pawn();
            var other = new Pawn();
            SSCIdentityUtility.OnSetIdentity = target => Hediff_ChainOfSexSlave.AddToPawn(target, other);
            var result = CorruptionProgressionUtility.ProcessCorruptionProgression(host, pawn, true);
            if (SSCBondUtility.GetBoundMaster(pawn) != other || SSCBondUtility.GetBridle(host) != null || result != null)
                throw new Exception("old ritual claimed the callback's replacement bond");
        });
        Check("first binding stops before writing the bridle when a health callback replaces the chain", () =>
        {
            var pawn = Subject(-1f, 0.9f);
            var host = new Pawn();
            var other = new Pawn();
            pawn.health.AfterAdd = original =>
            {
                pawn.health.AfterAdd = null;
                pawn.health.RemoveHediff(original);
                Hediff_ChainOfSexSlave.AddToPawn(pawn, other);
            };
            var result = CorruptionProgressionUtility.ProcessCorruptionProgression(host, pawn, true);
            if (SSCBondUtility.GetBoundMaster(pawn) != other || SSCBondUtility.GetBridle(host) != null || result != null)
                throw new Exception("old ritual wrote the bridle for an absent chain");
        });
        Check("first binding drops the obsolete bridle target when its own health callback changes the owner", () =>
        {
            var pawn = Subject(-1f, 0.9f);
            var host = new Pawn();
            var other = new Pawn();
            host.health.AfterAdd = _ => SSCBondUtility.GetChain(pawn).target = other;
            var result = CorruptionProgressionUtility.ProcessCorruptionProgression(host, pawn, true);
            if (SSCBondUtility.GetBoundMaster(pawn) != other || SSCBondUtility.GetBridle(host) != null || result != null)
                throw new Exception("old ritual retained the bridle for a different owner's chain");
        });
        Check("severity just below the threshold uses one exact stage", () =>
        {
            var pawn = Subject(0.49995f, 0.9f);
            Equal(2, SSCIdentityUtility.GetSexSlaveStage(pawn));
            Equal(0.3f, TrainingOutcomeUtility.GetCorruptionStageFloor(pawn));
            Equal(1f, TrainingOutcomeUtility.GetChainCap(pawn));
            Equal(0.5f, TrainingOutcomeUtility.GetCorruptionMaintenanceStageUpper(pawn));
            Ritual(pawn);
            if (SSCBondUtility.GetChain(pawn).Severity != 0.5f) throw new Exception("did not land on the next stage boundary");
        });
        Check("ritual does not advance a replacement chain", () =>
        {
            var pawn = Subject(0.4f, 0.9f);
            var original = SSCBondUtility.GetChain(pawn);
            SSCRestrictionGameComponent.AfterBind = target =>
            {
                target.health.hediffSet.hediffs.Remove(original);
                Hediff_ChainOfSexSlave.AddToPawn(target, original.LinkedPawn).Severity = 0.4f;
            };
            Ritual(pawn);
            Equal(0.4f, SSCBondUtility.GetChain(pawn).Severity);
        });
        Check("delegated host advances existing chain and reports the actual stage", () =>
        {
            var pawn = Subject(0.4f, 0.9f);
            var owner = SSCBondUtility.GetBoundMaster(pawn);
            var host = new Pawn();
            var progress = new CorruptionProgressionUtility.RitualChainProgress(host, pawn, () => true);
            string result = CorruptionProgressionUtility.ProcessCorruptionProgression(host, pawn, true, 0f, progress);
            Equal(0.5f, SSCBondUtility.GetChain(pawn).Severity);
            if (SSCBondUtility.GetBoundMaster(pawn) != owner || SSCBondUtility.GetBridle(host) != null || result != "stage 3")
                throw new Exception("wrong owner, bridle, or stage result");
        });
        Check("ritual rechecks original need value for conversion and growth after callbacks", () =>
        {
            var pawn = Subject(0.4f, 0.9f);
            pawn.IsSlave = false;
            pawn.IsColonist = true;
            SSCMod.settings.enableRitualEnslavement = true;
            float conversionValue = -1f;
            WillReductionUtility.OnCalculateEnslaveChance = value => conversionValue = value;
            SSCRestrictionGameComponent.AfterBind = target => target.needs.TryGetNeed<Need_Corruption>().SetCorruption(0.45f);
            Ritual(pawn);
            Equal(0.45f, SSCBondUtility.GetChain(pawn).Severity);
            Equal(0.45f, conversionValue);
        });
        Check("ritual never reads a replacement corruption need for growth", () =>
        {
            var pawn = Subject(0.4f, 0.9f);
            SSCRestrictionGameComponent.AfterBind = target =>
            {
                target.needs.allNeeds.Remove(target.needs.TryGetNeed<Need_Corruption>());
                var replacement = new Need_Corruption(target);
                replacement.SetCorruption(1f);
                target.needs.allNeeds.Add(replacement);
            };
            Ritual(pawn);
            Equal(0.4f, SSCBondUtility.GetChain(pawn).Severity);
        });
        Check("ritual rechecks hosting authorization before conversion and growth", () =>
        {
            var pawn = Subject(0.4f, 0.9f);
            pawn.IsSlave = false;
            pawn.IsColonist = true;
            SSCMod.settings.enableRitualEnslavement = true;
            WillReductionUtility.OnCalculateEnslaveChance = _ => throw new Exception("stale ritual attempted conversion");
            Pawn host = SSCBondUtility.GetBoundMaster(pawn);
            var progress = new CorruptionProgressionUtility.RitualChainProgress(host, pawn, () => true);
            SSCRestrictionGameComponent.AfterBind = _ => SSCRestrictionTrainingUtility.AuthorizeRitual = () => false;
            CorruptionProgressionUtility.ProcessCorruptionProgression(host, pawn, true, 0f, progress);
            Equal(0.4f, SSCBondUtility.GetChain(pawn).Severity);
        });
        Check("settlement cannot continue on the same pawn after a personality swap in a callback", () =>
        {
            var pawn = Subject(0.4f, 0.9f);
            Pawn host = SSCBondUtility.GetBoundMaster(pawn);
            var progress = new CorruptionProgressionUtility.RitualChainProgress(host, pawn, () =>
            {
                pawn.PersonalityToken = new object();
                return true;
            });
            if (progress.CanContinueSettlement()) throw new Exception("settlement survived a personality swap");
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
