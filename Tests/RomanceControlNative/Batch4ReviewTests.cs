// AI 辅助生成：提交前复核捕获上下文与路径查询回调之间的授权窗口。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static Action reviewSigningCallback, reviewCleanupCallback;
    /// <summary>回调改变原执行者后，预览查询与提交均应拒绝，不能为旧点击签发新人格/绑定的授权。</summary>
    private static void Batch4ReviewTests()
    {
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
        foreach (string change in new[] { "personality", "hollow", "master", "faction" })
        foreach (bool queryOnly in new[] { false, true })
            Check($"batch4 review task query cannot renew actor context {action}/{change}/query={queryOnly}", () =>
            {
                CommandReset(); var (a, b) = CommandPair(action); var context = SSCRomanceCommandUtility.Capture(a, b, action);
                taskDuringCheck = () =>
                {
                    if (change == "hollow")
                    { var shell = new Hediff { pawn = a, def = SSCDefOf.SSC_PersonalityExcreted_Done }; a.health.AddHediff(shell); a.health.RemoveHediff(shell); }
                    else if (change == "faction")
                    { var other = Bare<Faction>(); other.def = new FactionDef { defName = "RomanceReviewNpc" }; AccessTools.Field(typeof(Thing), "factionInt").SetValue(a, other); }
                    else ChangeCommandContext(a, b, change);
                };
                if (queryOnly) Require(SSCRomanceCommandUtility.Failure(context) != null);
                else Require(!SSCRomanceCommandUtility.TrySubmit(context, out _, out _));
                Equal(0, SSCRomanceCommandGameComponent.Current.Records.Count); Require(a.CurJob == null); Equal(0, bubbleCalls);
            });
        foreach (var action in new[] { SSCRomanceAction.Proposal, SSCRomanceAction.Breakup })
        foreach (bool queryOnly in new[] { false, true })
            Check($"batch4 review relationship lost in task query cannot be scheduled {action}/{queryOnly}", () =>
            {
                CommandReset(); var (a, b) = CommandPair(action); var context = SSCRomanceCommandUtility.Capture(a, b, action);
                taskDuringCheck = () => a.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, b);
                if (queryOnly) Require(SSCRomanceCommandUtility.Failure(context) != null);
                else Require(!SSCRomanceCommandUtility.TrySubmit(context, out _, out _));
                Equal(0, SSCRomanceCommandGameComponent.Current.Records.Count); Require(a.CurJob == null);
            });
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("batch4 review signing observation cannot replace captured epoch " + action, () =>
            {
                CommandReset(); var (a, b) = CommandPair(action);
                // 模拟外部公开字段写入后、锁链 Tick 尚未观察的视图；签名维护会旋转世代，原视图不能借用新世代。
                SSCBondUtility.GetChain(a).target = Pawn(PawnIdentity.Master);
                var context = SSCRomanceCommandUtility.Capture(a, b, action);
                Require(!SSCRomanceCommandUtility.TrySubmit(context, out _, out _));
                Equal(0, SSCRomanceCommandGameComponent.Current.Records.Count); Require(a.CurJob == null);
            });
        var signing = new Harmony("ssc.tests.romance.batch4.review-signing");
        signing.Patch(AccessTools.Method(typeof(SSCRomanceLifecycle), nameof(SSCRomanceLifecycle.TryAuthorize)), postfix: new HarmonyMethod(typeof(Program), nameof(ReviewAfterSigning)));
        try
        {
            foreach (var action in Enum.GetValues<SSCRomanceAction>())
            foreach (string change in new[] { "faction", "relationship" })
                Check($"batch4 review signing callback cannot schedule invalid participants {action}/{change}", () =>
                {
                    CommandReset(); var (a, b) = CommandPair(action); var context = SSCRomanceCommandUtility.Capture(a, b, action);
                    reviewSigningCallback = () =>
                    {
                        if (change == "faction") AccessTools.Field(typeof(Thing), "factionInt").SetValue(a, null);
                        else if (action == SSCRomanceAction.Romance) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                        else a.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, b);
                    };
                    Require(!SSCRomanceCommandUtility.TrySubmit(context, out _, out _)); Equal(0, SSCRomanceCommandGameComponent.Current.Records.Count);
                });
        }
        finally { reviewSigningCallback = null; signing.UnpatchAll(signing.Id); }
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("batch4 review current preference callback remains a legitimate one-shot request " + action, () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(action); var context = SSCRomanceCommandUtility.Capture(a, b, action);
                taskDuringCheck = () => Comp(a).romanceConfig = Denied();
                Require(SSCRomanceCommandUtility.TrySubmit(context, out _, out _)); ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(1, bubbleCalls);
            });
        Check("batch4 review ended native driver cannot claim request", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
            var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; driver.ended = true;
            Require(SSCRomanceCommandGameComponent.Current.Claim(driver) == null);
        });
        foreach (bool claimed in new[] { false, true })
            Check("batch4 review late execution on ended driver has no interaction " + claimed, () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
                if (claimed) Require(SSCRomanceCommandGameComponent.Current.Claim(driver) != null);
                driver.ended = true; captureEnd = true; driver.ExecuteOnce(); Equal(0, bubbleCalls);
            });
        foreach (bool trackerQuery in new[] { false, true })
            Check("batch4 review native ended flag raised inside query stops command effects " + trackerQuery, () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
                if (trackerQuery) socialDuringCanInteract = () => driver.ended = true;
                else taskDuringCheck = () => driver.ended = true;
                captureEnd = true; driver.ExecuteOnce(); Equal(0, bubbleCalls);
            });
        foreach (string timing in new[] { "alreadyEnded", "taskQuery", "interactionQuery" })
            Check("batch4 review interrupted wait callback never advances original toil " + timing, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver);
                AccessTools.Field(typeof(JobDriver), "curToilIndex").SetValue(driver, 2);
                AccessTools.Field(typeof(JobDriver), "wantBeginNextToil").SetValue(driver, false);
                if (timing == "alreadyEnded") driver.ended = true;
                else if (timing == "taskQuery") taskDuringCheck = () => driver.ended = true;
                else socialDuringCanInteract = () => driver.ended = true;
                captureEnd = true; AccessTools.Method(driver.GetType(), "WaitForInteraction").Invoke(driver, new object[] { 1 });
                Require(!(bool)AccessTools.Field(typeof(JobDriver), "wantBeginNextToil").GetValue(driver));
                Equal(2, (int)AccessTools.Field(typeof(JobDriver), "curToilIndex").GetValue(driver)); Equal(0, bubbleCalls);
            });
        Check("batch4 review actual StopAll cleanup prefix cannot execute interrupted command", () =>
        {
            var cleanup = new Harmony("ssc.tests.romance.batch4.review-cleanup");
            cleanup.Patch(AccessTools.Method(typeof(JobDriver), nameof(JobDriver.Cleanup)), prefix: new HarmonyMethod(typeof(Program), nameof(ReviewDuringCleanup)));
            cleanup.Patch(AccessTools.Method(typeof(Pawn), nameof(Verse.Pawn.ClearReservationsForJob)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            try
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                a.stances = new Pawn_StanceTracker(a); a.carryTracker = new Pawn_CarryTracker(a);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver);
                reviewCleanupCallback = driver.ExecuteOnce; a.jobs.StopAll(canReturnToPool: false);
                Equal(0, bubbleCalls); Equal(SSCRomanceCommandStatus.Cancelled, record.Status);
            }
            finally { reviewCleanupCallback = null; cleanup.UnpatchAll(cleanup.Id); }
        });
        Check("batch4 review save inside actual interruption cannot restore ended Job as pending", () =>
        {
            var saving = new Harmony("ssc.tests.romance.batch4.review-cleanup-save");
            saving.Patch(AccessTools.Method(typeof(JobDriver), nameof(JobDriver.Cleanup)), prefix: new HarmonyMethod(typeof(Program), nameof(ReviewDuringCleanup)));
            saving.Patch(AccessTools.Method(typeof(Pawn), nameof(Verse.Pawn.ClearReservationsForJob)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            saving.Patch(AccessTools.Method(typeof(SSCRestrictionGameComponent), nameof(SSCRestrictionGameComponent.AllPawns)), prefix: new HarmonyMethod(typeof(Program), nameof(ScenePawnSnapshot)));
            saving.Patch(AccessTools.Method(typeof(SSCRestrictionLifecycle), nameof(SSCRestrictionLifecycle.Refresh)), prefix: new HarmonyMethod(typeof(Program), nameof(SceneLegacyRefresh)));
            try
            {
                CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                a.stances = new Pawn_StanceTracker(a); a.carryTracker = new Pawn_CarryTracker(a);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; PrepareReviewDriver(driver); string saved = null;
                // 用实际 StopAll 设置 ended，保存点处于它调用驱动清理、尚未执行本模块 Finish 的窗口。
                reviewCleanupCallback = () => saved = SaveBatch4Scene(new Batch4Scene(a, b, SSCBondUtility.GetBoundMaster(a)));
                a.jobs.StopAll(canReturnToPool: false); Require(saved != null);
                var restored = LoadBatch4Scene(saved); Equal(SSCRomanceCommandStatus.Cancelled, restored.Store.Records.Single().Status);
                captureEnd = true; ((JobDriver_SSCRomanceCommand)restored.Actor.jobs.curDriver).ExecuteOnce(); Equal(0, bubbleCalls);
            }
            finally { batch4RestoredPawns = null; reviewCleanupCallback = null; saving.UnpatchAll(saving.Id); }
        });
    }
    /// <summary>模拟签名完成后的外部回调，输入只消费一次，避免改写后续授权。</summary>
    private static void ReviewAfterSigning()
    { var callback = reviewSigningCallback; reviewSigningCallback = null; callback?.Invoke(); }
    /// <summary>原版已设置 ended、尚未运行结束动作时触发迟到执行，验证真实中断窗口。</summary>
    private static void ReviewDuringCleanup(JobDriver __instance)
    {
        if (__instance is not JobDriver_SSCRomanceCommand) return;
        var callback = reviewCleanupCallback; reviewCleanupCallback = null; callback?.Invoke();
    }
}
