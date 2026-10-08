using System;
using System.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void NoDailyAwards()
    {
        Equal(0, TestWorld.DailyOutcomes, "daily outcome");
        Equal(0, TestWorld.DailyCooldowns, "daily cooldown");
        Equal(0, TestWorld.CombatantProgressAwards, "target specialization award");
        Equal(0, TestWorld.TrainerProgressAwards, "trainer award");
    }

    private static void RunTrainingIdentityTests()
    {
        foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master })
        foreach (bool rules in new[] { true, false })
        {
            Check($"ritual stage entry rejects {identity} with restriction rules={rules}", () =>
            {
                var f = new RitualFixture();
                SSCMod.settings.enableSexSlaveProtectionRules = rules;
                f.Comp.pawnIdentity = identity;
                f.Comp.specializationProgress = 0.73f;
                f.Comp.lastTrainingTick = 12345;
                Require(BindingRitualStateUtility.IsActiveRitualFor(f.Master, f.Slave, f.Lord), "identity changed structural ownership");
                Require(!BindingRitualStateUtility.TryBeginPhase(f.Master, f.Slave, f.Lord), "invalid identity began phase");
                Equal(1, f.Job.CancelCalls, "matching ritual cancellation");
                Cleared(f.Comp);
                Equal(0.73f, f.Comp.specializationProgress, "saved progress");
                Equal(12345, f.Comp.lastTrainingTick, "saved cooldown");
                Equal(f.Master, f.Comp.selectedTrainer, "saved trainer");
            });
        }
        Check("unbound SSC slave can start first binding with its assigned SSC master", () =>
        {
            var f = new RitualFixture(); f.Slave.BoundMaster = null;
            var execution = new PhaseExecution(f); execution.StartScene();
            Require(f.Comp.isRitualTraining, "first binding occupancy");
            Equal(0, f.Job.CancelCalls, "first binding cancellation");
        });
        Check("identity loss while walking cancels the matching ritual and preserves growth", () =>
        {
            var f = new RitualFixture(); var execution = new PhaseExecution(f);
            f.Comp.specializationProgress = 0.6f; f.Comp.pawnIdentity = PawnIdentity.Unset;
            Require(execution.Driver.FailConditions.Any(fail => fail()), "lost identity did not fail running job");
            execution.FinishCurrent(JobCondition.Incompletable);
            Equal(1, f.Job.CancelCalls, "walking cancellation"); Cleared(f.Comp);
            Equal(0.6f, f.Comp.specializationProgress, "walking saved progress");
            Equal(0, TestWorld.ProcessSexCalls, "walking processing"); NoDailyAwards();
        });
        Check("identity change inside RJW Start cancels without phase advancement", () =>
        {
            var f = new RitualFixture(); var execution = new PhaseExecution(f);
            TestWorld.OnRjwStart = _ => f.Comp.pawnIdentity = PawnIdentity.Unset;
            execution.StartScene(); execution.FinishCurrent(JobCondition.Incompletable);
            Equal(1, f.Job.CancelCalls, "Start cancellation"); Cleared(f.Comp);
            Equal(0, TestWorld.ProcessSexCalls, "Start processing");
            Equal(0, f.Lord.ReceivedMemos.Count, "Start completion memo"); NoDailyAwards();
        });
        Check("identity change inside SexTick does not mark final phase complete", () =>
        {
            var f = new RitualFixture(); Advance(f, 5);
            var execution = new PhaseExecution(f); execution.StartScene();
            TestWorld.OnSexTick = () => f.Comp.pawnIdentity = PawnIdentity.Unset;
            execution.Driver.ticks_left = 1; execution.CurrentToil.tickAction();
            Require(!execution.Driver.ReadyForNext, "lost identity marked completed phase");
            execution.FinishCurrent(JobCondition.Incompletable);
            Equal(1, f.Job.CancelCalls, "SexTick cancellation"); Cleared(f.Comp);
            Equal(0, TestWorld.ProcessSexCalls, "SexTick processing");
            Equal(0, f.Lord.ReceivedMemos.Count, "SexTick completion memo"); NoDailyAwards();
        });
        Check("identity loss after final tick blocks native phase processing and completion memo", () =>
        {
            var f = new RitualFixture(); Advance(f, 5);
            var execution = new PhaseExecution(f); execution.StartScene(); execution.ReachPhaseEnd();
            f.Comp.pawnIdentity = PawnIdentity.Unset; execution.FinishCurrent(JobCondition.Succeeded);
            Equal(1, f.Job.CancelCalls, "final callback cancellation");
            Equal(0, TestWorld.ProcessSexCalls, "final callback processing");
            Equal(0, f.Lord.ReceivedMemos.Count, "final callback memo");
        });
        Check("identity change inside native phase processing cancels before stage advancement", () =>
        {
            var f = new RitualFixture(); Advance(f, 5);
            var execution = new PhaseExecution(f); execution.StartScene(); execution.ReachPhaseEnd();
            TestWorld.OnProcessSex = () => f.Comp.pawnIdentity = PawnIdentity.Unset;
            execution.FinishCurrent(JobCondition.Succeeded);
            Equal(1, TestWorld.ProcessSexCalls, "entered native processing");
            Equal(1, f.Job.CancelCalls, "native processing cancellation"); Cleared(f.Comp);
            Equal(0, f.Lord.ReceivedMemos.Count, "native processing completion memo");
            Require(!BindingRitualStateUtility.TryClaimOutcome(f.Job), "invalid identity claimed ritual award");
        });
        Check("replacement ritual inside native processing retains its stage and occupancy", () =>
        {
            var previous = new RitualFixture(); var execution = new PhaseExecution(previous);
            execution.StartScene(); execution.ReachPhaseEnd(); RitualFixture current = null;
            TestWorld.OnProcessSex = () =>
            {
                previous.End(); current = new RitualFixture(previous.Master, previous.Slave);
                Advance(current, 2); new PhaseExecution(current);
            };
            execution.FinishCurrent(JobCondition.Succeeded);
            Equal(2, current.Comp.ritualPhase, "replacement phase");
            Equal(current.Lord, current.Comp.bindingRitualLord, "replacement ownership");
            Require(current.Comp.isRitualTraining, "old callback cleared replacement occupancy");
            Equal(0, TestWorld.RjwEndCalls, "old callback ended replacement scene");
            Equal(0, current.Lord.ReceivedMemos.Count, "replacement premature memo");
        });
        Check("delayed fallback animation callback cannot rewrite replacement phase timings", () =>
        {
            var previous = new RitualFixture(); TestWorld.Animating = false;
            var execution = new PhaseExecution(previous); execution.StartScene();
            Action<int> callback = TestWorld.AnimationTicksCallback;
            Require(callback != null, "fallback callback missing");
            previous.End(); var current = new RitualFixture(previous.Master, previous.Slave);
            var replacement = new PhaseExecution(current); replacement.StartScene();
            int ticks = replacement.Driver.ticks_left;
            int receiverTicks = (current.Slave.jobs.curDriver as rjw.JobDriver_Sex).ticks_left;
            callback(9000);
            Equal(ticks, replacement.Driver.ticks_left, "replacement actor timing");
            Equal(receiverTicks, (current.Slave.jobs.curDriver as rjw.JobDriver_Sex).ticks_left, "replacement receiver timing");
        });
        foreach (bool changeJobId in new[] { false, true })
            Check($"ritual callbacks retain original target when stage job is repurposed; pooled={changeJobId}", () =>
            {
                var f = new RitualFixture(); var execution = new PhaseExecution(f); execution.StartScene();
                Pawn replacement = TestWorld.NewPawn(); var comp = replacement.TryGetComp<CompSexSlaveTraining>();
                comp.pawnIdentity = PawnIdentity.Slave; comp.bindingRitualLord = f.Lord;
                comp.isRitualTraining = comp.isBeingTrained = true; comp.ritualPhase = 2;
                f.Lord.ownedPawns.Add(replacement); replacement.lord = f.Lord;
                f.Job.Roles["slave"] = replacement; execution.Driver.job.targetA = new LocalTargetInfo(replacement);
                if (changeJobId) execution.Driver.job.loadID++;
                execution.Driver.ticks_left = 1; execution.CurrentToil.tickAction();
                execution.FinishCurrent(JobCondition.Succeeded);
                Require(!execution.Driver.ReadyForNext, "old tick finished repurposed job");
                Require(comp.isRitualTraining && comp.isBeingTrained, "old callback cleared replacement phase");
                Equal(2, comp.ritualPhase, "replacement phase progression");
                Equal(0, TestWorld.ProcessSexCalls, "old callback processing");
                Equal(0, TestWorld.RjwEndCalls, "old callback scene cleanup");
            });
        Check("direct phase completion rejects identity loss and cancels the matching Lord", () =>
        {
            var f = new RitualFixture(); Advance(f, 2); f.Comp.pawnIdentity = PawnIdentity.Unset;
            Require(!BindingRitualStateUtility.TryCompletePhase(f.Master, f.Slave, f.Lord), "invalid phase advanced");
            Equal(1, f.Job.CancelCalls, "direct phase cancellation"); Cleared(f.Comp);
        });
        Check("six-stage outcome claim rejects identity loss and cannot revive after identity restoration", () =>
        {
            var f = new RitualFixture(); Advance(f, 6); f.Comp.pawnIdentity = PawnIdentity.Unset;
            Require(!BindingRitualStateUtility.TryClaimOutcome(f.Job), "invalid outcome claimed");
            Equal(1, f.Job.CancelCalls, "outcome cancellation"); Cleared(f.Comp);
            f.Comp.pawnIdentity = PawnIdentity.Slave;
            Require(!BindingRitualStateUtility.TryClaimOutcome(f.Job), "cancelled outcome revived");
        });
        Check("claimed outcome rechecks identity before later rewards", () =>
        {
            var f = new RitualFixture(); Advance(f, 6);
            Require(BindingRitualStateUtility.TryClaimOutcome(f.Job), "initial outcome claim");
            Require(BindingRitualStateUtility.CanContinueOutcome(f.Job, f.Master, f.Slave), "valid claimed outcome");
            f.Comp.pawnIdentity = PawnIdentity.Unset;
            Require(!BindingRitualStateUtility.CanContinueOutcome(f.Job, f.Master, f.Slave), "invalid identity retained continuation");
            Equal(1, f.Job.CancelCalls, "claimed outcome cancellation"); Cleared(f.Comp);
        });
        Check("claimed outcome continuation cannot borrow replacement ritual eligibility", () =>
        {
            var previous = new RitualFixture(); Advance(previous, 6);
            Require(BindingRitualStateUtility.TryClaimOutcome(previous.Job), "previous outcome claim");
            previous.End(); var current = new RitualFixture(previous.Master, previous.Slave); Advance(current, 6);
            Require(BindingRitualStateUtility.TryClaimOutcome(current.Job), "replacement outcome claim");
            Require(!BindingRitualStateUtility.CanContinueOutcome(previous.Job, previous.Master, previous.Slave), "old outcome continued");
            Require(BindingRitualStateUtility.CanContinueOutcome(current.Job, current.Master, current.Slave), "new outcome damaged");
        });
        foreach (bool savedOwner in new[] { true, false })
            Check($"restored invalid ritual cancels after references are available; savedOwner={savedOwner}", () =>
            {
                var f = new RitualFixture(); Advance(f, 3);
                if (!savedOwner) f.Comp.bindingRitualLord = null;
                f.Comp.pawnIdentity = PawnIdentity.Unset;
                BindingRitualStateUtility.RecoverPawnState(f.Slave);
                Equal(1, f.Job.CancelCalls, "restored ritual cancellation"); Cleared(f.Comp);
                Equal(0, TestWorld.ProcessSexCalls, "restored ritual processing");
            });

        foreach (string boundary in new[] { "Start", "SexTick", "ProcessSex" })
            Check($"daily identity loss inside {boundary} blocks later awards and cooldown", () =>
            {
                var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction();
                if (boundary == "Start") TestWorld.OnRjwStart = _ => f.comp.pawnIdentity = PawnIdentity.Unset;
                f.toils[3].initAction();
                if (boundary == "SexTick") TestWorld.OnSexTick = () => f.comp.pawnIdentity = PawnIdentity.Unset;
                f.driver.ticks_left = 1; f.toils[3].tickAction(); f.toils[3].Finish();
                if (boundary == "ProcessSex") TestWorld.OnProcessSex = () => f.comp.pawnIdentity = PawnIdentity.Unset;
                f.toils[4].initAction();
                Equal(boundary == "ProcessSex" ? 1 : 0, TestWorld.ProcessSexCalls, "native processing boundary");
                NoDailyAwards();
            });
        foreach (string boundary in new[] { "Start", "SexTick", "ProcessSex" })
            Check($"daily replacement inside {boundary} retains new target occupancy and cooldown", () =>
            {
                var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction();
                CompSexSlaveTraining replacement = null;
                Action replace = () =>
                {
                    var next = Daily(f.driver.pawn); replacement = next.comp;
                    replacement.lastTrainingTick = 12345; next.toils[0].initAction();
                };
                if (boundary == "Start") TestWorld.OnRjwStart = _ => replace();
                f.toils[3].initAction();
                if (boundary == "SexTick") TestWorld.OnSexTick = replace;
                f.driver.ticks_left = 1; f.toils[3].tickAction(); f.toils[3].Finish();
                if (boundary == "ProcessSex") TestWorld.OnProcessSex = replace;
                f.toils[4].initAction();
                Require(replacement != null && replacement.isBeingTrained, "old callback cleared new target occupancy");
                Equal(12345, replacement.lastTrainingTick, "new target cooldown"); NoDailyAwards();
            });
        foreach (bool changeJobId in new[] { false, true })
            Check($"daily native processing callback cannot borrow a reused job or changed target; pooled={changeJobId}", () =>
            {
                var f = CompletedDaily(); Pawn replacement = TestWorld.NewPawn();
                var replacementComp = replacement.TryGetComp<CompSexSlaveTraining>();
                replacementComp.pawnIdentity = PawnIdentity.Slave;
                replacementComp.isBeingTrained = true; replacementComp.lastTrainingTick = 12345;
                TestWorld.OnProcessSex = () =>
                {
                    f.driver.job.targetA = new LocalTargetInfo(replacement);
                    if (changeJobId) f.driver.job.loadID++;
                };
                f.toils[4].initAction(); f.toils[3].Finish();
                NoDailyAwards();
                Require(replacementComp.isBeingTrained, "changed target occupancy cleared");
                Equal(12345, replacementComp.lastTrainingTick, "changed target cooldown");
            });
        foreach (bool replaceJob in new[] { false, true })
            Check($"daily position setter change stops before SexTick; replaceJob={replaceJob}", () =>
            {
                var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
                Pawn target = f.driver.Partner; target.Position = new IntVec3(10, 0, 0);
                CompSexSlaveTraining next = null;
                f.driver.pawn.OnPositionChanged = () =>
                {
                    if (replaceJob) { var replacement = Daily(f.driver.pawn); next = replacement.comp; replacement.toils[0].initAction(); }
                    else f.comp.pawnIdentity = PawnIdentity.Unset;
                };
                f.driver.ticks_left = 1; f.toils[3].tickAction();
                Equal(1, f.driver.ticks_left, "position setter ran stale SexTick");
                Require(!f.driver.ReadyForNext, "position setter finished stale job");
                if (replaceJob) Require(next != null && next.isBeingTrained, "replacement occupancy after position setter");
                NoDailyAwards();
            });
        Check("loaded started daily job with invalid SSC identity does not retain payout permission", () =>
        {
            var f = CompletedDaily();
            Scribe.mode = LoadSaveMode.Saving; f.driver.ExposeData();
            var loaded = Daily(); loaded.comp.pawnIdentity = PawnIdentity.Unset;
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.driver.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            loaded.driver.ticks_left = 0; loaded.driver.Sexprops = f.driver.Sexprops;
            loaded.toils[4].initAction();
            Equal(0, TestWorld.ProcessSexCalls, "loaded invalid daily processing"); NoDailyAwards();
        });
    }
}
