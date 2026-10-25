// AI 辅助生成：真实生产登记/驱动/tracker/worker/Scribe，地图寻路及 UI 设备边界明确隔离。
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private enum ScheduleMode { Current, Queue, Reject, Ignore, Clone, Synchronous }
    private static ScheduleMode scheduleMode;
    private static string taskFailure;
    private static readonly Queue<bool> commandWilling = new();
    private static int commandFeedback;
    private static bool nativeSchedule, nativeTask, captureEnd;
    private static JobCondition? commandEnd;
    private static Action taskDuringCheck;
    /// <summary>装真实生产补丁并扩展第三批；只隔离地图/输入调度，业务与原生关系结算不替换。</summary>
    private static void CommandTests(Harmony harmony, string repo)
    {
        // 前两批会直接调用 Transpiler 测试失配回退；卸下再安装真实补丁，恢复实际安装结果而非伪填能力标志。
        foreach (string patch in new[] { "Prefix", "Finalizer", "Transpiler" })
            harmony.Unpatch(AccessTools.Method(typeof(Pawn_InteractionsTracker), "TryInteractWith"), AccessTools.Method(typeof(Harmony_SSCRomanceInteraction), patch));
        foreach (var method in Harmony_SSCRomanceAcceptance.TargetMethods()) harmony.Unpatch(method, AccessTools.Method(typeof(Harmony_SSCRomanceAcceptance), "Transpiler"));
        foreach (var type in new[] { typeof(Harmony_SSCRomanceInteraction), typeof(Harmony_SSCRomanceAcceptance),
            typeof(Harmony_SSCRomanceCommandWorker), typeof(Harmony_SSCRomanceCommandWillingness) }) harmony.CreateClassProcessor(type).Patch();
        SSCRomanceDefOf.SSC_RomanceCommand = new JobDef { defName = "SSC_RomanceCommand", driverClass = typeof(JobDriver_SSCRomanceCommand), suspendable = false };
        DefDatabase<JobDef>.Add(SSCRomanceDefOf.SSC_RomanceCommand);
        MessageTypeDefOf.NeutralEvent = new MessageTypeDef { defName = "NeutralEvent" };
        harmony.Patch(AccessTools.Method(typeof(SSCRomanceCommandUtility), nameof(SSCRomanceCommandUtility.TaskFailure)), prefix: new HarmonyMethod(typeof(Program), nameof(CommandTaskBoundary)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob)), prefix: new HarmonyMethod(typeof(Program), nameof(CommandScheduleBoundary)));
        harmony.Patch(AccessTools.Method(typeof(Messages), "Message", new[] { typeof(Message), typeof(bool) }), postfix: new HarmonyMethod(typeof(Program), nameof(CommandMessageBoundary)));
        Check("batch3 critical production hooks are actually installed", () => Require(SSCRomanceCommandHooks.Ready,
            string.Join(",", typeof(SSCRomanceCommandHooks).GetFields(BindingFlags.Static | BindingFlags.NonPublic).Select(f => f.Name + "=" + f.GetValue(null)))));
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("batch3 command overrides own denial and owner-only for one real interaction " + action, () =>
            {
                CommandReset(0f); var (a, b) = CommandPair(action); var saved = Comp(a).romanceConfig = Denied();
                Require(!SSCRomanceInteractionUtility.CanStart(a, b, action));
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, action), out var r, out var reason), reason);
                ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce();
                Equal(SSCRomanceCommandStatus.Completed, r.Status); Equal(SSCRomanceCommandOutcome.RelationshipChanged, r.Outcome);
                Equal(1, bubbleCalls); Require(ReferenceEquals(saved, Comp(a).romanceConfig));
                ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(1, bubbleCalls); Equal(1, commandFeedback);
            });
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
        foreach (bool passive in new[] { false, true })
            Check($"batch3 target can reject command and native failure branch is preserved {action}/{passive}", () =>
            {
                CommandReset(.99f, .8f); var (a, b) = CommandPair(action, targetSlave: passive);
                if (passive) { Comp(b).romanceConfig = new() { allowAcceptRomance = false, allowAcceptProposal = false }; socialChance = 1f; }
                SubmitAndRun(a, b, action, out var r); Equal(SSCRomanceCommandStatus.Completed, r.Status); Equal(SSCRomanceCommandOutcome.Rejected, r.Outcome);
                Equal(1, bubbleCalls); Require(a.needs.mood.thoughts.memories.Memories.Count > 0);
            });
        Check("batch3 master guarantee remains exact at zero chance and random endpoint one", () =>
        {
            CommandReset(1f); socialChance = 0; var a = SocialSlave(); var b = SocialSlave(a); a.jobs = new Pawn_JobTracker(a);
            Comp(b).romanceConfig = new() { allowAcceptRomance = false, alwaysAcceptMaster = true };
            SubmitAndRun(a, b, SSCRomanceAction.Romance, out var r); Equal(SSCRomanceCommandOutcome.RelationshipChanged, r.Outcome);
        });
        foreach (float roll in new[] { .3999f, .4f })
            Check("batch3 commanded rejected proposal keeps native forty-percent breakup " + roll, () =>
            {
                CommandReset(0f, roll); var (a, b) = CommandPair(SSCRomanceAction.Proposal, true); Comp(b).romanceConfig = new() { allowAcceptProposal = false };
                SubmitAndRun(a, b, SSCRomanceAction.Proposal, out var r); Equal(SSCRomanceCommandOutcome.Rejected, r.Outcome);
                Equal(roll < .4f, a.relations.DirectRelationExists(PawnRelationDefOf.ExLover, b));
            });
        foreach (var relation in new[] { PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance, PawnRelationDefOf.Spouse })
            Check("batch3 actual breakup command handles relationship and retains chain " + relation.defName, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); a.relations.AddDirectRelation(relation, b);
                var chain = SSCBondUtility.GetChain(a); SubmitAndRun(a, b, SSCRomanceAction.Breakup, out var r);
                Equal(SSCRomanceCommandOutcome.RelationshipChanged, r.Outcome); Equal(chain, SSCBondUtility.GetChain(a));
                Require(!a.relations.DirectRelationExists(relation, b));
            });
        harmony.Patch(AccessTools.Method(typeof(JobDriver), nameof(JobDriver.EndJobWith)), prefix: new HarmonyMethod(typeof(Program), nameof(CommandEndBoundary)));
        CommandSubmissionTests(); CommandExecutionTests(); CommandSaveTests(); CommandDriverTests(); CommandIdeologyTests(harmony); CommandUiAndHookTests(repo); CommandHookLossTests(harmony, repo); Batch3ReviewTests();
    }
    /// <summary>每例使用独立游戏内命令组件，清掉测试调度边界和随机输入，不跨例保留授权。</summary>
    private static void CommandReset(params float[] rolls)
    {
        ResetSocial(rolls); commandWilling.Clear(); taskFailure = null; commandFeedback = 0; scheduleMode = ScheduleMode.Current;
        nativeSchedule = nativeTask = captureEnd = false; commandEnd = null;
        taskDuringCheck = null;
        SSCMod.settings ??= new SSCSettings();
        var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(Current.Game);
        components.RemoveAll(c => c is SSCRomanceCommandGameComponent);
        var store = new SSCRomanceCommandGameComponent(Current.Game); components.Add(store); store.ReconcileAfterLoad();
    }
    /// <summary>建立真实社交双方和必要关系；具体地图能力由独立边界提供。</summary>
    private static (Pawn actor, Pawn target) CommandPair(SSCRomanceAction action, bool targetSlave = false)
    {
        var a = SocialSlave(); var b = targetSlave ? SocialSlave() : SocialPawn(); a.jobs = new Pawn_JobTracker(a);
        if (action != SSCRomanceAction.Romance) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
        return (a, b);
    }
    /// <summary>通过公开生产入口安排，并由真实生产驱动执行，不在宿主重做命令授权或去重。</summary>
    private static void SubmitAndRun(Pawn a, Pawn b, SSCRomanceAction action, out SSCRomanceCommandRecord record)
    {
        Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, action), out record, out string reason), reason);
        ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce();
    }
    /// <summary>隔离地图路径/清醒等输入，仍执行命令提交、登记、驱动及 tracker 的真实生产代码。</summary>
    private static bool CommandTaskBoundary(ref string __result)
    { if (nativeTask) return true; var callback = taskDuringCheck; taskDuringCheck = null; callback?.Invoke(); __result = taskFailure; return false; }
    /// <summary>调度边界覆盖原方法 true 的多种语义；不替代生产对实际原 Job 接纳的复查。</summary>
    private static bool CommandScheduleBoundary(Pawn_JobTracker __instance, Job job, ref bool __result)
    {
        if (nativeSchedule) return true;
        __result = scheduleMode != ScheduleMode.Reject;
        if (scheduleMode == ScheduleMode.Reject || scheduleMode == ScheduleMode.Ignore) return false;
        if (scheduleMode == ScheduleMode.Queue) { __instance.jobQueue.EnqueueLast(job); return false; }
        var actor = (Pawn)AccessTools.Field(typeof(Pawn_JobTracker), "pawn").GetValue(__instance);
        BindCommand(actor, scheduleMode == ScheduleMode.Clone ? job.Clone() : job);
        if (scheduleMode == ScheduleMode.Synchronous) ((JobDriver_SSCRomanceCommand)__instance.curDriver).ExecuteOnce();
        return false;
    }
    /// <summary>模拟原调度器将确切 Job 交给驱动；同编号 Clone 夹具不改变生产认领规则。</summary>
    private static JobDriver_SSCRomanceCommand BindCommand(Pawn actor, Job job)
    {
        var driver = new JobDriver_SSCRomanceCommand { pawn = actor, job = job };
        actor.jobs.curJob = job; actor.jobs.curDriver = driver; return driver;
    }
    /// <summary>统计被明确隔离的原生消息投递次数；原版关系/记忆仍实际执行。</summary>
    private static void CommandMessageBoundary() => commandFeedback++;
    /// <summary>有限等待夹具只截获调度器结束请求，记录判定结果，不替代命令的等待与取消逻辑。</summary>
    private static bool CommandEndBoundary(JobCondition condition)
    { if (!captureEnd) return true; commandEnd = condition; return false; }
    /// <summary>验证原菜单凭据、重复提交、排队与同步执行的提交语义。</summary>
    private static void CommandSubmissionTests()
    {
        foreach (var mode in Enum.GetValues<ScheduleMode>())
            Check("batch3 scheduling return value requires original Job ownership " + mode, () =>
            {
                CommandReset(0f); scheduleMode = mode; var (a, b) = CommandPair(SSCRomanceAction.Romance);
                bool accepted = SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _);
                Equal(mode is ScheduleMode.Current or ScheduleMode.Queue or ScheduleMode.Synchronous, accepted);
                if (mode == ScheduleMode.Synchronous) { Equal(SSCRomanceCommandStatus.Completed, r.Status); Equal(1, bubbleCalls); }
                if (mode == ScheduleMode.Clone) Require(SSCRomanceCommandGameComponent.Current.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null);
                if (mode == ScheduleMode.Queue)
                {
                    var job = a.jobs.jobQueue.Dequeue().job; var driver = BindCommand(a, job); driver.ExecuteOnce(); Equal(1, bubbleCalls);
                }
            });
        Check("batch3 double click and fresh duplicate pending preview cannot issue twice", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); var view = SSCRomanceCommandUtility.Capture(a, b);
            Require(SSCRomanceCommandUtility.TrySubmit(view, out _, out _));
            Require(!SSCRomanceCommandUtility.TrySubmit(view, out _, out var reason)); Equal("StaleContext", reason);
            Require(!SSCRomanceCommandUtility.TrySubmit(view.ForTarget(b, SSCRomanceAction.Romance), out _, out _));
            Require(!SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out reason)); Equal("DuplicateCommand", reason);
        });
        Check("batch3 actual original TryTakeOrderedJob same-job shortcut is not scheduling success", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            BindCommand(a, new Job { def = SSCRomanceDefOf.SSC_RomanceCommand, targetA = b, interaction = InteractionDefOf.RomanceAttempt, loadID = ++id });
            nativeSchedule = true;
            try { Require(!SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out var reason)); Equal("JobRejected", reason); }
            finally { nativeSchedule = false; }
        });
        foreach (string change in new[] { "master", "chain", "stage", "personality", "targetPersonality" })
            Check("batch3 old menu cannot acquire replacement context " + change, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); var view = SSCRomanceCommandUtility.Capture(a, b);
                ChangeCommandContext(a, b, change);
                Require(!SSCRomanceCommandUtility.TrySubmit(view, out _, out _)); Equal(0, bubbleCalls);
            });
        Check("batch3 expired context stays in its original game when selecting a new target", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); var view = SSCRomanceCommandUtility.Capture(a);
            var game = Current.Game;
            try { Current.Game = Bare<Game>(); Equal("StaleContext", SSCRomanceCommandUtility.Failure(view.ForTarget(b, SSCRomanceAction.Romance))); }
            finally { Current.Game = game; }
        });
        foreach (var action in new[] { SSCRomanceAction.Proposal, SSCRomanceAction.Breakup })
            Check("batch3 missing relationship cannot be manufactured by command " + action, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(!SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, action), out _, out _)); Equal(0, bubbleCalls);
            });
    }
    /// <summary>通过真实生命周期事件制造失效，再恢复相同主人或阶段也不能恢复旧世代。</summary>
    private static void ChangeCommandContext(Pawn a, Pawn b, string change)
    {
        var chain = SSCBondUtility.GetChain(a);
        if (change == "master") { var old = chain.target; chain.target = SocialPawn(); SSCRomanceLifecycle.Notify(a); chain.target = old; SSCRomanceLifecycle.Notify(a); }
        if (change == "stage") { chain.Severity = .3f; SSCRomanceLifecycle.Notify(a); chain.Severity = .9f; SSCRomanceLifecycle.Notify(a); }
        if (change == "chain") { a.health.hediffSet.hediffs.Remove(chain); SSCRomanceLifecycle.Notify(a); a.health.hediffSet.hediffs.Add(chain); SSCRomanceLifecycle.Notify(a); }
        if (change is "personality" or "targetPersonality")
        { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(change == "personality" ? a : b, out var scope)); scope.Dispose(); }
    }
    /// <summary>重复执行、回调变更、真实拒绝和异常后不重放，均跨实际 tracker/worker 验证。</summary>
    private static void CommandExecutionTests()
    {
        foreach (string change in new[] { "master", "chain", "stage", "personality", "targetPersonality" })
            Check("batch3 pending command loses original authorization permanently " + change, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
                ChangeCommandContext(a, b, change); Require(r.Terminal); Require(SSCRomanceCommandGameComponent.Current.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null);
                Equal(0, bubbleCalls);
            });
        Check("batch3 common final guard rejects authorization changed inside native callback", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            socialDuringCanInteract = () => ChangeCommandContext(a, b, "master");
            ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(0, bubbleCalls); Require(!r.EffectsStarted); Equal(SSCRomanceCommandOutcome.NotOccurred, r.Outcome);
        });
        Check("batch3 final task-query callback cannot keep old target personality authorization", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            socialDuringCanInteract = () => taskDuringCheck = () => ChangeCommandContext(a, b, "targetPersonality");
            ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(0, bubbleCalls); Require(!r.EffectsStarted);
        });
        Check("batch3 a nested tracker call cannot borrow command authorization", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance); Comp(a).romanceConfig = Denied(); bool nested = true;
            socialDuringCanInteract = () => nested = a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt);
            SubmitAndRun(a, b, SSCRomanceAction.Romance, out var r); Require(!nested); Equal(1, bubbleCalls); Equal(SSCRomanceCommandOutcome.RelationshipChanged, r.Outcome);
        });
        Check("batch3 unscoped call during a command Job has no override", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); Comp(a).romanceConfig = Denied();
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, bubbleCalls);
        });
        Check("batch3 no actual interaction does not consume success or invent rejection memories", () =>
        {
            CommandReset(); socialCanInteract = false; var (a, b) = CommandPair(SSCRomanceAction.Romance);
            SubmitAndRun(a, b, SSCRomanceAction.Romance, out var r); Equal(SSCRomanceCommandStatus.Failed, r.Status); Equal(SSCRomanceCommandOutcome.NotOccurred, r.Outcome);
            Equal(0, bubbleCalls); Equal(0, a.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("batch3 changed target response is read at execution, not frozen in preview", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(SSCRomanceCommandOutcome.Rejected, r.Outcome);
        });
        Check("batch3 exception after native relationship write leaves non-replayable result", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            var boundary = new Harmony("ssc.tests.romance.command.throw");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), postfix: new HarmonyMethod(typeof(Program), nameof(CommandThrow)));
                try { ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Require(false); } catch (InvalidOperationException) { }
                Equal(SSCRomanceCommandStatus.Indeterminate, r.Status); Require(a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
            }
            finally { boundary.UnpatchAll(boundary.Id); }
            ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(0, bubbleCalls);
            Require(SSCRomancePolicy.Evaluate(a, SocialPawn(), SSCRomanceAction.Romance).CanInitiate);
        });
        Check("batch3 repeated cleanup does not replay worker or duplicate feedback", () =>
        {
            CommandReset(0f); var (a, b) = CommandPair(SSCRomanceAction.Romance); SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
            var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
            ((IEnumerable<Toil>)AccessTools.Method(driver.GetType(), "MakeNewToils").Invoke(driver, null)).ToList();
            driver.Cleanup(JobCondition.Succeeded); driver.Cleanup(JobCondition.InterruptForced); Equal(1, bubbleCalls); Equal(1, commandFeedback);
        });
    }
    /// <summary>模拟第三方在原 worker 返回后抛异常，不替代或回滚已完成的原生关系变更。</summary>
    private static void CommandThrow() => throw new InvalidOperationException("expected command fixture exception");
    /// <summary>执行真实驱动等待及早期实体检查；地图槽只用于 Spawned/Map，未模拟地图寻路。</summary>
    private static void CommandDriverTests()
    {
        Check("batch3 real driver waits finitely and never manufactures an interaction", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver; socialCanInteract = false; captureEnd = true;
            var wait = AccessTools.Method(driver.GetType(), "WaitForInteraction");
            wait.Invoke(driver, new object[] { 599 }); Require(commandEnd == null); Equal(SSCRomanceCommandStatus.Claimed, r.Status);
            wait.Invoke(driver, new object[] { 1 }); Equal<JobCondition?>(JobCondition.Incompletable, commandEnd);
            Equal(SSCRomanceCommandStatus.Cancelled, r.Status); Equal(0, bubbleCalls);
        });
        Check("batch3 real driver reports cancelled original request without reauthorizing it", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
            ChangeCommandContext(a, b, "master"); ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce();
            Equal(SSCRomanceCommandStatus.Cancelled, r.Status); Equal(0, bubbleCalls);
        });
        Check("batch3 real task eligibility rejects off-map participants", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); nativeTask = true;
            try { Equal("SameMapRequired", SSCRomanceCommandUtility.TaskFailure(a, b, SSCRomanceAction.Romance)); }
            finally { nativeTask = false; }
        });
        foreach (bool actorYoung in new[] { false, true })
            Check("batch3 actual task eligibility retains minimum romance age " + actorYoung, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
                var maps = AccessTools.Field(typeof(Game), "maps"); var previous = maps.GetValue(Current.Game);
                try
                {
                    maps.SetValue(Current.Game, new List<Map> { Bare<Map>() });
                    foreach (var p in new[] { a, b })
                    { AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(p, (sbyte)0); p.mindState = new Pawn_MindState(p); }
                    a.pather = Bare<Pawn_PathFollower>(); (actorYoung ? a : b).ageTracker.AgeBiologicalTicks = 15L * GenDate.TicksPerYear;
                    nativeTask = true; Equal("InvalidRomancePair", SSCRomanceCommandUtility.TaskFailure(a, b, SSCRomanceAction.Romance));
                }
                finally { maps.SetValue(Current.Game, previous); nativeTask = false; }
            });
        Check("batch3 negative Job identity is rejected while zero is a legitimate first native ID", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            var proof = Authorize(a, b); var invalid = new Job { loadID = -1, def = SSCRomanceDefOf.SSC_RomanceCommand, targetA = b, interaction = InteractionDefOf.RomanceAttempt };
            var register = AccessTools.Method(typeof(SSCRomanceCommandGameComponent), "Register");
            Require(register.Invoke(SSCRomanceCommandGameComponent.Current, new object[] { invalid, proof }) == null);
            invalid.loadID = 0; Require(register.Invoke(SSCRomanceCommandGameComponent.Current, new object[] { invalid, proof }) != null);
        });
    }
    /// <summary>实际求婚公式中只放行发起者意愿，目标的拒绝与正常概率继续保留。</summary>
    private static void CommandIdeologyTests(Harmony harmony)
    {
        var method = AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance");
        harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        try
        {
            foreach (bool targetWilling in new[] { true, false })
                Check("batch3 actual proposal formula separates initiator and recipient ideology " + targetWilling, () =>
                {
                    CommandReset(0f, .8f); var (a, b) = CommandPair(SSCRomanceAction.Proposal);
                    commandWilling.Enqueue(false); commandWilling.Enqueue(targetWilling);
                    SubmitAndRun(a, b, SSCRomanceAction.Proposal, out var r);
                    Equal(targetWilling ? SSCRomanceCommandOutcome.RelationshipChanged : SSCRomanceCommandOutcome.Rejected, r.Outcome);
                    Equal(0, commandWilling.Count);
                });
            Check("batch3 unscoped actual proposal formula keeps initiator ideological veto", () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Proposal); commandWilling.Enqueue(false);
                Equal(0f, InteractionWorker_MarriageProposal.AcceptanceChance(a, b));
            });
        }
        finally { harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(SocialProbability))); commandWilling.Clear(); }
    }
    /// <summary>原生 IL 锚点、冻结资源和双板块接线；屏幕像素与完整地图仍留实机。</summary>
    private static void CommandUiAndHookTests(string repo)
    {
        Check("batch3 actual willingness IL wraps first vote only and preserves recipient vote", () =>
        {
            var method = AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance");
            var original = PatchProcessor.GetOriginalInstructions(method).ToList();
            var rewritten = Harmony_SSCRomanceCommandWillingness.Transpiler(original).ToList();
            Equal(original.Count + 3, rewritten.Count); Equal(1, rewritten.Count(c => c.Calls(AccessTools.Method(typeof(SSCRomanceExecutionScope), "InitiatorWilling"))));
        });
        Check("batch3 missing/nonunique command anchor disables commands rather than granting ordinary fallback", () =>
        {
            var original = new List<CodeInstruction> { new(OpCodes.Ret) }; Equal(1, Harmony_SSCRomanceCommandWillingness.Transpiler(original).Count());
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); Equal("HooksUnavailable", SSCRomanceCommandUtility.ActorFailure(a));
            var method = AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance");
            Harmony_SSCRomanceCommandWillingness.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToList(); Require(SSCRomanceCommandHooks.Ready);
        });
        foreach (float width in new[] { 320f, 640f, 645f, 646f, 800f, 1920f })
            Check("batch3 sidebar width fallback preserves minimum real content width " + width, () => Equal(width >= 646, SSCBehaviorControlUI.CanFitSidebar(width)));
        Check("batch3 old global switch cannot hide romance section or change its rules", () =>
        {
            CommandReset(); var a = SocialSlave(); bool old = SSCMod.settings.enableSexSlaveProtectionRules;
            try { SSCMod.settings.enableSexSlaveProtectionRules = false; Require(SSCBehaviorControlUI.CanInspect(a)); Require(SSCRomancePreferences.GetView(a, SSCRomanceSetting.AcceptRomance).CanChange); }
            finally { SSCMod.settings.enableSexSlaveProtectionRules = old; }
        });
        Check("batch3 unbound saved choices and first stage remain inspectable", () =>
        {
            var a = SocialSlave(severity: .1f); Require(SSCBehaviorControlUI.CanInspect(a)); Require(!SSCRomancePreferences.GetView(a, SSCRomanceSetting.AcceptRomance).Unlocked);
            a.health.hediffSet.hediffs.Clear(); Require(SSCBehaviorControlUI.CanInspect(a)); Equal(SSCRomanceReason.NoBinding, SSCRomancePreferences.GetView(a, SSCRomanceSetting.AcceptRomance).Reason);
        });
        Check("batch3 UI uses two independent blocks and same drawers in sidebar and dialog", () =>
        {
            string drawer = File.ReadAllText(Path.Combine(repo, "Sexslavecraft/common/Romance/SSCRomanceUI.cs"));
            Require(drawer.Contains("DrawBlock(listing, \"SSC_Romance_SexSection\"") && drawer.Contains("DrawBlock(listing, \"SSC_Romance_RomanceSection\""));
            foreach (string path in new[] { "Sexslavecraft/ITab/ITab_SexSlaveTraining.cs", "Sexslavecraft/common/Restrictions/Dialog_SSCRestrictions.cs" })
                Require(File.ReadAllText(Path.Combine(repo, path)).Contains("SSCBehaviorControlUI.DrawPawn"));
            Require(drawer.Contains("view.Context") && !drawer.Contains("SSCAbilityToggleUtility.TrySet"));
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("batch3 localized job resource and all enum/command reasons " + language, () =>
            {
                string path = $"Languages/{language}/DefInjected/JobDef/SSC_RomanceCommand.xml";
                Equal(File.ReadAllText(Path.Combine(repo, "1.7", path)), File.ReadAllText(Path.Combine(repo, "Sexslavecraft", path)));
                var keys = XDocument.Load(Path.Combine(repo, "1.7/Languages", language, "Keyed/SSC_Romance.xml")).Root.Elements().Select(e => e.Name.LocalName).ToHashSet();
                foreach (var setting in Enum.GetValues<SSCRomanceSetting>()) Require(keys.Contains("SSC_Romance_Setting_" + setting));
                foreach (var reason in Enum.GetValues<SSCRomanceReason>().Where(r => r != SSCRomanceReason.None)) Require(keys.Contains("SSC_Romance_" + reason));
            });
    }
    /// <summary>真实卸载/重装关键补丁验证能力门禁；不能只凭曾经匹配过 IL 就继续授予命令豁免。</summary>
    private static void CommandHookLossTests(Harmony harmony, string repo)
    {
        var tracker = AccessTools.Method(typeof(Pawn_InteractionsTracker), "TryInteractWith");
        var targets = new[]
        {
            (tracker, typeof(Harmony_SSCRomanceInteraction), "Prefix"),
            (tracker, typeof(Harmony_SSCRomanceInteraction), "Finalizer"),
            (tracker, typeof(Harmony_SSCRomanceInteraction), "Transpiler"),
            (AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), typeof(Harmony_SSCRomanceAcceptance), "Transpiler"),
            (AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "Interacted"), typeof(Harmony_SSCRomanceAcceptance), "Transpiler"),
            (AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance"), typeof(Harmony_SSCRomanceCommandWillingness), "Transpiler"),
            (AccessTools.Method(typeof(InteractionWorker_Breakup), "Interacted"), typeof(Harmony_SSCRomanceCommandWorker), "Postfix"),
        };
        foreach (var (method, type, name) in targets)
            Check("batch3 actual missing critical hook disables command " + method.DeclaringType.Name + "/" + type.Name + "/" + name, () =>
            {
                var patch = AccessTools.Method(type, name); harmony.Unpatch(method, patch);
                try { Require(!SSCRomanceCommandHooks.Ready); }
                finally
                {
                    var installed = new HarmonyMethod(patch);
                    harmony.Patch(method, prefix: name == "Prefix" ? installed : null, postfix: name == "Postfix" ? installed : null,
                        transpiler: name == "Transpiler" ? installed : null, finalizer: name == "Finalizer" ? installed : null);
                }
                Require(SSCRomanceCommandHooks.Ready);
            });
        Check("batch3 expanded isolated host patch combination includes command adapters", () =>
        {
            var methods = Harmony_SSCRomanceRandomSelection.TargetMethods().Concat(Harmony_SSCRomanceCommandWorker.TargetMethods()).Append(tracker)
                .Append(AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance")).Append(Harmony_SSCRomanceRjwClick.TargetMethod())
                .Append(AccessTools.Method(typeof(RelationsUtility), "RomanceOption")).Distinct().ToArray();
            var data = methods.Select(m => new { Method = m.DeclaringType.FullName + "." + m.Name,
                Patches = new[] { Harmony.GetPatchInfo(m).Prefixes, Harmony.GetPatchInfo(m).Postfixes, Harmony.GetPatchInfo(m).Transpilers, Harmony.GetPatchInfo(m).Finalizers }
                    .SelectMany(p => p).Select(p => new { p.owner, p.priority, p.before, p.after, Patch = p.PatchMethod.DeclaringType.FullName + "." + p.PatchMethod.Name }).ToArray() }).ToArray();
            string directory = Path.Combine(repo, ".builds/romance-batch3"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "HostPatchCombination.json"), System.Text.Json.JsonSerializer.Serialize(new { Scope = "isolated native host, not live game", Production = production.Location,
                ProductionSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(production.Location))).ToLowerInvariant(), Entries = data }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Equal(10, data.Length);
        });
    }
}
