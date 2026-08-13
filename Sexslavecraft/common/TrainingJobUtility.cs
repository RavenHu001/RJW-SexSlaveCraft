using System;
using RimWorld;
using Verse;
using Verse.AI;

// EN: This file provides shared flow helpers for training-style jobs.
// EN: It handles target validation, receiver Job startup, same-cell sync, and cleanup of training state.
// CN: 这个文件提供调教类 Job 的流程辅助工具。
// CN: 它负责目标校验、receiver Job 启动、同格同步，以及训练状态的清理。
namespace SexSlaveCraft
{
    public static class TrainingJobUtility
    {
        public static bool TryValidateTarget(Pawn target, bool forced, string logPrefix)
        {
            return TryValidateTarget(target, forced, logPrefix, out _);
        }

        public static bool TryValidateTarget(Pawn target, bool forced, string logPrefix, out string shortReason)
        {
            shortReason = null;
            if (target == null) return false;

            SSCLog.Verbose($"[{logPrefix}] ValidateTarget start: target={target.LabelShort}, forced={forced}, job={target.CurJobDef?.defName ?? "null"}, ritual={target.TryGetComp<CompSexSlaveTraining>()?.isRitualTraining ?? false}, training={target.TryGetComp<CompSexSlaveTraining>()?.isBeingTrained ?? false}");

            // EN: Step 1: clear LifeForce conflicts before any sex-target eligibility check runs.
            // CN: 步骤 1：先清理 LifeForce 冲突，再进入性行为目标资格判定。
            LifeForceConflictUtility.TryRemoveLifeForceGeneIfConflicting(target);

            if (Trainjudge.TryCanBeFuckedWithReason(target, out string failureReason, out string detailedReport))
            {
                shortReason = null;
                return true;
            }

            if (forced)
            {
                JobFailReason.Is(failureReason);
            }

            shortReason = failureReason;
            if (forced)
            {
                Log.Warning($"[{logPrefix}] Target eligibility failed: {target.LabelShort}. {failureReason}\n{detailedReport}");
            }
            else
            {
                SSCLog.Verbose($"[{logPrefix}] Automatic target eligibility failed: {target.LabelShort}. {failureReason}");
            }
            return false;
        }

        public static bool TryValidateStartOrAbort(Pawn actor, Pawn target, string logPrefix)
        {
            SSCLog.Verbose($"[{logPrefix}] ValidateStart start: actor={actor?.LabelShort ?? "null"}, target={target?.LabelShort ?? "null"}, actorJob={actor?.CurJobDef?.defName ?? "null"}, targetJob={target?.CurJobDef?.defName ?? "null"}");
            if (Trainjudge.TryCanBeFuckedWithReason(target, out string shortReason, out string detailedReport))
            {
                SSCLog.Verbose($"[{logPrefix}] ValidateStart passed: target={target?.LabelShort ?? "null"}");
                return true;
            }

            // EN: This is the last guard before sex starts, so it must also clear any prepared training state.
            // CN: 这是性行为真正开始前的最后一道守卫，所以也必须顺手清掉已准备好的训练状态。
            MarkValidationFailure(target, logPrefix);
            Messages.Message(shortReason, target, MessageTypeDefOf.RejectInput, false);
            Log.Warning($"[{logPrefix}] Start blocked: {target?.LabelShort}. {shortReason}\n{detailedReport}");
            NotifyTrainingAborted(target);
            actor.jobs.EndCurrentJob(JobCondition.Incompletable);
            return false;
        }

        public static void SyncPartnerPosition(Pawn actor, Pawn partner, IntVec3? forcedCell = null)
        {
            if (actor == null || partner == null) return;

            // EN: SSC training expects both pawns to share one cell before RJW setup and animation start.
            // CN: SSC 调教要求双方在进入 RJW 初始化和动画前先站到同一格上。
            IntVec3 destination = forcedCell ?? partner.Position;
            if (partner.Position != destination)
            {
                partner.Position = destination;
                partner.Notify_Teleported(true, false);
            }

            if (actor.Position != destination)
            {
                actor.Position = destination;
                actor.Notify_Teleported(true, false);
            }

            actor.pather.StopDead();
            partner.pather.StopDead();
        }

        public static void EnsureAwake(Pawn pawn)
        {
            if (pawn?.jobs?.curDriver != null)
            {
                pawn.jobs.curDriver.asleep = false;
            }
        }

        public static void MarkTrainingStarted(Pawn pawn, bool isRitual)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            comp.isBeingTrained = true;
            if (isRitual)
            {
                comp.isRitualTraining = true;
            }

            SSCLog.Verbose($"[SSC TrainingState] Started: pawn={pawn.LabelShort}, isRitual={isRitual}, ritualPhase={comp.ritualPhase}");
        }

        public static void NotifyTrainingAborted(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            comp.isBeingTrained = false;
            comp.isRitualTraining = false;
            SSCLog.Verbose($"[SSC TrainingState] Aborted: pawn={pawn.LabelShort}");
        }

        public static void CleanupTrainingState(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.isRitualTraining) return;

            comp.isBeingTrained = false;
            SSCLog.Verbose($"[SSC TrainingState] Cleanup normal training: pawn={pawn.LabelShort}");
        }

        public static void MarkValidationFailure(Pawn pawn, string logPrefix)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            comp.lastFailedTrainingValidationTick = Find.TickManager.TicksGame;
            SSCLog.Verbose($"[{logPrefix}] Validation failure cooldown armed: pawn={pawn.LabelShort}, retryTicks={CompSexSlaveTraining.FailedValidationRetryTicks}");
        }

        public static bool TryStartDailyTrainingReceiver(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef)
        {
            SSCLog.Verbose($"[SSC Receiver] Daily training receiver start requested: actor={actor?.LabelShort ?? "null"}, partner={partner?.LabelShort ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}");
            return TryStartReceiverJobCore(actor, partner, parentJob, receiverJobDef, false, true, false, null);
        }

        public static bool TryStartPersonalityExcretionReceiver(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef)
        {
            SSCLog.Verbose($"[SSC Receiver] PE receiver start requested: actor={actor?.LabelShort ?? "null"}, partner={partner?.LabelShort ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}");
            return TryStartReceiverJobCore(actor, partner, parentJob, receiverJobDef, false, true, false, null);
        }

        public static bool TryStartBindingRitualReceiver(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef, IntVec3 ritualSpot)
        {
            // Binding Ritual is allowed to restart the receiver job so the slave always rebinds to the current ritual spot.
            SSCLog.Verbose($"[SSC Receiver] Binding ritual receiver start requested: actor={actor?.LabelShort ?? "null"}, partner={partner?.LabelShort ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}, ritualSpot={ritualSpot}");
            return TryStartReceiverJobCore(actor, partner, parentJob, receiverJobDef, true, false, true, ritualSpot);
        }

        private static bool TryStartReceiverJobCore(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef, bool playerForced, bool releaseReservation, bool forceRestartExisting, IntVec3? forcedCell)
        {
            if (actor == null || partner == null || !partner.Spawned || receiverJobDef == null)
            {
                SSCLog.WarningImportant(
                    $"[SSC Receiver] Invalid receiver start request: actor={actor?.LabelShort ?? "null"}, " +
                    $"partner={partner?.LabelShort ?? "null"}, spawned={partner?.Spawned ?? false}, " +
                    $"receiverJob={receiverJobDef?.defName ?? "null"}");
                return false;
            }

            SSCLog.Verbose($"[SSC Receiver] Core start: actor={actor.LabelShort}, partner={partner.LabelShort}, actorJob={actor.CurJobDef?.defName ?? "null"}, partnerJob={partner.CurJobDef?.defName ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}, releaseReservation={releaseReservation}, forceRestart={forceRestartExisting}, playerForced={playerForced}, forcedCell={(forcedCell.HasValue ? forcedCell.Value.ToString() : "null")}");

            if (OnaholeCompatibilityUtility.IsPawnOnOnahole(partner))
            {
                // EN: Onahole targets should keep their own receiver Job if partner registration succeeds.
                // CN: Onahole 目标如果注册 partner 成功，就应继续保留自己的专用 receiver Job。
                if (OnaholeCompatibilityUtility.TryRegisterOnaholePartner(partner, actor))
                {
                    SyncPartnerPosition(actor, partner, forcedCell);
                    SSCLog.Important($"[SSC Onahole] Compatible receiver retained: {partner.LabelShort}. State: {OnaholeCompatibilityUtility.GetOnaholeReceiverStateReport(partner, receiverJobDef)}");
                    return true;
                }

                // Never replace BeOnahole with SSC_TrainingReceiver. Interrupting the
                // bound receiver makes Onahole rebind its pawn and can recursively
                // restart the initiator's work job in the same tick.
                SSCLog.WarningImportant($"[SSC Onahole] Partner registration failed; preserving the existing Onahole receiver. State: {OnaholeCompatibilityUtility.GetOnaholeReceiverStateReport(partner, receiverJobDef)}");
                return false;
            }

            SyncPartnerPosition(actor, partner, forcedCell);

            if (releaseReservation && actor.Map != null && parentJob != null)
            {
                SSCLog.Verbose($"[SSC Receiver] Releasing reservation: actor={actor.LabelShort}, target={parentJob.targetA.Thing?.Label ?? parentJob.targetA.Cell.ToString()}, parentJob={parentJob.def?.defName ?? "null"}");
                actor.Map.reservationManager.Release(parentJob.targetA, actor, parentJob);
            }

            if (!forceRestartExisting && partner.CurJobDef == receiverJobDef)
            {
                SSCLog.Verbose($"[SSC Receiver] Partner already in receiver job: partner={partner.LabelShort}, receiverJob={receiverJobDef?.defName ?? "null"}");
                return true;
            }

            Job receiverJob = forcedCell.HasValue
                ? JobMaker.MakeJob(receiverJobDef, actor, forcedCell.Value)
                : JobMaker.MakeJob(receiverJobDef, actor);

            receiverJob.playerForced = playerForced;
            try
            {
                partner.jobs.StartJob(receiverJob, JobCondition.InterruptForced);
            }
            catch (Exception ex)
            {
                SSCLog.Error(
                    $"[SSC Receiver] Receiver job start threw: actor={actor.LabelShort}, " +
                    $"partner={partner.LabelShort}, receiverJob={receiverJobDef.defName}, error={ex}");
                return false;
            }

            if (partner.CurJobDef != receiverJobDef)
            {
                SSCLog.WarningImportant(
                    $"[SSC Receiver] Receiver job failed to become current: actor={actor.LabelShort}, " +
                    $"partner={partner.LabelShort}, expected={receiverJobDef?.defName ?? "null"}, " +
                    $"actual={partner.CurJobDef?.defName ?? "null"}");
                return false;
            }

            SSCLog.Important($"[SSC Receiver] Receiver job started: actor={actor.LabelShort}, partner={partner.LabelShort}, receiverJob={receiverJob.def.defName}, forcedRestart={forceRestartExisting}, releaseReservation={releaseReservation}");
            return true;
        }
    }
}
