// AI 辅助生成：原生类型与真实生产代码；图形、完整健康结算及界面显示不属于此宿主。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static class Entry
{
    /// <summary>先注册实际游戏与依赖解析，再进入含游戏类型的宿主，避免 JIT 提前绑定错误程序集。</summary>
    private static int Main(string[] args)
    {
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        using var info = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "1.7/BUILD-INFO.json")));
        var dirs = new[] { game }.Concat(info.RootElement.GetProperty("references").EnumerateArray()
            .Select(x => Path.GetDirectoryName(x.GetProperty("path").GetString()))).ToArray();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string dir in dirs) { string path = Path.Combine(dir, name.Name + ".dll"); if (File.Exists(path)) return context.LoadFromAssemblyPath(path); }
            return null;
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { repo, game });
    }
}
internal static partial class Program
{
    private static int passed, failed, id = 2000000;
    private static bool bootstrap = true;
    private static string saveDirectory;
    private static ThingDef human;
    private static Faction player;
    private static readonly List<string> errors = new();
    private static readonly Assembly production = typeof(SSCRomanceConfig).Assembly;
    /// <summary>建立独立原生宿主，执行生产规则及真实存档测试；退出时卸载测试补丁并释放游戏根。</summary>
    private static int Run(string repo, string game)
    {
        Console.WriteLine($"Game: {File.ReadAllText(Path.Combine(game, "Version.txt")).Trim()}, MVID: {typeof(Pawn).Module.ModuleVersionId:N}");
        Console.WriteLine($"Production: {production.Location}, SHA256: {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(production.Location))).ToLowerInvariant()}");
        var harmony = new Harmony("ssc.tests.romance-control.native");
        void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
        saveDirectory = Path.Combine(repo, ".builds/romance-batch1/userdata");
        Directory.CreateDirectory(Path.Combine(saveDirectory, "Config"));
        File.WriteAllText(Path.Combine(saveDirectory, "Config/ModsConfig.xml"), "<ModsConfigData><version>1.7.4985</version><activeMods/><knownExpansions/></ModsConfigData>");
        Patch(AccessTools.PropertyGetter(typeof(GenFilePaths), nameof(GenFilePaths.SaveDataFolderPath)), nameof(SaveDirectory));
        Patch(AccessTools.Method(typeof(ModLister), "RebuildModList"), nameof(Skip));
        Patch(AccessTools.Method(typeof(ContentFinder<UnityEngine.Texture2D>), "Get"), nameof(NoTexture));
        Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CheckForStateChange)), nameof(Skip));
        Patch(AccessTools.Method(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.Notify_CapacityLevelsDirty)), nameof(Skip));
        Patch(AccessTools.PropertyGetter(typeof(Faction), nameof(Faction.OfPlayer)), nameof(PlayerFaction));
        Patch(AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly)), nameof(ScribeTypeBoundary));
        foreach (string name in new[] { "Error", "Warning", "Message" })
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == name && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                Patch(method, nameof(LogBoundary));
        try
        {
            AccessTools.Field(typeof(UnityData), "mainThreadId").SetValue(null, Environment.CurrentManagedThreadId);
            AccessTools.Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
            var root = Bare<Game>(); root.tickManager = new TickManager(); root.uniqueIDsManager = new UniqueIDsManager();
            AccessTools.Field(typeof(Game), "components").SetValue(root, new List<GameComponent>()); Current.Game = root;
            var pack = Bare<ModContentPack>(); pack.assemblies = new ModAssemblyHandler(pack);
            pack.assemblies.loadedAssemblies.Add(production); pack.assemblies.loadedAssemblies.Add(typeof(Program).Assembly);
            ((List<ModContentPack>)AccessTools.Field(typeof(LoadedModManager), "runningMods").GetValue(null)).Add(pack);
            human = Bare<ThingDef>(); human.defName = "RomanceTestHuman"; human.category = ThingCategory.Pawn;
            var torso = new BodyPartDef { defName = "RomanceTestTorso", hitPoints = 40 };
            var body = new BodyDef { defName = "RomanceTestBody", corePart = new BodyPartRecord { def = torso } }; body.ResolveReferences();
            human.race = new RaceProperties { intelligence = Intelligence.Humanlike, body = body };
            DefDatabase<ThingDef>.Add(human);
            SSCDefOf.ChainOfSexSlave = new HediffDef { defName = "Hediff_ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave),
                maxSeverity = 1, stages = new List<HediffStage> { new() { minSeverity = .1f }, new() { minSeverity = .3f }, new() { minSeverity = .5f }, new() { minSeverity = .9f } } };
            DefDatabase<HediffDef>.Add(SSCDefOf.ChainOfSexSlave);
            SSCDefOf.SSC_Hediff_Bus = new HediffDef { defName = "SSC_Hediff_Bus" };
            SSCDefOf.SSC_Hediff_Bus_Final = new HediffDef { defName = "SSC_Hediff_Bus_Final" };
            SSCDefOf.SSC_Hediff_Cow = new HediffDef { defName = "SSC_Hediff_Cow" };
            SSCDefOf.SSC_Hediff_Cow_Final = new HediffDef { defName = "SSC_Hediff_Cow_Final" };
            foreach (string pet in new[] { "PetCat", "PetDog", "PetRabbit" })
            foreach (string suffix in new[] { "", "_Final" }) DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Hediff_" + pet + suffix });
            SSCDefOf.SSC_PersonalityExcreted_Done = new HediffDef { defName = "SSC_PersonalityExcreted_Done" };
            player = Bare<Faction>(); player.def = new FactionDef { defName = "RomanceTestPlayer", isPlayer = true };
            // 使用真实生产健康通知 postfix；只隔离健康通知下游，不替换授权或阶段逻辑。
            harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged)),
                postfix: new HarmonyMethod(typeof(Program), nameof(HealthPostfix)));
            // 与其他原生宿主一致，预热独立 CLR 的类型缓存；其不提供完整游戏 Harmony 3 环境。
            // 后续每例仍将原版 Error 判为失败；Scribe 三个测试类型由上述精确映射解析。
            _ = GenTypes.AllTypes;
            bootstrap = false;
            harmony.CreateClassProcessor(production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodAdmissionTail")).Patch();
            PolicyTests(); PreferenceTests(); LifecycleTests(); SnapshotTests(); SaveTests(); WiringTests(repo);
            InteractionTests(harmony, repo, game);
            CommandTests(harmony, repo);
            Batch4LifecycleTests(harmony);
            Batch4PersonalityTests();
            Batch4CloneTests();
            Batch4ReviewTests();
            PassiveFeedbackTests(harmony, repo);
            TargetMenuTests();
            FeedbackReviewTests();
            OtherAcceptanceTests();
            RomanceCompositeFoundationTests(repo);
            RomanceCompositeReviewTests();
            RomanceCompositeGroupTests();
            RomanceSemanticGroupTests();
            RomanceCompositeHistoryTests();
            RomanceCompositeBatch2ReviewTests();
            RomanceCompositeEventTests(harmony, repo);
            RomanceSemanticInteractionTests();
            RomanceSemanticFormerTests();
            RomanceCompositeBatch4Tests(harmony, repo);
            RomanceSocialBatch1Tests(harmony, repo);
            RomanceSocialReviewTests();
            RomanceSocialBatch2Tests(harmony, repo, game);
        }
        finally { harmony.UnpatchAll(harmony.Id); Scribe.mode = LoadSaveMode.Inactive; Current.Game = null; }
        Console.WriteLine($"RESULT: {passed}/{passed + failed} passed"); return failed == 0 ? 0 : 1;
    }
    /// <summary>仅为宿主搭建绕过游戏构造流程，所需字段由各夹具显式提供。</summary>
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    /// <summary>读取真实训练组件，不复制生产配置的保存或判定逻辑。</summary>
    private static CompSexSlaveTraining Comp(Pawn pawn) => pawn.TryGetComp<CompSexSlaveTraining>();
    /// <summary>创建独立 ID、健康追踪器和真实训练组件；玩家归属仅供提交资格测试。</summary>
    private static Pawn Pawn(PawnIdentity identity = PawnIdentity.Unset)
    {
        var pawn = new Pawn { def = human, thingIDNumber = ++id, Name = new NameSingle("RomanceTest" + id) };
        pawn.health = new Pawn_HealthTracker(pawn); pawn.needs = new Pawn_NeedsTracker(pawn);
        AccessTools.Field(typeof(Thing), "factionInt").SetValue(pawn, player);
        AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(pawn, new List<ThingComp> { new CompSexSlaveTraining { parent = pawn, pawnIdentity = identity } });
        return pawn;
    }
    /// <summary>建立指定阶段的真实从属锁链并初始化观察基线，不模拟完整绑定仪式。</summary>
    private static Pawn Slave(float severity = .9f, Pawn master = null)
    {
        var pawn = Pawn(PawnIdentity.Slave); master ??= Pawn(PawnIdentity.Master);
        var chain = new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave, pawn = pawn, target = master, loadID = ++id };
        chain.Severity = severity; pawn.health.hediffSet.hediffs.Add(chain); SSCRomanceLifecycle.Notify(pawn); return pawn;
    }
    /// <summary>构造行为禁止、仅主人目标集合及主人必须接受的固定来源，检查显式值和副本保留。</summary>
    private static SSCRomanceConfig Denied() => new() { allowInitiateRomance = false, allowInitiateProposal = false, allowInitiateBreakup = false,
        allowAcceptRomance = false, allowAcceptProposal = false, allowInitiateToMaster = true, allowInitiateToSexSlave = false, allowInitiateToOthers = false, alwaysAcceptMaster = true };
    /// <summary>直接调用生产双边策略，测试期望值由用例给定而不在宿主重写策略。</summary>
    private static SSCRomanceDecision Decide(Pawn actor, Pawn recipient, SSCRomanceAction action = SSCRomanceAction.Romance)
        => SSCRomancePolicy.Evaluate(actor, recipient, action);
    /// <summary>隔离每例错误并汇总结果；异常时结束 Scribe，避免一个坏存档影响后续用例。</summary>
    private static void Check(string name, Action action)
    {
        errors.Clear();
        try { action(); Require(errors.Count == 0, string.Join('\n', errors)); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex + "\n" + string.Join('\n', errors)); Scribe.loader.ForceStop(); Scribe.mode = LoadSaveMode.Inactive; }
    }
    /// <summary>断言业务条件，不成立时保留具体原因供回归报告定位。</summary>
    private static void Require(bool value, string reason = "assertion failed") { if (!value) throw new Exception(reason); }
    /// <summary>比较精确值或引用，避免独立副本及授权归属问题被近似比较掩盖。</summary>
    private static void Equal<T>(T expected, T actual) => Require(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, actual {actual}");
    /// <summary>仅跳过明确隔离的完整游戏下游，不替代被测浪漫业务。</summary>
    private static bool Skip() => false;
    /// <summary>原生宿主没有图形设备，不加载纹理；本套件不据此声明视觉验证。</summary>
    private static bool NoTexture(ref UnityEngine.Texture2D __result) { __result = null; return false; }
    /// <summary>将配置与测试存档定位到工作区隔离目录，避免读写玩家真实文件。</summary>
    private static bool SaveDirectory(ref string __result) { __result = saveDirectory; return false; }
    /// <summary>提供测试玩家阵营根，不改变生产规则对 Pawn 当前阵营的读取。</summary>
    private static bool PlayerFaction(ref Faction __result) { __result = player; return false; }
    /// <summary>仅精确映射三个被测存档类型，后续实例构造、字段读写及引用恢复仍走原生 Scribe。</summary>
    private static bool ScribeTypeBoundary(string typeName, ref Type __result)
    {
        foreach (var type in new[] { typeof(RomanceMoodScene), typeof(SSCRomanceInteractionFact), typeof(SSCRomanceMoodOrderGameComponent), typeof(SSCRomanceMoodData), typeof(StoredMemoryData), typeof(Thought_MemorySSCRomance), typeof(PlayLogEntry_SSCRomance), typeof(Batch4Scene), typeof(Hediff_ChainOfSexSlave), typeof(Hediff_BridleOfSexSlave), typeof(RomanceFixture), typeof(SSCRomanceConfig), typeof(SSCRomanceCommandStamp), typeof(SSCRomanceCommandRecord), typeof(RomanceCommandFixture), typeof(RomanceTrackerFixture), typeof(JobDriver_SSCRomanceCommand), typeof(Verse.AI.Job), typeof(Verse.AI.JobQueue) })
            if (typeName == type.FullName || typeName == type.Name) { __result = type; return false; }
        return true;
    }
    // 适配器仅跨测试 Harmony 2／生产 Harmony 3 的注解边界；实际 postfix 仍执行生产实现。
    /// <summary>跨测试 Harmony 版本转发真实生产健康通知补丁，不在宿主重做阶段撤权。</summary>
    private static void HealthPostfix(Hediff hediff) => production.GetType("SexSlaveCraft.SSCBondHealthChanged").GetMethod("Postfix").Invoke(null, new object[] { hediff });
    /// <summary>隔离图形日志投递；预热结束后的原版 Error 纳入当前用例失败结果。</summary>
    private static bool LogBoundary(MethodBase __originalMethod, object[] __args)
    {
        if (captureMoodDiagnostics && __originalMethod.Name == "Message" && ((string)__args[0]).StartsWith("[SSC RomanceMood]"))
        { if (throwMoodDiagnostic) throw new InvalidOperationException("diagnostic sink fault"); moodDiagnostics.Add((string)__args[0]); moodDiagnosticEvidence.Add((string)__args[0]); }
        if (!bootstrap && __originalMethod.Name == "Error") errors.Add((string)__args[0]); return false;
    }
}
