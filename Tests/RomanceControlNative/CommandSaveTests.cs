// AI 辅助生成：真实 Scribe 读写命令组件及原 Job；外部 Pawn 作为已恢复世界实体登记。
using HarmonyLib;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>任务状态与原 Job 的实际序列化往返，不以普通对象复制冒充跨引用恢复。</summary>
    private static void CommandSaveTests()
    {
        Check("batch3 actual Pawn_JobTracker and driver Scribe restore pending progress without restarting", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
            var originalDriver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
            Require((bool)AccessTools.Method(originalDriver.GetType(), "EnsureClaim").Invoke(originalDriver, null));
            AccessTools.Field(originalDriver.GetType(), "waitTicks").SetValue(originalDriver, 333);
            string path = Path.Combine(saveDirectory, "romance-tracker-" + ++id + ".xml");
            var fixture = new RomanceTrackerFixture(SSCRomanceCommandGameComponent.Current, a);
            Scribe.saver.InitSaving(path, "TrackerFixture"); Scribe_Deep.Look(ref fixture, "scene"); Scribe.saver.FinalizeSaving();
            var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(Current.Game);
            components.RemoveAll(c => c is SSCRomanceCommandGameComponent); var store = new SSCRomanceCommandGameComponent(Current.Game); components.Add(store);
            a.jobs = new Pawn_JobTracker(a); fixture = null; Scribe.loader.InitLoading(path);
            var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
            foreach (var p in new[] { a, b, SSCBondUtility.GetBoundMaster(a) }) directory.RegisterLoaded(p);
            Scribe_Deep.Look(ref fixture, "scene", store, a); Scribe.loader.FinalizeLoading(); store.ReconcileAfterLoad();
            var loadedDriver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
            Require(loadedDriver != originalDriver); Equal(333, (int)AccessTools.Field(loadedDriver.GetType(), "waitTicks").GetValue(loadedDriver));
            Equal(a, loadedDriver.pawn); Equal(a.CurJob, loadedDriver.job); loadedDriver.ExecuteOnce(); Equal(1, bubbleCalls);
        });
        foreach (bool queued in new[] { false, true })
            Check("batch3 actual Scribe original current/queued Job resumes once " + queued, () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance); if (queued) scheduleMode = ScheduleMode.Queue;
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
                var job = queued ? a.jobs.jobQueue.Dequeue().job : a.CurJob;
                string path = SaveCommand(SSCRomanceCommandGameComponent.Current, job);
                var loaded = LoadCommand(path, a, b, out var store); var driver = BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
                Equal(1, store.Records.Count); Equal(SSCRomanceCommandStatus.Pending, store.Records[0].Status);
                Require(!ReferenceEquals(job, loaded.Job)); Equal(job.loadID, loaded.Job.loadID);
                driver.ExecuteOnce(); Equal(SSCRomanceCommandStatus.Completed, store.Records[0].Status); Equal(1, bubbleCalls);
                driver.ExecuteOnce(); Equal(1, bubbleCalls);
            });
        Check("batch3 actual Scribe claimed in-flight Job reclaims with fresh driver", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
            Require(SSCRomanceCommandGameComponent.Current.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) != null);
            string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
            var loaded = LoadCommand(path, a, b, out var store); var driver = BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
            driver.ExecuteOnce(); Equal(SSCRomanceCommandStatus.Completed, store.Records[0].Status); Equal(1, bubbleCalls);
        });
        Check("batch3 actual Scribe completed Job cannot replay native relationship or feedback", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance); SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
            string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
            var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
            Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null); Equal(SSCRomanceCommandStatus.Completed, store.Records[0].Status);
            Equal(1, bubbleCalls); Equal(1, commandFeedback);
        });
        foreach (string field in new[] { "version", "jobId", "requestId", "stamp" })
            Check("batch3 actual Scribe incomplete command record is rejected " + field, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
                Edit(path, doc => doc.Descendants("sscRomanceCommands").Elements("li").Elements(field).Remove());
                var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
                Require(store.Records.Count == 0 || store.Records.All(r => r.Terminal)); Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null); Equal(0, bubbleCalls);
            });
        Check("batch3 actual Scribe executing state is indeterminate and never retried", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
            string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
            Edit(path, doc => doc.Descendants("sscRomanceCommands").Elements("li").Elements("status").Single().Value = "Executing");
            var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
            Equal(SSCRomanceCommandStatus.Indeterminate, store.Records[0].Status); Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null);
        });
        Check("batch3 actual Scribe cancelled target personality does not revive in same body", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _)); ChangeCommandContext(a, b, "targetPersonality");
            string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
            var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
            Equal(SSCRomanceCommandStatus.Cancelled, store.Records[0].Status); Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null);
        });
        Check("batch3 duplicate load IDs after actual Scribe cannot claim either cloned Job", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
            string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
            var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); a.jobs.jobQueue.EnqueueLast(loaded.Job.Clone()); store.ReconcileAfterLoad();
            Equal(SSCRomanceCommandStatus.Cancelled, store.Records[0].Status); Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null);
        });
        Check("batch3 new clone initialization cancels command without resetting the source", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); Comp(a).romanceConfig = Denied();
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            var clone = SocialSlave(); SSCRomanceLifecycle.ResetNewClone(clone);
            Equal(SSCRomanceCommandStatus.Pending, r.Status); Require(Comp(a).romanceConfig.alwaysAcceptMaster); Require(Comp(clone).romanceConfig == null);
            SSCRomanceLifecycle.ResetNewClone(a); Equal(SSCRomanceCommandStatus.Cancelled, r.Status);
        });
    }
    /// <summary>保存本批组件与实际 Job，路径独立于玩家真实存档。</summary>
    private static string SaveCommand(SSCRomanceCommandGameComponent store, Job job)
    {
        string path = Path.Combine(saveDirectory, "romance-command-" + ++id + ".xml");
        var fixture = new RomanceCommandFixture(store) { Job = job };
        Scribe.saver.InitSaving(path, "CommandFixture"); Scribe_Deep.Look(ref fixture, "scene"); Scribe.saver.FinalizeSaving(); return path;
    }
    /// <summary>原生跨引用恢复后，调用方把已恢复 Job 交给驱动再执行生产恢复出口。</summary>
    private static RomanceCommandFixture LoadCommand(string path, Pawn a, Pawn b, out SSCRomanceCommandGameComponent store)
    {
        var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(Current.Game);
        components.RemoveAll(c => c is SSCRomanceCommandGameComponent); store = new SSCRomanceCommandGameComponent(Current.Game); components.Add(store);
        RomanceCommandFixture fixture = null; Scribe.loader.InitLoading(path);
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        foreach (var pawn in new[] { a, b, SSCBondUtility.GetBoundMaster(a) }.Distinct()) directory.RegisterLoaded(pawn);
        Scribe_Deep.Look(ref fixture, "scene", store); Scribe.loader.FinalizeLoading(); return fixture;
    }
}
public sealed class RomanceTrackerFixture : IExposable
{
    private readonly SSCRomanceCommandGameComponent store;
    private readonly Pawn actor;
    /// <summary>将已加载的外部角色及本存档组件注入，恢复过程直接执行原 Pawn_JobTracker。</summary>
    public RomanceTrackerFixture(SSCRomanceCommandGameComponent store, Pawn actor) { this.store = store; this.actor = actor; }
    /// <summary>原 tracker 保存/恢复当前任务、驱动和队列，生产驱动自行保存归属与等待进度。</summary>
    public void ExposeData() { store.ExposeData(); actor.jobs.ExposeData(); }
}
public sealed class RomanceCommandFixture : IExposable
{
    private readonly SSCRomanceCommandGameComponent store;
    public Job Job;
    /// <summary>载体由独立世界提供，实际组件保存入口由生产代码完成。</summary>
    public RomanceCommandFixture(SSCRomanceCommandGameComponent store) { this.store = store; }
    /// <summary>原生组件记录与 Job 各自序列化，避免用驱动私有字段副本替代任务保存。</summary>
    public void ExposeData() { store.ExposeData(); Scribe_Deep.Look(ref Job, "job"); }
}
