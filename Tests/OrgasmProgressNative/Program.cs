using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using HarmonyLib;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static class Entry
{
    /// <summary>为独立宿主安装实际游戏／模组程序集解析器，再进入包含游戏类型的验证入口。</summary>
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("repositoryRoot gameRoot");
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string referenceInfo = Environment.GetEnvironmentVariable("SSC_TEST_BUILD_INFO_PATH") ?? Path.Combine(repo, "1.7/BUILD-INFO.json");
        using (var doc = JsonDocument.Parse(File.ReadAllText(referenceInfo)))
            foreach (var reference in doc.RootElement.GetProperty("references").EnumerateArray())
            {
                string path = reference.GetProperty("path").GetString();
                dependencies[AssemblyName.GetAssemblyName(path).Name] = path;
            }
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = dependencies.TryGetValue(name.Name, out string configured) ? configured : Path.Combine(game, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { repo, game });
    }
}

internal static class Program
{
    private static int passed;
    private static Action<JobDriver_Sex> atOrgasm;
    private static bool animationBranch;
    private static bool resetThrows;
    private static int bodyCalls;

    /// <summary>运行本套受影响契约与真实 DLL 路径验证；隔离地图服务，不启动正式 ModStartup 或游戏。</summary>
    internal static int Run(string repo, string game)
    {
        SscTestProfile.Start();
        if (!SscTestProfile.IncludesSuite) { Console.WriteLine("RESULT: 0/0 passed (suite excluded)"); return 0; }
        string uapPath = Path.Combine(game, "Mods/RJW_动画_UAP终极动画包/Assemblies/UAP_Animations.dll");
        if (!File.Exists(uapPath)) throw new FileNotFoundException("Actual UAP DLL is required for five-site IL verification", uapPath);
        Assembly uap = AssemblyLoadContext.Default.LoadFromAssemblyPath(uapPath);
        _ = typeof(Rimworld_Animations.HarmonyPatch_SexTick);
        Console.WriteLine("Game: " + File.ReadAllText(Path.Combine(game, "Version.txt")).Trim());
        Console.WriteLine("RJW MVID: " + typeof(JobDriver_Sex).Module.ModuleVersionId.ToString("N"));
        Console.WriteLine("Harmony: " + typeof(Harmony).Assembly.GetName().Version);
        var harmony = new Harmony("ssc.tests.orgasm-progress");
        // 必须先隔离地图服务，再生成生产包装，避免 JIT 已把未隔离的小型时钟函数内联。
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Gen)).Where(x => x.Name == "IsHashIntervalTick" && x.ReturnType == typeof(bool) && !x.ContainsGenericParameters))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(NoInterval)));
        // 只隔离原生音效／体液等地图服务；采样与观察补丁本身使用生产实现。
        harmony.Patch(AccessTools.Method(typeof(JobDriver_Sex), "Orgasm"), prefix: new HarmonyMethod(typeof(Program), nameof(ObserveOrgasm)));
        SSCOrgasmProgressPatches.Install();
        Console.WriteLine(SSCOrgasmProgressPatches.Describe());

        Check("实际安装的四条来源与五处写入全部匹配", () =>
        {
            foreach (var source in new[] { SSCOrgasmAdvanceSource.RjwSexTick, SSCOrgasmAdvanceSource.AnimationSexTick,
                SSCOrgasmAdvanceSource.UapInitiator, SSCOrgasmAdvanceSource.UapReceiver })
                Require(SSCOrgasmProgressPatches.IsReady(source), source.ToString());
        });

        // 先读实际 DLL 原 IL 核对五处写回；这一步不重跑第三方 Transpiler。
        var field = AccessTools.Field(typeof(JobDriver_Sex), "sex_ticks");
        var orgasm = AccessTools.Method(typeof(JobDriver_Sex), "Orgasm");
        var bridge = AccessTools.Method(typeof(SSCOrgasmProgressSampling), "WriteRjw");
        foreach (var item in new[] {
            (AccessTools.Method(typeof(JobDriver_Sex), "SexTick"), 2),
            (AccessTools.Method(typeof(Rimworld_Animations.HarmonyPatch_SexTick), "Prefix"), 1),
            (AccessTools.Method(uap.GetType("UAP_Animations.JobDriver_UAPGroupSexInitiator", true), "TickInitiatorOnly"), 1),
            (AccessTools.Method(uap.GetType("UAP_Animations.JobDriver_UAPGroupSexInitiator", true), "TickSharedReceiverOnce"), 1) })
            Check("真实 IL 保留原控制流／高潮调用：" + item.Item1.DeclaringType.Name + "." + item.Item1.Name, () =>
            {
                var input = PatchProcessor.GetOriginalInstructions(item.Item1);
                Require(SSCOrgasmProgressPatches.TryRewrite(input, field, orgasm, bridge, item.Item2, out var output, out var reason), reason);
                Equal(input.Count, output.Count);
                Equal(item.Item2, output.Count(x => x.opcode == OpCodes.Call && Equals(x.operand, bridge)));
                Equal(item.Item2, output.Count(x => Equals(x.operand, orgasm)));
                Equal(item.Item2, input.Count(x => x.opcode == OpCodes.Stfld && Equals(x.operand, field)));
            });

        // 随后执行真实入口，覆盖仅看转换结果无法证明的 detour／JIT 调用路径。
        ActualPathChecks(harmony, uap);

        // 最小派生驱动补充变量速度与重入夹具；仍使用同一生产桥和游戏 Harmony。
        var samplingHarmony = new Harmony(SSCOrgasmProgressPatches.Owner);
        SSCOrgasmProgressPatches.InstallAdvance(samplingHarmony, AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Step)), SSCOrgasmAdvanceSource.RjwSexTick, 1);
        SSCOrgasmProgressPatches.InstallAdvance(samplingHarmony, AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Pair)), SSCOrgasmAdvanceSource.RjwSexTick, 2);
        // 对 Prefix 方法本身做 detour，再生成调用该 Prefix 的包装。
        SSCOrgasmProgressPatches.InstallAdvance(samplingHarmony, AccessTools.Method(typeof(Program), nameof(AnimationPrefix)), SSCOrgasmAdvanceSource.AnimationSexTick, 1);
        harmony.Patch(AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Entry)), prefix: new HarmonyMethod(typeof(Program), nameof(AnimationPrefix)));
        SSCOrgasmProgressPatches.InstallAdvance(samplingHarmony, AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Entry)), SSCOrgasmAdvanceSource.RjwSexTick, 1);
        SSCOrgasmProgressPatches.ValidateInstalledOrder();
        AccessTools.Method(typeof(SSCOrgasmProgressPatches), "PatchObserver").Invoke(null, new object[] {
            new Harmony(SSCOrgasmProgressPatches.Owner), AccessTools.DeclaredMethod(typeof(TickFixture), "SetupOrgasmTicks"), "ResetPrefix", "ResetFinalizer" });
        SSCOrgasmProgressPatches.ValidateInstalledOrder();

        Check("变量减量在真实 Harmony 写回桥中读取，阈值 Prefix 前已有样本", () =>
        {
            var d = Driver(); long raw = -1;
            atOrgasm = x => { Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(x, out var r)); raw = r.RawAdvance; };
            int scene = d.ticks_left; d.Step(3);
            Equal(3L, raw); Equal(997, d.sex_ticks); Equal(scene - 1, d.ticks_left); Equal(0, d.orgasms);
        });
        Check("同一真实结果可重复读取，查询不创建下一次推进", () =>
        {
            atOrgasm = null; var d = Driver(); d.Step(2);
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var a));
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var b));
            Equal(a.Operation, b.Operation); Equal(a.RawAdvance, b.RawAdvance); Equal(998, d.sex_ticks);
            Require(SSCOrgasmProgressUtility.IsStillCurrent(a));
        });
        Check("同一 tick 连续操作各自采样，不按 TickManager 去重", () =>
        {
            var d = Driver(); d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var a));
            d.Step(4); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var b));
            Equal(a.Operation + 1, b.Operation); Equal(4L, b.RawAdvance); Equal(995, d.sex_ticks);
        });
        Check("同驱动嵌套推进持有各自操作结果，不覆盖外层差值", () =>
        {
            var d = Driver(); bool nested = false; SSCOrgasmAdvance outer = default, inner = default;
            atOrgasm = x =>
            {
                Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(x, out var result));
                if (!nested) { outer = result; nested = true; d.Step(2); }
                else inner = result;
            };
            d.Step(3); Equal(3L, outer.RawAdvance); Equal(2L, inner.RawAdvance); Equal(outer.Operation + 1, inner.Operation); Equal(995, d.sex_ticks);
        });
        Check("有效零增量仍为有效结果", () =>
        { var d = Driver(); d.Step(0); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var r)); Equal(0L, r.RawAdvance); });
        Check("双方按各自驱动采样，不将发起方样本套给接收者", () =>
        {
            var a = Driver(); var b = Driver(); a.Pair(b, 3, 7);
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(a, out var x));
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(b, out var y));
            Equal(3L, x.RawAdvance); Equal(7L, y.RawAdvance); Require(!ReferenceEquals(x.Actor, y.Actor));
        });
        Check("动画 Prefix 方法的 detour 真正到达写回桥并保持 skip-original", () =>
        {
            bodyCalls = 0; animationBranch = true; var d = Driver(); d.Entry(); animationBranch = false;
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var r));
            Equal(SSCOrgasmAdvanceSource.AnimationSexTick, r.Source); Equal(1L, r.RawAdvance); Equal(0, bodyCalls);
        });
        Check("动画放行时只采基础原方法，不双重采样", () =>
        {
            bodyCalls = 0; var d = Driver(); d.Entry(); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var r));
            Equal(SSCOrgasmAdvanceSource.RjwSexTick, r.Source); Equal(1UL, r.Operation); Equal(1, bodyCalls);
        });
        // 公共接口独立验证，不借自动采样的缓存结果替代前后快照计算。
        PureInterfaceChecks();

        // 生命周期测试分别覆盖嵌套范围、同值重设、记录替换和原异常传播。
        Check("嵌套周期初始化只失效一次且范围在异常后可关闭", () =>
        {
            var d = Driver(); d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var a));
            var first = SSCOrgasmProgressSampling.BeginReset(d); var nested = SSCOrgasmProgressSampling.BeginReset(d);
            SSCOrgasmProgressSampling.WriteRjw(d, 998); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            SSCOrgasmProgressSampling.EndReset(nested); SSCOrgasmProgressSampling.EndReset(first);
            d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var b)); Equal(a.Cycle + 1, b.Cycle);
        });
        Check("同值重设也使旧样本失效", () =>
        {
            var d = Driver(); d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var old));
            SSCOrgasmProgressSampling.NotifyReset(d); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            Require(!SSCOrgasmProgressUtility.IsStillCurrent(old));
            d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var next)); Require(next.Cycle > old.Cycle);
        });
        Check("实际重设观察补丁在原方法异常后关闭范围且保留异常", () =>
        {
            var d = Driver(); d.Step(1); resetThrows = true;
            try { d.ResetForTest(); throw new Exception("expected reset failure"); }
            catch (InvalidOperationException e) { Equal("reset failure", e.Message); }
            finally { resetThrows = false; }
            Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            d.Step(2); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var r)); Equal(2L, r.RawAdvance);
        });
        Check("旧重设范围不能关闭同驱动重建记录中的新范围", () =>
        {
            var d = Driver(); d.Step(1);
            var oldScope = SSCOrgasmProgressSampling.BeginReset(d);
            SSCOrgasmProgressSampling.Forget(d); d.Step(1);
            var newScope = SSCOrgasmProgressSampling.BeginReset(d);
            SSCOrgasmProgressSampling.EndReset(oldScope);
            d.Step(1); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            SSCOrgasmProgressSampling.EndReset(newScope);
            d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
        });
        Check("旧高潮完成回调不能清理同驱动后来重建的记录", () =>
        {
            var d = Driver(); d.Step(1); var oldScope = SSCOrgasmProgressSampling.BeginOrgasm(d);
            SSCOrgasmProgressSampling.Forget(d); d.Step(2);
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var before));
            SSCOrgasmProgressSampling.EndOrgasm(d, oldScope, new InvalidOperationException("old call failed"));
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var after));
            Equal(before.Operation, after.Operation); Equal(2L, after.RawAdvance);
        });
        Check("真实计数变化清理样本，不以已调用 Orgasm 伪造新周期", () =>
        {
            var d = Driver(); atOrgasm = x => x.orgasms++; d.Step(1); atOrgasm = null;
            Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
        });
        Check("高潮调用被拦截不会补发或增加计数", () =>
        { var d = Driver(); d.sex_ticks = 180; d.Step(1); Equal(179, d.sex_ticks); Equal(0, d.orgasms); });
        Check("高潮前置异常保留原写回和原异常，旧样本失效", () =>
        {
            var d = Driver(); atOrgasm = _ => throw new InvalidOperationException("original event failure");
            try { d.Step(3); throw new Exception("exception was swallowed"); }
            catch (InvalidOperationException e) { Equal("original event failure", e.Message); }
            finally { atOrgasm = null; }
            Equal(997, d.sex_ticks); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
        });
        Check("所有加载阶段只保留原写入，不生成或重放样本", () =>
        {
            foreach (var mode in new[] { LoadSaveMode.LoadingVars, LoadSaveMode.ResolvingCrossRefs, LoadSaveMode.PostLoadInit })
            {
                var d = Driver(); d.Step(1); Scribe.mode = mode;
                try { SSCOrgasmProgressSampling.WriteRjw(d, 777); Equal(777, d.sex_ticks); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _)); }
                finally { Scribe.mode = LoadSaveMode.Inactive; }
                d.Step(2); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var r)); Equal(2L, r.RawAdvance);
            }
        });
        Check("任务替换／SexProps 替换／外部写入使旧结果不可提交", () =>
        {
            var d = Driver(); d.Step(1); d.Sexprops = Blank<SexProps>(); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            d.Step(1); d.sex_ticks -= 10; Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            d.Step(1); d.pawn.jobs.curDriver = Driver(); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
        });
        Check("缺上下文或失效路径仍执行原写入，空实例保留字段异常", () =>
        {
            var d = Driver(); d.Sexprops = null; SSCOrgasmProgressSampling.WriteRjw(d, 42); Equal(42, d.sex_ticks);
            Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            try { SSCOrgasmProgressSampling.WriteRjw(null, 1); throw new Exception("expected null field write"); }
            catch (NullReferenceException) { }
        });
        // 静态拒绝路径与诊断重放都不得改变已经发布的运行结果。
        MatcherChecks(field, orgasm, bridge);
        Check("诊断重放不改变运行开关、周期或既有样本", () =>
        {
            var d = Driver(); d.Step(1); Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var before));
            _ = PatchProcessor.GetCurrentInstructions(AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Step)));
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out var after));
            Equal(before.Cycle, after.Cycle); Equal(before.Operation, after.Operation);
        });
        Check("热路径稳定采样不逐次分配", () =>
        {
            var d = Driver(); d.sex_ticks = 1000000;
            for (int i = 0; i < 128; i++) SSCOrgasmProgressSampling.WriteRjw(d, d.sex_ticks - 1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) SSCOrgasmProgressSampling.WriteRjw(d, d.sex_ticks - 1);
            Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        });
        Check("Harmony 打破循环依赖后不能伪装顺序门槛通过", () =>
        {
            var original = AccessTools.Method(typeof(Program), nameof(SideEffect));
            new Harmony("ssc.tests.cycle.a").Patch(original, prefix: new HarmonyMethod(typeof(Program), nameof(NoopA)) { before = new[] { "ssc.tests.cycle.b" } });
            new Harmony("ssc.tests.cycle.b").Patch(original, prefix: new HarmonyMethod(typeof(Program), nameof(NoopB)) { before = new[] { "ssc.tests.cycle.a" } });
            var info = Harmony.GetPatchInfo(original);
            Require(!SSCOrgasmProgressPatches.DependenciesSatisfied(info.Prefixes.ToArray(), PatchProcessor.GetSortedPatchMethods(original, info.Prefixes.ToArray())));
        });
        // 故意破坏门槛的场景放最后，不让失败关闭影响前面的正常推进验证。
        Check("新增 Transpiler 即使没有打乱末位排序也使旧安装凭据失效", () =>
        {
            var method = AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Step));
            new Harmony("ssc.tests.late-transpiler").Patch(method, transpiler: new HarmonyMethod(typeof(Program), nameof(PassThrough)));
            SSCOrgasmProgressPatches.ValidateInstalledOrder();
            Require(!SSCOrgasmProgressPatches.IsReady(SSCOrgasmAdvanceSource.RjwSexTick));
            Require(SSCOrgasmProgressPatches.IsReady(SSCOrgasmAdvanceSource.AnimationSexTick));
            var d = Driver(); d.Step(3); Equal(997, d.sex_ticks); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
        });
        Check("重复安装不重复桥接，缺少必要观察点后关闭采样但保留原写入", () =>
        {
            var method = AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Step));
            SSCOrgasmProgressPatches.InstallAdvance(samplingHarmony, method, SSCOrgasmAdvanceSource.RjwSexTick, 1);
            Equal(1, Harmony.GetPatchInfo(method).Transpilers.Count(x => x.PatchMethod.DeclaringType == typeof(SSCOrgasmProgressPatches)));
            Require(SSCOrgasmProgressPatches.IsReady(SSCOrgasmAdvanceSource.AnimationSexTick));
            harmony.Unpatch(AccessTools.Method(typeof(JobDriver_Sex), "ExposeData"), HarmonyPatchType.Prefix, SSCOrgasmProgressPatches.Owner);
            SSCOrgasmProgressPatches.ValidateInstalledOrder();
            Require(!SSCOrgasmProgressPatches.IsReady(SSCOrgasmAdvanceSource.AnimationSexTick));
            var d = Driver(); d.Step(3); Equal(997, d.sex_ticks); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            SSCOrgasmProgressSampling.WriteAnimation(d, 333); Equal(333, d.sex_ticks); Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
        });
        Console.WriteLine($"RESULT: {passed}/{passed} passed");
        return 0;
    }

    /// <summary>实际执行 RJW、动画及 UAP 方法，验证写回前后边界、Prefix 替代和共享受方去重。</summary>
    private static void ActualPathChecks(Harmony harmony, Assembly uap)
    {
        // 独立进程没有地图 TickManager；只隔离推进之后的推送动画／声音时机。
        new Harmony("rjwanim").Patch(AccessTools.Method(typeof(JobDriver_Sex), "SexTick"),
            prefix: new HarmonyMethod(typeof(Rimworld_Animations.HarmonyPatch_SexTick), "Prefix"));
        SSCOrgasmProgressPatches.ValidateInstalledOrder();
        Type initiatorType = uap.GetType("UAP_Animations.JobDriver_UAPGroupSexInitiator", true);
        Type receiverType = uap.GetType("UAP_Animations.JobDriver_UAPGroupSexReceiver", true);
        var shared = AccessTools.Method(initiatorType, "TickSharedReceiverOnce");
        Check("实际 RJW 原路径分别推进自身与接收者", () =>
        {
            var actor = Driver(); var receiver = (JobDriver_SexBaseReciever)Driver(receiverType);
            receiver.parteners = new List<Pawn> { actor.pawn };
            actor.SexTick(actor.pawn, receiver.pawn);
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(actor, out var a));
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(receiver, out var b));
            Equal(SSCOrgasmAdvanceSource.RjwSexTick, a.Source); Equal(a.Source, b.Source); Equal(1L, a.RawAdvance); Equal(1L, b.RawAdvance);
        });
        Check("实际动画 Prefix 替代原方法且只推进自身一次", () =>
        {
            var actor = Driver(); var other = Driver(); actor.SexTick(actor.pawn, other.pawn);
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(actor, out var a)); Equal(SSCOrgasmAdvanceSource.AnimationSexTick, a.Source);
            Equal(1L, a.RawAdvance); Equal(999, actor.sex_ticks); Equal(1000, other.sex_ticks); Equal(1UL, a.Operation);
        });
        Check("实际 UAP 发起方路径经过采样桥", () =>
        {
            var actor = Driver(initiatorType); AccessTools.Method(initiatorType, "TickInitiatorOnly").Invoke(actor, null);
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(actor, out var a)); Equal(SSCOrgasmAdvanceSource.UapInitiator, a.Source); Equal(1L, a.RawAdvance);
        });
        Check("实际 UAP 共享受方先初始化 SexProps 且保留 PrimaryInitiator 去重", () =>
        {
            var actor = Driver(initiatorType); var other = Driver(initiatorType); var receiver = Driver(receiverType);
            receiver.Sexprops = null;
            AccessTools.Field(typeof(JobDriver_Sex), "PartnerPawn").SetValue(actor, receiver.pawn);
            AccessTools.Field(typeof(JobDriver_Sex), "PartnerPawn").SetValue(other, receiver.pawn);
            actor.Sexprops.pawn = actor.pawn; actor.Sexprops.partner = receiver.pawn;
            actor.Sexprops.interaction = new rjw.Modules.Interactions.SexInteraction(Blank<RimWorld.InteractionDef>());
            actor.Sexprops._resolved = Blank<rjw.Modules.Interactions.SexInteractionResolved>();
            actor.Sexprops._resolved.Interaction = actor.Sexprops.interaction;
            var session = RuntimeHelpers.GetUninitializedObject(shared.GetParameters()[0].ParameterType);
            AccessTools.Field(session.GetType(), "PrimaryInitiator").SetValue(session, actor.pawn);
            Require((bool)shared.Invoke(actor, new[] { session }));
            Require(receiver.Sexprops != null && SSCOrgasmProgressUtility.TryGetLastObservedAdvance(receiver, out _));
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(receiver, out var first)); Equal(SSCOrgasmAdvanceSource.UapReceiver, first.Source);
            Require((bool)shared.Invoke(other, new[] { session })); Equal(999, receiver.sex_ticks);
            Require(SSCOrgasmProgressUtility.TryGetLastObservedAdvance(receiver, out var next)); Equal(first.Operation, next.Operation);
        });
    }

    /// <summary>用真实类型字段验证公共接口的只读性、零／变速／阈值差值以及上下文失效。</summary>
    private static void PureInterfaceChecks()
    {
        Check("空或错误的来源凭据不能获得可用样本", () =>
        {
            var d = Driver(); var empty = SSCOrgasmProgressUtility.CaptureBeforeAdvance(d, default);
            Require(!empty.IsValid); Equal(SSCOrgasmAdvanceFailure.InvalidContext, empty.Failure);
            var unauthorized = new SSCOrgasmObservationContext(SSCOrgasmAdvanceSource.RjwSexTick, new object(), 1, 1, true);
            Require(!SSCOrgasmProgressUtility.CaptureBeforeAdvance(d, unauthorized).IsValid); Equal(1000, d.sex_ticks);
        });
        foreach (int delta in new[] { 0, 1, 3, 123 })
            Check("公共接口纯读实际减量 " + delta, () =>
            {
                var d = Driver(); var c = Context(); var before = SSCOrgasmProgressUtility.CaptureBeforeAdvance(d, c);
                d.sex_ticks -= delta;
                Require(SSCOrgasmProgressUtility.TryGetOriginalAdvance(before, d, c, out var r));
                Equal((long)delta, r.RawAdvance); Equal(1000 - delta, d.sex_ticks);
                Require(SSCOrgasmProgressUtility.IsStillCurrent(r, c));
                Require(!SSCOrgasmProgressUtility.TryGetLastObservedAdvance(d, out _));
            });
        Check("差值使用宽整数而非 int 溢出", () =>
        {
            var d = Driver(); d.sex_ticks = int.MaxValue; var c = Context(); var s = SSCOrgasmProgressUtility.CaptureBeforeAdvance(d, c);
            d.sex_ticks = int.MinValue; Require(SSCOrgasmProgressUtility.TryGetOriginalAdvance(s, d, c, out var r)); Equal(4294967295L, r.RawAdvance);
        });
        Check("显式移动阈值来源按距离计算，普通来源拒绝阈值变化", () =>
        {
            var d = Driver(); var c = Context(); var s = SSCOrgasmProgressUtility.CaptureBeforeAdvance(d, c);
            d.orgasmstick += 3; d.sex_ticks -= 3;
            Require(!SSCOrgasmProgressUtility.TryGetOriginalAdvance(s, d, c, out _, out var failure)); Equal(SSCOrgasmAdvanceFailure.UnexpectedThresholdChange, failure);
            c = new SSCOrgasmObservationContext(SSCOrgasmAdvanceSource.External, new object(), 1, 1, true);
            s = SSCOrgasmProgressUtility.CaptureBeforeAdvance(d, c); d.orgasmstick += 3; d.sex_ticks -= 3;
            Require(SSCOrgasmProgressUtility.TryGetOriginalAdvance(s, d, c, out var r)); Equal(3L, r.CounterAdvance); Equal(3L, r.ThresholdAdvance); Equal(6L, r.RawAdvance);
        });
        foreach (string change in new[] { "job", "props", "cycle", "operation", "counter", "start", "duration", "count", "actor" })
            Check("公共接口拒绝失效边界：" + change, () =>
            {
                var d = Driver(); var c = Context(); var s = SSCOrgasmProgressUtility.CaptureBeforeAdvance(d, c); d.sex_ticks--;
                switch (change)
                {
                    case "job": d.job = Blank<Job>(); break;
                    case "props": d.Sexprops = Blank<SexProps>(); break;
                    case "cycle": c = new SSCOrgasmObservationContext(c.Source, c.CycleOwner, 2, c.Operation); break;
                    case "operation": c = new SSCOrgasmObservationContext(c.Source, c.CycleOwner, c.Cycle, 2); break;
                    case "counter": d.sex_ticks = 1001; break;
                    case "start": d.orgasmStartTick--; break;
                    case "duration": d.duration--; break;
                    case "count": d.orgasms++; break;
                    case "actor": d.pawn = Blank<Pawn>(); break;
                }
                Require(!SSCOrgasmProgressUtility.TryGetOriginalAdvance(s, d, c, out _));
            });
    }

    /// <summary>构造不可安全识别的 IL 变体，验证原子拒绝与标签保留，不执行构造出的错误路径。</summary>
    private static void MatcherChecks(FieldInfo field, MethodInfo orgasm, MethodInfo bridge)
    {
        var method = AccessTools.Method(typeof(TickFixture), nameof(TickFixture.Step));
        foreach (string change in new[] { "missing write", "extra write", "threshold write", "missing orgasm", "side effect" })
            Check("IL 失配整段原样返回：" + change, () =>
            {
                var il = PatchProcessor.GetOriginalInstructions(method); int index = il.FindIndex(x => x.opcode == OpCodes.Stfld && Equals(x.operand, field));
                if (change == "missing write") il[index] = new CodeInstruction(OpCodes.Nop);
                if (change == "extra write") il.Add(new CodeInstruction(OpCodes.Stfld, field));
                if (change == "threshold write") il.Add(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(JobDriver_Sex), "orgasmstick")));
                if (change == "missing orgasm") il.RemoveAll(x => Equals(x.operand, orgasm));
                if (change == "side effect") il.Insert(index - 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Program), nameof(SideEffect))));
                Require(!SSCOrgasmProgressPatches.TryRewrite(il, field, orgasm, bridge, 1, out var output, out _));
                Require(il.SequenceEqual(output));
            });
        Check("成功替换保留原标签／异常块且不修改输入对象", () =>
        {
            var il = PatchProcessor.GetOriginalInstructions(method); int i = il.FindIndex(x => x.opcode == OpCodes.Stfld && Equals(x.operand, field));
            var marker = new DynamicMethod("labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
            il[i].labels.Add(marker); il[i].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            Require(SSCOrgasmProgressPatches.TryRewrite(il, field, orgasm, bridge, 1, out var output, out _));
            Require(il[i].opcode == OpCodes.Stfld && output[i].opcode == OpCodes.Call);
            Require(output[i].labels.Contains(marker) && output[i].blocks.Count == il[i].blocks.Count);
        });
        Check("第二锚点失败不提交已匹配的第一处写回", () =>
        {
            var il = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(JobDriver_Sex), "SexTick"));
            int last = il.FindLastIndex(x => x.opcode == OpCodes.Stfld && Equals(x.operand, field));
            il[last - 1] = new CodeInstruction(OpCodes.Nop);
            Require(!SSCOrgasmProgressPatches.TryRewrite(il, field, orgasm, bridge, 2, out var output, out _)); Require(il.SequenceEqual(output));
        });
        Check("拒绝分支绕过减法直接将初始化值写回的路径", () =>
        {
            var il = PatchProcessor.GetOriginalInstructions(method);
            int write = il.FindIndex(x => x.opcode == OpCodes.Stfld && Equals(x.operand, field));
            var target = new DynamicMethod("bypass", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
            il[write].labels.Add(target);
            il.InsertRange(0, new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldc_I4, 500), new CodeInstruction(OpCodes.Br, target) });
            Require(!SSCOrgasmProgressPatches.TryRewrite(il, field, orgasm, bridge, 1, out var output, out _));
            Require(il.SequenceEqual(output));
        });
    }

    /// <summary>创建没有地图或构造副作用的真实类型实例；随后由夹具明确填写所需字段。</summary>
    private static T Blank<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    /// <summary>建立用于变量减量／事件边界验证的派生驱动，并复用真实 Pawn、Job 与 SexProps 类型。</summary>
    private static TickFixture Driver() => (TickFixture)Driver(typeof(TickFixture));
    /// <summary>建立指定实际驱动类型的最小活动上下文，不替换生产采样或 UAP 的业务实现。</summary>
    private static JobDriver_Sex Driver(Type type)
    {
        var d = (JobDriver_Sex)RuntimeHelpers.GetUninitializedObject(type); d.pawn = Blank<Pawn>(); d.job = Blank<Job>();
        d.pawn.jobs = Blank<Pawn_JobTracker>(); d.pawn.jobs.curDriver = d; d.pawn.jobs.curJob = d.job;
        d.Sexprops = Blank<SexProps>(); d.sex_ticks = d.orgasmStartTick = 1000; d.duration = d.ticks_left = 2000; d.orgasmstick = 180;
        return d;
    }
    /// <summary>为独立公共接口夹具发行一个外部观察者上下文，不加入自动采样弱表。</summary>
    private static SSCOrgasmObservationContext Context() => new SSCOrgasmObservationContext(SSCOrgasmAdvanceSource.External, new object(), 1, 1);
    /// <summary>遵守分级选择并统计用例；每次恢复宿主钩子与加载模式，避免异常污染后续测试。</summary>
    private static void Check(string name, Action test)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        try { test(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error) { throw new Exception("FAIL: " + name, error); }
        finally { atOrgasm = null; animationBranch = false; Scribe.mode = LoadSaveMode.Inactive; }
    }
    /// <summary>断言技术契约成立，失败交给统一用例包装保留场景名称。</summary>
    private static void Require(bool value, string detail = "assertion failed") { if (!value) throw new Exception(detail); }
    /// <summary>比较端点或操作标识，错误消息同时保留预期和实测值。</summary>
    private static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
    /// <summary>只隔离原高潮副作用服务，允许用例在真实事件入口检查采样或模拟计数／异常。</summary>
    private static bool ObserveOrgasm(JobDriver_Sex __instance) { atOrgasm?.Invoke(__instance); return false; }
    /// <summary>隔离无地图宿主的时钟推送，不修改高潮计数、倒计时或采样差分。</summary>
    private static bool NoInterval(ref bool __result) { __result = false; return false; }
    /// <summary>受测 Prefix 方法自身包含推进，验证先打补丁再生成入口包装能够真正到达桥。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AnimationPrefix(TickFixture __instance)
    {
        if (!animationBranch) return true;
        __instance.sex_ticks--; __instance.Orgasm(); return false;
    }
    /// <summary>提供额外调用和 owner 依赖测试的静态锚点，本身不包含业务行为。</summary>
    private static void SideEffect() { }
    /// <summary>构成循环 before 约束的一端，仅检查 Harmony 实际排序后的依赖是否仍成立。</summary>
    private static void NoopA() { }
    /// <summary>构成循环 before 约束的另一端，不修改原方法的返回或参数。</summary>
    private static void NoopB() { }
    /// <summary>模拟安装后新增的纯转换，证明集合变化即使不改变末位排序也会使旧门槛失效。</summary>
    private static IEnumerable<CodeInstruction> PassThrough(IEnumerable<CodeInstruction> instructions) => instructions;

    internal sealed class TickFixture : JobDriver_Sex
    {
        /// <summary>满足驱动抽象契约，不在此夹具中构建或启动游戏场景。</summary>
        protected override IEnumerable<Toil> MakeNewToils() { yield break; }
        /// <summary>提供正常／抛错的真实可补丁 override，验证生产范围在异常时也能正确关闭。</summary>
        protected override void SetupOrgasmTicks() { if (resetThrows) throw new InvalidOperationException("reset failure"); sex_ticks = orgasmStartTick = 1000; }
        /// <summary>通过虚调用触发已安装的周期观察，禁止内联以保留受测边界。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public void ResetForTest() => SetupOrgasmTicks();
        /// <summary>以参数减量推进并进入原 Orgasm 包装，验证原表达式计算值被保留。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public void Step(int delta) { ticks_left--; sex_ticks -= delta; Orgasm(); }
        /// <summary>模拟双方独立写回，验证接收者不使用发起者的差值或记录。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public void Pair(TickFixture receiver, int own, int received)
        { sex_ticks -= own; Orgasm(); receiver.sex_ticks -= received; receiver.Orgasm(); }
        /// <summary>作为被动画 Prefix 替代的基础入口，bodyCalls 用来识别是否误执行了原方法。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public void Entry() { bodyCalls++; sex_ticks--; Orgasm(); }
    }
}
