// AI 辅助生成。原生 JobTracker、预约和生产占用。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void DailyTrainingOccupancyChecks()
    {
        Check("daily training occupancy survives cleanup reentry and rejects a second actor until release", () =>
        {
            var first = Pair(started: false);
            Pawn b = Pawn(PawnIdentity.Master);
            JobDriver_Training second = InstallDaily(b, first.receiver);
            var aContext = new TrainingJobUtility.JobContext(first.driver);
            var bContext = new TrainingJobUtility.JobContext(second);
            var claim = SSCDailyTrainingOccupancy.TryClaim(first.receiver, aContext);
            Require(claim?.IsCurrentFor(aContext) == true);
            Require(SSCDailyTrainingOccupancy.TryClaim(first.receiver, bContext) == null);
            first.receiver.TryGetComp<CompSexSlaveTraining>().isBeingTrained = false;
            claim.EnterCallback();
            first.driver.ended = true;
            first.actor.jobs.curJob = null;
            first.actor.jobs.curDriver = null;
            int cleanups = 0;
            claim.Finish(() =>
            {
                cleanups++;
                claim.EnterCallback();
                Require(SSCDailyTrainingOccupancy.TryClaim(first.receiver, bContext) == null);
                claim.ExitCallback();
            });
            Require(cleanups == 0 && SSCDailyTrainingOccupancy.IsBusy(first.receiver));
            claim.ExitCallback();
            Require(cleanups == 1 && !SSCDailyTrainingOccupancy.IsBusy(first.receiver));
            var next = SSCDailyTrainingOccupancy.TryClaim(first.receiver, bContext);
            claim.Finish(() => throw new Exception("old cleanup ran twice"));
            Require(next?.IsCurrentFor(bContext) == true);
        });

        Check("daily training restores an in-transit saved target and prefers the actual receiver in a conflict", () =>
        {
            var pair = Pair(started: false);
            var comp = pair.receiver.TryGetComp<CompSexSlaveTraining>();
            var receiver = pair.receiver.jobs.curDriver;
            pair.receiver.jobs.curDriver = null;
            pair.receiver.jobs.curJob = null;
            Set(pair.driver, "preparedTrainingTarget", pair.receiver);
            SSCDailyTrainingOccupancy.ReconcileAfterLoad(new[] { pair.actor, pair.receiver });
            Require(comp.DailyTrainingOccupancy?.IsCurrentFor(new TrainingJobUtility.JobContext(pair.driver)) == true);
            var firstClaim = comp.DailyTrainingOccupancy;
            SSCDailyTrainingOccupancy.ReconcileAfterLoad(new[] { pair.actor, pair.receiver });
            Require(ReferenceEquals(firstClaim, comp.DailyTrainingOccupancy) &&
                ((JobDriver_Training)pair.driver).HasDailyTrainingOccupancy);

            comp.DailyTrainingOccupancy.Finish(() => { });
            Pawn b = Pawn(PawnIdentity.Master);
            JobDriver_Training second = InstallDaily(b, pair.receiver);
            Set(second, "preparedTrainingTarget", pair.receiver);
            pair.receiver.jobs.curDriver = receiver;
            pair.receiver.jobs.curJob = receiver.job;
            SSCDailyTrainingOccupancy.ReconcileAfterLoad(new[] { b, pair.actor, pair.receiver });
            Require(comp.DailyTrainingOccupancy?.IsCurrentFor(new TrainingJobUtility.JobContext(pair.driver)) == true);
            Require(!second.HasDailyTrainingOccupancy);
        });

        Check("forced daily training cannot reserve another trainer's queued target", () =>
        {
            Pawn first = Pawn(PawnIdentity.Master), second = Pawn(PawnIdentity.Master), target = Pawn(PawnIdentity.Slave);
            var faction = Bare<Faction>();
            first.SetFactionDirect(faction);
            second.SetFactionDirect(faction);
            target.stackCount = 1;
            map.reservationManager = new ReservationManager(map);
            map.physicalInteractionReservationManager = new PhysicalInteractionReservationManager();
            var reservations = map.reservationManager.ReservationsReadOnly;
            var firstJob = InstallDaily(first, target).job;
            var secondJob = InstallDaily(second, target).job;
            secondJob.playerForced = true;
            reservations.Add(new ReservationManager.Reservation(first, firstJob, 1, 0, target, null));

            Require(second.CanReserve(target, 1, 0, null, true));
            Require(!TrainerAssignmentUtility.CanReserveDailyTraining(second, target));
            Require(TrainerAssignmentUtility.CanReserveDailyTraining(first, target));
            Require(reservations.Count == 1 && ReferenceEquals(reservations[0].Job, firstJob));
        });

        Check("daily handoff replaces an ended receiver for the same trainer", () =>
        {
            var pair = Pair(started: false);
            var oldReceiver = pair.receiver.jobs.curDriver;
            oldReceiver.ended = true;
            var context = new TrainingJobUtility.JobContext(pair.driver);
            Require(SSCDailyTrainingOccupancy.TryClaim(pair.receiver, context) != null);

            Require(TrainingJobUtility.TryStartDailyTrainingReceiver(pair.actor, pair.receiver, context.Job,
                SSCDefOf.SSC_TrainingReceiver));
            Require(!ReferenceEquals(pair.receiver.jobs.curDriver, oldReceiver));
            Require(pair.receiver.CurJob.targetA.Pawn == pair.actor);
        });

        Check("daily handoff leaves the target job alone when a path callback revokes occupancy", () =>
        {
            var pair = Pair(started: false);
            var receiver = pair.receiver.jobs.curDriver;
            var context = new TrainingJobUtility.JobContext(pair.driver);
            Require(SSCDailyTrainingOccupancy.TryClaim(pair.receiver, context) != null);
            var method = AccessTools.Method(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StopDead));
            var patch = new Harmony(environment.Id + ".handoff");
            patch.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(ObserveHandoffPathStop)) { priority = Priority.First });
            try
            {
                onHandoffPathStop = () => Set(pair.receiver, "comps", new List<ThingComp>
                    { new CompSexSlaveTraining { parent = pair.receiver, pawnIdentity = PawnIdentity.Slave } });
                Require(!TrainingJobUtility.TryStartDailyTrainingReceiver(pair.actor, pair.receiver, context.Job,
                    SSCDefOf.SSC_TrainingReceiver));
                Require(onHandoffPathStop == null && ReferenceEquals(pair.receiver.jobs.curDriver, receiver));
            }
            finally
            {
                onHandoffPathStop = null;
                patch.Unpatch(method, HarmonyPatchType.Prefix, patch.Id);
            }
        });

        Check("daily training receiver handoff retains the initiator's native reservation", () =>
        {
            var first = Pair(started: false);
            Pawn b = Pawn(PawnIdentity.Master);
            JobDriver_Training second = InstallDaily(b, first.receiver);
            map.reservationManager = new ReservationManager(map);
            var reservations = (List<ReservationManager.Reservation>)AccessTools.Field(typeof(ReservationManager), "reservations").GetValue(map.reservationManager);
            reservations.Add(new ReservationManager.Reservation(first.actor, first.driver.job, 1, 0, first.receiver, null));
            first.receiver.jobs.curDriver = null;
            first.receiver.jobs.curJob = null;
            var claim = SSCDailyTrainingOccupancy.TryClaim(first.receiver, new TrainingJobUtility.JobContext(first.driver));
            Require(claim != null);
            Require(TrainingJobUtility.TryStartDailyTrainingReceiver(first.actor, first.receiver, first.driver.job, SSCDefOf.SSC_TrainingReceiver));
            Job receiverJob = first.receiver.CurJob;
            Require(receiverJob.targetA.Pawn == first.actor && map.reservationManager.ReservedBy(first.receiver, first.actor, first.driver.job));
            Require(!TrainingJobUtility.TryStartDailyTrainingReceiver(b, first.receiver, second.job, SSCDefOf.SSC_TrainingReceiver));
            Require(first.receiver.CurJob == receiverJob && reservations.Count == 1);
        });

        Check("daily training removal cancels just the matching queued job and its native reservation", () =>
        {
            Pawn actor = Pawn(PawnIdentity.Master);
            Pawn target = Pawn(PawnIdentity.Slave);
            Pawn unrelated = Pawn(PawnIdentity.Slave);
            var originalDef = SSCDefOf.TrainingSexSlave;
            SSCDefOf.TrainingSexSlave = new JobDef { defName = "SSC_Training_SexSlave", driverClass = typeof(JobDriver_Training) };
            var comp = target.TryGetComp<CompSexSlaveTraining>();
            Set(Current.Game, "maps", new List<Map>());
            try
            {
                comp.ReplaceAssignedTrainers(new[] { actor });
                Job queued = JobMaker.MakeJob(SSCDefOf.TrainingSexSlave, target);
                Job other = JobMaker.MakeJob(SSCDefOf.TrainingSexSlave, unrelated);
                actor.jobs.jobQueue.EnqueueLast(queued);
                actor.jobs.jobQueue.EnqueueLast(other);
                // 隔离预约添加的地图事件，保留原生排队删除、释放和引用匹配。
                map.reservationManager = new ReservationManager(map);
                map.pawnDestinationReservationManager = new PawnDestinationReservationManager();
                map.enrouteManager = new EnrouteManager(map);
                map.physicalInteractionReservationManager = new PhysicalInteractionReservationManager();
                map.attackTargetReservationManager = new AttackTargetReservationManager(map);
                Find.Maps.Add(map);
                var reservation = new ReservationManager.Reservation(actor, queued, 1, 0, target, null);
                var reservations = (List<ReservationManager.Reservation>)AccessTools.Field(typeof(ReservationManager), "reservations").GetValue(map.reservationManager);
                reservations.Add(reservation);
                comp.RemoveAssignedTrainer(actor);
                Require(!actor.jobs.AllJobs().Contains(queued) && actor.jobs.AllJobs().Contains(other));
                Require(reservations.Count == 0);
            }
            finally
            {
                Find.Maps?.Remove(map);
                SSCDefOf.TrainingSexSlave = originalDef;
            }
        });
    }

    private static Action onHandoffPathStop;

    private static void ObserveHandoffPathStop()
    {
        var callback = onHandoffPathStop;
        onHandoffPathStop = null;
        callback?.Invoke();
    }

    private static JobDriver_Training InstallDaily(Pawn actor, Pawn target)
    {
        var driver = Bare<JobDriver_Training>();
        driver.pawn = actor;
        driver.job = JobMaker.MakeJob(new JobDef { defName = "SSC_Training", driverClass = driver.GetType() }, target);
        actor.jobs.curJob = driver.job;
        actor.jobs.curDriver = driver;
        return driver;
    }
}
