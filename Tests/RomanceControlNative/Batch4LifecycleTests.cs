// AI 辅助生成：真实健康事件、人格凭据及单次 Job 生命周期，不替代生产撤权逻辑。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>安装原有生产健康钩子，覆盖外部空壳、重复通知、加载基线及打断后不重试。</summary>
    private static void Batch4LifecycleTests(Harmony harmony)
    {
        foreach (string type in new[] { "SSCAbilityToggleHealthAdded", "SSCAbilityToggleHealthRemoved" })
            harmony.CreateClassProcessor(production.GetType("SexSlaveCraft." + type)).Patch();
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
        foreach (string role in new[] { "actor", "target", "targetWithoutComp" })
            Check($"batch4 external hollow cancels old {role} command and preview {action}", () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(action, true);
                if (role == "targetWithoutComp") AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(b, null);
                var preview = SSCRomanceCommandUtility.Capture(a, b, action);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, action), out var record, out _));
                var changed = role == "actor" ? a : b;
                var shell = new Hediff { pawn = changed, def = SSCDefOf.SSC_PersonalityExcreted_Done };
                changed.health.AddHediff(shell);
                Equal(SSCRomanceCommandStatus.Cancelled, record.Status);
                changed.health.RemoveHediff(shell);
                Equal("StaleContext", SSCRomanceCommandUtility.Failure(preview));
                ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce();
                Equal(0, bubbleCalls); Equal(SSCRomanceCommandOutcome.NotOccurred, record.Outcome);
            });
        Check("batch4 repeated hollow notifications preserve newly captured target context", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            var shell = new Hediff { pawn = b, def = SSCDefOf.SSC_PersonalityExcreted_Done }; b.health.AddHediff(shell);
            var current = SSCRomanceCommandUtility.Capture(a, b); object token = SSCPersonalityRuntimeUtility.Capture(b);
            for (int i = 0; i < 3; i++) SSCRomanceLifecycle.NotifyHollowChanged(b);
            Require(SSCPersonalityRuntimeUtility.IsCurrent(b, token)); Require(SSCRomanceCommandUtility.Failure(current) == null);
            b.health.RemoveHediff(shell); b.health.AddHediff(shell);
            Equal("StaleContext", SSCRomanceCommandUtility.Failure(current));
        });
        Check("batch4 hollow inside normal mutation does not invalidate its own restoration scope", () =>
        {
            var p = SocialSlave(); Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope));
            using (scope)
            {
                p.health.AddHediff(new Hediff { pawn = p, def = SSCDefOf.SSC_PersonalityExcreted_Done });
                Require(scope.IsCurrent); Require(SSCPersonalityRuntimeUtility.IsMutating(p));
            }
            Require(!SSCPersonalityRuntimeUtility.IsMutating(p));
        });
        Check("batch4 direct hollow list deletion notification arms the next real entry", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            var shell = new Hediff { pawn = b, def = SSCDefOf.SSC_PersonalityExcreted_Done }; b.health.AddHediff(shell);
            b.health.hediffSet.hediffs.Remove(shell); b.health.Notify_HediffChanged(shell);
            var preview = SSCRomanceCommandUtility.Capture(a, b);
            b.health.hediffSet.hediffs.Add(shell); b.health.Notify_HediffChanged(shell);
            Equal("StaleContext", SSCRomanceCommandUtility.Failure(preview));
        });
        Check("batch4 loading existing hollow establishes baseline without replacing target personality", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            b.health.hediffSet.hediffs.Add(new Hediff { pawn = b, def = SSCDefOf.SSC_PersonalityExcreted_Done });
            object token = SSCPersonalityRuntimeUtility.Capture(b);
            Scribe.mode = LoadSaveMode.PostLoadInit;
            try { SSCRomanceLifecycle.NotifyHollowChanged(b); SSCRomanceLifecycle.InvalidatePersonality(b); }
            finally { Scribe.mode = LoadSaveMode.Inactive; }
            SSCRomanceLifecycle.InitializeAfterLoad(b);
            var preview = SSCRomanceCommandUtility.Capture(a, b);
            SSCRomanceLifecycle.NotifyHollowChanged(b); SSCRomanceLifecycle.InitializeAfterLoad(b);
            Require(SSCPersonalityRuntimeUtility.IsCurrent(b, token)); Require(SSCRomanceCommandUtility.Failure(preview) == null);
        });
        foreach (bool claimed in new[] { false, true })
        foreach (var condition in new[] { JobCondition.InterruptForced, JobCondition.InterruptOptional, JobCondition.Incompletable })
            Check($"batch4 interrupted one-shot Job never retries claimed={claimed}/{condition}", () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver);
                if (claimed) Require(SSCRomanceCommandGameComponent.Current.Claim(driver) != null);
                driver.Cleanup(condition); driver.Cleanup(condition);
                Equal(SSCRomanceCommandStatus.Cancelled, record.Status);
                captureEnd = true; driver.ExecuteOnce(); Equal(0, bubbleCalls); Equal(0, a.jobs.jobQueue.Count);
                Equal(1, commandFeedback);
            });
        foreach (string change in new[] { "master", "chain", "stage", "personality", "targetPersonality" })
            Check("batch4 queued original request cannot resume after lifecycle " + change, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); scheduleMode = ScheduleMode.Queue;
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                var original = a.jobs.jobQueue.Dequeue().job; ChangeCommandContext(a, b, change);
                var driver = BindCommand(a, original); driver.ExecuteOnce(); Equal(0, bubbleCalls); Require(record.Terminal);
            });
        foreach (string state in new[] { "deadActor", "deadTarget", "destroyedTarget", "offMap", "drafted", "relationLost" })
            Check("batch4 unavailable participants cancel one request without later retry " + state, () =>
            {
                CommandReset(0f); var action = state == "relationLost" ? SSCRomanceAction.Proposal : SSCRomanceAction.Romance;
                var (a, b) = CommandPair(action);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, action), out var record, out _));
                Action restore;
                if (state.StartsWith("dead"))
                {
                    var p = state == "deadActor" ? a : b;
                    AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(p.health, PawnHealthState.Dead);
                    restore = () => AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(p.health, PawnHealthState.Mobile);
                }
                else if (state == "destroyedTarget")
                {
                    AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(b, (sbyte)(-2));
                    restore = () => AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(b, (sbyte)(-1));
                }
                else if (state == "relationLost")
                { a.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, b); restore = () => a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b); }
                else
                { taskFailure = state == "offMap" ? "SameMapRequired" : "UnableToInteract"; restore = () => taskFailure = null; }
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver); captureEnd = true; driver.ExecuteOnce();
                // EndJobWith 的地图下游由宿主捕获，随后执行引擎会调用的真实驱动清理。
                driver.Cleanup(JobCondition.Incompletable);
                Require(record.Terminal); restore(); driver.ExecuteOnce(); Equal(0, bubbleCalls);
            });
        Check("batch4 owner death and absence retain binding preference and unrelated original command", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance); Comp(a).romanceConfig = Denied();
            var saved = Comp(a).romanceConfig; var owner = SSCBondUtility.GetBoundMaster(a);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
            AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(owner.health, PawnHealthState.Dead);
            SSCRomanceLifecycle.Notify(a); Require(!owner.Spawned); Equal(owner, SSCBondUtility.GetBoundMaster(a));
            Require(ReferenceEquals(saved, Comp(a).romanceConfig)); Equal(SSCRomanceCommandStatus.Pending, record.Status);
            ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(1, bubbleCalls);
        });
        Check("batch4 recipient rebind and preference change keep personality but read new reception", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
            object token = SSCPersonalityRuntimeUtility.Capture(b);
            Hediff_ChainOfSexSlave.AddToPawn(b, SocialPawn()); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            Require(SSCPersonalityRuntimeUtility.IsCurrent(b, token)); Equal(SSCRomanceCommandStatus.Pending, record.Status);
            ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(SSCRomanceCommandOutcome.Rejected, record.Outcome);
        });
        foreach (bool claimed in new[] { false, true })
            Check("batch4 native StopAll clears original and queued Jobs with no automatic rescheduling " + claimed, () =>
            {
                var boundary = new Harmony("ssc.tests.romance.batch4.stop-reservations");
                boundary.Patch(AccessTools.Method(typeof(Pawn), nameof(Verse.Pawn.ClearReservationsForJob)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
                try
                {
                    CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                    a.stances = new Pawn_StanceTracker(a); a.carryTracker = new Pawn_CarryTracker(a);
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                    var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver);
                    if (claimed) Require(SSCRomanceCommandGameComponent.Current.Claim(driver) != null);
                    scheduleMode = ScheduleMode.Queue;
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, SocialPawn()), out _, out _));
                    a.jobs.StopAll(canReturnToPool: false);
                    Require(a.CurJob == null && a.jobs.curDriver == null); Equal(0, a.jobs.jobQueue.Count);
                    Equal(SSCRomanceCommandStatus.Cancelled, record.Status); Equal(0, bubbleCalls);
                    captureEnd = true; driver.ExecuteOnce(); Equal(0, bubbleCalls);
                    scheduleMode = ScheduleMode.Current;
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var fresh, out _));
                    Require(fresh.RequestId != record.RequestId); Equal(1, SSCRomanceCommandGameComponent.Current.Records.Count);
                }
                finally { boundary.UnpatchAll(boundary.Id); }
            });
    }
}
