using System.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>通过生产日常驱动建立独立执行器，返回真实步骤列表供失败与迟到回调测试。</summary>
    private static (JobDriver_Training driver, Toil[] toils, CompSexSlaveTraining comp) Daily()
    {
        Pawn actor = TestWorld.NewPawn(), target = TestWorld.NewPawn();
        actor.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Master;
        var comp = target.TryGetComp<CompSexSlaveTraining>(); comp.pawnIdentity = PawnIdentity.Slave;
        comp.selectedTrainer = actor; target.BoundMaster = actor;
        var job = new Job { def = new JobDef { defName = "SSC_Training_SexSlave" }, targetA = new LocalTargetInfo(target) };
        var driver = new JobDriver_Training { pawn = actor, job = job };
        actor.jobs.curJob = job; actor.jobs.curDriver = driver;
        Require(driver.TryMakePreToilReservations(false), "daily reservation");
        return (driver, driver.CreateToilsForTest().ToArray(), comp);
    }

    /// <summary>让仪式主持者成为获准的非主人指定者，便于验证下一阶段撤销许可。</summary>
    private static RitualFixture NonOwnerRitual()
    {
        var ritual = new RitualFixture();
        ritual.Slave.BoundMaster = TestWorld.NewPawn();
        ritual.Slave.BoundMaster.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Master;
        ritual.Comp.restrictionConfig.rules.receiveTraining = true;
        return ritual;
    }

    /// <summary>验证真实日常与仪式驱动的清理和结算，不在替身中实现这些流程。</summary>
    private static void RunStage3BTests()
    {
        Check("daily training follows moving targets using reachable touch mode", () =>
        {
            // 工作扫描已用 Touch 判断可达；直接枚举生产驱动，验证连续
            // 分配任务时的移动步骤也以同一语义跟随尚可自由活动的目标。
            Daily();
            Equal(PathEndMode.Touch, TestWorld.LastGotoThingMode, "daily movement end mode");
        });
#if SSC_TEST_WITH_UAP
        Check("daily approach clears only the trainer's old UAP position lock", () =>
        {
            // 用生产 Goto toil 的包装入口验证调用时序：前一场动画留下的
            // 调教员位置锁须在寻路开始前清理，目标当前的锁不应被误删。
            var f = Daily();
            var target = f.driver.job.targetA.Thing as Pawn;
            UAP_Animations.UAP_AnimationPositionLockManager.LockParticipants(f.driver.pawn, target);
            TestWorld.OnGotoInit = () =>
            {
                Require(!UAP_Animations.UAP_AnimationPositionLockManager.IsLocked(f.driver.pawn), "trainer lock reached path start");
                Require(UAP_Animations.UAP_AnimationPositionLockManager.IsLocked(target), "target lock was changed");
            };
            f.toils[1].initAction();
        });
#endif
        Check("loaded idle daily approach retries a stopped path and resumes movement", () =>
        {
            // 模拟存档直接恢复在走位步骤：initAction 不再运行，旧锁曾让
            // pather 保持静止。生产 tick 回调须补发 Touch 寻路且限频。
            var f = Daily();
            f.driver.pawn.pather.BlockStarts = true;
            Find.TickManager.TicksGame = 60;
            f.toils[1].tickAction();
            Equal(1, f.driver.pawn.pather.StartPathCalls, "first path retry");
            Equal(PathEndMode.Touch, f.driver.pawn.pather.LastEndMode, "retry end mode");
            Find.TickManager.TicksGame = 61;
            f.toils[1].tickAction();
            Equal(1, f.driver.pawn.pather.StartPathCalls, "retry frequency");
            f.driver.pawn.pather.BlockStarts = false;
            Find.TickManager.TicksGame = 120;
            f.toils[1].tickAction();
            Require(f.driver.pawn.pather.MovingNow, "path did not resume");
            Find.TickManager.TicksGame = 180;
            f.toils[1].tickAction();
            Equal(2, f.driver.pawn.pather.StartPathCalls, "moving path should not restart");
        });
        Check("permanently blocked daily approach ends instead of displaying endless Training", () =>
        {
            var f = Daily();
            f.driver.pawn.pather.BlockStarts = true;
            for (int i = 1; i <= 11; i++)
            {
                Find.TickManager.TicksGame = i * 60;
                f.toils[1].tickAction();
            }
            Equal(JobCondition.Incompletable, f.driver.pawn.jobs.EndCondition.Value, "blocked path outcome");
            Equal(10, f.driver.pawn.pather.StartPathCalls, "bounded retries");
            Equal(1, TestWorld.PathValidationFailures, "target retry cooldown");
        });
        Check("daily walking interruption releases occupancy without payout or cooldown", () =>
        {
            var f = Daily(); f.toils[0].initAction(); Require(f.comp.isBeingTrained, "prepared flag");
            f.driver.Finish(JobCondition.Incompletable);
            Require(!f.comp.isBeingTrained, "walking occupancy leaked");
            Equal(0, TestWorld.RjwEndCalls, "unstarted End"); Equal(0, TestWorld.DailyCooldowns, "cooldown");
        });
        Check("daily receiver preparation failure clears occupancy without scene completion", () =>
        {
            var f = Daily(); f.toils[0].initAction(); TestWorld.ReceiverSucceeds = false;
            f.toils[2].initAction(); f.driver.Finish(JobCondition.Incompletable);
            Require(!f.comp.isBeingTrained, "receiver failure occupancy");
            Equal(JobCondition.Incompletable, f.driver.pawn.jobs.EndCondition.Value, "failure condition");
            Equal(0, TestWorld.DailyOutcomes, "outcome"); Equal(0, TestWorld.DailyCooldowns, "cooldown");
        });
        Check("daily scene initialization failure does not invoke native End or payout", () =>
        {
            var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction();
            TestWorld.SynchronizeSucceeds = false; f.toils[3].initAction(); f.toils[3].Finish();
            f.toils[4].initAction(); f.driver.Finish(JobCondition.Incompletable);
            Equal(0, TestWorld.RjwEndCalls, "unstarted scene End"); Equal(0, TestWorld.ProcessSexCalls, "ProcessSex");
            Equal(0, TestWorld.DailyCooldowns, "cooldown"); Require(!f.comp.isBeingTrained, "occupancy");
        });
        Check("daily completed scene preserves native processing and daily payout", () =>
        {
            var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
            f.driver.ticks_left = 1; f.toils[3].tickAction(); Require(f.driver.ReadyForNext, "scene did not finish");
            f.toils[3].Finish(); f.toils[4].initAction(); f.driver.Finish(JobCondition.Succeeded);
            Equal(1, TestWorld.RjwEndCalls, "End"); Equal(1, TestWorld.ProcessSexCalls, "ProcessSex");
            Equal(1, TestWorld.DailyOutcomes, "outcome"); Equal(1, TestWorld.DailyCooldowns, "cooldown");
            Equal(1, TestWorld.TrainerProgressAwards, "trainer specialization completion event");
            Require(!f.comp.isBeingTrained, "occupancy");
        });
        Check("repeated daily payout callback cannot grant trainer progress twice", () =>
        {
            // 同一个真实发起 Job 完成后若即时步骤重入，新增经验只领取一次。
            var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
            f.driver.ticks_left = 1; f.toils[3].tickAction(); f.toils[3].Finish();
            f.toils[4].initAction(); f.toils[4].initAction();
            Equal(1, TestWorld.TrainerProgressAwards, "repeated daily trainer progress");
        });
        Check("daily late callbacks preserve replacement task and target occupancy", () =>
        {
            var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
            var replacement = new JobDriver_Training { pawn = f.driver.pawn, job = new Job() };
            f.driver.pawn.jobs.curDriver = replacement;
            f.toils[3].Finish(); f.toils[4].initAction(); f.driver.Finish(JobCondition.Incompletable);
            Require(f.comp.isBeingTrained, "old callback cleared new occupancy");
            Equal(0, TestWorld.RjwEndCalls, "late End"); Equal(0, TestWorld.ProcessSexCalls, "late processing");
        });
        Check("daily late cleanup does not clear a newly active ritual", () =>
        {
            var f = Daily(); f.toils[0].initAction(); f.comp.isRitualTraining = true;
            f.driver.Finish(JobCondition.Incompletable);
            Require(f.comp.isBeingTrained && f.comp.isRitualTraining, "ritual occupancy cleared");
        });
        Check("ritual request denied before phase creation cancels and clears occupancy", () =>
        {
            var f = NonOwnerRitual(); Require(BindingRitualStateUtility.TryBeginPhase(f.Master, f.Slave, f.Lord), "prepare ritual");
            f.Comp.restrictionConfig.rules.receiveTraining = false;
            Require(new JobGiver_RitualBinding().GiveJobForTest(f.Master) == null, "denied phase issued");
            Equal(1, f.Job.CancelCalls, "cancel signal"); Cleared(f.Comp);
            Equal(0, f.Lord.ReceivedMemos.Count, "completion memo"); Equal(0, TestWorld.DailyCooldowns, "daily cooldown");
        });
        Check("started ritual phase finishes but revoked next phase cancels without outcome claim", () =>
        {
            var f = NonOwnerRitual(); var execution = new PhaseExecution(f); execution.StartScene();
            f.Comp.restrictionConfig.rules.receiveTraining = false;
            execution.ReachPhaseEnd(); execution.FinishCurrent(JobCondition.Succeeded);
            Equal(1, f.Comp.ritualPhase, "current phase completion"); Equal(1, TestWorld.ProcessSexCalls, "scene completion");
            Require(new JobGiver_RitualBinding().GiveJobForTest(f.Master) == null, "next phase issued");
            Equal(1, f.Job.CancelCalls, "cancel"); Require(!BindingRitualStateUtility.TryClaimOutcome(f.Job), "cancel granted outcome");
            Cleared(f.Comp); Equal(0, f.Lord.ReceivedMemos.Count, "completion memo");
        });
        Check("owner ritual keeps starting with empty assignment and invalid config", () =>
        {
            var f = new RitualFixture(); f.Comp.selectedTrainer = null; f.Comp.restrictionConfig.version = 999;
            var execution = new PhaseExecution(f); execution.StartScene(); execution.ReachPhaseEnd(); execution.FinishCurrent(JobCondition.Succeeded);
            Equal(1, f.Comp.ritualPhase, "owner phase"); Equal(0, f.Job.CancelCalls, "owner cancellation");
        });
        Check("stale rejected ritual driver cannot cancel a replacement ritual", () =>
        {
            var old = new RitualFixture(); var execution = new PhaseExecution(old); old.End();
            var current = new RitualFixture(old.Master, old.Slave);
            Require(BindingRitualStateUtility.TryBeginPhase(current.Master, current.Slave, current.Lord), "new ritual");
            execution.Driver.AbortForRestriction("stale");
            Equal(0, current.Job.CancelCalls, "new ritual cancellation");
            Equal(current.Lord, current.Comp.bindingRitualLord, "new ownership");
        });
    }
}
