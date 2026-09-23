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
        /// <summary>执行兼容准备和身体校验的简便入口；调用方应已通过低成本身份、排班和许可检查。</summary>
        public static bool TryValidateTarget(Pawn target, bool forced, string logPrefix)
        {
            return TryValidateTarget(target, forced, logPrefix, out _);
        }

        /// <summary>先执行既有 LifeForce 冲突迁移，再校验实际身体条件；此方法不是无副作用的查询。</summary>
        public static bool TryValidateTarget(Pawn target, bool forced, string logPrefix, out string shortReason)
        {
            shortReason = null;
            if (target == null) return false;

            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[{logPrefix}] ValidateTarget start: target={target.LabelShort}, forced={forced}, job={target.CurJobDef?.defName ?? "null"}, ritual={target.TryGetComp<CompSexSlaveTraining>()?.isRitualTraining ?? false}, training={target.TryGetComp<CompSexSlaveTraining>()?.isBeingTrained ?? false}");

            PrepareTargetCompatibility(target);

            if (Trainjudge.TryCanBeFuckedWithReason(target, out string failureReason))
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
                Log.Warning($"[{logPrefix}] Target eligibility failed: {target.LabelShort}. {failureReason}");
            }
            else
            {
                if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[{logPrefix}] Automatic target eligibility failed: {target.LabelShort}. {failureReason}");
            }
            return false;
        }

        /// <summary>执行既有的 LifeForce 冲突迁移；只在目标进入完整兼容校验后调用，不用于候选扫描。</summary>
        private static void PrepareTargetCompatibility(Pawn target)
        {
            // 冲突工具仅在目标实际持有指定 SSC 状态且存在活动 LifeForce 基因时处理，
            // 保留其生成基因包、移除冲突基因和通知行为。不能为了把查询改成只读而
            // 删除此调用，否则先前可训练的兼容对象可能在身体校验前再次被阻断。
            LifeForceConflictUtility.TryRemoveLifeForceGeneIfConflicting(target);
        }

        /// <summary>开始场景前复核身体资格；失败时记录重试冷却、释放准备状态并终止当前发起任务。</summary>
        public static bool TryValidateStartOrAbort(Pawn actor, Pawn target, string logPrefix)
        {
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[{logPrefix}] ValidateStart start: actor={actor?.LabelShort ?? "null"}, target={target?.LabelShort ?? "null"}, actorJob={actor?.CurJobDef?.defName ?? "null"}, targetJob={target?.CurJobDef?.defName ?? "null"}");
            if (Trainjudge.TryCanBeFuckedWithReason(target, out string shortReason))
            {
                if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[{logPrefix}] ValidateStart passed: target={target?.LabelShort ?? "null"}");
                return true;
            }

            // EN: This is the last guard before sex starts, so it must also clear any prepared training state.
            // CN: 这是性行为真正开始前的最后一道守卫，所以也必须顺手清掉已准备好的训练状态。
            MarkValidationFailure(target, logPrefix);
            Messages.Message(shortReason, target, MessageTypeDefOf.RejectInput, false);
            Log.Warning($"[{logPrefix}] Start blocked: {target?.LabelShort}. {shortReason}");
            NotifyTrainingAborted(target);
            actor.jobs.EndCurrentJob(JobCondition.Incompletable);
            return false;
        }

        /// <summary>同步参与者到接收者位置或明确的仪式格，并终止旧寻路以防动画开始后再次走离。</summary>
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

        /// <summary>在已有任务驱动上清除睡眠标志，不创建或更换角色当前任务。</summary>
        public static void EnsureAwake(Pawn pawn)
        {
            if (pawn?.jobs?.curDriver != null)
            {
                pawn.jobs.curDriver.asleep = false;
            }
        }

        /// <summary>登记本次调教准备占用；仪式额外保留阶段间连续占用标记。</summary>
        public static void MarkTrainingStarted(Pawn pawn, bool isRitual)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            comp.isBeingTrained = true;
            if (isRitual)
            {
                comp.isRitualTraining = true;
            }

            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC TrainingState] Started: pawn={pawn.LabelShort}, isRitual={isRitual}, ritualPhase={comp.ritualPhase}");
        }

        /// <summary>释放本次训练启动标记并恢复失效仪式状态；仍有效的仪式保留占用，允许阶段重试。</summary>
        public static void NotifyTrainingAborted(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            // 单次启动失败可能只是当前仪式阶段需要重试。整场仍有效时保留仪式
            // 占用；整场已结束则交给统一恢复入口解除，避免两套清理规则分叉。
            BindingRitualStateUtility.RecoverPawnState(pawn);
            comp.isBeingTrained = false;
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC TrainingState] Aborted: pawn={pawn.LabelShort}");
        }

        /// <summary>清除日常调教占用；有效仪式的占用由仪式生命周期单独管理。</summary>
        public static void CleanupTrainingState(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.isRitualTraining) return;

            comp.isBeingTrained = false;
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC TrainingState] Cleanup normal training: pawn={pawn.LabelShort}");
        }

        /// <summary>记录身体校验失败时刻，让后续工作扫描按既有重试冷却等待。</summary>
        public static void MarkValidationFailure(Pawn pawn, string logPrefix)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            comp.lastFailedTrainingValidationTick = Find.TickManager.TicksGame;
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[{logPrefix}] Validation failure cooldown armed: pawn={pawn.LabelShort}, retryTicks={CompSexSlaveTraining.FailedValidationRetryTicks}");
        }

        /// <summary>为日常调教启动接收任务，复用现存的相同接收任务并释放发起任务的目标预约。</summary>
        public static bool TryStartDailyTrainingReceiver(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef)
        {
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Daily training receiver start requested: actor={actor?.LabelShort ?? "null"}, partner={partner?.LabelShort ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}");
            return TryStartReceiverJobCore(actor, partner, parentJob, receiverJobDef, false, true, false, null);
        }

        /// <summary>为人格排泄启动接收任务，沿用日常接收流程但不改变上层的普通行为许可分类。</summary>
        public static bool TryStartPersonalityExcretionReceiver(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef)
        {
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] PE receiver start requested: actor={actor?.LabelShort ?? "null"}, partner={partner?.LabelShort ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}");
            return TryStartReceiverJobCore(actor, partner, parentJob, receiverJobDef, false, true, false, null);
        }

        /// <summary>在当前仪式地点重新创建接收任务，使下一阶段重新绑定当前地点和主持者。</summary>
        public static bool TryStartBindingRitualReceiver(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef, IntVec3 ritualSpot)
        {
            // 仪式每阶段允许重新启动接收任务，以当前仪式位置为准重新建立配对。
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Binding ritual receiver start requested: actor={actor?.LabelShort ?? "null"}, partner={partner?.LabelShort ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}, ritualSpot={ritualSpot}");
            return TryStartReceiverJobCore(actor, partner, parentJob, receiverJobDef, true, false, true, ritualSpot);
        }

        /// <summary>执行接收任务交接：保留 Onahole 专用接收器，按调用用途处理预约并确认新任务实际接管。</summary>
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

            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Core start: actor={actor.LabelShort}, partner={partner.LabelShort}, actorJob={actor.CurJobDef?.defName ?? "null"}, partnerJob={partner.CurJobDef?.defName ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}, releaseReservation={releaseReservation}, forceRestart={forceRestartExisting}, playerForced={playerForced}, forcedCell={(forcedCell.HasValue ? forcedCell.Value.ToString() : "null")}");

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

                // 家具专用接收任务持有自己的绑定状态，不能替换成 SSC_TrainingReceiver。
                // 打断它可能使家具在同一 tick 重新绑定角色，递归重启发起者的工作任务。
                SSCLog.WarningImportant($"[SSC Onahole] Partner registration failed; preserving the existing Onahole receiver. State: {OnaholeCompatibilityUtility.GetOnaholeReceiverStateReport(partner, receiverJobDef)}");
                return false;
            }

            SyncPartnerPosition(actor, partner, forcedCell);

            if (releaseReservation && actor.Map != null && parentJob != null)
            {
                if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Releasing reservation: actor={actor.LabelShort}, target={parentJob.targetA.Thing?.Label ?? parentJob.targetA.Cell.ToString()}, parentJob={parentJob.def?.defName ?? "null"}");
                actor.Map.reservationManager.Release(parentJob.targetA, actor, parentJob);
            }

            if (!forceRestartExisting && partner.CurJobDef == receiverJobDef)
            {
                if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Partner already in receiver job: partner={partner.LabelShort}, receiverJob={receiverJobDef?.defName ?? "null"}");
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
