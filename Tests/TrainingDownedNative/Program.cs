// AI 辅助生成：真实 SSC/RJW/Harmony；隔离地图、身体数值输入和显示终端，不启动游戏。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse.AI;
using Verse.AI.Group;
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
    private static Map map;
    private static ThingDef human;
    private static int passed, failed, nextId = 100000, cleanups;
    private static Type patches, guard;
    private static string repository;
    private static bool bootstrap = true;
    private static readonly List<string> errors = new();
    private static readonly Dictionary<Pawn, Lord> lords = new();
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Set(object obj, string field, object value) => AccessTools.Field(obj.GetType(), field).SetValue(obj, value);
    private static void Patch(Type type, string method, string boundary)
    {
        var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == method).ToArray();
        if (methods.Length == 0) throw new MissingMethodException(type.FullName, method);
        foreach (var target in methods) environment.Patch(target, prefix: new HarmonyMethod(typeof(Program), boundary));
    }
    private static void Getter(Type type, string name, string boundary) => environment.Patch(AccessTools.PropertyGetter(type, name), prefix: new HarmonyMethod(typeof(Program), boundary));
    private static object Call(Type type, string name, object instance, params object[] args) => AccessTools.Method(type, name).Invoke(instance, args);
    private static void Require(bool value, string why = "assertion failed") { if (!value) throw new Exception(why); }

    internal static int Run(string repo, string game)
    {
        repository = repo;
        SscTestProfile.Start(); if (!SscTestProfile.IncludesSuite) return 0;
        environment = new Harmony("ssc.tests.training-downed.environment");
        foreach (string name in new[] { "Message", "Warning", "Error" })
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == name && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                environment.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(Log)));
        Patch(typeof(DefOfHelper), "EnsureInitializedInCtor", nameof(Skip));
        Patch(typeof(ContentFinder<UnityEngine.Texture2D>), "Get", nameof(NoTexture));
        Patch(typeof(MaterialPool), "MatFrom", nameof(NoMaterial));
        AccessTools.Field(typeof(UnityData), "mainThreadId").SetValue(null, Environment.CurrentManagedThreadId);
        Getter(typeof(Thing), "Spawned", nameof(True)); Getter(typeof(Thing), "Map", nameof(Map));
        Getter(typeof(Pawn), "InMentalState", nameof(False)); Getter(typeof(Pawn), "Drafted", nameof(False));
        Patch(typeof(Pawn), "DropAndForbidEverything", nameof(Skip));
        Patch(typeof(RestUtility), "InBed", nameof(False));
        Patch(typeof(RestUtility), "IsLayingForJobCleanup", nameof(False));
        Getter(typeof(Pawn), "IsMutant", nameof(False));
        Patch(typeof(PawnDiedOrDownedThoughtsUtility), "TryGiveThoughts", nameof(Skip));
        Patch(typeof(Pawn_StanceTracker), "CancelBusyStanceSoft", nameof(Skip));
        Patch(typeof(Pawn_MindState), "Reset", nameof(Skip)); Patch(typeof(Pawn), "VerifyReservations", nameof(Skip));
        Patch(typeof(Pawn_PathFollower), "StopDead", nameof(Skip));
        Patch(typeof(Pawn_JobTracker), "CleanupCurrentJob", nameof(Cleanup)); Patch(typeof(Pawn_JobTracker), "ClearQueuedJobs", nameof(Skip));
        Patch(typeof(PortraitsCache), "SetDirty", nameof(Skip));
        Patch(typeof(GlobalTextureAtlasManager), "TryMarkPawnFrameSetDirty", nameof(Skip));
        Patch(typeof(GenHostility), "Notify_PawnLostForTutor", nameof(Skip));
        Patch(typeof(Storyteller), "Notify_PawnEvent", nameof(Skip));
        Patch(typeof(Lord), "Notify_PawnDowned", nameof(Skip));
        Patch(typeof(Genital_Helper), "get_AllPartsHediffList", nameof(Parts));
        Patch(typeof(LordUtility), "GetLord", nameof(LordFor));
        Patch(typeof(Pawn_JobTracker), "StartJob", nameof(InstallReceiver));
        // 本宿主不启动完整世界，只准备原MakeDowned所需的通知终端。
        var root = Bare<Game>(); root.tickManager = new TickManager(); root.uniqueIDsManager = new UniqueIDsManager();
        Set(root, "components", new List<GameComponent>()); Current.Game = root;
        Set(root, "storyteller", Bare<Storyteller>());
        AccessTools.Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
        map = Bare<Map>(); map.lordManager = Bare<LordManager>();
        Set(map.lordManager, "map", map); Set(map.lordManager, "lords", new List<Lord>());
        human = Bare<ThingDef>(); human.defName = "DownedNativeHuman"; human.category = ThingCategory.Pawn;
        human.race = new RaceProperties { intelligence = Intelligence.Humanlike };
        SSCMod.settings = new SSCSettings();
        SSCDefOf.SSC_TrainingReceiver = new JobDef { defName = "SSC_TrainingReceiver", driverClass = typeof(JobDriver_TrainingReceiver) };
        SSCDefOf.Training_Ritual = new JobDef { defName = "SSC_Training_Ritual", driverClass = typeof(JobDriver_RitualTraining) };
        SSCDefOf.ChainOfSexSlave = new HediffDef { defName = "ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave) };
        SSCDefOf.SSC_PersonalityExcreted_Done = new HediffDef { defName = "SSC_PersonalityExcreted_Done" };
        patches = typeof(SSCBondUtility).Assembly.GetType("SexSlaveCraft.SSCTrainingDownedPatches", true);
        guard = typeof(SSCBondUtility).Assembly.GetType("SexSlaveCraft.SSCRestrictionJobGuard", true);
        Call(patches, "Install", null);
        foreach (var assembly in new[] { typeof(Pawn).Assembly, typeof(xxx).Assembly, typeof(SSCBondUtility).Assembly })
            Console.WriteLine($"ASSEMBLY: {assembly.Location} MVID={assembly.ManifestModule.ModuleVersionId:N} SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant()}");
        bootstrap = false; errors.Clear();
        try { Checks(repo); }
        finally { environment.UnpatchAll(environment.Id); Current.Game = null; }
        Console.WriteLine($"RESULT: {passed}/{passed + failed} passed"); return failed == 0 ? 0 : 1;
    }

    private static Pawn Pawn(PawnIdentity identity)
    {
        var p = new Pawn { def = human, thingIDNumber = ++nextId, Name = new NameSingle("Downed" + nextId) };
        p.health = new Pawn_HealthTracker(p); p.needs = new Pawn_NeedsTracker(p); p.mindState = new Pawn_MindState(p);
        p.jobs = new Pawn_JobTracker(p); p.stances = new Pawn_StanceTracker(p); p.pather = new Pawn_PathFollower(p);
        Set(p, "comps", new List<ThingComp> { new CompSexSlaveTraining { parent = p, pawnIdentity = identity } });
        return p;
    }
    private static (Pawn actor, Pawn receiver, JobDriver_SexBaseInitiator driver) Pair(bool ritual = false, bool started = true, bool ordinary = false)
    {
        Pawn a = Pawn(PawnIdentity.Master), b = Pawn(PawnIdentity.Slave);
        b.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { pawn = b, def = SSCDefOf.ChainOfSexSlave, target = a });
        JobDriver_SexBaseInitiator driver;
        if (ordinary) driver = Bare<JobDriver_SexQuick>();
        else if (ritual) driver = Bare<JobDriver_RitualTraining>();
        else driver = Bare<JobDriver_Training>();
        driver.pawn = a; driver.job = JobMaker.MakeJob(ritual ? SSCDefOf.Training_Ritual : new JobDef { defName = "SSC_Training", driverClass = driver.GetType() }, b);
        a.jobs.curJob = driver.job; a.jobs.curDriver = driver;
        var recv = Bare<JobDriver_TrainingReceiver>(); recv.pawn = b; recv.job = JobMaker.MakeJob(SSCDefOf.SSC_TrainingReceiver, a);
        recv.parteners = new List<Pawn> { a }; b.jobs.curJob = recv.job; b.jobs.curDriver = recv;
        if (ritual) Ritual(repository, a, b);
        if (started) Call(guard, "TryMarkStarted", null, driver, true);
        return (a, b, driver);
    }
    private static void Down(Pawn p) => Call(typeof(Pawn_HealthTracker), "MakeDowned", p.health, null,
        new Hediff { pawn = p, def = new HediffDef { defName = "DownedTestCause", recordDownedTale = false } });
    private static void Check(string name, Action test)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        cleanups = 0; lords.Clear(); errors.Clear();
        try { test(); Require(errors.Count == 0, string.Join("\n", errors)); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void Checks(string repo)
    {
        Check("native MakeDowned preserves the started daily receiver job", () =>
        {
            var p = Pair(); var job = p.receiver.CurJob; Down(p.receiver);
            Require(p.receiver.Downed); Require(p.receiver.CurJob == job && cleanups == 0);
        });
        Check("native MakeDowned preserves the ritual receiver job", () =>
        {
            var p = Pair(ritual: true); var job = p.receiver.CurJob; Down(p.receiver);
            Require(p.receiver.Downed && p.receiver.CurJob == job && cleanups == 0);
        });
        Check("ordinary RJW receiver keeps native downing interruption", () =>
        {
            var p = Pair(ordinary: true); Down(p.receiver); Require(cleanups == 1 && p.receiver.CurJob == null);
        });
        Check("unstarted SSC receiver keeps native downing interruption", () =>
        {
            var p = Pair(started: false); Down(p.receiver); Require(cleanups == 1 && p.receiver.CurJob == null);
        });
        Check("trainer downing keeps native interruption", () =>
        {
            var p = Pair(); Down(p.actor); Require(cleanups == 1 && p.actor.CurJob == null);
        });
        Check("stale receiver ownership keeps native interruption", () =>
        {
            var p = Pair(); p.driver.job.targetA = Pawn(PawnIdentity.Slave); Down(p.receiver); Require(cleanups == 1);
        });
        Check("native abort capture permits later receiver downing and still detects trainer downing", () =>
        {
            var p = Pair(); var conditions = p.driver.ConditionsToAbortSex();
            Set(p.receiver.health, "healthState", PawnHealthState.Down);
            Require(!conditions.Any(c => c()));
            Set(p.actor.health, "healthState", PawnHealthState.Down);
            Require(conditions.Any(c => c()));
        });
        Check("native receiver abort capture preserves death condition", () =>
        {
            var p = Pair(); var recv = (JobDriver_Sex)p.receiver.jobs.curDriver;
            var conditions = recv.ConditionsToAbortSex(); Set(p.receiver.health, "healthState", PawnHealthState.Down);
            Require(!conditions.Any(c => c())); Set(p.receiver.health, "healthState", PawnHealthState.Dead);
            Require(conditions.Any(c => c()));
        });
        Check("downed ritual receiver can be rebuilt for the following phase", () =>
        {
            var p = Pair(ritual: true); Down(p.receiver); var previous = p.receiver.CurJob;
            Require(TrainingJobUtility.TryStartBindingRitualReceiver(p.actor, p.receiver, p.driver.job,
                SSCDefOf.SSC_TrainingReceiver, p.receiver.Position));
            Require(p.receiver.Downed && p.receiver.CurJob != previous && p.receiver.CurJobDef == SSCDefOf.SSC_TrainingReceiver);
            var conditions = ((JobDriver_Sex)p.receiver.jobs.curDriver).ConditionsToAbortSex();
            Require(!conditions.Any(c => c()));
        });
        Check("native ritual lost-pawn gate retains only the XML slave role when incapacitated", () =>
        {
            var p = Pair(ritual: true); var lord = Ritual(repo, p.actor, p.receiver);
            var job = (LordJob_Ritual)lord.LordJob;
            Require(!job.ShouldRemovePawn(p.receiver, PawnLostCondition.Incapped));
            lord.Notify_PawnLost(p.receiver, PawnLostCondition.Incapped);
            Require(lord.ownedPawns.Contains(p.receiver));
            Require(job.ShouldRemovePawn(p.actor, PawnLostCondition.Incapped));
            Require(job.ShouldRemovePawn(p.receiver, PawnLostCondition.Killed));
        });
    }

    private static Lord Ritual(string repo, Pawn actor, Pawn receiver)
    {
        var document = XDocument.Load(Path.Combine(repo, "1.7/Defs/RitualDefs/Ritual_Behavior.xml"));
        var roleXml = document.Descendants("roles").Single().Elements("li").ToArray();
        var slaveRole = new RitualRole_BindingSlave { id = "slave", allowDowned = bool.Parse(roleXml.Single(x => x.Element("id")?.Value == "slave").Element("allowDowned")?.Value ?? "false") };
        var masterRole = new RitualRole_BindingMaster { id = "master" };
        var roles = Bare<RitualRoleAssignments>(); Set(roles, "allRoles", new List<RitualRole> { slaveRole, masterRole });
        Set(roles, "spectators", new List<Pawn>()); Set(roles, "assignedRoles", new Dictionary<string,List<Pawn>> { ["slave"] = new() { receiver }, ["master"] = new() { actor } });
        var job = Bare<LordJob_Ritual>(); job.assignments = roles;
        var lord = Bare<Lord>(); Set(lord, "curJob", job); job.lord = lord;
        Set(lord, "lordManager", map.lordManager); lord.ownedPawns = new List<Pawn> { actor, receiver };
        map.lordManager.lords.Add(lord); lords[actor] = lords[receiver] = lord;
        var behavior = Bare<RitualBehaviorWorker>(); behavior.def = new RitualBehaviorDef { defName = "SSC_BindingRitualBehavior" };
        var ritual = Bare<Precept_Ritual>(); ritual.behavior = behavior; Set(job, "ritual", ritual);
        var comp = receiver.TryGetComp<CompSexSlaveTraining>(); comp.bindingRitualLord = lord; comp.isBeingTrained = true; comp.isRitualTraining = true;
        return lord;
    }
    private static bool NoTexture(ref UnityEngine.Texture2D __result) { __result = null; return false; }
    private static bool NoMaterial(ref UnityEngine.Material __result) { __result = null; return false; }
    private static bool Skip() => false;
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool Map(ref Map __result) { __result = map; return false; }
    private static bool LordFor(object __0, ref Lord __result) { __result = null; if (__0 is Pawn p) lords.TryGetValue(p, out __result); return false; }
    private static bool Parts(ref List<Hediff> __result) { __result = new List<Hediff>(); return false; }
    /// <summary>仅隔离引擎安装终端；实际交接/新job创建/状态确认均由生产TrainingJobUtility执行。</summary>
    private static bool InstallReceiver(Pawn_JobTracker __instance, Job newJob)
    {
        if (newJob.def != SSCDefOf.SSC_TrainingReceiver) return true;
        Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_JobTracker), "pawn").GetValue(__instance);
        var receiver = new JobDriver_TrainingReceiver { pawn = pawn, job = newJob, parteners = new List<Pawn> { newJob.targetA.Pawn } };
        __instance.curJob = newJob; __instance.curDriver = receiver;
        return false;
    }
    private static bool Cleanup(Pawn_JobTracker __instance) { cleanups++; __instance.curJob = null; __instance.curDriver = null; return false; }
    private static bool Log(MethodBase __originalMethod, object[] __args) { if (!bootstrap && __originalMethod.Name == "Error") errors.Add(__args[0]?.ToString()); return false; }
}
