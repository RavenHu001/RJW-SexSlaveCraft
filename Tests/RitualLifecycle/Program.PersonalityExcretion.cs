using System.Linq;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private sealed class PEExecution
    {
        public readonly Pawn Actor, Target;
        public readonly JobDriver_PE Driver;
        public readonly Toil[] Toils;

        public PEExecution(Pawn target = null, bool forced = false, Pawn actor = null)
        {
            Actor = actor ?? TestWorld.NewPawn();
            Target = target ?? TestWorld.NewPawn();
            if (!Target.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreting))
                Target.health.AddHediff(SSCDefOf.SSC_PersonalityExcreting).Severity = 1f;
            var job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PE, Target);
            job.playerForced = forced;
            Actor.jobs.curJob = job;
            Actor.jobs.curDriver = Driver = new JobDriver_PE { pawn = Actor, job = job };
            Toils = Driver.CreateToilsForTest().ToArray();
        }

        public void Start()
        {
            Require(Driver.TryMakePreToilReservations(false), "PE initial reservation");
            Toils[1].initAction();
            Toils[2].initAction();
            Require(Actor.jobs.EndCondition == null, "PE unexpectedly ended during start");
        }

        // 原版在 Job 清理时释放其预约。这里只模拟引擎边界，不替生产交接工具保留或释放预约。
        public void EngineCleanup()
        {
            var manager = Actor.Map.reservationManager;
            if (manager.ReservedBy(Driver.job.targetA, Actor, Driver.job))
                manager.Release(Driver.job.targetA, Actor, Driver.job);
            Actor.jobs.curDriver = null;
            Actor.jobs.curJob = null;
        }
    }

    private static void RunPersonalityExcretionTests()
    {
        RunPersonalityExcretionApproachTests();
        Check("PE 通用能力快照保存后清源，旧人格凭据失效", () =>
        {
            var execution=new PEExecution();var comp=execution.Target.TryGetComp<CompSexSlaveTraining>();
            comp.abilityToggles.Set(SSCAbilityToggleUtility.MasterDesireInfestation,true);
            object token=SSCPersonalityRuntimeUtility.Capture(execution.Target);
            execution.Start();execution.Toils[3].initAction();
            var store=GenSpawn.Spawned.Last().TryGetComp<CompPersonalityStore>();
            Require(store.abilityToggles.Get(SSCAbilityToggleUtility.MasterDesireInfestation),"snapshot lost preference");
            Require(!comp.abilityToggles.HasEntries && !SSCPersonalityRuntimeUtility.IsCurrent(execution.Target,token),"source not cleared");
            Require(!SSCPersonalityRuntimeUtility.IsMutating(execution.Target),"scope leaked");
        });
        Check("PE scene snapshots Master qualification before clearing the original body", () =>
        {
            var execution = new PEExecution();
            var comp = execution.Target.TryGetComp<CompSexSlaveTraining>(); comp.ReplaceMasterQualification(true);
            Require(SSCMasterProgressionUtility.RestoreRecord(execution.Target, 40), "source growth setup");
            execution.Start();
            Require(comp.masterQualificationUnlocked, "scene start cleared qualification before extraction");
            execution.Toils[3].initAction();
            var store = GenSpawn.Spawned.Last().TryGetComp<CompPersonalityStore>();
            Require(store.MasterQualificationUnlocked, "scene snapshot lost the source qualification");
            Equal(40f, store.MasterGrowthPoints, "scene snapshot before source growth clear");
            Equal(-1f, SSCMasterProgressionUtility.GetExperience(execution.Target), "emptied source growth");
            Require(!comp.masterQualificationUnlocked && comp.masterQualificationVersion == 1,
                "scene extraction kept qualification on the emptied body");
        });
        Check("PE cancellation or missing personality storage cannot clear Master qualification", () =>
        {
            var cancelled = new PEExecution(); var comp = cancelled.Target.TryGetComp<CompSexSlaveTraining>();
            comp.abilityToggles.Set(SSCAbilityToggleUtility.MasterDesireInfestation,true);
            comp.ReplaceMasterQualification(true); cancelled.Start();
            cancelled.Toils[2].Finish(); cancelled.EngineCleanup();
            Require(comp.masterQualificationUnlocked, "cancelled scene cleared qualification");
            Require(comp.abilityToggles.Get(SSCAbilityToggleUtility.MasterDesireInfestation),"cancelled scene cleared ability preference");
            var missing = new PEExecution(); var missingComp = missing.Target.TryGetComp<CompSexSlaveTraining>();
            missingComp.abilityToggles.Set(SSCAbilityToggleUtility.MasterDesireInfestation,true);
            missingComp.ReplaceMasterQualification(true); missing.Start();
            ThingMaker.IncludePersonalityStore = false;
            try { missing.Toils[3].initAction(); }
            finally { ThingMaker.IncludePersonalityStore = true; }
            Require(missingComp.masterQualificationUnlocked, "missing storage cleared qualification without preserving it");
            Require(missingComp.abilityToggles.Get(SSCAbilityToggleUtility.MasterDesireInfestation),"missing storage cleared ability preference");
        });
        Check("PE handoff retains its reservation and repeated handoff preserves the receiver", () =>
        {
            var first = new PEExecution();
            first.Start();
            var receiver = (JobDriver_SexBaseReciever)first.Target.jobs.curDriver;
            receiver.ticks_left = 37;
            first.Toils[1].initAction();
            Equal(receiver, first.Target.jobs.curDriver, "original receiver");
            Equal(37, receiver.ticks_left, "remaining progress");
            Equal(1, first.Target.jobs.StartCalls, "receiver starts");
            Require(first.Actor.Map.reservationManager.ReservedBy(first.Driver.job.targetA, first.Actor, first.Driver.job),
                "PE reservation was released during handoff");
            Equal(0, first.Actor.Map.reservationManager.ReleaseCalls, "handoff must not release target");
        });
        foreach (bool firstForced in new[] { false, true })
        foreach (bool secondForced in new[] { false, true })
            Check($"PE contender cannot take an occupied target (first={firstForced}, second={secondForced})", () =>
            {
                var first = new PEExecution(forced: firstForced);
                first.Start();
                var receiver = (JobDriver_SexBaseReciever)first.Target.jobs.curDriver;
                var second = new PEExecution(first.Target, secondForced);
                int previousFailure = first.Target.TryGetComp<CompSexSlaveTraining>().lastFailedTrainingValidationTick;
                second.Actor.Position = new IntVec3(8, 0, 0);
                receiver.ticks_left = 43;
                for (int attempt = 0; attempt < 10; attempt++)
                    Require(new WorkGiver_PE().JobOnThing(second.Actor, first.Target, secondForced) == null,
                        "occupied target was offered again");
                if (secondForced) Equal("SSC_PE_TargetBusy", JobFailReason.LastReason, "busy reason");
                Require(!second.Driver.TryMakePreToilReservations(false), "contender acquired reservation");
                Require(second.Driver.FailConditions.Any(f => f()), "stale in-flight contender was not rejected");
                // 即使直接跳到交接回调，生产工具也应在位置同步和接收器复用之前拒绝。
                second.Toils[1].initAction();
                Equal(JobCondition.Incompletable, second.Actor.jobs.EndCondition.Value, "only contender ends");
                Require(first.Actor.jobs.EndCondition == null, "first actor was interrupted");
                Equal(new IntVec3(8, 0, 0), second.Actor.Position, "contender must not be teleported");
                Equal(receiver, first.Target.jobs.curDriver, "receiver must remain unchanged");
                Equal(43, receiver.ticks_left, "progress must not reset");
                Require(receiver.parteners.SequenceEqual(new[] { first.Actor }), "contender joined participants");
                Equal(previousFailure, first.Target.TryGetComp<CompSexSlaveTraining>().lastFailedTrainingValidationTick,
                    "contention must not trigger body-validation cooldown");
            });
        Check("PE active pair blocks another actor even if external code removed the reservation", () =>
        {
            var first = new PEExecution(); first.Start();
            var reservations = first.Actor.Map.reservationManager;
            if (reservations.ReservedBy(first.Driver.job.targetA, first.Actor, first.Driver.job))
                reservations.Release(first.Driver.job.targetA, first.Actor, first.Driver.job);
            var second = new PEExecution(first.Target, true);
            Require(!WorkGiverTargetUtility.IsValidPETarget(second.Actor, first.Target, true), "forced query bypassed ownership");
            Require(!second.Driver.TryMakePreToilReservations(false), "direct reservation bypassed ownership");
            Require(!TrainingJobUtility.TryStartPersonalityExcretionReceiver(second.Actor, first.Target, second.Driver.job, SSCDefOf.SSC_TrainingReceiver),
                "direct handoff bypassed ownership");
        });
        Check("PE preparation protects its first actor before scene Start", () =>
        {
            var first = new PEExecution();
            Require(first.Driver.TryMakePreToilReservations(false), "first reservation");
            first.Toils[1].initAction();
            var second = new PEExecution(first.Target);
            Equal(first.Actor, PersonalityExcretionJobUtility.GetActiveInitiator(first.Target), "prepared owner");
            Require(!second.Driver.TryMakePreToilReservations(false), "pre-Start contender admitted");
        });
        Check("PE reservation excludes a second actor while the first is still approaching", () =>
        {
            var first = new PEExecution();
            var second = new PEExecution(first.Target, true);
            Require(first.Driver.TryMakePreToilReservations(false), "first walking reservation");
            Require(!second.Driver.TryMakePreToilReservations(false), "second walking reservation");
            first.EngineCleanup();
            Require(second.Driver.TryMakePreToilReservations(false), "cancelled approach kept its reservation");
        });
        Check("PE cancel releases ownership and new actor replaces the stale receiver", () =>
        {
            var first = new PEExecution(); first.Start();
            Job oldReceiver = first.Target.CurJob;
            first.Toils[2].Finish(); first.EngineCleanup();
            var second = new PEExecution(first.Target);
            Require(new WorkGiver_PE().JobOnThing(second.Actor, first.Target) != null, "cancel left target busy");
            second.Start();
            Require(first.Target.CurJob != oldReceiver, "new actor reused old actor's receiver job");
            Equal(second.Actor, first.Target.CurJob.targetA.Thing, "new receiver target");
            Equal(second.Actor, PersonalityExcretionJobUtility.GetActiveInitiator(first.Target), "new owner");
        });
        Check("PE first actor completes one extraction after a rejected contender", () =>
        {
            GenSpawn.Spawned.Clear();
            PetExtractionBoundary.Calls.Clear();
            var first = new PEExecution(); first.Start();
            var second = new PEExecution(first.Target);
            second.Toils[1].initAction();
            first.Driver.ticks_left = 1;
            first.Toils[2].tickAction();
            Require(first.Driver.ReadyForNext, "first actor could not finish scene");
            first.Toils[2].Finish();
            first.Toils[3].initAction();
            first.EngineCleanup();
            Equal(1, GenSpawn.Spawned.Count, "personality gel count");
            Equal(first.Target, GenSpawn.Spawned.Single().TryGetComp<CompPersonalityStore>().Stored, "gel source");
            Equal(1, TestWorld.ProcessSexCalls, "extraction count");
            Require(PetExtractionBoundary.Calls.SequenceEqual(new[] { "store", "cat", "dog" }),
                "scene extraction must save once before detaching cat and dog personality effects");
            Require(first.Target.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done), "hollow state missing");
            Require(!first.Target.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreting), "excretion state not removed");
            Require(PersonalityExcretionJobUtility.GetActiveInitiator(first.Target) == null, "completed scene kept ownership");
            Require(new WorkGiver_PE().JobOnThing(second.Actor, first.Target) == null, "completed body offered for extraction again");
        });
        Check("PE does not detach cat or dog effects before a personality snapshot exists", () =>
        {
            PetExtractionBoundary.Calls.Clear();
            var execution = new PEExecution(); execution.Start();
            Require(PetExtractionBoundary.Calls.Count == 0, "approach or scene start detached personality effects");
            try
            {
                ThingMaker.IncludePersonalityStore = false;
                execution.Toils[2].Finish();
                execution.Toils[3].initAction();
                Require(PetExtractionBoundary.Calls.Count == 0, "missing storage component detached personality effects");
            }
            finally { ThingMaker.IncludePersonalityStore = true; }
        });
    }
}
