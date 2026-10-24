using System.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;
#if SSC_TEST_WITH_UAP
using Uap = UAP_Animations.UAP_AnimationPositionLockManager;
#endif

internal static partial class Program
{
    // Run the production approach callbacks; the host only supplies path requests,
    // arrival signals, save fields, and external lock/notification boundaries.
    private static void RunPersonalityExcretionApproachTests()
    {
        Check("PE approach follows its target using Touch without starting the receiver early", () =>
        {
            var execution = new PEExecution();
            execution.Target.Position = new IntVec3(12, 0, 0);
            Require(execution.Driver.TryMakePreToilReservations(false), "initial reservation");
            execution.Toils[0].initAction();
            Equal(PathEndMode.Touch, execution.Actor.pather.LastEndMode, "approach end mode");
            Equal(execution.Target, execution.Actor.pather.LastTarget.Thing, "path destination");
            Require(execution.Actor.pather.MovingNow, "approach did not start moving");
            Equal(0, execution.Target.jobs.StartCalls, "premature receiver");
            execution.Actor.pather.OnArrival = () =>
            {
                execution.Toils[1].initAction();
                execution.Toils[2].initAction();
            };
            execution.Actor.pather.Arrive();
            Equal(execution.Target.Position, execution.Actor.Position, "scene position sync");
            Equal(1, execution.Target.jobs.StartCalls, "receiver after arrival");
            Require(execution.Actor.Map.reservationManager.ReservedBy(execution.Driver.job.targetA,
                execution.Actor, execution.Driver.job), "handoff released the PE reservation");
        });

        Check("PE restored idle approach retries at intervals without rerunning init", () =>
        {
            var execution = new PEExecution();
            execution.Actor.pather.BlockStarts = true;
            Find.TickManager.TicksGame = 1;
            execution.Toils[0].tickAction();
            Equal(0, execution.Actor.pather.StartPathCalls, "off-interval retry");
            Find.TickManager.TicksGame = 60;
            execution.Toils[0].tickAction();
            Equal(1, execution.Actor.pather.StartPathCalls, "first restored retry");
            Equal(PathEndMode.Touch, execution.Actor.pather.LastEndMode, "retry end mode");
            Find.TickManager.TicksGame = 61;
            execution.Toils[0].tickAction();
            Equal(1, execution.Actor.pather.StartPathCalls, "retry frequency");
            execution.Actor.pather.BlockStarts = false;
            Find.TickManager.TicksGame = 120;
            execution.Toils[0].tickAction();
            Require(execution.Actor.pather.MovingNow, "restored approach did not resume");
            Find.TickManager.TicksGame = 180;
            execution.Toils[0].tickAction();
            Equal(2, execution.Actor.pather.StartPathCalls, "moving path restarted");
            Equal(0, execution.Target.jobs.StartCalls, "retry started receiver before arrival");
        });

        Check("PE permanently blocked approach exits with cooldown and no extraction", () =>
        {
            GenSpawn.Spawned.Clear();
            var execution = new PEExecution();
            Require(execution.Driver.TryMakePreToilReservations(false), "initial reservation");
            execution.Actor.pather.BlockStarts = true;
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                Find.TickManager.TicksGame = attempt * 60;
                execution.Toils[0].tickAction();
                Require(execution.Actor.jobs.EndCondition == null, "approach ended before retry budget");
            }
            Find.TickManager.TicksGame = 660;
            execution.Toils[0].tickAction();
            Equal(JobCondition.Incompletable, execution.Actor.jobs.EndCondition.Value, "blocked outcome");
            Equal(10, execution.Actor.pather.StartPathCalls, "bounded retry count");
            Equal(660, execution.Target.TryGetComp<CompSexSlaveTraining>().lastFailedTrainingValidationTick,
                "target retry cooldown");
            Equal(0, execution.Target.jobs.StartCalls, "blocked receiver starts");
            Equal(0, TestWorld.ProcessSexCalls, "blocked extraction processing");
            Equal(0, GenSpawn.Spawned.Count, "blocked gel production");
            execution.EngineCleanup();
            Require(!execution.Actor.Map.reservationManager.ReservedBy(execution.Driver.job.targetA,
                execution.Actor, execution.Driver.job), "engine cleanup left reservation");
        });

        Check("PE approach save restores the remaining retry budget", () =>
        {
            var execution = new PEExecution();
            execution.Actor.pather.BlockStarts = true;
            for (int attempt = 1; attempt <= 7; attempt++)
            {
                Find.TickManager.TicksGame = attempt * 60;
                execution.Toils[0].tickAction();
            }
            Scribe.mode = LoadSaveMode.Saving;
            execution.Driver.ExposeData();
            var restored = new JobDriver_PE { pawn = execution.Actor, job = execution.Driver.job };
            execution.Actor.jobs.curDriver = restored;
            Scribe.mode = LoadSaveMode.LoadingVars;
            restored.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive;
            Toil approach = restored.CreateToilsForTest().First();
            for (int attempt = 8; attempt <= 11; attempt++)
            {
                Find.TickManager.TicksGame = attempt * 60;
                approach.tickAction();
            }
            Equal(10, execution.Actor.pather.StartPathCalls, "restored total retry budget");
            Equal(JobCondition.Incompletable, execution.Actor.jobs.EndCondition.Value, "restored timeout");
        });

        Check("PE legacy save without a retry field receives the full bounded budget", () =>
        {
            var execution = new PEExecution();
            execution.Actor.pather.BlockStarts = true;
            Scribe.mode = LoadSaveMode.LoadingVars;
            execution.Driver.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive;
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                Find.TickManager.TicksGame = attempt * 60;
                execution.Toils[0].tickAction();
            }
            Require(execution.Actor.jobs.EndCondition == null, "legacy save lost its retry budget");
            Find.TickManager.TicksGame = 660;
            execution.Toils[0].tickAction();
            Equal(JobCondition.Incompletable, execution.Actor.jobs.EndCondition.Value, "legacy timeout");
        });

        Check("PE approach resets consecutive failure count once movement resumes", () =>
        {
            var execution = new PEExecution();
            execution.Actor.pather.BlockStarts = true;
            for (int attempt = 1; attempt <= 9; attempt++)
            {
                Find.TickManager.TicksGame = attempt * 60;
                execution.Toils[0].tickAction();
            }
            execution.Actor.pather.BlockStarts = false;
            Find.TickManager.TicksGame = 600;
            execution.Toils[0].tickAction();
            Require(execution.Actor.pather.MovingNow, "movement did not resume");
            Find.TickManager.TicksGame = 601;
            execution.Toils[0].tickAction();
            execution.Actor.pather.StopDead();
            execution.Actor.pather.BlockStarts = true;
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                Find.TickManager.TicksGame = 660 + attempt * 60;
                execution.Toils[0].tickAction();
                Require(execution.Actor.jobs.EndCondition == null, "successful movement failed to reset budget");
            }
            Equal(20, execution.Actor.pather.StartPathCalls, "retry count after movement reset");
        });

        foreach (string replacement in new[] { "job", "driver", "id", "target", "cached-target", "map" })
            Check("PE late approach callbacks preserve replacement state: " + replacement, () =>
            {
                var execution = new PEExecution();
                Pawn nextTarget = TestWorld.NewPawn();
                if (replacement == "job")
                {
                    var nextJob = JobMaker.MakeJob(SSCDefOf.SSC_Job_PE, nextTarget);
                    execution.Driver.job = nextJob;
                    execution.Actor.jobs.curJob = nextJob;
                }
                else if (replacement == "driver")
                    execution.Actor.jobs.curDriver = new JobDriver_PE { pawn = execution.Actor, job = execution.Driver.job };
                else if (replacement == "id") execution.Driver.job.loadID++;
                else if (replacement == "target" || replacement == "cached-target")
                {
                    if (replacement == "cached-target") execution.Driver.PartnerPawn = execution.Target;
                    execution.Driver.job.targetA = new LocalTargetInfo(nextTarget);
                }
                else execution.Actor.Map = new Map();
#if SSC_TEST_WITH_UAP
                Uap.LockParticipants(execution.Actor, nextTarget);
#endif
                execution.Toils[0].initAction();
                Find.TickManager.TicksGame = 660;
                execution.Toils[0].tickAction();
                Equal(0, execution.Actor.pather.StartPathCalls, "stale path request");
                Require(execution.Actor.jobs.EndCondition == null, "replacement job ended");
                Equal(-999999, execution.Target.TryGetComp<CompSexSlaveTraining>().lastFailedTrainingValidationTick,
                    "old target cooldown");
                Equal(-999999, nextTarget.TryGetComp<CompSexSlaveTraining>().lastFailedTrainingValidationTick,
                    "replacement target cooldown");
#if SSC_TEST_WITH_UAP
                Require(Uap.IsLocked(execution.Actor) && Uap.IsLocked(nextTarget), "replacement locks removed");
#endif
            });

#if SSC_TEST_WITH_UAP
        Check("control: an external position lock can swallow a PE path request", () =>
        {
            var execution = new PEExecution();
            Uap.LockParticipants(execution.Actor);
            execution.Actor.pather.BeforeStartPath = () => execution.Actor.pather.BlockStarts = Uap.IsLocked(execution.Actor);
            execution.Actor.pather.StartPath(new LocalTargetInfo(execution.Target), PathEndMode.Touch);
            Require(!execution.Actor.pather.MovingNow, "control did not block path");
        });

        Check("PE same actor completes two targets despite the previous scene's position lock", () =>
        {
            GenSpawn.Spawned.Clear();
            var first = new PEExecution();
            first.Target.Position = new IntVec3(3, 0, 0);
            TestWorld.OnRjwStart = _ => Uap.LockParticipants(first.Actor, first.Target);
            first.Start();
            first.Driver.ticks_left = 1;
            first.Toils[2].tickAction();
            Require(first.Driver.ReadyForNext, "first scene did not finish");
            first.Toils[2].Finish();
            first.Toils[3].initAction();
            first.EngineCleanup();
            Require(Uap.IsLocked(first.Actor), "test failed to retain the preceding lock");
            var second = new PEExecution(actor: first.Actor);
            second.Target.Position = new IntVec3(12, 0, 0);
            Pawn outsider = TestWorld.NewPawn();
            Uap.LockParticipants(second.Target, outsider);
            // Boundary callback represents the external path interception, not SSC recovery logic.
            second.Actor.pather.BeforeStartPath = () => second.Actor.pather.BlockStarts = Uap.IsLocked(second.Actor);
            Require(second.Driver.TryMakePreToilReservations(false), "second reservation");
            TestWorld.OnRjwStart = _ => Uap.LockParticipants(second.Actor, second.Target);
            second.Actor.pather.OnArrival = () =>
            {
                second.Toils[1].initAction();
                second.Toils[2].initAction();
            };
            second.Toils[0].initAction();
            Require(second.Actor.pather.MovingNow, "second approach remained blocked");
            Equal(PathEndMode.Touch, second.Actor.pather.LastEndMode, "second end mode");
            Equal(second.Target, second.Actor.pather.LastTarget.Thing, "second destination");
            Require(Uap.IsLocked(second.Target) && Uap.IsLocked(outsider), "approach unlocked other participants");
            Equal(0, second.Target.jobs.StartCalls, "second receiver before arrival");
            second.Actor.pather.Arrive();
            Equal(second.Target.Position, second.Actor.Position, "second scene synchronization");
            second.Driver.ticks_left = 1;
            second.Toils[2].tickAction();
            Require(second.Driver.ReadyForNext, "second scene could not finish");
            second.Toils[2].Finish();
            second.Toils[3].initAction();
            second.EngineCleanup();
            Equal(2, TestWorld.ProcessSexCalls, "two completed extractions");
            Require(GenSpawn.Spawned.Select(p => p.TryGetComp<CompPersonalityStore>().Stored)
                .SequenceEqual(new[] { first.Target, second.Target }), "gel source or count");
            Require(Uap.IsLocked(outsider), "unrelated animation lock removed");
        });

        Check("PE restored approach removes only the actor's lock before retrying", () =>
        {
            var execution = new PEExecution();
            Uap.LockParticipants(execution.Actor, execution.Target);
            execution.Actor.pather.BeforeStartPath = () => execution.Actor.pather.BlockStarts = Uap.IsLocked(execution.Actor);
            Find.TickManager.TicksGame = 60;
            execution.Toils[0].tickAction();
            Require(execution.Actor.pather.MovingNow, "restored lock blocked retry");
            Require(Uap.IsLocked(execution.Target), "receiver lock removed");
        });

        Check("PE toil reconstruction leaves external position locks untouched", () =>
        {
            var execution = new PEExecution();
            Uap.LockParticipants(execution.Actor, execution.Target);
            execution.Driver.CreateToilsForTest().ToArray();
            Require(Uap.IsLocked(execution.Actor) && Uap.IsLocked(execution.Target), "enumeration unlocked participants");
        });

        foreach (bool retry in new[] { false, true })
            Check("PE unlock callback cannot borrow a pooled replacement job; retry=" + retry, () =>
            {
                var execution = new PEExecution();
                Pawn nextTarget = TestWorld.NewPawn();
                Uap.LockParticipants(execution.Actor);
                Uap.AfterUnlock = () =>
                {
                    execution.Driver.job.loadID++;
                    execution.Driver.job.targetA = new LocalTargetInfo(nextTarget);
                    Uap.LockParticipants(execution.Actor, nextTarget);
                };
                Find.TickManager.TicksGame = 60;
                if (retry) execution.Toils[0].tickAction();
                else execution.Toils[0].initAction();
                Equal(0, execution.Actor.pather.StartPathCalls, "path started after task replacement");
                Require(execution.Actor.jobs.EndCondition == null, "replacement task ended");
                Require(Uap.IsLocked(execution.Actor) && Uap.IsLocked(nextTarget), "replacement locks removed");
                Equal(-999999, nextTarget.TryGetComp<CompSexSlaveTraining>().lastFailedTrainingValidationTick,
                    "replacement cooldown");
            });

        Check("PE unlock API failure still ends a permanently blocked approach within its budget", () =>
        {
            var execution = new PEExecution();
            Uap.LockParticipants(execution.Actor);
            Uap.ThrowOnUnlock = true;
            execution.Actor.pather.BeforeStartPath = () => execution.Actor.pather.BlockStarts = Uap.IsLocked(execution.Actor);
            execution.Toils[0].initAction();
            for (int attempt = 1; attempt <= 11; attempt++)
            {
                Find.TickManager.TicksGame = attempt * 60;
                execution.Toils[0].tickAction();
            }
            Equal(JobCondition.Incompletable, execution.Actor.jobs.EndCondition.Value, "API-failure timeout");
            Equal(11, execution.Actor.pather.StartPathCalls, "initial attempt plus bounded retries");
        });
#endif
    }
}
