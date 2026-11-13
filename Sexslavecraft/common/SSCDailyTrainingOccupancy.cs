// AI 辅助生成。日常占用随当前发起 Job 持有，读档后由发起驱动重建。
using System;
using System.Collections.Generic;
using System.Linq;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    internal sealed class SSCDailyTrainingOccupancy
    {
        private readonly CompSexSlaveTraining targetComp;
        private readonly Pawn target;
        private readonly TrainingJobUtility.JobContext initiator;
        private int callbackDepth;
        private Action pendingFinish;
        private bool finishing;

        private SSCDailyTrainingOccupancy(CompSexSlaveTraining comp, Pawn target, TrainingJobUtility.JobContext initiator)
        {
            targetComp = comp;
            this.target = target;
            this.initiator = initiator;
        }

        internal bool IsHeld => ReferenceEquals(targetComp.DailyTrainingOccupancy, this) &&
            target.TryGetComp<CompSexSlaveTraining>() == targetComp;

        internal bool IsCurrent => IsHeld && initiator.IsCurrent && !initiator.Driver.ended && target.Map == initiator.Map;

        internal bool IsCurrentFor(TrainingJobUtility.JobContext context)
            => IsCurrent && initiator.Driver == context.Driver && ReferenceEquals(initiator.Job, context.Job) &&
                initiator.JobId == context.JobId;

        internal bool InitiatorStillInstalled => initiator.IsCurrent;

        // 原版先解除预约、再调用驱动清理。在 Finish 完成前继续阻止其他发起者。
        private bool IsBlocking => IsHeld && (initiator.IsCurrent || callbackDepth > 0 || finishing);
        internal static bool IsBusy(Pawn target)
            => target?.TryGetComp<CompSexSlaveTraining>()?.DailyTrainingOccupancy?.IsBlocking == true;

        internal static bool HasOtherReceiver(Pawn target, Pawn actor)
        {
            if (!(target?.jobs?.curDriver is JobDriver_SexBaseReciever receiver) || receiver.ended) return false;
            if (OnaholeCompatibilityUtility.IsBeOnaholeDriver(receiver))
                return receiver.parteners?.Any(partner => partner != null && partner != actor) == true;
            if (receiver.job?.def != SSCDefOf.SSC_TrainingReceiver || receiver.Partner == actor) return false;
            var other = new TrainingJobUtility.JobContext(SSCRestrictionJobContext.FindInitiator(receiver));
            return other.IsCurrent && !other.Driver.ended;
        }

        internal static SSCDailyTrainingOccupancy TryClaim(Pawn target, TrainingJobUtility.JobContext context)
        {
            var comp = target?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.DailyTrainingOccupancy?.IsBlocking == true || !context.IsCurrent ||
                context.Driver.ended || context.Target.Thing != target || context.Map == null ||
                target.Map != context.Map || !SSCIdentityUtility.IsSexSlave(target) ||
                BindingRitualStateUtility.HasActiveTargetRitual(target)) return null;

            var claim = new SSCDailyTrainingOccupancy(comp, target, context);
            comp.DailyTrainingOccupancy = claim;
            comp.isBeingTrained = true;
            return claim;
        }

        internal void EnterCallback() => callbackDepth++;

        internal void ExitCallback()
        {
            if (--callbackDepth == 0 && pendingFinish != null && !finishing) CompleteFinish();
        }

        internal void Finish(Action cleanup)
        {
            if (!IsHeld || pendingFinish != null) return;
            pendingFinish = cleanup;
            if (callbackDepth == 0) CompleteFinish();
        }

        private void CompleteFinish()
        {
            finishing = true;
            try { pendingFinish(); }
            finally
            {
                if (ReferenceEquals(targetComp.DailyTrainingOccupancy, this))
                {
                    targetComp.DailyTrainingOccupancy = null;
                    if (!targetComp.isRitualTraining) targetComp.isBeingTrained = false;
                }
                pendingFinish = null;
                finishing = false;
            }
        }

        internal static void ReconcileAfterLoad(IEnumerable<Pawn> pawns)
        {
            // 先按证据排序认领。冲突驱动在其下一次运行时只清理自身。
            var drivers = pawns.Select(pawn => pawn.jobs?.curDriver).OfType<JobDriver_Training>()
                .Where(driver => !driver.ended && driver.Partner != null)
                .Select(driver => (Driver: driver, Evidence: driver.DailyTrainingLoadEvidence()))
                .Where(entry => entry.Evidence != 0)
                .OrderByDescending(entry => entry.Evidence)
                .ThenBy(entry => entry.Driver.job.loadID).ToArray();
            foreach (var entry in drivers) entry.Driver.RestoreDailyTrainingOccupancy();
            foreach (Pawn actor in pawns)
            {
                if (actor.jobs == null) continue;
                var targets = actor.jobs.AllJobs().Where(job => job != null && job != actor.CurJob && job.def == SSCDefOf.TrainingSexSlave)
                    .Select(job => job.targetA.Pawn).Where(target => target != null).Distinct().ToArray();
                foreach (Pawn target in targets) RemoveUnauthorizedQueuedJobs(target, actor);
            }
        }

        internal static void RemoveUnauthorizedQueuedJobs(Pawn target, Pawn actor)
        {
            if (actor?.jobs == null || Scribe.mode != LoadSaveMode.Inactive) return;
            var tracker = actor.jobs;
            var jobs = tracker.AllJobs().Where(job => job != actor.CurJob && IsTrainingFor(job, target))
                .Select(job => (Job: job, Id: job.loadID, Forced: job.playerForced)).ToArray();
            foreach (var entry in jobs)
            {
                if (entry.Job.loadID != entry.Id || !IsTrainingFor(entry.Job, target)) continue;
                if (SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(actor, target, false), !entry.Forced).Allowed)
                    continue;
                if (actor.jobs == tracker && entry.Job != actor.CurJob && entry.Job.loadID == entry.Id &&
                    entry.Job.playerForced == entry.Forced &&
                    IsTrainingFor(entry.Job, target) && tracker.AllJobs().Contains(entry.Job))
                    tracker.EndCurrentOrQueuedJob(entry.Job, JobCondition.Incompletable, startNewJob: false);
            }
        }

        private static bool IsTrainingFor(Job job, Pawn target)
            => job != null && job.def == SSCDefOf.TrainingSexSlave && job.targetA.Pawn == target;
    }
}
