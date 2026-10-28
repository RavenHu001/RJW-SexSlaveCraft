// AI 辅助生成：真实 SSC/RJW/Harmony；隔离地图、身体数值输入和显示终端，不启动游戏。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using rjw.Modules.Attraction;
using SexSlaveCraft;
using Verse;

internal static class Entry
{
    /// <summary>先根据真实构建记录定位依赖，再进入含游戏类型的宿主，避免入口提前解析失败。</summary>
    private static int Main(string[] args)
    {
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        string infoPath = Environment.GetEnvironmentVariable("SSC_TEST_BUILD_INFO_PATH") ?? Path.Combine(repo, "1.7/BUILD-INFO.json");
        using var info = JsonDocument.Parse(File.ReadAllText(infoPath));
        string[] dirs = info.RootElement.GetProperty("references").EnumerateArray()
            .Select(r => Path.GetDirectoryName(r.GetProperty("path").GetString())).Prepend(game).ToArray();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = dirs.Select(d => Path.Combine(d, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : context.LoadFromAssemblyPath(path);
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { repo, game });
    }
}

internal static class Program
{
    private static Harmony environment;
    private static ThingDef human;
    private static int passed, failed, id = 100000, reserved;
    private static bool bootstrap = true;
    private static readonly List<string> errors = new();
    private static readonly List<string> installationMessages = new();
    private static readonly Dictionary<Pawn, float[]> factors = new();
    private static readonly Dictionary<Pawn, int> opinions = new();
    private static readonly Dictionary<Pawn, float> romance = new();
    private static Action<AttractionMode> evaluating;
    private static Action atPath;
    private static Action atReservations;
    private static Action<Pawn> atOpinion;
    private static List<Pawn> candidates = new();
    private static string explanation;
    private static readonly WindowStack windows = Bare<WindowStack>();
    private static Type productionPatches, productionUtility;
    private static readonly AttractionMode[] complete = { AttractionMode.Romantic, AttractionMode.Physical, AttractionMode.Social, AttractionMode.Age };

    /// <summary>为测试创建真实对象；只填写引擎环境字段，业务仍由实际程序集执行。</summary>
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Set(object obj, string field, object value) => AccessTools.Field(obj.GetType(), field).SetValue(obj, value);
    private static void Patch(MethodBase method, string prefix) => environment.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));

    /// <summary>安装外部环境输入，再装真实生产补丁；本套不加载正式 ModStartup 或无关模块。</summary>
    internal static int Run(string repo, string game)
    {
        SscTestProfile.Start();
        if (!SscTestProfile.IncludesSuite) return 0;
        environment = new Harmony("ssc.tests.master-attraction.environment");
        foreach (string name in new[] { "Message", "Warning", "Error" })
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == name && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                Patch(method, nameof(Log));
        Patch(AccessTools.Method(typeof(DefOfHelper), "EnsureInitializedInCtor"), nameof(Skip));
        Patch(AccessTools.Method(typeof(ContentFinder<UnityEngine.Texture2D>), "Get"), nameof(NoTexture));
        Patch(AccessTools.PropertyGetter(typeof(Thing), "Suspended"), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(Pawn), "Suspended"), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(Find), "WindowStack"), nameof(Windows));
        Patch(AccessTools.Method(typeof(WindowStack), "Add"), nameof(CaptureWindow));
        // 健康、分类和身体数值为本套外部输入，Preflight 主体与模式/总分算法均不替换。
        Patch(AccessTools.Method(typeof(xxx), "is_healthy_enough"), nameof(True));
        Patch(AccessTools.Method(typeof(xxx), "is_starved"), nameof(False));
        Patch(AccessTools.Method(typeof(xxx), "is_not_dying"), nameof(True));
        Patch(AccessTools.Method(typeof(xxx), "is_nympho"), nameof(False));
        Patch(AccessTools.Method(typeof(xxx), "is_psychopath"), nameof(False));
        Patch(AccessTools.Method(typeof(xxx), "is_rapist"), nameof(False));
        Patch(AccessTools.Method(typeof(xxx), "is_bloodlust"), nameof(False));
        Patch(AccessTools.Method(typeof(PawnDesignations_Hero), "IsDesignatedHero"), nameof(False));
        Patch(AccessTools.Method(typeof(PawnDesignations_Hero), "IsHeroOwner"), nameof(False));
        Patch(AccessTools.Method(typeof(AttractionUtility), "ReservedCount"), nameof(Reservations));
        // 固定原生 Preference 输入，仍执行真实三阶段评价、零分短路、Preflight和Finalize。
        Patch(AccessTools.Method(typeof(AttractionUtility), "SetupRequest"), nameof(Preferences));
        // 仅提供姓名显示输入，保留原Explain全部分项、合成与窗口构造。
        Patch(AccessTools.Method(typeof(AttractionUtility), "GetPawnLabel"), nameof(PawnLabel));
        Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), "OpinionOf"), nameof(Opinion));
        Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), "SecondaryRomanceChanceFactor"), nameof(Romance));
        Patch(AccessTools.Method(typeof(AttractionUtility), "CanFoolAround", new[] { typeof(Pawn), typeof(bool) }), nameof(False));
        Patch(AccessTools.Method(typeof(CasualSex_Helper), "PotentialPartnersFor"), nameof(Candidates));
        Patch(AccessTools.Method(typeof(CasualSex_Helper).GetNestedType("RNG"), "CanPawnHookup"), nameof(True));
        Patch(AccessTools.Method(typeof(CasualSex_Helper).GetNestedType("RNG"), "FindQuickiePartner"), nameof(ChooseCandidates));
        Patch(AccessTools.Method(typeof(Pather_Utility), "can_path_to_target"), nameof(PathBoundary));
        SSCMod.settings = new SSCSettings();
        AccessTools.Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
        var gameState = Bare<Game>(); gameState.tickManager = new TickManager(); gameState.uniqueIDsManager = new UniqueIDsManager();
        Set(gameState.tickManager, "gameStartAbsTick", 3600000);
        Set(gameState, "components", new List<GameComponent>()); Current.Game = gameState;
        AccessTools.Field(typeof(UnityData), "mainThreadId").SetValue(null, Environment.CurrentManagedThreadId);
        SSCDefOf.ChainOfSexSlave = new HediffDef { defName = "ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave) };
        SSCDefOf.SSC_PersonalityExcreted_Done = new HediffDef { defName = "SSC_PersonalityExcreted_Done" };
        human = Bare<ThingDef>(); human.defName = "MasterAttractionTestHuman"; human.category = ThingCategory.Pawn;
        human.race = new RaceProperties { intelligence = Intelligence.Humanlike,
            lifeStageAges = new() { new LifeStageAge { minAge = 0, def = new LifeStageDef { defName = "Adult", developmentalStage = DevelopmentalStage.Adult } } } };
        var corpses = Bare<ThingCategoryDef>(); corpses.defName = "CorpsesHumanlike";
        Set(human.race, "fleshType", new FleshTypeDef { defName = "Normal", corpseCategory = corpses });
        LoadLanguage(repo);
        FactionDefOf.PlayerColony ??= new FactionDef { defName = "AttractionPlayerColony", pawnSingular = "colonist", pawnsPlural = "colonists" };
        ColoredText.ResetStaticData();
        // 首次RJW/Need静态预热会请求未参与本套的Def和纹理；在环境预热期隔离，不把诊断移到机制断言中。
        RuntimeHelpers.RunClassConstructor(typeof(xxx).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Need).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Need_Sex).TypeHandle);
        productionPatches = typeof(SSCBondUtility).Assembly.GetType("SexSlaveCraft.SSCMasterAttractionPatches", true);
        productionUtility = typeof(SSCBondUtility).Assembly.GetType("SexSlaveCraft.SSCMasterAttractionUtility", true);
        foreach (var assembly in new[] { typeof(Pawn).Assembly, typeof(xxx).Assembly, typeof(SSCBondUtility).Assembly })
            Console.WriteLine($"ASSEMBLY: {assembly.Location} MVID={assembly.ManifestModule.ModuleVersionId:N} SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant()}");
        CallProduction(productionPatches, "Install");
        bootstrap = false; errors.Clear();
        try
        {
            Checks();
        }
        finally { environment.UnpatchAll(environment.Id); Current.Game = null; }
        Console.WriteLine($"RESULT: {passed}/{passed + failed} passed");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>真实成年角色、训练Comp、锁链和健康列表，不用生产资格替身授予关系。</summary>
    private static Pawn Pawn(PawnIdentity identity)
    {
        var pawn = new Pawn { def = human, thingIDNumber = ++id, Name = new NameSingle("Attraction" + id), gender = Gender.Male };
        pawn.health = new Pawn_HealthTracker(pawn); pawn.needs = new Pawn_NeedsTracker(pawn);
        pawn.relations = new Pawn_RelationsTracker(pawn); pawn.story = new Pawn_StoryTracker(pawn);
        pawn.ageTracker = new Pawn_AgeTracker(pawn); Set(pawn.ageTracker, "cachedLifeStageIndex", 0); Set(pawn.ageTracker, "growth", 1f);
        pawn.ageTracker.AgeBiologicalTicks = 30L * GenDate.TicksPerYear;
        Set(pawn, "comps", new List<ThingComp> { new CompSexSlaveTraining { parent = pawn, pawnIdentity = identity } });
        factors[pawn] = new[] { 1f, 1f, 1f, 1f }; opinions[pawn] = 50; romance[pawn] = 1;
        return pawn;
    }

    private static (Pawn slave, Pawn master, Hediff_ChainOfSexSlave chain) Pair()
    {
        var slave = Pawn(PawnIdentity.Slave); var master = Pawn(PawnIdentity.Master);
        var chain = new Hediff_ChainOfSexSlave { pawn = slave, def = SSCDefOf.ChainOfSexSlave, target = master };
        slave.health.hediffSet.hediffs.Add(chain);
        return (slave, master, chain);
    }

    private static float Evaluate(Pawn observer, Pawn target, AttractionPurpose purpose = AttractionPurpose.ForFucking, AttractionMode[] modes = null, bool finalize = true)
    {
        var request = new AttractionRequest(purpose, observer, target) { finalize = finalize };
        return AttractionUtility.Evaluate(ref request, modes ?? complete);
    }

    private static AppraisalResult Appraisal(Pawn a, Pawn b) => new(a, new AppraisalSettings(AttractionPurpose.ForFucking), b, new AppraisalSettings(AttractionPurpose.ForFucking));

    /// <summary>运行代表性故障边界；每例重置外部输入，避免前例配置污染或借顺序取得通过。</summary>
    private static void Check(string name, Action action)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        factors.Clear(); opinions.Clear(); romance.Clear(); candidates.Clear(); reserved = 0; evaluating = null; atPath = null; atReservations = null; atOpinion = null; errors.Clear();
        RJWSettings.WildMode = false; RJWSettings.HippieMode = false;
        RJWPreferenceSettings.Malesex = RJWPreferenceSettings.AllowedSex.All;
        RJWPreferenceSettings.FeMalesex = RJWPreferenceSettings.AllowedSex.All;
        RJWHookupSettings.MinimumFuckabilityToHookup = .1f; RJWHookupSettings.MinimumRelationshipToHookup = 0; RJWHookupSettings.MinimumAttractivenessToHookup = .1f;
        try { action(); Require(errors.Count == 0, string.Join("\n", errors)); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }

    private static void Checks()
    {
        Check("all native production entry patches install together", () =>
            Require((bool)AccessTools.Property(productionPatches, "Ready").GetValue(null), string.Join("\n", installationMessages)));
        Check("native zero-mode short circuit reaches owner floor", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = 0;
            Near(1, Evaluate(p.slave, p.master, finalize: false));
        });
        Check("aggregate above floor keeps original cross-mode value", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = .2f; factors[p.slave][(int)AttractionMode.Physical] = 10;
            Near(2, Evaluate(p.slave, p.master, finalize: false));
        });
        Check("reverse direction and unrelated master retain zero", () =>
        {
            var p = Pair(); var other = Pawn(PawnIdentity.Master);
            factors[p.master][(int)AttractionMode.Romantic] = 0; factors[p.slave][(int)AttractionMode.Romantic] = 0;
            Near(0, Evaluate(p.master, p.slave)); Near(0, Evaluate(p.slave, other));
        });
        Check("non-consensual purpose and partial score are unchanged", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = 0;
            Near(0, Evaluate(p.slave, p.master, AttractionPurpose.General));
            Near(0, Evaluate(p.slave, p.master, AttractionPurpose.ForRape));
            Near(0, Evaluate(p.slave, p.master, modes: new[] { AttractionMode.Romantic }));
            factors[p.slave][(int)AttractionMode.Romantic] = 1; opinions[p.slave] = -100; romance[p.slave] = 0;
            var general = new AppraisalResult(p.master, new AppraisalSettings(AttractionPurpose.ForFucking),
                p.slave, new AppraisalSettings(AttractionPurpose.General));
            Require(!CasualSex_Helper.CasualHookupAllowedViaSettings(general));
        });
        Check("native global gender preflight rejects before floor", () =>
        {
            var p = Pair(); p.master.gender = Gender.Female; RJWPreferenceSettings.Malesex = RJWPreferenceSettings.AllowedSex.Homo;
            Near(0, Evaluate(p.slave, p.master));
        });
        Check("native finalize preserves desire gain and full reservation zero", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = 0;
            var need = Bare<Need_Sex>(); Set(need, "pawn", p.slave); need.def = new NeedDef { defName = "Sex", baseLevel = 0 }; Set(need, "curLevelInt", 0f);
            ((List<Need>)AccessTools.Field(typeof(Pawn_NeedsTracker), "needs").GetValue(p.slave.needs)).Add(need);
            Near(1.6f, Evaluate(p.slave, p.master)); reserved = xxx.max_rapists_per_prisoner;
            Near(0, Evaluate(p.slave, p.master));
        });
        Check("rebind during scoring cannot grant old owner floor", () =>
        {
            var p = Pair(); var next = Pawn(PawnIdentity.Master); factors[p.slave][(int)AttractionMode.Romantic] = 0;
            evaluating = mode => { if (mode == AttractionMode.Social) p.chain.target = next; };
            Near(0, Evaluate(p.slave, p.master)); evaluating = null;
            Near(1, Evaluate(p.slave, next, finalize: false));
        });
        Check("native finalization callback revokes a floor already applied", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = .01f;
            atReservations = () => p.slave.health.hediffSet.hediffs.Remove(p.chain);
            Near(0, Evaluate(p.slave, p.master));
        });
        Check("unbound and hollow body lose relationship benefit immediately", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = 0;
            p.slave.health.hediffSet.hediffs.Remove(p.chain); Near(0, Evaluate(p.slave, p.master));
            p.slave.health.hediffSet.hediffs.Add(p.chain);
            p.slave.health.hediffSet.hediffs.Add(new Hediff { pawn = p.slave, def = SSCDefOf.SSC_PersonalityExcreted_Done });
            Near(0, Evaluate(p.slave, p.master));
        });
        HookupChecks();
    }

    /// <summary>直接执行RJW的双向筛选与局部设置消费者，不用返回值替身绕过主人侧拒绝。</summary>
    private static void HookupChecks()
    {
        Check("same native appraisal revokes old owner cache after rebind and unbind", () =>
        {
            var p = Pair(); var next = Pawn(PawnIdentity.Master); factors[p.slave][(int)AttractionMode.Romantic] = .01f;
            var old = Appraisal(p.slave, p.master); Near(1.1f, old.ObserverAttraction);
            p.chain.target = next; Near(.011f, old.ObserverAttraction);
            var current = Appraisal(p.slave, next); Near(1.1f, current.ObserverAttraction);
            p.slave.health.hediffSet.hediffs.Remove(p.chain); Near(.011f, current.ObserverAttraction);
            p.slave.health.hediffSet.hediffs.Add(p.chain); Near(1.1f, current.ObserverAttraction);
            p.slave.health.hediffSet.hediffs.Remove(p.chain);
            factors[p.slave][(int)AttractionMode.Romantic] = 2f;
            var nativeHigh = Appraisal(p.slave, p.master); Near(2.2f, nativeHigh.ObserverAttraction);
            p.slave.health.hediffSet.hediffs.Add(p.chain); p.chain.target = p.master; Near(2.2f, nativeHigh.ObserverAttraction);
        });
        Check("native best-result selection keeps master attraction refusal", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = 0;
            Require(SexAppraiser.FindBestResults(new[] { Appraisal(p.master, p.slave) }).Any());
            factors[p.master][(int)AttractionMode.Romantic] = 0;
            Require(!SexAppraiser.FindBestResults(new[] { Appraisal(p.master, p.slave) }).Any());
        });
        Check("native casual settings waive slave opinion and romance only", () =>
        {
            var p = Pair(); opinions[p.slave] = -100; romance[p.slave] = 0; factors[p.slave][(int)AttractionMode.Romantic] = 0;
            Require(CasualSex_Helper.CasualHookupAllowedViaSettings(Appraisal(p.master, p.slave)));
            Require(CasualSex_Helper.CasualHookupAllowedViaSettings(Appraisal(p.slave, p.master)));
            opinions[p.master] = -1;
            Require(!CasualSex_Helper.CasualHookupAllowedViaSettings(Appraisal(p.master, p.slave)));
        });
        Check("native casual settings retain master vanilla attractiveness refusal", () =>
        {
            var p = Pair(); romance[p.master] = 0;
            Require(!CasualSex_Helper.CasualHookupAllowedViaSettings(Appraisal(p.slave, p.master)));
        });
        Check("native casual success rechecks bond after directed threshold was read", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = 2; opinions[p.slave] = -100;
            atOpinion = observer => { if (observer == p.slave) p.slave.health.hediffSet.hediffs.Remove(p.chain); };
            Require(!CasualSex_Helper.CasualHookupAllowedViaSettings(Appraisal(p.master, p.slave)));
        });
        Check("native right-click willingness preserves master opinion refusal", () =>
        {
            var p = Pair(); opinions[p.slave] = -100; factors[p.slave][(int)AttractionMode.Romantic] = 0;
            var checks = AccessTools.Method(typeof(xxx).Assembly.GetType("rjw.RMB.RMB_Sex", true), "DoChecks");
            Require(((AcceptanceReport)checks.Invoke(null, new object[] { p.master, p.slave })).Accepted);
            opinions[p.master] = -1;
            Require(!((AcceptanceReport)checks.Invoke(null, new object[] { p.master, p.slave })).Accepted);
        });
        Check("adult-only relationship eligibility excludes young biological age", () =>
        {
            var p = Pair(); p.slave.ageTracker.AgeBiologicalTicks = 17L * GenDate.TicksPerYear;
            factors[p.slave][(int)AttractionMode.Romantic] = 0;
            Near(0, Evaluate(p.slave, p.master));
            Require(!(bool)CallProduction(productionUtility, "IsEligibleDirectedPair", p.slave, p.master));
        });
        Check("native automatic willingness exception filters to actual owner candidate", () =>
        {
            var p = Pair(); var stranger = Pawn(PawnIdentity.Master);
            opinions[p.slave] = -100; romance[p.slave] = 0; factors[p.slave][(int)AttractionMode.Romantic] = 0;
            factors[stranger][(int)AttractionMode.Physical] = 100;
            candidates = new List<Pawn> { stranger, p.master };
            Require(CasualSex_Helper.FindPartner(p.slave) == p.master);
            // 原生评分本来就高时，路径回调解绑也必须撤销这次专属婚恋豁免，不能借原高分续权。
            factors[p.slave][(int)AttractionMode.Romantic] = 2;
            opinions[p.slave] = 50; romance[p.slave] = 1;
            atPath = () => p.slave.health.hediffSet.hediffs.Remove(p.chain);
            Require(CasualSex_Helper.FindPartner(p.slave) == null);
            atPath = null;
            p.slave.health.hediffSet.hediffs.Remove(p.chain);
            Require(CasualSex_Helper.FindPartner(p.slave) == null);
        });
        Check("native explanation preserves all RJW sections and appends actual bond result", () =>
        {
            var p = Pair(); factors[p.slave][(int)AttractionMode.Romantic] = 0;
            explanation = null; AttractionUtility.ExplainAttraction(p.slave, p.master, AttractionPurpose.ForFucking);
            Require(explanation != null && explanation.Contains(Evaluate(p.slave, p.master).ToString("0.###")), explanation);
            foreach (string section in new[] { "Observer:", "Target:", "Purpose:", "Age", "Physical", "Romantic", "Social", "Combined", "Finalized" })
                Require(explanation.Contains(section), "Lost native section: " + section);
            p.master.gender = Gender.Female; RJWPreferenceSettings.Malesex = RJWPreferenceSettings.AllowedSex.Homo;
            explanation = null; AttractionUtility.ExplainAttraction(p.slave, p.master, AttractionPurpose.ForFucking);
            string expected = "SSC_MasterAttraction_ExplainBody".Translate(p.slave.LabelShortCap, p.master.LabelShortCap, "0").ToString();
            Require(explanation != null && explanation.Contains(expected), explanation);
        });
    }

    private static object CallProduction(Type type, string name, params object[] args) => AccessTools.Method(type, name).Invoke(null, args);
    private static void Require(bool value, string reason = "assertion failed") { if (!value) throw new Exception(reason); }
    private static void Near(float expected, float actual) => Require(Math.Abs(expected - actual) < .0001f, $"expected {expected}; actual {actual}");
    private static bool PawnLabel(Pawn pawn, ref string __result) { __result = pawn.LabelShortCap.ToString(); return false; }
    private static bool Skip() => false;
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool Reservations(ref int __result) { atReservations?.Invoke(); __result = reserved; return false; }
    private static bool NoTexture(ref UnityEngine.Texture2D __result) { __result = null; return false; }
    private static bool PathBoundary(ref bool __result) { atPath?.Invoke(); __result = true; return false; }
    private static bool Windows(ref WindowStack __result) { __result = windows; return false; }
    private static bool CaptureWindow(Window window)
    { explanation = AccessTools.Field(typeof(Dialog_MessageBox), "text").GetValue(window)?.ToString(); return false; }
    private static bool Log(MethodBase __originalMethod, object[] __args)
    {
        string message = (string)__args[0];
        if (message.Contains("SSC MasterAttraction")) installationMessages.Add(message);
        if (!bootstrap && __originalMethod.Name == "Error") errors.Add(message);
        return false;
    }
    private static bool Opinion(Pawn_RelationsTracker __instance, ref int __result)
    { var observer = (Pawn)AccessTools.Field(typeof(Pawn_RelationsTracker), "pawn").GetValue(__instance); __result = opinions[observer]; atOpinion?.Invoke(observer); return false; }
    private static bool Romance(Pawn_RelationsTracker __instance, ref float __result) { __result = romance[(Pawn)AccessTools.Field(typeof(Pawn_RelationsTracker), "pawn").GetValue(__instance)]; return false; }
    private static bool Candidates(ref IEnumerable<Pawn> __result) { __result = candidates; return false; }
    /// <summary>只固定随机抽取终端，仍使用原生Appraisal与FindBestResults排序，后续原生配对门槛和寻路入口保留。</summary>
    private static bool ChooseCandidates(Pawn observer, IEnumerable<Pawn> candidates, ref IEnumerable<AppraisalResult> __result)
    { __result = SexAppraiser.FindBestResults(candidates.Select(target => Appraisal(observer, target))); return false; }

    /// <summary>偏好是外部数值输入，覆盖零因子和交叉项；不复制原生或SSC的评分规则。</summary>
    private sealed class InputPreference : AttractionPreference
    {
        internal InputPreference(AttractionMode mode) : base(mode, "Test_" + mode, FactorOperation.Multiply) { }
        protected override float GetOperand(ref AttractionRequest request, float factor) { evaluating?.Invoke(Mode); return factors[request.Pawn][(int)Mode]; }
    }
    private static bool Preferences(ref AttractionRequest request)
    {
        foreach (var mode in complete) request.SetPreference(new InputPreference(mode));
        return false;
    }

    /// <summary>真实翻译器仅装本功能键值，排除语言扫描与用户配置副作用。</summary>
    private static void LoadLanguage(string repo)
    {
        var language = Bare<LoadedLanguage>(); language.folderName = "English"; Set(language, "legacyFolderName", "English");
        language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) }; language.stringFiles = new(); language.keyedReplacements = new(); language.loadErrors = new();
        Set(language, "dataIsLoaded", true); Set(language, "wordInfo", new LanguageWordInfo());
        foreach (string file in Directory.GetFiles(Path.Combine(repo, "1.7/Languages/English/Keyed"), "*.xml"))
            foreach (var entry in XDocument.Load(file).Root.Elements()) language.keyedReplacements[entry.Name.LocalName] = new LoadedLanguage.KeyedReplacement { value = entry.Value };
        LanguageDatabase.activeLanguage = LanguageDatabase.defaultLanguage = language;
    }
}
