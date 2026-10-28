// AI 辅助生成：完整生产候选、当前 RJW／Harmony、真实 Scribe 与 Toil 屏障；不复制倍率或恢复算法。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static JobDef advanceJob;
    internal static int AdvanceBuildMode;
    internal static bool AdvanceNestedBuild;
    private static bool advanceEvents;
    private static int advanceOrgasmCalls, advanceRealEvents;

    /// <summary>在既有身体夹具之后安装实际计时适配，分别验证数学、实际桥接、生命周期与深存。</summary>
    private static void OrgasmAdvanceTests(string repo, string game, Harmony harmony)
    {
        // 旧成长专项隔离了原版 JobDriver.ExposeData；本批恢复该层，真实执行 SetupToils 与完整加载屏障。
        harmony.Unpatch(AccessTools.Method(typeof(JobDriver), nameof(JobDriver.ExposeData)), AccessTools.Method(typeof(Program), nameof(Skip)));
        // 前面绑定专项允许只设置 DefOf 夹具；真实 Scribe 必须让 DefOf 与 DefDatabase 指向同一定义。
        var chainDef = SSCDefOf.ChainOfSexSlave;
        var registeredChain = DefDatabase<HediffDef>.GetNamedSilentFail(chainDef.defName);
        if (registeredChain == null) DefDatabase<HediffDef>.Add(chainDef);
        else
        {
            registeredChain.hediffClass = typeof(Hediff_ChainOfSexSlave); registeredChain.stages = chainDef.stages;
            SSCDefOf.ChainOfSexSlave = registeredChain;
        }
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "Mods/RJW_动画_2.0核心框架/1.6/Assemblies/Rimworld-Animations.dll"));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "Mods/RJW_动画_UAP终极动画包/Assemblies/UAP_Animations.dll"));
        advanceJob = new JobDef { defName = "SSCAdvanceNativeFixture", driverClass = typeof(SSCAdvanceTestDriver) };
        DefDatabase<JobDef>.Add(advanceJob);
        harmony.Patch(AccessTools.Method(typeof(JobDriver_Sex), "Orgasm"), prefix: new HarmonyMethod(typeof(Program), nameof(AdvanceOrgasmBoundary)));
        harmony.Patch(AccessTools.Method(typeof(JobUtility), nameof(JobUtility.TryStartErrorRecoverJob)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        SSCOrgasmProgressPatches.Install();
        var owner = new Harmony(SSCOrgasmProgressPatches.Owner);
        SSCOrgasmProgressPatches.InstallAdvance(owner, AccessTools.Method(typeof(SSCAdvanceTestDriver), nameof(SSCAdvanceTestDriver.Step)), SSCOrgasmAdvanceSource.RjwSexTick, 1);
        AccessTools.Method(typeof(SSCOrgasmProgressPatches), "PatchObserver").Invoke(null, new object[] {
            owner, AccessTools.DeclaredMethod(typeof(SSCAdvanceTestDriver), "SetupOrgasmTicks"), "ResetPrefix", "ResetFinalizer" });
        SSCOrgasmProgressPatches.ValidateInstalledOrder();
        Require(SSCOrgasmProgressPatches.RecoveryReady, SSCOrgasmProgressPatches.Describe());
        foreach (var source in new[] { SSCOrgasmAdvanceSource.RjwSexTick, SSCOrgasmAdvanceSource.AnimationSexTick, SSCOrgasmAdvanceSource.UapInitiator, SSCOrgasmAdvanceSource.UapReceiver })
            Require(SSCOrgasmProgressPatches.IsReady(source), SSCOrgasmProgressPatches.Describe());
        AdvanceMathTests();
        AdvanceBridgeTests();
        AdvanceBuildILTests();
        AdvanceSaveTests(repo, harmony);
        AdvanceSelfTrainingTests(harmony);
        AdvanceBuildMode = 0; advanceEvents = false;
    }

    /// <summary>只隔离高潮的地图副作用；调用次数及真实计数变化由原观察 Finalizer 继续检测。</summary>
    private static bool AdvanceOrgasmBoundary(JobDriver_Sex __instance)
    {
        advanceOrgasmCalls++;
        if (advanceEvents && __instance.sex_ticks <= __instance.orgasmstick)
        {
            advanceRealEvents++; __instance.orgasms++;
            __instance.sex_ticks = __instance.orgasmStartTick = 1000;
        }
        return false;
    }

    /// <summary>外部世界只提供稳定 Pawn 容器；身体／任务／驱动与节点仍走原生保存。</summary>
    internal static Pawn AdvanceWorldPawn(int id, PawnIdentity role)
    {
        var pawn = SurgeryPawn(role); pawn.def = human; pawn.thingIDNumber = id;
        AccessTools.Field(typeof(Thing), "factionInt").SetValue(pawn, surgeryPlayer);
        pawn.jobs = new Pawn_JobTracker(pawn);
        return pawn;
    }

    /// <summary>建立独立实际驱动，可使用 XML 小数倍率；默认不触发场景初始化或高潮。</summary>
    private static SSCAdvanceTestDriver AdvanceDriver(int level = 1)
    {
        var pawn = SensitivityPawn(); pawn.def = human;
        SetSensitivity(pawn, level);
        pawn.jobs = new Pawn_JobTracker(pawn);
        var driver = new SSCAdvanceTestDriver { pawn = pawn, job = new Job { def = advanceJob, loadID = ++nextId },
            Sexprops = new SexProps(pawn, pawn), sex_ticks = 1000, orgasmStartTick = 1000, orgasmstick = 180, duration = 2000, ticks_left = 2000 };
        pawn.jobs.curDriver = driver; pawn.jobs.curJob = driver.job;
        SSCBondUtility.GetBoundMaster(pawn).def = human;
        return driver;
    }

    /// <summary>仅改真实 XML 扩展配置并清定义缓存，finally 恢复，数学预期不从生产实现反推。</summary>
    private static void WithAdvanceMultiplier(float value, Action action)
    {
        var ext = sensitivity.GetModExtension<SSCOrgasmSensitivityExtension>(); float previous = ext.multipliers[0];
        try { ext.multipliers[0] = value; ClearSensitivityConfig(); action(); }
        finally { ext.multipliers[0] = previous; ClearSensitivityConfig(); }
    }

    /// <summary>固定算术例子及饱和边界，包含 int 端点差值、零推进和极大倍率。</summary>
    private static void AdvanceMathTests()
    {
        foreach (var input in new[] { (3L, 16d, 0d, 800L, 45L, 0d), (1L, 1.25d, .5d, 800L, 0L, .75d),
            (3L, 1.5d, .75d, 800L, 2L, .25d), (0L, 16d, .75d, 800L, 0L, .75d),
            (1L, 16d, .5d, 3L, 3L, .5d), (1L, double.MaxValue, 0d, 3L, 3L, 0d),
            (4294967295L, 16d, .75d, 4294967295L, 4294967295L, .75d), (1L, 4d, .5d, 0L, 0L, .5d),
            (1L, double.MaxValue, .75d, 3L, 3L, .75d), (1L, 1.5d, .75d, 0L, 0L, .25d) })
            Check("advance native uniform arithmetic: " + input, () =>
            {
                Require(SSCOrgasmAdvanceRuntime.TryCalculate(input.Item1, input.Item2, input.Item3, input.Item4, out long extra, out double carry));
                Equal(input.Item5, extra); Require(Math.Abs(input.Item6 - carry) < 1e-10);
            });
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, -1d, .5d })
            Check("advance native invalid multiplier: " + value, () => Require(!SSCOrgasmAdvanceRuntime.TryCalculate(1, value, 0, 800, out _, out _)));
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, -1d, 1d })
            Check("advance native invalid carry: " + value, () => Require(!SSCOrgasmAdvanceRuntime.TryCalculate(1, 2, value, 800, out _, out _)));
    }

    /// <summary>执行安装后的真实写回桥，而非直接调用公式；检查样本共享、一次提交、生命周期和性能。</summary>
    private static void AdvanceBridgeTests()
    {
        foreach (var source in new[] { SSCOrgasmAdvanceSource.RjwSexTick, SSCOrgasmAdvanceSource.AnimationSexTick,
            SSCOrgasmAdvanceSource.UapInitiator, SSCOrgasmAdvanceSource.UapReceiver })
            Check("advance native same formula for installed source: " + source, () =>
            {
                var d = AdvanceDriver(4);
                if (source == SSCOrgasmAdvanceSource.RjwSexTick) SSCOrgasmProgressSampling.WriteRjw(d, 997);
                else if (source == SSCOrgasmAdvanceSource.AnimationSexTick) SSCOrgasmProgressSampling.WriteAnimation(d, 997);
                else if (source == SSCOrgasmAdvanceSource.UapInitiator) SSCOrgasmProgressSampling.WriteUapInitiator(d, 997);
                else SSCOrgasmProgressSampling.WriteUapReceiver(d, 997);
                Equal(952, d.sex_ticks); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var raw));
                Equal(3L, raw.RawAdvance); Equal(source, raw.Source);
            });
        foreach (int level in new[] { 1, 2, 3, 4 })
            Check("advance native configured level scales actual delta: " + level, () =>
            {
                var d = AdvanceDriver(level); int clock = d.ticks_left; d.Step(3);
                Equal(1000 - 3 * new[] { 2, 4, 8, 16 }[level - 1], d.sex_ticks); Equal(clock - 1, d.ticks_left);
                Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var sample)); Equal(3L, sample.RawAdvance);
                int after = d.sex_ticks; SSCOrgasmAdvanceRuntime.Apply(sample); Equal(after, d.sex_ticks);
                d.Step(3); Equal(after - 3 * new[] { 2, 4, 8, 16 }[level - 1], d.sex_ticks);
            });
        Check("advance native fractional zero and same tick operations keep exact carry", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(1); Near(.25f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d)); d.Step(0);
            Near(.25f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d)); d.Step(1); d.Step(1); d.Step(1);
            Equal(995, d.sex_ticks); Near(0, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
        }));
        Check("advance native huge XML rate reaches custom threshold once without event loop", () => WithAdvanceMultiplier(float.MaxValue, () =>
        {
            var d = AdvanceDriver(); d.orgasmstick = 713; advanceEvents = true;
            int before = advanceOrgasmCalls, events = advanceRealEvents, duration = d.duration;
            d.Step(1); Equal(before + 1, advanceOrgasmCalls); Equal(events + 1, advanceRealEvents);
            Equal(1, d.orgasms); Equal(1000, d.sex_ticks); Equal(duration, d.duration); Require(!d.neverendingsex);
            Near(0, (float)SSCOrgasmAdvanceRuntime.GetCarry(d)); advanceEvents = false;
        }));
        Check("advance native already crossed threshold stays below it", () =>
        {
            var d = AdvanceDriver(4); d.sex_ticks = d.orgasmstick - 2; d.Step(3); Equal(d.orgasmstick - 5, d.sex_ticks);
        });
        Check("advance native threshold saturation retains fractional carry until a real event", () => WithAdvanceMultiplier(1.25f, () =>
        {
            // 原Orgasm被既有边界跳过时，抵达阈值只丢整数超额；小数余数仍属于同一周期。
            var d = AdvanceDriver(); d.sex_ticks = d.orgasmStartTick = 187;
            d.Step(3); Equal(184, d.sex_ticks); Near(.75f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
            d.Step(3); Equal(180, d.sex_ticks); Near(.5f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var sample)); Equal(3L, sample.RawAdvance);
            SSCOrgasmAdvanceRuntime.Apply(sample); Near(.5f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
            d.Step(1); Equal(179, d.sex_ticks); Near(.75f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
            d.Step(0); Equal(179, d.sex_ticks); Near(.75f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
            // 只有随后真实计数变化／周期重设，才清掉该周期的余数，不能提前把阈值当事件。
            advanceEvents = true;
            try { d.Step(0); Equal(1, d.orgasms); Near(0, (float)SSCOrgasmAdvanceRuntime.GetCarry(d)); }
            finally { advanceEvents = false; }
        }));
        foreach (string change in new[] { "off-on", "level", "chain", "personality", "props", "unknown clock", "cycle" })
            Check("advance native carry invalidation: " + change, () => WithAdvanceMultiplier(1.25f, () =>
            {
                var d = AdvanceDriver(); d.Step(3); Near(.75f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
                switch (change)
                {
                    case "off-on": SetSensitivity(d.pawn, 0); SetSensitivity(d.pawn, 1); break;
                    case "level": SetSensitivity(d.pawn, 2); SetSensitivity(d.pawn, 1); break;
                    case "chain": var chain = SSCBondUtility.GetChain(d.pawn); chain.target = AdvanceWorldPawn(++nextId, PawnIdentity.Master); SSCOrgasmSensitivityUtility.NotifyStateChanged(d.pawn); break;
                    case "personality": Require(SSCPersonalityRuntimeUtility.TryBeginMutation(d.pawn, out var scope)); scope.Dispose(); break;
                    case "props": d.Sexprops = new SexProps(d.pawn, d.pawn); break;
                    case "unknown clock": d.sex_ticks--; break;
                    case "cycle": d.ResetForTest(); break;
                }
                int before = d.sex_ticks; d.Step(1); Equal(before - 1, d.sex_ticks); Near(.25f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
            }));
        Check("advance native unrelated mood or stage refresh keeps fractional carry", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3); SetChainStage(d.pawn, 3); SSCOrgasmSensitivityUtility.NotifyStateChanged(d.pawn);
            var ext = sensitivity.GetModExtension<SSCOrgasmSensitivityExtension>(); float offset = ext.vulnerabilityOffsets[0];
            try { ext.vulnerabilityOffsets[0] = offset + .1f; ClearSensitivityConfig(); d.Step(1); Equal(995, d.sex_ticks); }
            finally { ext.vulnerabilityOffsets[0] = offset; ClearSensitivityConfig(); }
        }));
        Check("advance native active hot path has no per-step allocations", () =>
        {
            var d = AdvanceDriver(); d.sex_ticks = d.orgasmStartTick = 1000000;
            for (int i = 0; i < 128; i++) d.Step(1);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) d.Step(1);
            Equal(0L, GC.GetAllocatedBytesForCurrentThread() - allocated);
        });
        Check("advance native cleanup removes samples and carry even when original is skipped", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3); Near(.75f, (float)SSCOrgasmAdvanceRuntime.GetCarry(d));
            var cleanup = AccessTools.Method(typeof(JobDriver), "Cleanup"); var boundary = new Harmony("ssc.tests.advance.cleanup");
            boundary.Patch(cleanup, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            try { d.Cleanup(JobCondition.InterruptForced); }
            finally { boundary.UnpatchAll(boundary.Id); }
            Near(0, (float)SSCOrgasmAdvanceRuntime.GetCarry(d)); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
        }));
    }

    /// <summary>成功标记只在正常离开 try 处，重放／失配不留下半段转换或修改原指令集合。</summary>
    private static void AdvanceBuildILTests()
    {
        Check("advance native build success anchor is atomic and replay safe", () =>
        {
            var method = AccessTools.Method(typeof(JobDriver), "SetupToils"); var input = PatchProcessor.GetOriginalInstructions(method);
            int labels = input.Sum(c => c.labels.Count), blocks = input.Sum(c => c.blocks.Count);
            Require(SSCOrgasmProgressPatches.TryRewriteBuild(input, out var output));
            Equal(input.Count + 2, output.Count); Equal(labels, input.Sum(c => c.labels.Count)); Equal(blocks, input.Sum(c => c.blocks.Count));
            Require(!SSCOrgasmProgressPatches.TryRewriteBuild(output, out var replay)); Require(output.SequenceEqual(replay));
            var invalid = input.Select(c => new CodeInstruction(c)).ToList();
            invalid.RemoveAll(c => c.operand is MethodInfo m && m.Name == "TryStartErrorRecoverJob");
            Require(!SSCOrgasmProgressPatches.TryRewriteBuild(invalid, out var unchanged)); Require(invalid.SequenceEqual(unchanged));
        });
    }

    /// <summary>真正深存驱动及健康，外部 Pawn 只提供世界容器；不保存任何测试影子余数。</summary>
    private static string SaveAdvance(SSCAdvanceScene fixture)
    {
        string path = Path.Combine(saveDirectory, "advance-" + ++nextId + ".xml");
        Scribe.saver.InitSaving(path, "SSCAdvanceFixture"); Scribe_Deep.Look(ref fixture, "scene"); Scribe.saver.FinalizeSaving(); return path;
    }

    /// <summary>加载到新驱动实例，父子后续回调由原生调度，不在夹具中递归调用子节点 ExposeData。</summary>
    private static SSCAdvanceScene LoadAdvance(string path)
    {
        SSCAdvanceScene fixture = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref fixture, "scene");
        Scribe.loader.FinalizeLoading(); return fixture;
    }

    /// <summary>使用非零小数余数覆盖真实深存、重建、重存、坏节点及正常路径成功屏障。</summary>
    private static void AdvanceSaveTests(string repo, Harmony harmony)
    {
        foreach (int mode in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
            Check("advance native Scribe pending carry and actual SetupToils mode " + mode, () => WithAdvanceMultiplier(1.25f, () =>
            {
                var d = AdvanceDriver(); d.Step(3); var fixture = new SSCAdvanceScene(d);
                string path = SaveAdvance(fixture); Require(XDocument.Load(path).Descendants("sscOrgasmAdvanceState").Any());
                AdvanceBuildMode = mode;
                var loaded = LoadAdvance(path); int before = loaded.Driver.sex_ticks;
                Equal(SSCOrgasmAdvanceRuntime.RestoreStatus.Pending, SSCOrgasmAdvanceRuntime.GetRestoreStatus(loaded.Driver));
                loaded.Driver.Step(1);
                bool keep = mode == 0 || mode == 2;
                Equal(before - (keep ? 2 : 1), loaded.Driver.sex_ticks);
                Near(keep ? 0 : .25f, (float)SSCOrgasmAdvanceRuntime.GetCarry(loaded.Driver));
                Equal(keep ? SSCOrgasmAdvanceRuntime.RestoreStatus.Restored : SSCOrgasmAdvanceRuntime.RestoreStatus.Rejected,
                    SSCOrgasmAdvanceRuntime.GetRestoreStatus(loaded.Driver));
                AdvanceBuildMode = 0;
            }));
        Check("advance native pending carry survives save before first loaded advance", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3); AdvanceBuildMode = 0;
            var first = LoadAdvance(SaveAdvance(new SSCAdvanceScene(d)));
            var second = LoadAdvance(SaveAdvance(first)); int before = second.Driver.sex_ticks; second.Driver.Step(1); Equal(before - 2, second.Driver.sex_ticks);
        }));
        Check("advance native saturated fractional carry survives saving before an actual event", () => WithAdvanceMultiplier(1.25f, () =>
        {
            // 抵达阈值但高潮原方法被跳过时，保存仍应只保留同周期小数，不保存待补发事件。
            var d = AdvanceDriver(); d.sex_ticks = d.orgasmStartTick = 187; d.Step(3); d.Step(3);
            var loaded = LoadAdvance(SaveAdvance(new SSCAdvanceScene(d)));
            loaded.Driver.Step(0); Equal(180, loaded.Driver.sex_ticks); Equal(0, loaded.Driver.orgasms);
            Near(.5f, (float)SSCOrgasmAdvanceRuntime.GetCarry(loaded.Driver));
            Equal(SSCOrgasmAdvanceRuntime.RestoreStatus.Restored, SSCOrgasmAdvanceRuntime.GetRestoreStatus(loaded.Driver));
        }));
        foreach (string corruption in new[] { "missing", "version", "missing version", "NaN", "range", "negative", "job", "type", "counter", "level", "rate", "body ref", "chain ref", "actor ref", "master ref" })
            Check("advance native invalid saved carry is discarded: " + corruption, () => WithAdvanceMultiplier(1.25f, () =>
            {
                var d = AdvanceDriver(); d.Step(3); string path = SaveAdvance(new SSCAdvanceScene(d));
                Edit(path, doc =>
                {
                    var node = doc.Descendants("sscOrgasmAdvanceState").Single();
                    switch (corruption)
                    {
                        case "missing": node.Remove(); break;
                        case "version": node.Element("schemaVersion").Value = "99"; break;
                        case "missing version": node.Element("schemaVersion").Remove(); break;
                        case "NaN": node.Element("carry").Value = "NaN"; break;
                        case "range": node.Element("carry").Value = "1"; break;
                        case "negative": node.Element("carry").Value = "-0.25"; break;
                        case "job": node.Element("jobLoadId").Value = "-1"; break;
                        case "type": node.Element("driverType").Value = "OtherDriver"; break;
                        case "counter": node.Element("counter").Value = "700"; break;
                        case "level": node.Element("level").Value = "4"; break;
                        case "rate": node.Element("multiplierBits").Value = "0"; break;
                        case "body ref": node.Element("body").Value = "null"; break;
                        case "chain ref": node.Element("chain").Value = "null"; break;
                        case "actor ref": node.Element("actor").Value = "null"; break;
                        case "master ref": node.Element("master").Value = "null"; break;
                    }
                });
                AdvanceBuildMode = 0; var loaded = LoadAdvance(path); int before = loaded.Driver.sex_ticks; loaded.Driver.Step(1); Equal(before - 1, loaded.Driver.sex_ticks);
            }));
        Check("advance native unknown clock rewrite cannot be blessed by saving", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3); d.sex_ticks--;
            var loaded = LoadAdvance(SaveAdvance(new SSCAdvanceScene(d))); int before = loaded.Driver.sex_ticks; loaded.Driver.Step(1); Equal(before - 1, loaded.Driver.sex_ticks);
        }));
        Check("advance native driver and health initialization order is independent", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3); var scene = new SSCAdvanceScene(d) { DriverFirst = true };
            var loaded = LoadAdvance(SaveAdvance(scene)); int before = loaded.Driver.sex_ticks; loaded.Driver.Step(1); Equal(before - 2, loaded.Driver.sex_ticks);
        }));
        Check("advance native dead master instruction preserves restored carry", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3);
            AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(SSCBondUtility.GetBoundMaster(d.pawn).health, PawnHealthState.Dead);
            var loaded = LoadAdvance(SaveAdvance(new SSCAdvanceScene(d))); Require(loaded.Master.Dead);
            int before = loaded.Driver.sex_ticks; loaded.Driver.Step(1); Equal(before - 2, loaded.Driver.sex_ticks);
        }));
        foreach (string transition in new[] { "level", "master", "personality" })
            Check("advance native pending carry invalidates before returning to old state: " + transition, () => WithAdvanceMultiplier(1.25f, () =>
            {
                var d = AdvanceDriver(); d.Step(3); var loaded = LoadAdvance(SaveAdvance(new SSCAdvanceScene(d)));
                if (transition == "level") { SetSensitivity(loaded.Actor, 2); SetSensitivity(loaded.Actor, 1); }
                else if (transition == "master")
                {
                    var chain = SSCBondUtility.GetChain(loaded.Actor); var master = chain.target;
                    chain.target = AdvanceWorldPawn(++nextId, PawnIdentity.Master); SSCOrgasmSensitivityUtility.NotifyStateChanged(loaded.Actor);
                    chain.target = master; SSCOrgasmSensitivityUtility.NotifyStateChanged(loaded.Actor);
                }
                else { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(loaded.Actor, out var scope)); scope.Dispose(); }
                int before = loaded.Driver.sex_ticks; loaded.Driver.Step(1); Equal(before - 1, loaded.Driver.sex_ticks);
            }));
        Check("advance native changed XML rate discards carry but applies current multiplier", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3); string path = SaveAdvance(new SSCAdvanceScene(d));
            var ext = sensitivity.GetModExtension<SSCOrgasmSensitivityExtension>(); ext.multipliers[0] = 1.5f; ClearSensitivityConfig();
            var loaded = LoadAdvance(path); int before = loaded.Driver.sex_ticks; loaded.Driver.Step(1); Equal(before - 1, loaded.Driver.sex_ticks);
            Near(.5f, (float)SSCOrgasmAdvanceRuntime.GetCarry(loaded.Driver));
        }));
        Check("advance native skipped SetupToils cannot restore carry", () => WithAdvanceMultiplier(1.25f, () =>
        {
            var d = AdvanceDriver(); d.Step(3); string path = SaveAdvance(new SSCAdvanceScene(d));
            var setup = AccessTools.Method(typeof(JobDriver), "SetupToils");
            harmony.Patch(setup, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            SSCAdvanceScene loaded;
            try { loaded = LoadAdvance(path); }
            finally { harmony.Unpatch(setup, AccessTools.Method(typeof(Program), nameof(Skip))); }
            int before = loaded.Driver.sex_ticks; loaded.Driver.Step(1); Equal(before - 1, loaded.Driver.sex_ticks);
        }));
    }

    /// <summary>独立 CLR 只隔离 setup_ticks 的设置／随机服务，仍实际调用已观察的 RJW 自慰周期初始化。</summary>
    private static bool SelfSetupBoundary(JobDriver_Sex __instance)
    {
        if (!(__instance is JobDriver_SelfTraining)) return true;
        __instance.duration = 777;
        AccessTools.DeclaredMethod(typeof(JobDriver_Masturbate), "SetupOrgasmTicks").Invoke(__instance, null);
        return false;
    }

    /// <summary>实际 SSC 自我调教迭代器恢复原计时及完成快照，附加节点不重复认领其既有收益。</summary>
    private static void AdvanceSelfTrainingTests(Harmony harmony)
    {
        var setupTicks = AccessTools.Method(typeof(JobDriver_Sex), "setup_ticks");
        harmony.Patch(setupTicks, prefix: new HarmonyMethod(typeof(Program), nameof(SelfSetupBoundary)));
        try
        {
            foreach (bool resave in new[] { false, true })
                Check("advance native actual SSC self-training resumes fractional carry, resave=" + resave, () => WithAdvanceMultiplier(1.25f, () =>
                {
                    var seed = AdvanceDriver();
                    seed.job.SetTarget(TargetIndex.A, seed.pawn);
                    var self = new JobDriver_SelfTraining { pawn = seed.pawn, job = seed.job, Sexprops = seed.Sexprops,
                        sex_ticks = seed.sex_ticks, orgasmStartTick = seed.orgasmStartTick, orgasmstick = seed.orgasmstick,
                        duration = seed.duration, ticks_left = seed.ticks_left };
                    seed.pawn.jobs.curDriver = self;
                    AccessTools.Field(typeof(JobDriver_SelfTraining), "snapshot").SetValue(self, new SSCSelfTrainingSnapshot { Captured = true });
                    self.Step(3); Near(.75f, (float)SSCOrgasmAdvanceRuntime.GetCarry(self));
                    var loaded = LoadAdvance(SaveAdvance(new SSCAdvanceScene(self)));
                    if (resave) loaded = LoadAdvance(SaveAdvance(loaded));
                    Require(loaded.Driver is JobDriver_SelfTraining); Equal(2000, loaded.Driver.duration); Equal(997, loaded.Driver.sex_ticks);
                    var snapshot = (SSCSelfTrainingSnapshot)AccessTools.Field(typeof(JobDriver_SelfTraining), "snapshot").GetValue(loaded.Driver);
                    Require(snapshot.Captured && !snapshot.CompletionClaimed);
                    loaded.Driver.Step(1); Equal(995, loaded.Driver.sex_ticks); Require(!snapshot.CompletionClaimed);
                }));
        }
        finally { harmony.Unpatch(setupTicks, AccessTools.Method(typeof(Program), nameof(SelfSetupBoundary))); }
    }

}

/// <summary>实际 JobDriver 子类只提供可控计时和 Toil 输入，所有余数与恢复逻辑来自生产程序集。</summary>
public sealed class SSCAdvanceTestDriver : JobDriver_Sex
{
    /// <summary>变量推进量经过真实已安装桥，保留一次原 Orgasm 调用。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] public void Step(int delta) { ticks_left--; sex_ticks -= delta; Orgasm(); }
    /// <summary>明确的真实重设入口，模式1保持数值相同以验证不能只比较指纹。</summary>
    protected override void SetupOrgasmTicks() { if (Program.AdvanceBuildMode != 1) sex_ticks = orgasmStartTick = 1000; }
    /// <summary>调用已观察的 override，不直接通知或模拟生产周期清理。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] public void ResetForTest() => SetupOrgasmTicks();
    /// <summary>0不重设，1同数字新周期，2恢复原周期，3内部捕获，4空枚举，5Dispose失败，6加载误推进，7内层成功后外层失败。</summary>
    protected override IEnumerable<Toil> MakeNewToils()
    {
        int counter = sex_ticks, start = orgasmStartTick;
        if (Program.AdvanceBuildMode == 1 || Program.AdvanceBuildMode == 2) SetupOrgasmTicks();
        if (Program.AdvanceBuildMode == 2) { sex_ticks = counter; orgasmStartTick = start; }
        if (Program.AdvanceBuildMode == 3) throw new InvalidOperationException("fixture SetupToils caught failure");
        if (Program.AdvanceBuildMode == 4) yield break;
        if (Program.AdvanceBuildMode == 6) SSCOrgasmProgressSampling.WriteRjw(this, sex_ticks);
        if (Program.AdvanceBuildMode == 7 && !Program.AdvanceNestedBuild)
        {
            Program.AdvanceNestedBuild = true;
            try { AccessTools.Method(typeof(JobDriver), "SetupToils").Invoke(this, null); }
            finally { Program.AdvanceNestedBuild = false; }
            throw new InvalidOperationException("outer failure after inner success");
        }
        try { yield return new Toil { defaultCompleteMode = ToilCompleteMode.Instant }; }
        finally { if (Program.AdvanceBuildMode == 5) throw new InvalidOperationException("fixture Dispose failure"); }
        if (Program.AdvanceBuildMode == 2) SSCOrgasmAdvanceRuntime.MarkRestored(this);
    }
}

/// <summary>实际 SSC 自我调教驱动的输入边界，不替代它的迭代器、原周期恢复或基类 Scribe。</summary>
public static class SSCAdvanceNativeStimulus
{
    /// <summary>普通夹具走真实已改写方法，自我调教直接提供原减量写回，再沿原高潮入口观察。</summary>
    public static void Step(this JobDriver_Sex driver, int delta)
    {
        if (driver is SSCAdvanceTestDriver fixture) { fixture.Step(delta); return; }
        driver.ticks_left--; SSCOrgasmProgressSampling.WriteRjw(driver, driver.sex_ticks - delta); driver.Orgasm();
    }
}

/// <summary>原生健康／Job／Driver 深存；只由外部世界提供基本 Pawn 容器和玩家身份组件。</summary>
public sealed class SSCAdvanceScene : IExposable
{
    internal Pawn Actor, Master;
    internal JobDriver_Sex Driver;
    private Job job;
    private HealthFixture health;
    private int actorId, masterId;
    internal bool DriverFirst;
    private bool masterDead;
    /// <summary>标准深存构造器；LoadingVars 才恢复外部世界容器。</summary>
    public SSCAdvanceScene() { }
    /// <summary>捕获当前外部身体与任务，执行余数不进入夹具字段。</summary>
    public SSCAdvanceScene(JobDriver_Sex driver)
    {
        Driver = driver; Actor = driver.pawn; Master = SSCBondUtility.GetBoundMaster(Actor); job = driver.job;
        health = new HealthFixture(Actor); actorId = Actor.thingIDNumber; masterId = Master.thingIDNumber;
    }
    /// <summary>LoadingVars 在深读驱动后才绑定 Pawn／Job，重现原版 JobTracker 的引用恢复时序。</summary>
    public void ExposeData()
    {
        Scribe_Values.Look(ref actorId, "actorId"); Scribe_Values.Look(ref masterId, "masterId");
        if (Scribe.mode == LoadSaveMode.Saving) masterDead = Master.Dead;
        Scribe_Values.Look(ref masterDead, "masterDead"); Scribe_Values.Look(ref DriverFirst, "driverFirst");
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Actor = Program.AdvanceWorldPawn(actorId, PawnIdentity.Slave); Master = Program.AdvanceWorldPawn(masterId, PawnIdentity.Master);
            if (masterDead) AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(Master.health, PawnHealthState.Dead);
        }
        if (!DriverFirst) Scribe_Deep.Look(ref health, "health", Actor);
        Scribe_Deep.Look(ref job, "job");
        Scribe_Deep.Look(ref Driver, "driver");
        if (DriverFirst) Scribe_Deep.Look(ref health, "health", Actor);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Driver.pawn = Actor; Driver.job = job; Actor.jobs.curDriver = Driver; Actor.jobs.curJob = job;
            var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
            directory.RegisterLoaded(Actor); directory.RegisterLoaded(Master);
        }
    }
}
