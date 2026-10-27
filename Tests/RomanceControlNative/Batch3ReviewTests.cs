// AI 辅助生成：第三批提交前复核，运行期任务归属、终态退出及旧目标人格凭据回归。
using HarmonyLib;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>直接执行生产公共入口及原生 JobDriver 全局结束检查，避免只检查最终 worker 拒绝。</summary>
    private static void Batch3ReviewTests()
    {
        Check("batch3 review legitimate pending command remains ongoing through original approach check", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver); captureEnd = true;
            foreach (int repeat in new[] { 0, 1 })
                Require(!(bool)AccessTools.Method(typeof(JobDriver), "CheckCurrentToilEndOrFail").Invoke(driver, null));
            Require(commandEnd == null); Equal(SSCRomanceCommandStatus.Claimed, r.Status); Equal(0, bubbleCalls);
        });
        foreach (string change in new[] { "master", "stage", "targetPersonality" })
            Check("batch3 review cancelled command ends before continuing approach " + change, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
                PrepareReviewDriver(driver);
                Require((bool)AccessTools.Method(driver.GetType(), "EnsureClaim").Invoke(driver, null));
                ChangeCommandContext(a, b, change); Require(r.Terminal);
                captureEnd = true;
                Require((bool)AccessTools.Method(typeof(JobDriver), "CheckCurrentToilEndOrFail").Invoke(driver, null), "cancelled approach did not end");
                Equal<JobCondition?>(JobCondition.Incompletable, commandEnd); Equal(0, bubbleCalls);
            });
        Check("batch3 review completed command exits successfully before any restored approach", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance); SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
            var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver); captureEnd = true;
            Require((bool)AccessTools.Method(typeof(JobDriver), "CheckCurrentToilEndOrFail").Invoke(driver, null), "completed approach did not end");
            Equal<JobCondition?>(JobCondition.Succeeded, commandEnd); Equal(1, bubbleCalls);
        });
        foreach (bool replaceInteraction in new[] { false, true })
            Check("batch3 review claimed Job mutation is rejected before original approach " + replaceInteraction, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver);
                Require((bool)AccessTools.Method(driver.GetType(), "EnsureClaim").Invoke(driver, null));
                if (replaceInteraction) driver.job.interaction = InteractionDefOf.MarriageProposal;
                else driver.job.targetA = SocialPawn();
                captureEnd = true;
                Require((bool)AccessTools.Method(typeof(JobDriver), "CheckCurrentToilEndOrFail").Invoke(driver, null), "mutated Job did not end");
                Equal<JobCondition?>(JobCondition.Incompletable, commandEnd); Equal(SSCRomanceCommandStatus.Cancelled, r.Status); Equal(0, bubbleCalls);
            });
        foreach (SSCRomanceAction action in Enum.GetValues<SSCRomanceAction>())
            Check("batch3 review changing action cannot renew selected target personality " + action, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                var original = SSCRomanceCommandUtility.Capture(a, b);
                ChangeCommandContext(a, b, "targetPersonality");
                Equal("StaleContext", SSCRomanceCommandUtility.Failure(original));
                Equal("StaleContext", SSCRomanceCommandUtility.Failure(original.ForTarget(b, action)));
            });
        Check("batch3 review task-query callback cannot replace target before approach reads it", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver);
            Require((bool)AccessTools.Method(driver.GetType(), "EnsureClaim").Invoke(driver, null));
            taskDuringCheck = () => driver.job.targetA = SocialPawn(); captureEnd = true;
            Require((bool)AccessTools.Method(typeof(JobDriver), "CheckCurrentToilEndOrFail").Invoke(driver, null), "task-query mutation did not end");
            Equal<JobCondition?>(JobCondition.Incompletable, commandEnd); Equal(SSCRomanceCommandStatus.Cancelled, r.Status);
        });
        foreach (string corruption in new[] { "missing status", "unknown status", "future completed version" })
            Check("batch3 review incomplete terminal metadata remains explicitly unconfirmed " + corruption, () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                if (corruption == "future completed version") SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
                else Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
                Edit(path, doc =>
                {
                    var record = doc.Descendants("sscRomanceCommands").Elements("li").Single();
                    if (corruption == "missing status") record.Element("status").Remove();
                    else if (corruption == "unknown status") record.Element("status").Value = "987";
                    else record.Element("version").Value = "77";
                });
                var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
                var restored = store.Records.Single();
                Equal(SSCRomanceCommandStatus.Indeterminate, restored.Status); Equal(SSCRomanceCommandOutcome.Unconfirmed, restored.Outcome);
                Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null);
                Equal(corruption == "future completed version" ? 1 : 0, bubbleCalls);
            });
        foreach (bool completed in new[] { false, true })
            Check("batch3 review actual saved terminal Job exits before restored approach " + completed, () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                if (completed) SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
                else
                {
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                    ChangeCommandContext(a, b, "master");
                }
                string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
                var loaded = LoadCommand(path, a, b, out var store); var driver = BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
                PrepareReviewDriver(driver); captureEnd = true;
                Require((bool)AccessTools.Method(typeof(JobDriver), "CheckCurrentToilEndOrFail").Invoke(driver, null));
                Equal<JobCondition?>(completed ? JobCondition.Succeeded : JobCondition.Incompletable, commandEnd);
                Equal(completed ? 1 : 0, bubbleCalls);
            });
    }
    /// <summary>按原生恢复同样方式建立 toils，使被测方法执行真实全局结束条件；不执行地图移动。</summary>
    private static void PrepareReviewDriver(JobDriver_SSCRomanceCommand driver)
    {
        AccessTools.Method(typeof(JobDriver), "SetupToils").Invoke(driver, null);
        // 全局结束条件在寻路 toil 继续之前运行，当前索引固定到原驱动的接近阶段。
        AccessTools.Field(typeof(JobDriver), "curToilIndex").SetValue(driver, 1);
    }
}
