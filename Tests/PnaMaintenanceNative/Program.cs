// AI 辅助生成：真实候选、原生工作优先级／XML补丁／Scribe，地图查找和最终摄入副作用隔离。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using UnityEngine;
using Verse;
using Verse.AI;

internal static class Entry
{
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("repositoryRoot gameRoot");
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        using var info = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "1.7/BUILD-INFO.json")));
        var paths = info.RootElement.GetProperty("references").EnumerateArray().Select(r => r.GetProperty("path").GetString()).ToArray();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = paths.FirstOrDefault(p => AssemblyName.GetAssemblyName(p).Name == name.Name) ?? Path.Combine(game, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { repo, game });
    }
}

// 将已存在角色／链作为存档外部引用登记；不在夹具里实现另一套引用恢复。
public sealed class SavedPnaDriver : IExposable
{
    public JobDriver_SSCPnaMaintenance Driver;
    public SavedPnaDriver(Pawn pawn, Job job) { Driver = new JobDriver_SSCPnaMaintenance { pawn = pawn, job = job }; }
    public void ExposeData() => Driver.ExposeData();
}

public sealed class SavedPnaSetting : IExposable
{
    public Hediff_ChainOfSexSlave Chain = new();
    public void ExposeData() => Chain.ExposeData();
}

internal static partial class Program
{
    private static int passed, failed, nextId = 70000, searchCalls, workCalls, ingestions, cancellations;
    private static bool canWork = true;
    private static readonly List<Map> maps = new();
    private static readonly List<string> errors = new();
    private static Faction player;
    private static string saves;
    private static ThingDef pawnDef, rawDef;
    private static HediffDef loadDef, chainDef;
    private static JobDef supplyDef;

    internal static int Run(string repo, string game)
    {
        SscTestProfile.Start();
        if (!SscTestProfile.IncludesSuite) { Console.WriteLine("RESULT: 0/0 passed (suite excluded)"); return 0; }
        var harmony = new Harmony("ssc.tests.pna-maintenance.native");
        void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
        saves = Path.Combine(repo, ".builds/pna-maintenance/userdata");
        Directory.CreateDirectory(Path.Combine(saves, "Config"));
        File.WriteAllText(Path.Combine(saves, "Config/ModsConfig.xml"), "<ModsConfigData><version>1.7.4995</version><activeMods/><knownExpansions/></ModsConfigData>");
        Patch(AccessTools.PropertyGetter(typeof(GenFilePaths), nameof(GenFilePaths.SaveDataFolderPath)), nameof(SavePath));
        Patch(AccessTools.Method(typeof(ModLister), "RebuildModList"), nameof(Skip));
        Patch(AccessTools.Method(typeof(ContentFinder<Texture2D>), "Get"), nameof(NoTexture));
        foreach (var method in typeof(SolidColorMaterials).GetMethods().Where(m => m.Name == nameof(SolidColorMaterials.NewSolidColorTexture))) Patch(method, nameof(NoTexture));
        foreach (var type in new[] { typeof(Translator), typeof(TranslatorFormattedStringExtensions) })
            foreach (var method in type.GetMethods().Where(m => m.Name == "Translate" && m.ReturnType == typeof(TaggedString) && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))) Patch(method, nameof(Translate));
        Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps)), nameof(Maps));
        Patch(AccessTools.PropertyGetter(typeof(Faction), nameof(Faction.OfPlayer)), nameof(Player));
        Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged)), nameof(Skip));
        // 库存增添后的整图通知不属于补药判断；容器持有／查询仍走原版。
        Patch(AccessTools.Method(typeof(ThingOwner), "NotifyAdded", new[] { typeof(Thing) }), nameof(Skip));
        Patch(AccessTools.PropertyGetter(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EverWork)), nameof(EverWork));
        Patch(AccessTools.Method(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage)), nameof(OrdinaryWork));
        Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob)), nameof(CancelBoundary));
        Patch(AccessTools.GetDeclaredMethods(typeof(GenClosest)).Single(m => m.Name == nameof(GenClosest.ClosestThingReachable) && !m.IsGenericMethod), nameof(NoMapDrug));
        Patch(AccessTools.PropertyGetter(typeof(JobDriver_Ingest), "ChewDurationMultiplier"), nameof(ChewSpeed));
        Patch(AccessTools.Method(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest)), nameof(FinalizeBoundary));
        foreach (var method in typeof(Log).GetMethods().Where(m => m.Name == "Error" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))) Patch(method, nameof(LogError));
        try
        {
            Current.Game = Bare<Game>(); Current.Game.tickManager = new TickManager(); Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Set(Current.Game.tickManager, "gameStartAbsTick", 3600000);
            Current.Game.World = Bare<World>(); Current.Game.World.worldPawns = new WorldPawns();
            SetStatic(typeof(UnityData), "mainThreadId", Environment.CurrentManagedThreadId);
            SetStatic(typeof(Prefs), "data", new PrefsData());
            player = new Faction { def = new FactionDef { defName = "PnaTestPlayer", isPlayer = true } };
            var map = new Map(); maps.Add(map); map.info.Size = new IntVec3(30, 1, 30);
            var pack = Bare<ModContentPack>(); pack.assemblies = new ModAssemblyHandler(pack);
            pack.assemblies.loadedAssemblies.Add(typeof(SSCPnaMaintenanceUtility).Assembly); pack.assemblies.loadedAssemblies.Add(typeof(Entry).Assembly);
            ((List<ModContentPack>)AccessTools.Field(typeof(LoadedModManager), "runningMods").GetValue(null)).Add(pack);
            LoadDefinitions(repo);
            errors.Clear();
            Check("实际XML只替换普通工作，保留紧急工作并正确解析专用驱动", () =>
            {
                var misc = new XmlDocument(); misc.Load(Path.Combine(game, "Data/Core/Defs/ThinkTreeDefs/SubTrees_Misc.xml"));
                var patch = new XmlDocument(); patch.Load(Path.Combine(repo, "1.7/Patches/SSC_PnaMaintenance.xml"));
                var operation = DirectXmlToObject.ObjectFromXml<PatchOperation>(patch.SelectSingleNode("/Patch/Operation"), true);
                Require(operation.Apply(misc));
                Equal(1, misc.SelectNodes("//li[@Class='SexSlaveCraft.JobGiver_SSCPnaMaintenanceOrWork']").Count);
                var humanlike = new XmlDocument(); humanlike.Load(Path.Combine(game, "Data/Core/Defs/ThinkTreeDefs/Humanlike.xml"));
                Require(humanlike.SelectSingleNode("//li[@Class='JobGiver_Work' and emergency='true']") != null);
                Equal(typeof(JobDriver_SSCPnaMaintenance), supplyDef.driverClass);
                Require(!supplyDef.allowOpportunisticPrefix && !supplyDef.suspendable);
                Require(new JobGiver_SSCPnaMaintenanceOrWork().DeepCopy(false) is JobGiver_SSCPnaMaintenanceOrWork);
            });
            Check("阶段读取实际Def下限，第一档消失补给与最高档边界不同", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.First);
                Require(SSCPnaMaintenanceUtility.NeedsDose(pawn, SSCPnaMaintenanceStage.First));
                Load(pawn, .2f); Require(!SSCPnaMaintenanceUtility.NeedsDose(pawn, SSCPnaMaintenanceStage.First));
                Load(pawn, 1.39f); Require(SSCPnaMaintenanceUtility.NeedsDose(pawn, SSCPnaMaintenanceStage.Fourth));
                Load(pawn, 1.4f); Require(!SSCPnaMaintenanceUtility.NeedsDose(pawn, SSCPnaMaintenanceStage.Fourth));
                Load(pawn, 1.94f); Require(!SSCPnaMaintenanceUtility.NeedsDose(pawn, SSCPnaMaintenanceStage.Fifth));
                Require(!SSCPnaMaintenanceUtility.NeedsDose(pawn, (SSCPnaMaintenanceStage)99));
                Require(!SSCPnaMaintenanceUtility.NeedsDose(pawn, SSCPnaMaintenanceStage.Off));
            });
            Check("原生工作排程优先级不变，无工作能力仍能补药", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Fourth); var giver = new JobGiver_SSCPnaMaintenanceOrWork();
                foreach (var assignment in new[] { TimeAssignmentDefOf.Anything, TimeAssignmentDefOf.Work, TimeAssignmentDefOf.Sleep, TimeAssignmentDefOf.Joy })
                {
                    for (int hour = 0; hour < 24; hour++) pawn.timetable.times[hour] = assignment;
                    Near(new JobGiver_Work().GetPriority(pawn), giver.GetPriority(pawn));
                }
                canWork = false; Require(giver.GetPriority(pawn) > 0f);
                Thing raw = Raw(pawn); var result = giver.TryIssueJobPackage(pawn, default);
                Equal(supplyDef, result.Job.def); Equal(1, result.Job.count); Equal(raw, result.Job.targetA.Thing);
                canWork = true;
            });
            Check("忙碌、强制命令、准备等待与排队均不被补药抢断", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Third); Raw(pawn);
                pawn.jobs.curJob = JobMaker.MakeJob(new JobDef { defName = "TrainingReceiver" });
                Require(SSCPnaMaintenanceUtility.TryMakeJob(pawn) == null);
                pawn.jobs.curJob = JobMaker.MakeJob(JobDefOf.Wait); pawn.CurJob.expiryInterval = 300;
                Require(!SSCPnaMaintenanceUtility.IsBetweenTasks(pawn));
                pawn.CurJob.expiryInterval = -1; pawn.CurJob.playerForced = true;
                Require(!SSCPnaMaintenanceUtility.IsBetweenTasks(pawn));
                pawn.jobs.curJob = null; pawn.jobs.jobQueue.EnqueueLast(JobMaker.MakeJob(JobDefOf.Wait));
                Require(!SSCPnaMaintenanceUtility.CanStart(pawn)); Set(pawn.jobs, "jobQueue", new JobQueue());
                pawn.jobs.curJob = JobMaker.MakeJob(JobDefOf.Wait_Wander);
                Require(SSCPnaMaintenanceUtility.CanStart(pawn));
            });
            Check("缺药返回原工作，背包有药不进行地图寻路", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Third); var giver = new JobGiver_SSCPnaMaintenanceOrWork();
                searchCalls = workCalls = 0;
                Require(!giver.TryIssueJobPackage(pawn, default).IsValid); Equal(1, searchCalls); Equal(1, workCalls);
                Thing raw = Raw(pawn); searchCalls = workCalls = 0;
                Equal(raw, giver.TryIssueJobPackage(pawn, default).Job.targetA.Thing); Equal(0, searchCalls); Equal(0, workCalls);
            });
            Check("原菜单拒绝换链、换主人及同值恢复后的旧请求", () =>
            {
                var pawn = Pawn(); var original = SSCPnaMaintenanceUtility.Capture(pawn);
                Require(SSCPnaMaintenanceUtility.TrySet(original, SSCPnaMaintenanceStage.Third, out _));
                Require(!SSCPnaMaintenanceUtility.TrySet(original, SSCPnaMaintenanceStage.Fifth, out _));
                var context = SSCPnaMaintenanceUtility.Capture(pawn); context.Chain.target = Pawn(false);
                Require(!SSCPnaMaintenanceUtility.TrySet(context, SSCPnaMaintenanceStage.Fifth, out _));
                context = SSCPnaMaintenanceUtility.Capture(pawn); pawn.health.hediffSet.hediffs.Remove(context.Chain);
                Chain(pawn); Require(!SSCPnaMaintenanceUtility.TrySet(context, SSCPnaMaintenanceStage.Fifth, out _));
            });
            Check("只有有效锁链性奴触发，零阶段有效链与无效残链可区分", () =>
            {
                var pawn = Pawn(); var chain = SSCPnaMaintenanceUtility.FindChain(pawn); SetStage(pawn, SSCPnaMaintenanceStage.Third);
                chain.Severity = 0f; Require(SSCPnaMaintenanceUtility.CanStart(pawn));
                var stale = new Hediff_ChainOfSexSlave { def = chainDef, pawn = pawn }; pawn.health.hediffSet.hediffs.Insert(0, stale);
                Equal(chain, SSCPnaMaintenanceUtility.FindChain(pawn));
                pawn.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Master;
                Require(!SSCPnaMaintenanceUtility.CanStart(pawn));
                pawn.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Slave; chain.target = null;
                Require(!SSCPnaMaintenanceUtility.CanStart(pawn));
            });
            Check("预检查驱动不领取候选，实际运行驱动持有原链并持续服用", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Third); Raw(pawn);
                Job job = SSCPnaMaintenanceUtility.TryMakeJob(pawn);
                var cached = (JobDriver_SSCPnaMaintenance)job.GetCachedDriver(pawn); cached.Notify_Starting();
                var actual = Start(pawn, job); Require(!ReferenceEquals(cached, actual));
                Require(SSCPnaMaintenanceUtility.CanContinue(Context(actual), actual));
                var giver = new JobGiver_SSCPnaMaintenanceOrWork();
                Equal(job, giver.TryIssueJobPackage(pawn, default).Job);
                cancellations = 0; SetStage(pawn, SSCPnaMaintenanceStage.Off);
                Equal(1, cancellations); Require(pawn.CurJob == null);
                SetStage(pawn, SSCPnaMaintenanceStage.Third); actual = Start(pawn, SSCPnaMaintenanceUtility.TryMakeJob(pawn));
                var chain = SSCPnaMaintenanceUtility.FindChain(pawn); chain.target = Pawn(false);
                Require(!SSCPnaMaintenanceUtility.CanContinue(Context(actual), actual));
            });
            Check("补给候选不因关闭后重开或Job对象池编号变化重新取得授权", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Third); Raw(pawn);
                Job job = SSCPnaMaintenanceUtility.TryMakeJob(pawn);
                SetStage(pawn, SSCPnaMaintenanceStage.Off); SetStage(pawn, SSCPnaMaintenanceStage.Third);
                var driver = Start(pawn, job); Require(!SSCPnaMaintenanceUtility.CanContinue(Context(driver), driver));
                pawn.jobs.curJob = null; pawn.jobs.curDriver = null;
                job = SSCPnaMaintenanceUtility.TryMakeJob(pawn); job.loadID++;
                driver = Start(pawn, job); Require(Context(driver) == null);
            });
            Check("途中已达标取消，实际服用完成后不把达标判为失败", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Second); Raw(pawn);
                var driver = Start(pawn, SSCPnaMaintenanceUtility.TryMakeJob(pawn));
                Load(pawn, .8f); Require(!SSCPnaMaintenanceUtility.CanContinue(Context(driver), driver));
                Load(pawn, 0f);
                var toils = ((IEnumerable<Toil>)AccessTools.Method(typeof(JobDriver_SSCPnaMaintenance), "MakeNewToils").Invoke(driver, null)).ToList();
                var finalize = toils.Single(t => t.debugName == nameof(Toils_Ingest.FinalizeIngest));
                ingestions = cancellations = 0;
                var originalJob = driver.job; var replacement = JobMaker.MakeJob(JobDefOf.Wait); pawn.jobs.curJob = replacement;
                finalize.initAction(); Equal(0, ingestions); Equal(0, cancellations); Equal(replacement, pawn.CurJob);
                pawn.jobs.curJob = originalJob; finalize.initAction();
                Equal(1, ingestions); Near(.4f, SSCPnaExposureUtility.GetBodyRecord(pawn).Severity);
                Require((bool)AccessTools.Field(typeof(JobDriver_SSCPnaMaintenance), "doseApplied").GetValue(driver));
                Require(SSCPnaMaintenanceUtility.CanContinue(Context(driver), driver, false));
            });
            Check("原生Scribe恢复请求原链，替换后的新链不能接管旧任务", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Third); Raw(pawn);
                var driver = Start(pawn, SSCPnaMaintenanceUtility.TryMakeJob(pawn)); var context = Context(driver);
                SavedPnaDriver holder = new(pawn, driver.job) { Driver = driver }; string path = Path.Combine(saves, "request.xml");
                Scribe.saver.InitSaving(path, "save"); Scribe_Deep.Look(ref holder, "holder"); Scribe.saver.FinalizeSaving();
                holder = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref holder, "holder", pawn, driver.job);
                var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
                directory.RegisterLoaded(pawn); directory.RegisterLoaded(context.Master); directory.RegisterLoaded(context.Chain);
                Scribe.loader.FinalizeLoading();
                pawn.jobs.curDriver = holder.Driver;
                var restored = Context(holder.Driver);
                Require(SSCPnaMaintenanceUtility.IsCurrent(restored)); Equal(context.Chain, restored.Chain);
                Require(SSCPnaMaintenanceUtility.CanContinue(restored, holder.Driver));
                Require(holder.Driver.EatingFromInventory);
                pawn.health.hediffSet.hediffs.Remove(context.Chain); Chain(pawn);
                Require(!SSCPnaMaintenanceUtility.IsCurrent(restored));
            });
            Check("锁链设置原生存读档保留，旧档及非法值关闭，原版复制不继承配置", () =>
            {
                var pawn = Pawn(); SetStage(pawn, SSCPnaMaintenanceStage.Fourth);
                var original = SSCPnaMaintenanceUtility.FindChain(pawn);
                var copy = new Hediff_ChainOfSexSlave { def = chainDef, pawn = Pawn(false) }; copy.CopyFrom(original);
                Equal(SSCPnaMaintenanceStage.Off, copy.PnaMaintenanceStage);
                SavedPnaSetting holder = new() { Chain = original }; string path = Path.Combine(saves, "setting.xml");
                Scribe.saver.InitSaving(path, "save"); Scribe_Deep.Look(ref holder, "holder"); Scribe.saver.FinalizeSaving();
                string saved = File.ReadAllText(path);
                foreach (int variant in new[] { 0, 1, 2 })
                {
                    var doc = new XmlDocument(); doc.LoadXml(saved);
                    var node = doc.SelectSingleNode("//sscPnaMaintenanceStage");
                    if (variant == 1) node.ParentNode.RemoveChild(node); else if (variant == 2) node.InnerText = "99";
                    doc.Save(path); holder = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref holder, "holder");
                    var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
                    directory.RegisterLoaded(original.LinkedPawn); Scribe.loader.FinalizeLoading();
                    Equal(variant == 0 ? SSCPnaMaintenanceStage.Fourth : SSCPnaMaintenanceStage.Off, holder.Chain.PnaMaintenanceStage);
                }
            });
            RunCaravanTests(harmony, repo);
        }
        finally { harmony.UnpatchAll(harmony.Id); Current.Game = null; }
        Console.WriteLine($"{passed}/{passed + failed} cases passed"); return failed == 0 ? 0 : 1;
    }

    private static void LoadDefinitions(string repo)
    {
        // 阶段取自本次实际XML；无需在宿主复制整套Stat和健康属性消费算法。
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/HediffOfBuff.xml"));
        loadDef = new HediffDef { defName = SSCPnaExposureUtility.RootedLoadDefName, hediffClass = typeof(HediffWithComps), maxSeverity = 2f, initialSeverity = .01f, stages = new() };
        foreach (XmlNode node in xml.SelectNodes("/Defs/HediffDef[defName='SSC_Hediff_PnaRootedLoad']/stages/li"))
            loadDef.stages.Add(new HediffStage { label = node["label"].InnerText, minSeverity = float.Parse(node["minSeverity"].InnerText, System.Globalization.CultureInfo.InvariantCulture) });
        DefDatabase<HediffDef>.Add(loadDef);
        chainDef = new HediffDef { defName = "Hediff_ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave), maxSeverity = 1f };
        DefDatabase<HediffDef>.Add(chainDef);
        var jobs = new XmlDocument(); jobs.Load(Path.Combine(repo, "1.7/Defs/JobDefs/SSC_PnaMaintenance.xml"));
        supplyDef = DirectXmlToObject.ObjectFromXml<JobDef>(jobs.SelectSingleNode("/Defs/JobDef"), true); DefDatabase<JobDef>.Add(supplyDef);
        rawDef = Bare<ThingDef>(); rawDef.defName = "Purple_Aphrodisiac_Nanofluid"; rawDef.category = ThingCategory.Item;
        rawDef.thingClass = typeof(ThingWithComps); rawDef.stackLimit = 75;
        rawDef.ingestible = new IngestibleProperties { drugCategory = DrugCategory.Medical, maxNumToIngestAtOnce = 1, defaultNumToIngestAtOnce = 1 };
        DefDatabase<ThingDef>.Add(rawDef);
        pawnDef = Bare<ThingDef>(); pawnDef.defName = "PnaPawnFixture"; pawnDef.category = ThingCategory.Pawn;
        pawnDef.race = new RaceProperties { intelligence = Intelligence.Humanlike };
        Set(pawnDef.race, "fleshType", new FleshTypeDef { defName = "PnaFlesh", isOrganic = true });
        pawnDef.race.body = new BodyDef { defName = "PnaBody", corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Torso" } } }; pawnDef.race.body.ResolveReferences();
        foreach (string name in new[] { "Anything", "Work", "Sleep", "Joy" }) AccessTools.Field(typeof(TimeAssignmentDefOf), name).SetValue(null, new TimeAssignmentDef { defName = name });
        foreach (string name in new[] { "Wait", "Wait_Wander" }) AccessTools.Field(typeof(JobDefOf), name).SetValue(null, new JobDef { defName = name });
    }

    private static Pawn Pawn(bool chained = true)
    {
        canWork = true;
        var pawn = new Pawn { def = pawnDef, thingIDNumber = ++nextId };
        Set(pawn, "mapIndexOrState", (sbyte)0); Set(pawn, "positionInt", new IntVec3(10, 0, 10)); Set(pawn, "factionInt", player);
        pawn.health = new Pawn_HealthTracker(pawn); pawn.mindState = new Pawn_MindState(pawn); pawn.needs = new Pawn_NeedsTracker(pawn);
        pawn.jobs = new Pawn_JobTracker(pawn); pawn.inventory = new Pawn_InventoryTracker(pawn); pawn.timetable = new Pawn_TimetableTracker(pawn);
        pawn.workSettings = Bare<Pawn_WorkSettings>(); Set(pawn.workSettings, "pawn", pawn);
        var comp = new CompSexSlaveTraining { parent = pawn, pawnIdentity = PawnIdentity.Slave };
        Set(pawn, "comps", new List<ThingComp> { comp });
        if (chained) Chain(pawn); return pawn;
    }
    private static Hediff_ChainOfSexSlave Chain(Pawn pawn)
    {
        var chain = new Hediff_ChainOfSexSlave { def = chainDef, pawn = pawn, target = Pawn(false), loadID = ++nextId };
        pawn.health.hediffSet.hediffs.Add(chain); return chain;
    }
    private static void SetStage(Pawn pawn, SSCPnaMaintenanceStage stage)
        => Require(SSCPnaMaintenanceUtility.TrySet(SSCPnaMaintenanceUtility.Capture(pawn), stage, out _));
    private static void Load(Pawn pawn, float severity)
    {
        var record = SSCPnaExposureUtility.GetBodyRecord(pawn);
        if (record == null) { record = new HediffWithComps { def = loadDef, pawn = pawn, loadID = ++nextId }; pawn.health.hediffSet.hediffs.Add(record); }
        record.Severity = severity;
    }
    private static Thing Raw(Pawn pawn)
    {
        var thing = new ThingWithComps { def = rawDef, stackCount = 3, thingIDNumber = ++nextId }; pawn.inventory.innerContainer.TryAdd(thing); return thing;
    }
    private static JobDriver_SSCPnaMaintenance Start(Pawn pawn, Job job)
    {
        var driver = (JobDriver_SSCPnaMaintenance)job.MakeDriver(pawn); pawn.jobs.curJob = job; pawn.jobs.curDriver = driver;
        driver.Notify_Starting(); return driver;
    }
    private static SSCPnaMaintenanceContext Context(JobDriver_SSCPnaMaintenance driver) => (SSCPnaMaintenanceContext)AccessTools.Field(typeof(JobDriver_SSCPnaMaintenance), "context").GetValue(driver);
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Set(object obj, string name, object value) => AccessTools.Field(obj.GetType(), name).SetValue(obj, value);
    private static void SetStatic(Type type, string name, object value) => AccessTools.Field(type, name).SetValue(null, value);
    private static void Require(bool value) { if (!value) throw new Exception("assertion failed"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, actual {actual}"); }
    private static void Near(float expected, float actual) { if (Math.Abs(expected - actual) > .0001f) throw new Exception($"expected {expected}, actual {actual}"); }
    private static void Check(string name, Action action)
    {
        if (!SscTestProfile.ShouldRun(name)) return; errors.Clear();
        try { action(); Require(errors.Count == 0); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex + "\n" + string.Join('\n', errors)); Scribe.ForceStop(); }
    }
    private static bool Skip() => false;
    private static bool NoTexture(ref Texture2D __result) { __result = null; return false; }
    private static bool Translate(string key, ref TaggedString __result) { __result = key; return false; }
    private static bool SavePath(ref string __result) { __result = saves; return false; }
    private static bool Maps(ref List<Map> __result) { __result = maps; return false; }
    private static bool Player(ref Faction __result) { __result = player; return false; }
    private static bool EverWork(ref bool __result) { __result = canWork; return false; }
    private static bool OrdinaryWork(ref ThinkResult __result) { workCalls++; __result = ThinkResult.NoJob; return false; }
    private static bool CancelBoundary(Pawn_JobTracker __instance) { cancellations++; __instance.curJob = null; __instance.curDriver = null; return false; }
    private static bool NoMapDrug(ref Thing __result) { searchCalls++; __result = null; return false; }
    private static bool ChewSpeed(ref float __result) { __result = 1f; return false; }
    private static bool FinalizeBoundary(Pawn ingester, ref Toil __result)
    {
        __result = ToilMaker.MakeToil(nameof(Toils_Ingest.FinalizeIngest)); __result.defaultCompleteMode = ToilCompleteMode.Instant;
        __result.initAction = () => { ingestions++; Load(ingester, .4f); }; return false;
    }
    private static bool LogError(string text) { errors.Add(text); return false; }
}
