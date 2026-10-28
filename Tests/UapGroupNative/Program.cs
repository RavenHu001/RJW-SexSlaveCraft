// AI 辅助生成：实际生产 SSC/RJW/UAP/Harmony；隔离 UI、地图调度及身体初始化，不启动游戏。
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static class Entry
{
    /// <summary>先安装实际程序集解析，再反射进入含游戏类型的入口，避免入口 JIT 提前查找尚未定位的依赖。</summary>
    private static int Main(string[] args)
    {
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        using var info = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "1.7/BUILD-INFO.json")));
        var dirs = info.RootElement.GetProperty("references").EnumerateArray()
            .Select(r => Path.GetDirectoryName(r.GetProperty("path").GetString())).Prepend(game).ToArray();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = dirs.Select(d => Path.Combine(d, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : context.LoadFromAssemblyPath(path);
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { repo, game, args.Contains("--no-uap") });
    }
}

internal static class Program
{
    private static Harmony h;
    private static Map map;
    private static Thing anchor;
    private static object plan, match;
    private static Pawn[] people;
    private static int passed, installs, failInstall, ended, nextId, initialized, processCalls;
    private static bool beginResult;
    private static string beginReason;
    private static int startBodies, otherStartPostfixes, automaticSelections, namedAnimationStarts;
    private static readonly List<string> logs = new();
    /// <summary>为测试边界创建没有构造副作用的真实类型实例；本例所需字段随后由夹具显式填写。</summary>
    private static object Bare(Type t) => RuntimeHelpers.GetUninitializedObject(t);
    /// <summary>提供同一无构造实例化边界的泛型入口，不复制生产对象的业务初始化。</summary>
    private static T Bare<T>() where T : class => (T)Bare(typeof(T));
    /// <summary>仅在宿主内填写真实对象的环境字段，不把可写反射接口暴露给正式 mod。</summary>
    private static void Set(object obj, string name, object value) => AccessTools.Field(obj.GetType(), name).SetValue(obj, value);
    /// <summary>只读访问测试需要的真实字段，使任务、驱动和步骤断言仍基于游戏对象。</summary>
    private static object Get(object obj, string name) => AccessTools.Field(obj.GetType(), name).GetValue(obj);
    /// <summary>给指定引擎服务安装本宿主的环境前缀；生产兼容判断仍由候选程序集执行。</summary>
    private static void Patch(MethodBase method, string name) => h.Patch(method, prefix: new HarmonyMethod(typeof(Program), name));
    /// <summary>调用实际类型声明的方法，用于验证私有 UAP 接入和原版步骤构建的真实 Harmony 路径。</summary>
    private static object Call(Type type, string name, object instance, params object[] args)
        => AccessTools.DeclaredMethod(type, name).Invoke(instance, args);

    /// <summary>安装引擎边界后运行实际兼容模块；分开报告标准用例和不加载 UAP 的独立变体。</summary>
    internal static int Run(string repo, string game, bool noUap)
    {
        SscTestProfile.Start();
        if (!SscTestProfile.IncludesSuite) return 0;
        h = new Harmony("ssc.tests.uap.environment");
        // 先隔离日志和 DefOf 服务，不触发正式 ModStartup 或枚举用户全部安装模组。
        foreach (string name in new[] { "Message", "Warning", "Error" })
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == name && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                Patch(method, nameof(Log));
        Patch(AccessTools.Method(typeof(DefOfHelper), "EnsureInitializedInCtor"), nameof(Skip));
        SSCMod.settings = new SSCSettings { enableSSCLogs = true, enableSpecializationRestrictionOverrides = false };
        AccessTools.Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
        // 独立进程不加载 UAP DLL，验证可选模块缺席；该结果与标准套件数量分别报告。
        if (noUap)
        {
            SSCUapGroupPatches.Install();
            Check("UAP absent: optional installer leaves ordinary policy intact", () => Require(!SSCUapGroupApi.Ready));
            Console.WriteLine($"RESULT: {passed}/{passed} passed"); return 0;
        }
        // 实际 UAP 类型、方法和字段必须来自安装件；环境服务在生成生产包装前隔离。
        Assembly uap = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "Mods/RJW_动画_UAP终极动画包/Assemblies/UAP_Animations.dll"));
        Require(SSCUapGroupApi.Resolve());
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(ReservationUtility)).Where(m => m.Name == "Reserve" && m.ReturnType == typeof(bool)))
            Patch(method, nameof(Reserve));
        Patch(AccessTools.PropertyGetter(typeof(Thing), "Spawned"), nameof(Spawned));
        Patch(AccessTools.PropertyGetter(typeof(Thing), "Map"), nameof(Map));
        Patch(AccessTools.PropertyGetter(typeof(Thing), "MaxHitPoints"), nameof(HitPoints));
        Patch(AccessTools.PropertyGetter(typeof(Pawn), "Dead"), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(Pawn), "Drafted"), nameof(False));
        Patch(AccessTools.Method(typeof(Pawn_JobTracker), "StartJob"), nameof(InstallJob));
        Patch(AccessTools.Method(typeof(Pawn_JobTracker), "EndCurrentJob"), nameof(EndJob));
        h.Patch(AccessTools.Method(typeof(JobDriver), "ReadyForNextToil"),
            prefix: new HarmonyMethod(typeof(Program), nameof(Advance)) { priority = Priority.Last });
        Patch(AccessTools.Method(typeof(SSCOrdinarySexPartsUtility), "Capture"), nameof(Skip));
        h.Patch(AccessTools.Method(typeof(SexUtility), "ProcessSex"),
            prefix: new HarmonyMethod(typeof(Program), nameof(ProcessBoundary)) { priority = Priority.Last });
        Patch(AccessTools.Method(typeof(JobDriver_Sex), "setup_ticks"), nameof(Skip));
        h.Patch(AccessTools.Method(SSCUapGroupApi.Matcher, "TryBuildLaunchPlan"),
            transpiler: new HarmonyMethod(typeof(Program), nameof(Match)));
        Patch(AccessTools.Method(SSCUapGroupApi.Request, "TryStart"), nameof(Gather));
        Patch(AccessTools.Method(typeof(Window), "Close"), nameof(Skip));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Messages)).Where(m => m.Name == "Message")) Patch(method, nameof(Skip));
        Patch(SSCUapGroupApi.Stop, nameof(Skip)); Patch(SSCUapGroupApi.RestoreOne, nameof(Skip));
        // 使用真实翻译器和生产键值，但不启动完整语言扫描或 Def 注入。
        var language = Bare<LoadedLanguage>(); language.folderName = "English";
        Set(language, "legacyFolderName", "English");
        language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
        language.stringFiles = new(); language.keyedReplacements = new(); language.loadErrors = new();
        Set(language, "dataIsLoaded", true); Set(language, "wordInfo", new LanguageWordInfo());
        foreach (string file in Directory.GetFiles(Path.Combine(repo, "1.7/Languages/English/Keyed"), "*.xml"))
            foreach (var entry in XDocument.Load(file).Root.Elements())
                language.keyedReplacements[entry.Name.LocalName] = new LoadedLanguage.KeyedReplacement { value = entry.Value };
        LanguageDatabase.activeLanguage = LanguageDatabase.defaultLanguage = language;
        SSCDefOf.ChainOfSexSlave = new HediffDef { defName = "ChainOfSexSlave" };
        foreach (var item in new[] { ("UAP_GroupSexReceiver", SSCUapGroupApi.Receiver),
            ("UAP_GroupSexInitiator", SSCUapGroupApi.Initiator), ("UAP_GroupPassiveParticipant", SSCUapGroupApi.Passive) })
            DefDatabase<JobDef>.Add(new JobDef { defName = item.Item1, driverClass = item.Item2 });
        // 使用实际 Guard 前缀；所有引擎边界须在生成包装前安装，以免原小型函数已内联。
        h.Patch(AccessTools.Method(typeof(JobDriver_Sex), "TryMakePreToilReservations"),
            prefix: new HarmonyMethod(typeof(Patch_JobDriver_Sex_ProtectChainOfSexSlave), "Prefix"));
        h.Patch(AccessTools.DeclaredMethod(typeof(JobDriver_SexBaseInitiator), "TryMakePreToilReservations"),
            prefix: new HarmonyMethod(typeof(SSCRestrictionReservationHook), "Prefix"));
        // 装入真实 RAF Start 后置；仅隔离 RJW 身体初始化和最终图形播放，自动匹配及其 PawnTest 保持原样。
        var rafStart = typeof(Rimworld_Animations.AnimationUtility).Assembly.GetType(
            "Rimworld_Animations.HarmonyPatch_JobDriver_SexBaseInitiator_Start", true);
        h.Patch(AccessTools.Method(typeof(JobDriver_SexBaseInitiator), "Start"),
            transpiler: new HarmonyMethod(typeof(Program), nameof(StartBodyBoundary)),
            postfix: new HarmonyMethod(typeof(Program), nameof(OtherStartPostfix)));
        new Harmony("rjwanim").Patch(AccessTools.Method(typeof(JobDriver_SexBaseInitiator), "Start"),
            postfix: new HarmonyMethod(AccessTools.DeclaredMethod(rafStart, "Postfix")));
        h.Patch(AccessTools.Method(typeof(Rimworld_Animations.AnimationUtility), "FindGroupAnimation"),
            prefix: new HarmonyMethod(typeof(Program), nameof(ObserveAutomaticSelection)));
        Patch(AccessTools.Method(typeof(Rimworld_Animations.AnimationUtility), "StartGroupAnimation",
            new[] { typeof(List<Pawn>), typeof(Rimworld_Animations.GroupAnimationDef), typeof(Thing) }), nameof(NamedAnimationBoundary));
        // 先运行实际 Start 包装，再安装对其 RAF 后置自身的补丁，防止重建上层包装掩盖已 JIT 的调用问题。
        WarmAutomaticStart();
        // 在生成生产包装前隔离小型地图 getter，避免包装已内联原 getter 后才做 detour。
        SSCUapGroupPatches.Install();
        h.Patch(AccessTools.Method(SSCUapGroupApi.Component, "TryBeginRequest"),
            postfix: new HarmonyMethod(typeof(Program), nameof(ObserveBegin)));
        Check("actual UAP signatures install complete adapter", () => Require(SSCUapGroupApi.Ready, string.Join("\n", logs)));
        Check("incompatible required signature is rejected instead of guessed", () =>
        {
            bool rejected = false;
            try { SSCUapGroupApi.Method(SSCUapGroupApi.Dialog, "TryInitiate", typeof(bool)); }
            catch (MissingMethodException) { rejected = true; }
            Require(rejected);
        });

        Check("real matcher failure preserves original reason and empty plan without throwing", () =>
        {
            var batch = Fixture(true, false);
            var method = AccessTools.Method(SSCUapGroupApi.Matcher, "TryBuildLaunchPlan");
            // 实际原方法的早期拒绝不依赖身体／动画环境；临时撤掉算法替身，验证失败 out 不被 Postfix 清空。
            h.Unpatch(method, HarmonyPatchType.Transpiler, h.Id);
            try
            {
                object[] args = { null, match, null, null };
                using (new SSCUapGroupRuntime.Scope(batch))
                    Require(!(bool)method.Invoke(null, args));
                Require(args[2] == null && args[3] is string reason && !string.IsNullOrWhiteSpace(reason));
                object[] begin = { anchor, people.Take(2).ToList(), match, null };
                Require(!(bool)Call(SSCUapGroupApi.Component, "TryBeginRequest", map.GetComponent(SSCUapGroupApi.Component), begin));
                Require(begin[3] is string rejected && !string.IsNullOrWhiteSpace(rejected));
            }
            finally { h.Patch(method, transpiler: new HarmonyMethod(typeof(Program), nameof(Match))); }
        });

        // 来源、准入和安装屏障覆盖原故障；原 Policy 在相同人物的普通请求中仍须拒绝。
        Check("GUI provenance carries override; receiver-first Deny group installs behind gate", () =>
        {
            var batch = Fixture(true, true);
            Require(batch.Manual && batch.Override);
            object[] launchArgs = { anchor, people.ToList(), match, null };
            using (new SSCUapGroupRuntime.Scope(batch))
                Require((bool)Call(SSCUapGroupApi.Launcher, "TryStartRJWBackedGroupSession", null, launchArgs),
                    launchArgs[3] + string.Join("\n", logs));
            Require(batch.Committed && batch.Jobs.Count == 3 && initialized == 0);
            Require(SSCRestrictionJobGuard.TryEnd((JobDriver_SexBaseInitiator)people[1].jobs.curDriver, out bool endAllowed) && !endAllowed);
            var receiver = (JobDriver_Sex)people[0].jobs.curDriver;
            Require(SSCRestrictionJobContext.TryCreate(receiver, out var request) && request.DirectionKnown && request.Initiator == people[1]);
            Require(SSCRestrictionJobGuard.TryReserve(receiver, out bool allow) && allow);
            Require(!SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(people[1], people[0], SSCInteractionKind.Consensual, true)).Allowed,
                "standalone policy must still reject the saved Deny");
            var toils = (List<Toil>)Get(receiver, "toils");
            toils[1].initAction = () => initialized++;
            Require(toils.Count >= 2 && toils[0].defaultCompleteMode == ToilCompleteMode.Never);
            // 实际前置 Toil 构建和提交状态已核实；推进转交给引擎边界，不执行地图/音效。
            Require(initialized == 0);
            toils[0].tickAction(); Require(initialized == 1, "committed gate must release original toil");
        });
        Check("override off returns actual pair rejection to GUI caller before any job installation", () =>
        {
            var batch = Fixture(false, true);
            Require(!beginResult && !string.IsNullOrWhiteSpace(beginReason) && beginReason.Contains(people[1].LabelShortCap));
            Require(installs == 0 && batch.Closed && batch.Denial != null);
        });
        Check("pending group initiators skip only RAF automatic Start selection and keep selected UAP animation", () =>
        {
            var batch = Fixture(true, true); Require(SSCUapGroupRuntime.Launch(batch, out _));
            var ready = (JobDriver_SexBaseInitiator)people[2].jobs.curDriver;
            var pending = (JobDriver_SexBaseInitiator)people[1].jobs.curDriver;
            var receiver = (JobDriver_SexBaseReciever)people[0].jobs.curDriver;
            ready.Sexprops = Bare<SexProps>(); ready.Sexprops.pawn = people[2]; ready.Sexprops.partner = people[0];
            Require(pending.Sexprops == null && receiver.parteners[0] == people[1]);
            // 真正的 RAF 过滤器会解引用尚未初始化的另一名发起者；不替换其匹配实现。
            var filter = new Rimworld_Animations.PawnTest_RJWInitiator();
            Set(filter, "interactionDefs", new List<InteractionDef> { new InteractionDef { defName = "FixtureInteraction" } });
            DefDatabase<Rimworld_Animations.GroupAnimationDef>.Add(new Rimworld_Animations.GroupAnimationDef {
                defName = "Fixture", numActors = 3, contexts = new List<Rimworld_Animations.GroupAnimationContext> {
                    new Rimworld_Animations.GroupAnimationContext { whitelist = new List<Rimworld_Animations.BasePawnTest> { filter, filter, filter } } } });
            ready.Start();
            Require(startBodies == 1 && otherStartPostfixes == 1 && automaticSelections == 0 && pending.Sexprops == null);
            object[] namedArgs = { people.ToList(), "Fixture", anchor, null };
            Require((bool)Call(SSCUapGroupApi.Dialog.Assembly.GetType("UAP_Animations.UAPGroupAnimationBridge", true),
                "TryStartNamedGroupAnimation", null, namedArgs), namedArgs[3] as string);
            Require(namedAnimationStarts == 1);
            // 同一 RJW Start 包装对未接管任务仍应调用 RAF 自动选择，其他后置也继续运行。
            Job ordinaryJob = JobMaker.MakeJob(SSCUapGroupApi.InitiatorDef, people[0]);
            var ordinary = (JobDriver_SexBaseInitiator)ordinaryJob.MakeDriver(people[2]);
            people[2].jobs.curJob = ordinaryJob; people[2].jobs.curDriver = ordinary;
            receiver.parteners.Clear(); receiver.parteners.Add(people[2]);
            ordinary.Start();
            Require(startBodies == 2 && otherStartPostfixes == 2 && automaticSelections == 1);
        });
        Check("non-GUI request keeps original UAP path without SSC session takeover", () =>
        {
            var batch = Fixture(true, false);
            Require(!batch.Manual && !batch.Override);
            object[] args = { anchor, people.ToList(), match, null };
            var component = map.GetComponent(SSCUapGroupApi.Component);
            Require((bool)Call(SSCUapGroupApi.Component, "TryBeginRequest", component, args));
            object request = ((IList)Get(component, "activeRequests"))[0];
            Require(!SSCUapGroupRuntime.FindRequest(request, out _) && installs == 0 &&
                Current.Game.GetComponent<SSCUapGroupGameComponent>().Batches.Count == 1);
        });
        Check("override off still fixes receiver-first when all actual pairs allow", () =>
        {
            var batch = Fixture(false, false, 4, true);
            foreach (var p in people.Skip(1)) p.TryGetComp<CompSexSlaveTraining>().restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Allow;
            Require(SSCUapGroupRuntime.Launch(batch, out var reason), reason);
            Require(batch.Committed && installs == 4 && batch.Jobs.All(s => s.Job.targetB.Thing == anchor));
        });
        // 失败和替换边界只允许清理登记过的任务、映射与结算对象。
        Check("mid-install failure cancels only owned jobs and never enters real toil", () =>
        {
            var batch = Fixture(true, true); failInstall = 2;
            Require(!SSCUapGroupRuntime.Launch(batch, out _));
            Require(batch.Aborted && installs == 2 && initialized == 0 && ended == 1);
            Require(people[0].CurJob == null && people[2].CurJob == null);
        });
        Check("replacement job survives cancellation and pooled Job loses old ticket", () =>
        {
            var batch = Fixture(true, true); Require(SSCUapGroupRuntime.Launch(batch, out _));
            var slot = batch.Jobs[1]; Job old = slot.Job;
            people[1].jobs.curJob = new Job { def = new JobDef(), loadID = 99999 };
            var index = (IDictionary)AccessTools.Field(SSCUapGroupApi.Manager, "sessionByPawnThingId").GetValue(null);
            index[people[1].thingIDNumber] = 9999;
            SSCUapGroupRuntime.Close(batch, true);
            Require(people[1].CurJob.loadID == 99999 && (int)index[people[1].thingIDNumber] == 9999);
            // 池化复用的新普通任务必须继续原版路径，不能被旧场景的全局推进钩子冻结。
            old.loadID++; old.def = new JobDef { driverClass = typeof(PooledOrdinaryDriver) };
            var d = old.MakeDriver(people[1]); people[1].jobs.curJob = old; people[1].jobs.curDriver = d;
            Require(!SSCUapGroupRuntime.Find(d, out _) && SSCUapGroupRuntime.CanAdvance(d));
        });
        Check("personality replacement invalidates old request before installation", () =>
        {
            var batch = Fixture(true, true);
            SSCPersonalityRuntimeUtility.ObserveHollow(people[1], true);
            Require(!SSCUapGroupRuntime.Launch(batch, out _) && installs == 0);
        });
        Check("cancelled or replaced context cannot produce ordinary outcome", () =>
        {
            var batch = Fixture(true, true); Require(SSCUapGroupRuntime.Launch(batch, out _));
            var d = (JobDriver_Sex)people[1].jobs.curDriver;
            d.Sexprops = Bare<SexProps>(); d.Sexprops.pawn = people[1]; d.Sexprops.partner = people[0]; d.ticks_left = 0;
            Require(SSCRestrictionJobGuard.TryMarkStarted((JobDriver_SexBaseInitiator)d, true));
            Require(SSCUapGroupRuntime.Outcome(d));
            SexUtility.ProcessSex(d.Sexprops); Require(processCalls == 1);
            Require(SSCRestrictionJobGuard.TryClaimOrdinarySexOutcome(d.Sexprops, out _, out _));
            Require(!SSCRestrictionJobGuard.TryClaimOrdinarySexOutcome(d.Sexprops, out _, out _));
            SSCUapGroupRuntime.Close(batch, true); Require(!SSCUapGroupRuntime.Outcome(d));
            SSCUapGroupRuntime.Cleanup(d); Require(!SSCUapGroupRuntime.Outcome(d.Sexprops));
            SexUtility.ProcessSex(d.Sexprops); Require(processCalls == 1);
        });
        // 完成通知保持原 UAP 顺序，集合取消与迟到回调分别验证自己的清理归属。
        Check("normal finalizers preserve once-only group end and old-task cleanup", () =>
        {
            var batch = Fixture(true, true); Require(SSCUapGroupRuntime.Launch(batch, out _));
            foreach (Pawn p in people.Skip(1))
            {
                var driver = (JobDriver_Sex)p.jobs.curDriver;
                driver.Sexprops = Bare<SexProps>(); driver.Sexprops.pawn = p; driver.Sexprops.partner = people[0];
                Require(SSCRestrictionJobGuard.TryMarkStarted((JobDriver_SexBaseInitiator)driver, true));
                SSCUapGroupRuntime.Find(driver, out var work);
                using (new SSCUapGroupRuntime.CallbackScope(work))
                    Call(SSCUapGroupApi.Manager, "NotifyInitiatorFinalized", null, p);
            }
            Require(batch.Closed && !batch.Aborted && ended == 0);
            Require(SSCUapGroupRuntime.Check((JobDriver_Sex)people[1].jobs.curDriver, out bool allowed) && allowed);
        });
        Check("gather cancellation retains replacement UAP gather job", () =>
        {
            var batch = Fixture(true, false);
            var request = Bare(SSCUapGroupApi.Request); SSCUapGroupRuntime.BindRequest(request, batch);
            foreach (Pawn p in people.Skip(1))
            {
                var job = JobMaker.MakeJob(SSCUapGroupApi.PassiveDef, new IntVec3(1, 0, 1));
                p.jobs.curJob = job; p.jobs.curDriver = job.MakeDriver(p);
                SSCUapGroupRuntime.Register(batch, p, job, null, true);
            }
            Job replacement = JobMaker.MakeJob(SSCUapGroupApi.PassiveDef, new IntVec3(2, 0, 2));
            people[1].jobs.curJob = replacement;
            Call(SSCUapGroupApi.Request, "ReleaseGatheringPawns", request);
            Require(people[1].CurJob == replacement && people[2].CurJob == null && ended == 1);
        });
        Check("withdrawn override before launch cancels rather than reauthorizing", () =>
        {
            var batch = Fixture(true, true); SSCMod.settings.allowUapManualGroupRestrictionOverride = false;
            Require(!SSCUapGroupRuntime.Launch(batch, out _) && installs == 0);
        });
        Check("late outcome/finalization/cancellation callbacks cannot affect replacement session", () =>
        {
            var batch = Fixture(true, true); Require(SSCUapGroupRuntime.Launch(batch, out _));
            var slot = batch.Jobs[1];
            var driver = (JobDriver_SexBaseInitiator)slot.Driver;
            driver.Sexprops = Bare<SexProps>(); driver.Sexprops.pawn = slot.Pawn; driver.Sexprops.partner = slot.Partner;
            Require(SSCRestrictionJobGuard.TryMarkStarted(driver, true));
            // 原 Job 尚未清理，新会话先接管索引；旧回调不能向该新会话转交完成或结算。
            var replacement = SSCUapGroupRuntime.Create(anchor, people, match, false);
            Require(SSCUapGroupApi.CreateSession(replacement, out _));
            var index = (IDictionary)AccessTools.Field(SSCUapGroupApi.Manager, "sessionByPawnThingId").GetValue(null);
            Require(slot.Owned && batch.Current && !SSCUapGroupApi.OwnsSession(batch));
            Require(!SSCUapGroupRuntime.Outcome(driver) && !SSCUapGroupRuntime.Outcome(driver.Sexprops));
            SexUtility.ProcessSex(driver.Sexprops); Require(processCalls == 0);
            using (new SSCUapGroupRuntime.CallbackScope(slot))
            {
                Call(SSCUapGroupApi.Manager, "NotifyInitiatorFinalized", null, slot.Pawn);
                Require(SSCUapGroupApi.Get<HashSet<int>>(replacement.Session, "FinalizedInitiatorThingIds").Count == 0);
                Call(SSCUapGroupApi.Manager, "AbortSessionForPawn", null, slot.Pawn, "cancel old");
            }
            Require(batch.Aborted && (int)index[slot.Pawn.thingIDNumber] == replacement.SessionId &&
                SSCUapGroupApi.OwnsSession(replacement));
        });
        // 不解释旧步骤索引续播；此处只验证加载闸门和 Scribe 结束后的精确取消。
        Check("load skips both old/new toil layouts and safely ends orphan after Scribe", () =>
        {
            var batch = Fixture(true, true); Require(SSCUapGroupRuntime.Launch(batch, out _));
            var d = people[1].jobs.curDriver;
            Scribe.mode = LoadSaveMode.PostLoadInit;
            Call(typeof(JobDriver), "SetupToils", d);
            Require(SSCUapGroupRuntime.IsOrphan(d));
            d.ReadyForNextToil(); Require(initialized == 0);
            Scribe.mode = LoadSaveMode.Inactive;
            Current.Game.GetComponent<SSCUapGroupGameComponent>().GameComponentUpdate();
            Require(people[1].CurJob == null);
        });
        // 同一进程同时安装两套真实观察补丁，验证本次生命周期接入没有使原 UAP 计时来源失效。
        Check("group adapter and existing UAP timing observers coexist", () =>
        {
            _ = typeof(Rimworld_Animations.HarmonyPatch_SexTick).Assembly;
            SSCOrgasmProgressPatches.Install();
            Require(SSCOrgasmProgressPatches.IsReady(SSCOrgasmAdvanceSource.UapInitiator), SSCOrgasmProgressPatches.Describe());
            Require(SSCOrgasmProgressPatches.IsReady(SSCOrgasmAdvanceSource.UapReceiver), SSCOrgasmProgressPatches.Describe());
        });
        Console.WriteLine($"RESULT: {passed}/{passed} passed"); return 0;
    }

    /// <summary>建立每例独立游戏、三／四人星形计划和拒绝配置；GUI 变体通过原窗口下令方法取得手动来源。</summary>
    private static SSCUapGroupRuntime.Batch Fixture(bool setting, bool gui, int count = 3, bool bed = false)
    {
        // 每例独立根对象与组件列表，不使用上例的会话索引或任务编号来推断当前权限。
        Scribe.mode = LoadSaveMode.Inactive;
        var game = Bare<Game>(); game.tickManager = new TickManager(); game.uniqueIDsManager = new UniqueIDsManager();
        Set(game, "components", new List<GameComponent>()); Current.Game = game;
        ((List<GameComponent>)Get(game, "components")).Add(new SSCUapGroupGameComponent(game));
        map = Bare<Map>(); Set(map, "components", new List<MapComponent>());
        anchor = bed ? new Building_Bed() : new Thing();
        anchor.def = new ThingDef { defName = "Spot", label = "spot", useHitPoints = false }; anchor.thingIDNumber = ++nextId;
        people = Enumerable.Range(0, count).Select(i =>
        {
            var p = new Pawn { def = new ThingDef { defName = "Human", race = new RaceProperties { intelligence = Intelligence.Humanlike } },
                Name = new NameSingle("P" + i), thingIDNumber = ++nextId };
            p.jobs = new Pawn_JobTracker(p); p.health = new Pawn_HealthTracker(p);
            var comp = new CompSexSlaveTraining { parent = p, restrictionConfig = new SSCRestrictionConfig {
                rules = new SSCRestrictionRules { consensualInitiation = SSCRestrictionValue.Deny, receiveConsensual = true } } };
            Set(p, "comps", new List<ThingComp> { comp }); return p;
        }).ToArray();
        // 直接提供已绑定与主动拒绝的初始数据，避免在此夹具里执行完整建绑或身份流程。
        foreach (Pawn p in people.Skip(1))
            p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave, pawn = p, target = people[0] });
        // 计划对象仍使用真实 UAP 类型，只固定匹配结果，不复制其匹配器的业务算法。
        var asm = SSCUapGroupApi.Dialog.Assembly;
        match = Activator.CreateInstance(asm.GetType("UAP_Animations.UAPGroupAnimationMatch")); Set(match, "DefName", "Fixture");
        plan = Activator.CreateInstance(asm.GetType("UAP_Animations.UAPGroupLaunchPlan"));
        Set(plan, "OrderedParticipants", people.ToList()); Set(plan, "Receiver", people[0]); Set(plan, "PrimaryInitiator", people[1]);
        foreach (Pawn p in people.Skip(1))
        {
            var pair = Activator.CreateInstance(asm.GetType("UAP_Animations.UAPGroupMechanicalPairPlan"));
            Set(pair, "Initiator", p); Set(pair, "Receiver", people[0]); ((IList)Get(plan, "MechanicalPairs")).Add(pair);
        }
        SSCMod.settings.allowUapManualGroupRestrictionOverride = setting;
        var component = (MapComponent)Activator.CreateInstance(SSCUapGroupApi.Component, map);
        ((List<MapComponent>)Get(map, "components")).Add(component);
        installs = failInstall = ended = initialized = processCalls = 0;
        startBodies = otherStartPostfixes = automaticSelections = namedAnimationStarts = 0;
        beginResult = false; beginReason = null; logs.Clear();
        if (!gui) return SSCUapGroupRuntime.Create(anchor, people, match, false);
        // 手动来源须经真实 TryInitiate -> TryBeginRequest 路径，不能在测试中直接把 Manual 改为 true。
        var dialog = Bare(SSCUapGroupApi.Dialog);
        Set(dialog, "anchor", anchor); Set(dialog, "readyPawns", people.ToList()); Set(dialog, "selectedAnimationDefName", "Fixture");
        var matches = (IList)Activator.CreateInstance(Get(dialog, "matchingAnimations")?.GetType() ??
            typeof(List<>).MakeGenericType(match.GetType())); matches.Add(match); Set(dialog, "matchingAnimations", matches);
        Require(anchor.Spawned && anchor.Map == map, "anchor map boundary");
        Require(map.GetComponent(SSCUapGroupApi.Component) == component, "map component boundary");
        Require(((List<Pawn>)Get(dialog, "readyPawns")).Count == 3 && ((IList)Get(dialog, "matchingAnimations")).Count == 1, "dialog boundary");
        Call(SSCUapGroupApi.Dialog, "TryInitiate", dialog);
        return Current.Game.GetComponent<SSCUapGroupGameComponent>().Batches.Single();
    }
    /// <summary>在接入安装前预热真实 RAF 后置调用；空动画库只做自动选择，不进入图形服务。</summary>
    private static void WarmAutomaticStart()
    {
        Fixture(false, false);
        Job receiverJob = JobMaker.MakeJob(SSCUapGroupApi.ReceiverDef, people[1]);
        var receiver = (JobDriver_SexBaseReciever)receiverJob.MakeDriver(people[0]);
        people[0].jobs.curJob = receiverJob; people[0].jobs.curDriver = receiver; receiver.parteners.Add(people[1]);
        Job job = JobMaker.MakeJob(SSCUapGroupApi.InitiatorDef, people[0]);
        var driver = (JobDriver_SexBaseInitiator)job.MakeDriver(people[1]);
        people[1].jobs.curJob = job; people[1].jobs.curDriver = driver;
        driver.Start();
        Require(startBodies == 1 && otherStartPostfixes == 1 && automaticSelections == 1);
    }
    /// <summary>跳过与本专项无关的 DefOf、窗口、渲染或身体服务，不在夹具里重写这些系统的算法。</summary>
    private static bool Skip() => false;
    /// <summary>收集实际代码发出的诊断供失败定位，隔离完整游戏的日志投递与界面服务。</summary>
    private static bool Log(string __0) { logs.Add(__0); return false; }
    /// <summary>为没有真实地图生成流程的对象提供已生成状态，不把此边界当作原版生成机制的验证。</summary>
    private static bool Spawned(ref bool __result) { __result = true; return false; }
    /// <summary>统一提供本例地图，以便实际请求和会话执行对象归属检查。</summary>
    private static bool Map(ref Map __result) { __result = map; return false; }
    /// <summary>为正常场景隔离死亡和征召查询；人物替换、取消等测试由明确操作触发。</summary>
    private static bool False(ref bool __result) { __result = false; return false; }
    /// <summary>只支撑原 UAP 提示文本中的名称生成，避免为显示标签加载整套原版 StatDef。</summary>
    private static bool HitPoints(ref int __result) { __result = 100; return false; }
    /// <summary>隔离真实地图的预约分配；原发起驱动预约方法和 SSC 许可前缀仍执行。</summary>
    private static bool Reserve(ref bool __result) { __result = true; return false; }
    /// <summary>计数能到达的 RJW 结算入口并跳过身体效果；生产前缀拒绝旧场时不能抵达此边界。</summary>
    private static bool ProcessBoundary() { processCalls++; return false; }
    /// <summary>只观察真实窗口请求返回给调用者的结果与原因，不改写任何 out 参数。</summary>
    private static void ObserveBegin(bool __result, string failureReason) { beginResult = __result; beginReason = failureReason; }
    /// <summary>隔离 RJW 身体初始化，但仍执行真实 Start 的全部 Harmony 后置。</summary>
    private static IEnumerable<CodeInstruction> StartBodyBoundary(IEnumerable<CodeInstruction> original)
    {
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(Program), nameof(RecordStartBody)));
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ret);
    }
    /// <summary>记录 Start 身体边界替身已执行，不能把跳过整个 Start 当作修复。</summary>
    private static void RecordStartBody() => startBodies++;
    /// <summary>代表其他后置回调，确认只抑制重复动画选择而非整个 RJW 后置链。</summary>
    private static void OtherStartPostfix() => otherStartPostfixes++;
    /// <summary>只观察实际自动匹配调用，不改写其返回值或过滤器。</summary>
    private static void ObserveAutomaticSelection() => automaticSelections++;
    /// <summary>隔离最终渲染，原 UAP 命名启动及参数选择仍执行。</summary>
    private static bool NamedAnimationBoundary(List<Pawn> __0, Rimworld_Animations.GroupAnimationDef __1, Thing __2)
    {
        Require(__0.SequenceEqual(people) && __1.defName == "Fixture" && __2 == anchor);
        namedAnimationStarts++; return false;
    }
    /// <summary>隔离匹配算法，方法体沿真实 out 引用写回；不能用 Prefix 改 __args 掩盖 Postfix 读取旧快照的问题。</summary>
    private static IEnumerable<CodeInstruction> Match(IEnumerable<CodeInstruction> original)
    {
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_2);
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldsfld, AccessTools.Field(typeof(Program), nameof(plan)));
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Castclass,
            SSCUapGroupApi.Dialog.Assembly.GetType("UAP_Animations.UAPGroupLaunchPlan", true));
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Stind_Ref);
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_3);
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldnull);
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Stind_Ref);
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_I4_1);
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ret);
    }
    /// <summary>隔离集合寻路，只返回请求派发成功；请求对象绑定和后续真实启动接入仍由生产补丁处理。</summary>
    private static bool Gather(ref bool __result) { __result = true; return false; }
    // 只隔离地图任务调度；实际 Job.MakeDriver、预约守卫、UAP MakeNewToils/SetupToils 都执行。
    /// <summary>隔离地图调度，仍执行实际 MakeDriver、预约守卫和步骤构建，并模拟进入第一个等待步骤。</summary>
    private static bool InstallJob(Pawn_JobTracker __instance, Job newJob)
    {
        // 注入失败发生在指定安装调用；前一人的任务此时仍应停在等待步骤，尚未执行真正初始化。
        installs++; if (installs == failInstall) return false;
        __instance.curJob = newJob; var pawn = (Pawn)Get(__instance, "pawn");
        var driver = newJob.MakeDriver(pawn); __instance.curDriver = driver;
        if (!driver.TryMakePreToilReservations(false)) { __instance.curJob = null; __instance.curDriver = null; return false; }
        // 保留真实 UAP 序列枚举及生产包装，第一步骤的初始化和 tick 都必须受提交状态约束。
        Call(typeof(JobDriver), "SetupToils", driver);
        Require(((List<Toil>)Get(driver, "toils")).Count > 1, string.Join("\n", logs));
        ((List<Toil>)Get(driver, "toils"))[0].initAction?.Invoke();
        Set(driver, "curToilIndex", 0);
        ((List<Toil>)Get(driver, "toils"))[0].tickAction?.Invoke();
        return false;
    }
    /// <summary>记录并清除当前任务以模拟引擎结束边界，不执行完整地图收尾或生成后续 AI 工作。</summary>
    private static bool EndJob(Pawn_JobTracker __instance)
    { ended++; __instance.curJob = null; __instance.curDriver = null; return false; }
    /// <summary>只执行本例指定的下一步骤初始化动作，用于观察提交屏障放行；不模拟完整引擎调度。</summary>
    private static bool Advance(JobDriver __instance)
    { ((List<Toil>)Get(__instance, "toils"))[1].initAction?.Invoke(); return false; }
    /// <summary>遵守专项范围选择，记录真实用例结果；失败时附原诊断，并恢复加载模式以免污染下一例。</summary>
    private static void Check(string name, Action body)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        try { body(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception e) { throw new Exception("FAIL: " + name + "\n" + string.Join("\n", logs), e); }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
    }
    /// <summary>对独立行为契约作最小断言，保留失败场景名称及调用栈。</summary>
    private static void Require(bool value, string reason = "assertion failed") { if (!value) throw new Exception(reason); }

    /// <summary>仅提供池化复用后的普通任务类型，核对兼容钩子不接管无关任务。</summary>
    private sealed class PooledOrdinaryDriver : JobDriver
    {
        /// <summary>不添加预约条件，只验证全局兼容钩子是否错误阻止新的普通任务。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        /// <summary>池化归属用例不执行实际任务，无需构造行为步骤。</summary>
        protected override IEnumerable<Toil> MakeNewToils() { yield break; }
    }
}
