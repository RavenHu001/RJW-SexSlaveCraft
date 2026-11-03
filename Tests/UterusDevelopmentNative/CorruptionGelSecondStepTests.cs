// AI 辅助生成：实际属性/需求/高潮/原生Scribe；只隔离声效等整场副作用，不仿写事件或催熟算法。
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static JobDef gelJob;
    private static Action gelOrgasmCallback;

    private static void GelSecondStepTests(string repo, Harmony harmony)
    {
        gelJob = new JobDef { defName = "GelSceneFixture", driverClass = typeof(SSCGelTestDriver) };
        DefDatabase<JobDef>.Add(gelJob);
        foreach (var type in new[] { typeof(SSCCorruptionGelOrgasmPatch), typeof(SSCCorruptionGelSceneSavePatch),
            typeof(SSCCorruptionGelSceneEndPatch), typeof(SSCCorruptionGelAbilityLifecycle),
            typeof(SSCCorruptionGelHealthLifecycle), typeof(SSCCorruptionGelMutationLifecycle),
            typeof(SSCCorruptionGelHealthAdded), typeof(SSCCorruptionGelHealthRemoved),
            typeof(SSCCorruptionGelSpawnLifecycle), typeof(SSCCorruptionGelDespawnLifecycle) })
            harmony.CreateClassProcessor(type).Patch();
        // 共存实际原始推进观察器；本分区不假造计时样本，也不改变它的排序契约。
        string game = Path.GetDirectoryName(typeof(Pawn).Assembly.Location);
        System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "Mods/RJW_动画_2.0核心框架/1.6/Assemblies/Rimworld-Animations.dll"));
        System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "Mods/RJW_动画_UAP终极动画包/Assemblies/UAP_Animations.dll"));
        SSCOrgasmProgressPatches.Install();
        Require(SSCOrgasmProgressPatches.RecoveryReady, SSCOrgasmProgressPatches.Describe());
        // 本机原版此Stat默认不可缓存；显式覆盖可缓存配置，沿原生API检验实际失效通知。
        var work = DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"); bool previousCacheable = work.cacheable;
        work.cacheable = true; work.Worker.SetCacheability(false);
        // 实际Orgasm继续执行计数与Setup；只隔离声效、满足度等宿主没有的完整世界服务。
        var sideEffects = new[] { AccessTools.Method(typeof(JobDriver_Sex), "PlayCumSound"),
            AccessTools.Method(typeof(JobDriver_Sex), "PlayOrgasmVoice"), AccessTools.Method(typeof(JobDriver_Sex), "CalculateSatisfactionPerTick"),
            AccessTools.Method(typeof(SexUtility), "SatisfyPersonal") };
        foreach (var method in sideEffects) harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(sideEffects[0], prefix: new HarmonyMethod(typeof(Program), nameof(GelOrgasmSideEffect)));
        var deliver = AccessTools.Method(typeof(HediffComp_SSCCorruptionGelProduction), "TryDeliverProduct");
        harmony.Patch(deliver, prefix: new HarmonyMethod(typeof(Program), nameof(GelDeliveryBoundary)));
        try
        {
            Check("gel producing cost clears actual stat cache at toggle, location and full-batch transitions", () =>
            {
                var p = GelPawn(); var comp = GelComp(p);
                Near(1, GelWork(p));
                var workCache = (System.Collections.IDictionary)AccessTools.Field(typeof(StatWorker), "temporaryStatCache")
                    .GetValue(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal").Worker);
                Require(workCache?.Contains(p) == true, "work-speed check must populate the real temporary stat cache");
                GelToggle(p, true); Near(.9f, GelWork(p)); Near(2, comp.GetDecayMultiplier());
                GelToggle(p, false); Near(1, GelWork(p)); Near(1, comp.GetDecayMultiplier());
                GelToggle(p, true); gelLocation = false; GelTick(comp, 1); Near(1, GelWork(p)); Near(1, comp.GetDecayMultiplier());
                gelLocation = true; GelTick(comp, 1); Near(.9f, GelWork(p));
                gelAccept = false; GelTick(comp, 120000); Near(1, GelWork(p)); Near(1, comp.GetDecayMultiplier());
                // 完成品交付不能被之后的零生产速率阻断；代价仍因无法继续积累而暂停。
                float rate = comp.Props.progressPerDay;
                try
                {
                    comp.Props.progressPerDay = 0; gelAccept = true; GelTick(comp, 250);
                    Near(0, (float)comp.BatchProgress); Near(1, GelWork(p)); Near(1, comp.GetDecayMultiplier());
                }
                finally { comp.Props.progressPerDay = rate; }
                GelTick(comp, 1); Near(.9f, GelWork(p)); Near(2, comp.GetDecayMultiplier());
            });
            Check("gel health and mutation lifecycle immediately suspend and restore work cost", () =>
            {
                var p = GelPawn(); GelToggle(p, true); Near(.9f, GelWork(p));
                var organ = p.health.hediffSet.hediffs.First(h => h.def == gelOrgan);
                p.health.RemoveHediff(organ); Near(1, GelWork(p));
                p.health.AddHediff(gelOrgan, human.race.body.corePart); Near(.9f, GelWork(p));
                p.GetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
                SSCAbilityToggleUtility.SyncMarkers(p); Near(1, GelWork(p));
                p.GetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Slave;
                SSCAbilityToggleUtility.SyncMarkers(p); Near(.9f, GelWork(p));
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var mutation)); Near(1, GelWork(p));
                mutation.Dispose(); Near(.9f, GelWork(p));
                p.health.RemoveHediff(GelComp(p).parent); Near(1, GelWork(p));
            });
            GelDecayTests();
            Check("gel actual Orgasm counts only actor success and first eligible scene event", () =>
            {
                var d = GelDriver(); var comp = GelComp(d.pawn); var partner = GelPawn(); GelToggle(partner, true);
                // 必须接上真实Partner及SexProps，不能用未关联的第三人冒充伴侣归属边界。
                d.PartnerPawn = partner; d.Sexprops.partner = partner; d.job.targetA = partner;
                Require(ReferenceEquals(d.Partner, partner));
                d.sex_ticks = 100; d.Orgasm(); Equal(0, d.orgasms); Near(0, (float)comp.BatchProgress);
                GelFire(d); Equal(1, d.orgasms); Near(.1f, (float)comp.BatchProgress); Near(0, (float)GelComp(partner).BatchProgress);
                GelFire(d); Equal(2, d.orgasms); Near(.1f, (float)comp.BatchProgress);
                GelToggle(d.pawn, false); GelToggle(d.pawn, true); GelFire(d); Near(.1f, (float)comp.BatchProgress);
            });
            Check("gel first eligible full-batch orgasm consumes chance without banking after delivery", () =>
            {
                var d = GelDriver(); var comp = GelComp(d.pawn); gelAccept = false; GelTick(comp, 120000);
                GelFire(d); Near(1, (float)comp.BatchProgress);
                gelAccept = true; GelTick(comp, 250); Near(0, (float)comp.BatchProgress);
                GelFire(d); Near(0, (float)comp.BatchProgress);
                var next = GelDriver(d.pawn); GelFire(next); Near(.1f, (float)comp.BatchProgress);
            });
            Check("gel ineligible orgasm does not consume chance and boost caps at one batch", () =>
            {
                var d = GelDriver(); var comp = GelComp(d.pawn); GelToggle(d.pawn, false); GelFire(d);
                GelToggle(d.pawn, true); gelLocation = false; GelFire(d); Near(0, (float)comp.BatchProgress);
                gelLocation = true; GelTick(comp, 114000); Near(.95f, (float)comp.BatchProgress);
                GelFire(d); Near(1, (float)comp.BatchProgress); Near(1, GelWork(d.pawn));
                GelFire(d); Near(1, (float)comp.BatchProgress);
            });
            Check("gel claimed scene survives producer replacement and cannot claim on new body owner", () =>
            {
                var d = GelDriver(); GelFire(d); d.pawn.health.RemoveHediff(GelComp(d.pawn).parent);
                SSCCorruptionGelProductionUtility.EnsureProducer(d.pawn); GelToggle(d.pawn, true); GelFire(d);
                Near(0, (float)GelComp(d.pawn).BatchProgress);
            });
            Check("gel actual orgasm callbacks cannot reward replaced owner or personality", () =>
            {
                var d = GelDriver(); var old = GelComp(d.pawn);
                gelOrgasmCallback = () => { d.pawn.health.RemoveHediff(old.parent); SSCCorruptionGelProductionUtility.EnsureProducer(d.pawn); GelToggle(d.pawn, true); };
                GelFire(d); Near(0, (float)old.BatchProgress); Near(0, (float)GelComp(d.pawn).BatchProgress);
                gelOrgasmCallback = () => { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(d.pawn, out var scope)); scope.Dispose(); };
                GelFire(d); Near(0, (float)GelComp(d.pawn).BatchProgress);
                GelFire(d); Near(.1f, (float)GelComp(d.pawn).BatchProgress);
            });
            Check("gel nested and exceptional real Orgasm callbacks do not duplicate or prematurely claim", () =>
            {
                var d = GelDriver(); gelOrgasmCallback = () => GelFire(d); GelFire(d);
                Equal(2, d.orgasms); Near(.1f, (float)GelComp(d.pawn).BatchProgress);
                var failed = GelDriver(); gelOrgasmCallback = () => throw new InvalidOperationException("gel scene fixture exception");
                bool threw = false; try { GelFire(failed); } catch (InvalidOperationException) { threw = true; }
                Require(threw); Near(0, (float)GelComp(failed.pawn).BatchProgress);
                GelFire(failed); Near(.1f, (float)GelComp(failed.pawn).BatchProgress);
            });
            Check("gel Cleanup blocks first late orgasm even when scene has no prior state", () =>
            {
                var d = GelDriver(); var cleanup = AccessTools.Method(typeof(JobDriver), nameof(JobDriver.Cleanup));
                harmony.Patch(cleanup, prefix: new HarmonyMethod(typeof(Program), nameof(GelCleanupBoundary)) { priority = Priority.Last });
                try { d.Cleanup(JobCondition.Succeeded); GelFire(d); Near(0, (float)GelComp(d.pawn).BatchProgress); }
                finally { harmony.Unpatch(cleanup, AccessTools.Method(typeof(Program), nameof(GelCleanupBoundary))); }
            });
            GelSceneSaveTests(harmony);
        }
        finally
        {
            gelOrgasmCallback = null; gelAccept = gelLocation = true;
            work.cacheable = previousCacheable; work.Worker.DeleteStatCache(); work.Worker.SetCacheability(work.immutable);
            foreach (var method in sideEffects) harmony.Unpatch(method, AccessTools.Method(typeof(Program), nameof(Skip)));
            harmony.Unpatch(sideEffects[0], AccessTools.Method(typeof(Program), nameof(GelOrgasmSideEffect)));
            harmony.Unpatch(deliver, AccessTools.Method(typeof(Program), nameof(GelDeliveryBoundary)));
        }
    }

    private static float GelWork(Pawn p) => DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal").Worker.GetValue(p, cacheStaleAfterTicks: 600);
    private static SSCGelTestDriver GelDriver(Pawn pawn = null)
    {
        pawn ??= GelPawn(); GelToggle(pawn, true); pawn.jobs ??= new Pawn_JobTracker(pawn);
        var driver = new SSCGelTestDriver { pawn = pawn, job = new Job { def = gelJob, loadID = ++nextId },
            Sexprops = new SexProps(pawn, pawn) { usedCondom = true }, orgasmstick = 1, sex_ticks = 100 };
        pawn.jobs.curDriver = driver; pawn.jobs.curJob = driver.job; return driver;
    }
    private static void GelFire(JobDriver_Sex driver) { driver.sex_ticks = 0; driver.Orgasm(); }
    private static void GelOrgasmSideEffect() { var callback = gelOrgasmCallback; gelOrgasmCallback = null; callback?.Invoke(); }
    private static bool GelCleanupBoundary(JobDriver __instance) { GelFire((JobDriver_Sex)__instance); return false; }

    private static void GelDecayTests()
    {
        Check("gel native NeedInterval multiplies natural decay and preserves global off, zero and old scoring", () =>
        {
            var previous = SSCMod.settings;
            try
            {
                SSCMod.settings = new SSCSettings { corruptionDecayPerDay = .2f };
                var p = GelPawn(); var need = new GelTestCorruption(p); var list = p.needs.AllNeeds; list.Add(need);
                need.SetCorruption(.1f); need.NeedInterval(); float normal = .1f - need.CurLevel;
                Require(normal > 0); GelToggle(p, true); need.SetCorruption(.1f); need.NeedInterval();
                Require(Math.Abs(normal * 2 - (.1f - need.CurLevel)) < 0.0000001f, "active decay must double the baseline");
                SSCMod.settings.enableCorruptionDecay = false; need.SetCorruption(.1f); need.NeedInterval(); Near(.1f, need.CurLevel);
                SSCMod.settings.enableCorruptionDecay = true; SSCMod.settings.corruptionDecayPerDay = 0; need.NeedInterval(); Near(.1f, need.CurLevel);
                SSCMod.settings.corruptionDecayPerDay = .2f; SSCMod.settings.useOldScoring = true; need.NeedInterval(); Near(.1f, need.CurLevel);
                Require(GelComp(p).ProductionEnabled && GelComp(p).IsProducing);
            }
            finally { SSCMod.settings = previous; }
        });
        Check("gel native decay keeps existing protection floor and historical corruption", () =>
        {
            var previous = SSCMod.settings;
            try
            {
                SSCMod.settings = new SSCSettings(); var p = GelPawn(); GelToggle(p, true);
                var floorDef = new HediffDef { defName = "GelFloor" + ++nextId, hediffClass = typeof(HediffWithComps), initialSeverity = 1,
                    comps = new() { new HediffCompProperties_CorruptionModifier { decayMultiplier = 1, minCorruptionLevel = .1f } } };
                DefDatabase<HediffDef>.Add(floorDef); p.health.AddHediff(floorDef);
                var need = new GelTestCorruption(p); p.needs.AllNeeds.Add(need); need.SetCorruption(.1f); need.NeedInterval();
                Near(.1f, need.CurLevel); Near(.1f, need.HighestCorruptionLevel); Near(0, GelComp(p).GetMinCorruption());
            }
            finally { SSCMod.settings = previous; }
        });
    }

    private static void GelSceneSaveTests(Harmony harmony)
    {
        var baseExpose = AccessTools.Method(typeof(JobDriver), nameof(JobDriver.ExposeData));
        bool alreadySkipped = Harmony.GetPatchInfo(baseExpose)?.Prefixes.Any(p => p.PatchMethod == AccessTools.Method(typeof(Program), nameof(Skip))) == true;
        if (!alreadySkipped) harmony.Patch(baseExpose, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        var typeLookup = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly));
        // 同一映射已由早期真实存档分区安装时只借用，不在结束后撤掉原边界。
        bool hadRjwTypeBoundary = Harmony.GetPatchInfo(typeLookup)?.Prefixes.Any(p => p.PatchMethod == AccessTools.Method(typeof(Program), nameof(RjwScribeTypeBoundary))) == true;
        if (!hadRjwTypeBoundary) harmony.Patch(typeLookup, prefix: new HarmonyMethod(typeof(Program), nameof(RjwScribeTypeBoundary)));
        try
        {
            Check("gel native RJW Scribe restores claimed scene onto new driver without repeat boost", () =>
            {
                var d = GelDriver(); GelFire(d); string path = GelSave(d);
                Require(XDocument.Load(path).Descendants("sscCorruptionGelScene").Any());
                var restored = GelLoad(path, d.pawn); Require(!ReferenceEquals(d, restored));
                GelFire(restored); Near(.1f, (float)GelComp(d.pawn).BatchProgress);
                restored = GelLoad(GelSave(restored), d.pawn); GelFire(restored); Near(.1f, (float)GelComp(d.pawn).BatchProgress);
            });
            Check("gel Scribe old missing scene allows first claim while mismatched saved job fails closed", () =>
            {
                var d = GelDriver(); GelFire(d); string path = GelSave(d);
                Edit(path, xml => xml.Descendants("jobId").Single().Value = "-99");
                GelFire(GelLoad(path, d.pawn)); Near(.1f, (float)GelComp(d.pawn).BatchProgress);
                Edit(path, xml => xml.Descendants("sscCorruptionGelScene").Remove());
                var old = GelLoad(path, d.pawn); GelFire(old); Near(.2f, (float)GelComp(d.pawn).BatchProgress);
                GelFire(old); Near(.2f, (float)GelComp(d.pawn).BatchProgress);
                var ended = GelDriver(d.pawn); SSCCorruptionGelSceneRuntime.Finish(ended);
                GelFire(GelLoad(GelSave(ended), d.pawn)); Near(.2f, (float)GelComp(d.pawn).BatchProgress);
            });
        }
        finally
        {
            if (!alreadySkipped) harmony.Unpatch(baseExpose, AccessTools.Method(typeof(Program), nameof(Skip)));
            if (!hadRjwTypeBoundary) harmony.Unpatch(typeLookup, AccessTools.Method(typeof(Program), nameof(RjwScribeTypeBoundary)));
        }
    }
    private static string GelSave(JobDriver_Sex driver)
    {
        var node = new GelSceneFixture(driver); string path = Path.Combine(saveDirectory, "gel-scene-" + ++nextId + ".xml");
        Scribe.saver.InitSaving(path, "GelScene"); Scribe_Deep.Look(ref node, "scene"); Scribe.saver.FinalizeSaving(); return path;
    }
    private static JobDriver_Sex GelLoad(string path, Pawn pawn)
    {
        GelSceneFixture node = null; Scribe.loader.InitLoading(path);
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        directory.RegisterLoaded(pawn);
        Scribe_Deep.Look(ref node, "scene", pawn); Scribe.loader.FinalizeLoading(); return node.Driver;
    }
}

// 只排除睡眠／冻结这个外部世界输入；NeedInterval、修正收集、底线和历史记录全部执行生产代码。
public sealed class GelTestCorruption : SexSlaveCraft.Need_Corruption
{
    public GelTestCorruption(Pawn pawn) : base(pawn) { }
    protected override bool IsFrozen => false;
}
public sealed class SSCGelTestDriver : JobDriver_Sex
{
    protected override void SetupOrgasmTicks() { sex_ticks = orgasmStartTick = 100; }
    protected override IEnumerable<Toil> MakeNewToils() { yield return new Toil(); }
}
public sealed class GelSceneFixture : IExposable
{
    internal JobDriver_Sex Driver;
    private Job job;
    private readonly Pawn pawn;
    public GelSceneFixture(Pawn pawn) { this.pawn = pawn; }
    public GelSceneFixture(JobDriver_Sex driver) { Driver = driver; job = driver.job; pawn = driver.pawn; }
    public void ExposeData()
    {
        Scribe_Deep.Look(ref job, "job"); Scribe_Deep.Look(ref Driver, "driver");
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Driver.pawn = pawn; Driver.job = job; pawn.jobs.curDriver = Driver; pawn.jobs.curJob = job;
        }
    }
}
