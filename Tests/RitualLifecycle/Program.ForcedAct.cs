using System;
using SexSlaveCraft;
using Verse.AI;

internal static partial class Program
{
    /// <summary>实际任务必须处理解析失败，并在解析回调返回后保持原任务归属。</summary>
    private static void RunForcedActBoundaryTests()
    {
        Check("daily forced act failure stops before Start and payout", () =>
        {
            var f = Daily(); f.comp.selectedMode = TrainingActType.Vaginal;
            f.toils[0].initAction(); f.toils[2].initAction();
            bool started = false; TestWorld.OnRjwStart = _ => started = true; TestWorld.ResolveActSucceeds = false;
            f.toils[3].initAction();
            Require(!started && f.driver.pawn.jobs.EndCondition == JobCondition.Incompletable, "invalid daily act started");
            Require(!f.comp.isBeingTrained, "daily occupancy remains"); NoDailyAwards();
        });
        Check("daily forced act callback cannot end a replacement job", () =>
        {
            var f = Daily(); f.comp.selectedMode = TrainingActType.Vaginal;
            f.toils[0].initAction(); f.toils[2].initAction(); CompSexSlaveTraining replacement = null;
            TestWorld.ResolveActSucceeds = false;
            TestWorld.OnResolveAct = () => { var next = Daily(f.driver.pawn); next.toils[0].initAction(); replacement = next.comp; };
            f.toils[3].initAction();
            Require(replacement != null && replacement.isBeingTrained && f.driver.pawn.jobs.EndCondition == null, "new daily job was aborted");
            NoDailyAwards();
        });
        Check("ritual invalid definition still cancels before receiver Start and phase completion", () =>
        {
            var f = new RitualFixture(); var e = new PhaseExecution(f); TestWorld.RitualActSetupSucceeds = false;
            e.Toils[1].initAction();
            Equal(1, f.Job.CancelCalls, "invalid ritual cancellation");
            Equal(0, TestWorld.ProcessSexCalls, "invalid ritual processing"); Equal(0, f.Comp.ritualPhase, "invalid ritual progression");
            NoDailyAwards();
        });
        Check("ritual with unresolved body parts can complete all six original phases", () =>
        {
            var f = new RitualFixture(); TestWorld.ResolveActSucceeds = false;
            for (int phase = 0; phase < 6; phase++)
            {
                var e = new PhaseExecution(f); e.StartScene(); e.ReachPhaseEnd();
                e.FinishCurrent(JobCondition.Succeeded);
            }
            Equal(0, f.Job.CancelCalls, "anatomy cancelled original ritual");
            Equal(6, TestWorld.ProcessSexCalls, "all phases process once");
            Equal(1, f.Lord.ReceivedMemos.Count, "one final completion memo");
        });
        Check("ritual forced act callback preserves a replacement ritual", () =>
        {
            var previous = new RitualFixture(); var e = new PhaseExecution(previous); RitualFixture current = null;
            TestWorld.ResolveActSucceeds = false;
            TestWorld.OnResolveAct = () => { previous.End(); current = new RitualFixture(previous.Master, previous.Slave); Advance(current, 2); new PhaseExecution(current); };
            e.Toils[1].initAction();
            Require(current != null && current.Comp.isRitualTraining, "new ritual occupancy lost");
            Equal(0, current.Job.CancelCalls, "new ritual cancellation"); Equal(2, current.Comp.ritualPhase, "new ritual progression");
        });
        Check("PE fixed act failure stops before Start and extraction", () =>
        {
            var e = new PEExecution(); e.Toils[1].initAction(); bool started = false;
            TestWorld.OnRjwStart = _ => started = true; TestWorld.ResolveActSucceeds = false;
            e.Toils[2].initAction();
            Require(!started && e.Actor.jobs.EndCondition == JobCondition.Incompletable, "invalid PE act started");
            Equal(0, TestWorld.ProcessSexCalls, "invalid PE processing");
        });
        Check("PE fixed act callback cannot end the replacement job", () =>
        {
            var e = new PEExecution(); e.Toils[1].initAction(); TestWorld.ResolveActSucceeds = false;
            TestWorld.OnResolveAct = () => { var next = Daily(e.Actor); next.toils[0].initAction(); };
            e.Toils[2].initAction();
            Require(e.Actor.jobs.curDriver != e.Driver && e.Actor.jobs.EndCondition == null, "new job aborted by old PE resolution");
            Equal(0, TestWorld.ProcessSexCalls, "stale PE processing");
        });
    }
}
