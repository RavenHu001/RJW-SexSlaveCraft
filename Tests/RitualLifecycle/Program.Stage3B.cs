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
            Require(!f.comp.isBeingTrained, "occupancy");
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
