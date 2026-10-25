// AI 辅助生成：重建全部参与者的场景级 Scribe，不以原内存 Pawn 代替跨引用恢复。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static List<Pawn> batch4RestoredPawns;
    /// <summary>联合保存新角色实例、真实训练/健康/关系/记忆/任务组件，并执行完整生产初始化出口。</summary>
    private static void Batch4SceneSaveTests()
    {
        var boundary = new Harmony("ssc.tests.romance.batch4.scene");
        boundary.Patch(AccessTools.Method(typeof(SSCRestrictionGameComponent), nameof(SSCRestrictionGameComponent.AllPawns)),
            prefix: new HarmonyMethod(typeof(Program), nameof(ScenePawnSnapshot)));
        // 旧性行为模板在其既有专项中验证；这里隔离缺少完整 Def 库的旧初始化，保留浪漫完整恢复顺序。
        boundary.Patch(AccessTools.Method(typeof(SSCRestrictionLifecycle), nameof(SSCRestrictionLifecycle.Refresh)), prefix: new HarmonyMethod(typeof(Program), nameof(SceneLegacyRefresh)));
        boundary.CreateClassProcessor(production.GetType("SexSlaveCraft.SSCRestrictionCloneHook")).Patch();
        try
        {
            foreach (var action in Enum.GetValues<SSCRomanceAction>())
            foreach (bool queued in new[] { false, true })
                Check($"batch4 rebuilt whole scene resumes exactly original current/queued Job {action}/{queued}", () =>
                {
                    CommandReset(0f); var (a, b) = CommandPair(action); if (queued) scheduleMode = ScheduleMode.Queue;
                    Comp(a).romanceConfig = Denied();
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, action), out _, out _));
                    var originalJob = queued ? a.jobs.jobQueue.First().job : a.CurJob;
                    string path = SaveBatch4Scene(new Batch4Scene(a, b, SSCBondUtility.GetBoundMaster(a)));
                    var restored = LoadBatch4Scene(path);
                    Require(restored.Actor != a && restored.Target != b && restored.Master != SSCBondUtility.GetBoundMaster(a));
                    Require(ReferenceEquals(restored.Master, SSCBondUtility.GetBoundMaster(restored.Actor)));
                    var job = queued ? restored.Actor.jobs.jobQueue.Dequeue().job : restored.Actor.CurJob;
                    Require(job != originalJob); Equal(originalJob.loadID, job.loadID);
                    Equal(restored.Target, job.targetA.Pawn); Require(!Comp(restored.Actor).romanceConfig.allowInitiateRomance);
                    var driver = queued ? BindCommand(restored.Actor, job) : (JobDriver_SSCRomanceCommand)restored.Actor.jobs.curDriver;
                    driver.ExecuteOnce(); driver.ExecuteOnce(); Equal(1, bubbleCalls);
                    Equal(SSCRomanceCommandStatus.Completed, restored.Store.Records.Single().Status);
                });
            foreach (string change in new[] { "master", "chain", "stage", "personality", "targetPersonality", "externalHollow", "interrupt" })
                Check("batch4 rebuilt scene cannot revive cancelled original request " + change, () =>
                {
                    CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                    if (change == "externalHollow")
                    { var shell = new Hediff { pawn = b, def = SSCDefOf.SSC_PersonalityExcreted_Done }; b.health.AddHediff(shell); b.health.RemoveHediff(shell); }
                    else if (change == "interrupt")
                    { var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver); driver.Cleanup(JobCondition.InterruptForced); }
                    else ChangeCommandContext(a, b, change);
                    Require(record.Terminal);
                    var restored = LoadBatch4Scene(SaveBatch4Scene(new Batch4Scene(a, b, SSCBondUtility.GetBoundMaster(a))));
                    Equal(SSCRomanceCommandStatus.Cancelled, restored.Store.Records.Single().Status);
                    captureEnd = true; ((JobDriver_SSCRomanceCommand)restored.Actor.jobs.curDriver).ExecuteOnce();
                    var again = LoadBatch4Scene(SaveBatch4Scene(restored));
                    ((JobDriver_SSCRomanceCommand)again.Actor.jobs.curDriver).ExecuteOnce(); Equal(0, bubbleCalls);
                });
            foreach (bool sceneEntry in new[] { false, true })
                Check("batch4 extraction processed snapshot scene rebuild insertion never transfers old command " + sceneEntry, () =>
                {
                    CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); PutOnTransferMap(a); Comp(a).romanceConfig = Denied();
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                    ExtractForTransfer(a, sceneEntry); var raw = transferProduct;
                    new Recipe_PSTagWorker { recipe = new RecipeDef { defName = "RomanceTransferRecipe" } }.Notify_IterationCompleted(a, new() { raw });
                    var saved = new Batch4Scene(a, b, SSCBondUtility.GetBoundMaster(a)) { Gel = transferProduct.TryGetComp<CompPersonalityStore>() };
                    var restored = LoadBatch4Scene(SaveBatch4Scene(saved));
                    Equal(SSCRomanceCommandStatus.Cancelled, restored.Store.Records.Single().Status);
                    var receiver = SocialSlave(); Require(ExcretionUtility.InheritEverything(receiver, restored.Gel));
                    Require(!Comp(receiver).romanceConfig.allowAcceptProposal && Comp(receiver).romanceConfig.alwaysAcceptMaster);
                    Require(receiver.CurJob == null); Equal(restored.Master, SSCBondUtility.GetBoundMaster(receiver));
                    Equal(0, bubbleCalls); Require(!SSCPersonalityRuntimeUtility.IsMutating(receiver));
                });
            foreach (var action in Enum.GetValues<SSCRomanceAction>())
            foreach (bool rejected in action == SSCRomanceAction.Breakup ? new[] { false } : new[] { false, true })
                Check($"batch4 completed result relationship memories and feedback survive scene reload once {action}/{rejected}", () =>
                {
                    CommandReset(rejected ? .99f : 0f, .8f); var (a, b) = CommandPair(action);
                    SubmitAndRun(a, b, action, out var record);
                    int actorMemories = a.needs.mood.thoughts.memories.Memories.Count, targetMemories = b.needs.mood.thoughts.memories.Memories.Count;
                    var restored = LoadBatch4Scene(SaveBatch4Scene(new Batch4Scene(a, b, SSCBondUtility.GetBoundMaster(a))));
                    Equal(record.Outcome, restored.Store.Records.Single().Outcome);
                    ((JobDriver_SSCRomanceCommand)restored.Actor.jobs.curDriver).ExecuteOnce();
                    Equal(actorMemories, restored.Actor.needs.mood.thoughts.memories.Memories.Count);
                    Equal(targetMemories, restored.Target.needs.mood.thoughts.memories.Memories.Count);
                    var again = LoadBatch4Scene(SaveBatch4Scene(restored));
                    ((JobDriver_SSCRomanceCommand)again.Actor.jobs.curDriver).ExecuteOnce(); Equal(1, bubbleCalls); Equal(1, commandFeedback);
                });
            Check("batch4 existing hollow target baseline permits only its legitimate saved original Job", () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                b.health.hediffSet.hediffs.Add(new Hediff { pawn = b, def = SSCDefOf.SSC_PersonalityExcreted_Done, loadID = ++id });
                SSCRomanceLifecycle.InitializeAfterLoad(b);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                var restored = LoadBatch4Scene(SaveBatch4Scene(new Batch4Scene(a, b, SSCBondUtility.GetBoundMaster(a))));
                Equal(SSCRomanceCommandStatus.Pending, restored.Store.Records.Single().Status);
                SSCRomanceLifecycle.NotifyHollowChanged(restored.Target);
                ((JobDriver_SSCRomanceCommand)restored.Actor.jobs.curDriver).ExecuteOnce(); Equal(1, bubbleCalls);
            });
            Check("batch4 partial native settlement remains indeterminate across whole scene reload", () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                var throwing = new Harmony("ssc.tests.romance.batch4.scene-throw");
                throwing.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), postfix: new HarmonyMethod(typeof(Program), nameof(CommandThrow)));
                try { ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Require(false); }
                catch (InvalidOperationException) { }
                finally { throwing.UnpatchAll(throwing.Id); }
                Equal(SSCRomanceCommandStatus.Indeterminate, record.Status); Require(a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
                var restored = LoadBatch4Scene(SaveBatch4Scene(new Batch4Scene(a, b, SSCBondUtility.GetBoundMaster(a))));
                Equal(SSCRomanceCommandStatus.Indeterminate, restored.Store.Records.Single().Status);
                ((JobDriver_SSCRomanceCommand)restored.Actor.jobs.curDriver).ExecuteOnce(); Equal(0, bubbleCalls);
                Require(restored.Actor.relations.DirectRelationExists(PawnRelationDefOf.Lover, restored.Target));
            });
            foreach (bool matureBeforeLoad in new[] { false, true })
                Check("batch4 newborn clone health age source link and own choices survive full scene maturity " + matureBeforeLoad, () =>
                {
                    CommandReset();
                    var linkDef = RabbitCloneUtility.RabbitCloneLinkDef;
                    if (linkDef == null)
                    {
                        linkDef = new HediffDef { defName = "SSC_Hediff_RabbitCloneLink", hediffClass = typeof(HediffWithComps),
                            initialSeverity = .01f, maxSeverity = 1, comps = new() { new HediffCompProperties_RabbitCloneLink() } };
                        DefDatabase<HediffDef>.Add(linkDef);
                    }
                    var source = SocialSlave(); Comp(source).romanceConfig = Denied();
                    var clone = SocialSlave(SSCBondUtility.GetBoundMaster(source)); clone.jobs = new Pawn_JobTracker(clone);
                    AccessTools.Method(typeof(RabbitCloneUtility), "PrepareFreshRabbitCloneBody").Invoke(null, new object[] { source, clone });
                    foreach (var setting in new[] { SSCRomanceSetting.AcceptProposal, SSCRomanceSetting.InitiateOnlyMaster })
                    { var view = SSCRomancePreferences.GetView(clone, setting); Require(SSCRomancePreferences.TrySet(clone, setting, setting == SSCRomanceSetting.InitiateOnlyMaster, view.Context, out _)); }
                    var link = ((HediffWithComps)clone.health.AddHediff(linkDef)).TryGetComp<HediffComp_RabbitCloneLink>();
                    link.Initialize(source, clone.ageTracker.AgeBiologicalTicks);
                    if (matureBeforeLoad) RabbitCloneUtility.MatureRabbitClone(clone, source, link);
                    long savedAge = clone.ageTracker.AgeBiologicalTicks;
                    var restored = LoadBatch4Scene(SaveBatch4Scene(new Batch4Scene(clone, source, SSCBondUtility.GetBoundMaster(source))));
                    var restoredLink = RabbitCloneUtility.GetRabbitCloneLinkComp(restored.Actor);
                    Require(restoredLink != link && restoredLink.source == restored.Target); Equal(savedAge, restored.Actor.ageTracker.AgeBiologicalTicks);
                    Equal(matureBeforeLoad, restoredLink.mature); var own = Comp(restored.Actor).romanceConfig;
                    RabbitCloneUtility.MatureRabbitClone(restored.Actor, restored.Target, restoredLink);
                    RabbitCloneUtility.MatureRabbitClone(restored.Actor, restored.Target, restoredLink);
                    Require(ReferenceEquals(own, Comp(restored.Actor).romanceConfig)); Require(!own.allowAcceptProposal && own.initiateOnlyMaster && !own.alwaysAcceptMaster);
                    var again = LoadBatch4Scene(SaveBatch4Scene(restored));
                    Require(RabbitCloneUtility.GetRabbitCloneLinkComp(again.Actor).mature); Require(!Comp(again.Actor).romanceConfig.allowAcceptProposal);
                    Require(Comp(again.Target).romanceConfig.alwaysAcceptMaster);
                });
        }
        finally { batch4RestoredPawns = null; boundary.UnpatchAll(boundary.Id); }
    }
    /// <summary>提供明确的容器角色快照边界；不在宿主伪造地图、旅队或重力船引擎。</summary>
    private static bool ScenePawnSnapshot(ref List<Pawn> __result)
    { if (batch4RestoredPawns == null) return true; __result = batch4RestoredPawns; return false; }
    /// <summary>仅隔离旧性行为模板初始化，不替换浪漫恢复或任务认领。</summary>
    private static bool SceneLegacyRefresh(out SSCRestrictionResolution error, ref bool __result)
    { error = null; __result = false; return false; }
    /// <summary>把场景写入工作区独立文件，所有业务字段委托实际生产组件序列化。</summary>
    private static string SaveBatch4Scene(Batch4Scene scene)
    {
        // 原规则夹具的主人只需要身份和健康；场景保存再补齐其实际社交追踪器。
        if (scene.Master.relations == null) SocialPawn(scene.Master);
        foreach (var pawn in new[] { scene.Actor, scene.Target, scene.Master })
        {
            // 固定宿主未模拟的逆龄需求及成长点，年龄本身使用完整原版追踪器保存。
            pawn.ageTracker.growthPoints = 0;
            AccessTools.Field(typeof(Pawn_AgeTracker), "ageReversalDemandedAtAgeTicks").SetValue(pawn.ageTracker, 0L);
        }
        string path = Path.Combine(saveDirectory, "batch4-scene-" + ++id + ".xml");
        Scribe.saver.InitSaving(path, "RomanceScene"); Scribe_Deep.Look(ref scene, "scene"); Scribe.saver.FinalizeSaving(); return path;
    }
    /// <summary>完整引用恢复后从新实体快照进入原 FinalizeInit，再允许合法原 Job 取得新运行凭据。</summary>
    private static Batch4Scene LoadBatch4Scene(string path)
    {
        Batch4Scene scene = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref scene, "scene"); Scribe.loader.FinalizeLoading();
        batch4RestoredPawns = new() { scene.Actor, scene.Target, scene.Master };
        var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(Current.Game);
        components.RemoveAll(c => c is SSCRestrictionGameComponent); var restore = new SSCRestrictionGameComponent(Current.Game); components.Add(restore);
        restore.FinalizeInit(); Require(scene.Store.Ready); return scene;
    }
    /// <summary>创建当前定义下的真实 Pawn/追踪器，由场景在加载最初阶段赋回原持久身份。</summary>
    internal static Pawn NewScenePawn(int persistentId)
    { var pawn = SocialPawn(); pawn.thingIDNumber = persistentId; pawn.jobs = new Pawn_JobTracker(pawn); return pawn; }
}

public sealed class Batch4Scene : IExposable
{
    public Pawn Actor, Target, Master;
    public SSCRomanceCommandGameComponent Store;
    public CompPersonalityStore Gel = new();
    private int actorId, targetId, masterId;
    /// <summary>Scribe 建立空场景后，先恢复实体身份再解析全部引用。</summary>
    public Batch4Scene() { }
    /// <summary>固定保存时的实际参与者及唯一命令组件，未把命令搬入人格凝胶。</summary>
    public Batch4Scene(Pawn actor, Pawn target, Pawn master)
    { Actor = actor; Target = target; Master = master; actorId = actor.thingIDNumber; targetId = target.thingIDNumber; masterId = master.thingIDNumber; Store = SSCRomanceCommandGameComponent.Current; }
    /// <summary>新旧进程实例分离；训练、健康、社交和 Job 各自使用原序列化入口。</summary>
    public void ExposeData()
    {
        Scribe_Values.Look(ref actorId, "actorId"); Scribe_Values.Look(ref targetId, "targetId"); Scribe_Values.Look(ref masterId, "masterId");
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Actor = Program.NewScenePawn(actorId); Target = Program.NewScenePawn(targetId); Master = Program.NewScenePawn(masterId);
            var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
            foreach (var pawn in new[] { Actor, Target, Master }) directory.RegisterLoaded(pawn);
            var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(Current.Game);
            components.RemoveAll(c => c is SSCRomanceCommandGameComponent);
            Store = new SSCRomanceCommandGameComponent(Current.Game); components.Add(Store);
        }
        PawnData(Actor, "actor"); PawnData(Target, "target"); PawnData(Master, "master"); Store.ExposeData();
        Scribe.EnterNode("gel"); Gel.PostExposeData(); Scribe.ExitNode();
    }
    /// <summary>每个角色保留独立节点，健康 Set 恢复链引用，任务 Tracker 恢复实际驱动和排队顺序。</summary>
    private static void PawnData(Pawn pawn, string node)
    {
        Scribe.EnterNode(node);
        Scribe.EnterNode("training"); pawn.TryGetComp<CompSexSlaveTraining>().PostExposeData(); Scribe.ExitNode();
        Scribe.EnterNode("health"); pawn.health.hediffSet.ExposeData(); Scribe.ExitNode();
        Scribe.EnterNode("age"); pawn.ageTracker.ExposeData(); Scribe.ExitNode();
        Scribe.EnterNode("relations"); pawn.relations.ExposeData(); Scribe.ExitNode();
        Scribe.EnterNode("memories"); pawn.needs.mood.thoughts.memories.ExposeData(); Scribe.ExitNode();
        Scribe.EnterNode("jobs"); pawn.jobs?.ExposeData(); Scribe.ExitNode();
        Scribe.ExitNode();
    }
}
