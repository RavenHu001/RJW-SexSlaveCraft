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
        /// <summary>在可重入调用前保存任务身份；Job 可被对象池复用，因此同时校验驱动、引用和编号。</summary>
        internal readonly struct JobContext
        {
            internal readonly JobDriver Driver;
            internal readonly Pawn Actor;
            internal readonly Job Job;
            internal readonly int JobId;
            internal readonly Map Map;
            internal readonly LocalTargetInfo Target;

            internal JobContext(JobDriver driver)
            {
                // 在通知、StartJob 等可重入操作前固定编号、地图和预约目标。
                // 后续即使同一 Job 对象被加工任务复用，也不能重新读取它来确认旧任务归属。
                Driver = driver;
                Actor = driver?.pawn;
                Job = driver?.job;
                JobId = Job?.loadID ?? -1;
                Map = Actor?.Map;
                Target = Job != null ? Job.targetA : default(LocalTargetInfo);
            }

            // 跟踪器、驱动与 Job 必须仍指向同一次执行；地图变化也使旧地图预约失效。
            internal bool IsCurrent => Actor?.jobs != null && Job != null &&
                Actor.jobs.curDriver == Driver && ReferenceEquals(Actor.CurJob, Job) &&
                ReferenceEquals(Driver.job, Job) && Job.loadID == JobId && Actor.Map == Map;
        }

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
            // 先验证调用对象，再进行身体检查；旧流程不得给新任务的目标写入失败冷却。
            var context = new JobContext(actor?.jobs?.curDriver);
            if (!context.IsCurrent || context.Target.Thing != target) return false;
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[{logPrefix}] ValidateStart start: actor={actor?.LabelShort ?? "null"}, target={target?.LabelShort ?? "null"}, actorJob={actor?.CurJobDef?.defName ?? "null"}, targetJob={target?.CurJobDef?.defName ?? "null"}");
            if (Trainjudge.TryCanBeFuckedWithReason(target, out string shortReason))
            {
                // 资格检查可能经过外部逻辑；检查通过也不代表原任务仍在执行。
                if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[{logPrefix}] ValidateStart passed: target={target?.LabelShort ?? "null"}");
                return context.IsCurrent;
            }

            if (!context.IsCurrent) return false;
            // EN: This is the last guard before sex starts, so it must also clear any prepared training state.
            // CN: 这是性行为真正开始前的最后一道守卫，所以也必须顺手清掉已准备好的训练状态。
            MarkValidationFailure(target, logPrefix);
            Messages.Message(shortReason, target, MessageTypeDefOf.RejectInput, false);
            Log.Warning($"[{logPrefix}] Start blocked: {target?.LabelShort}. {shortReason}");
            NotifyTrainingAborted(target);
            if (context.IsCurrent) actor.jobs.EndCurrentJob(JobCondition.Incompletable);
            return false;
        }

        /// <summary>保留双方任务并同步位置；通知期间任务被替换时返回 false，让旧回调停止。</summary>
        public static bool SyncPartnerPosition(Pawn actor, Pawn partner, IntVec3? forcedCell = null)
        {
            return SyncPartnerPosition(new JobContext(actor?.jobs?.curDriver), partner, forcedCell);
        }

        private static bool SyncPartnerPosition(JobContext context, Pawn partner, IntVec3? forcedCell)
        {
            // 同时保存接收者当前任务。位置通知若替换了任意一端，余下同步步骤立即停止。
            if (!context.IsCurrent || partner == null || context.Target.Thing != partner) return false;
            Pawn actor = context.Actor;
            var partnerContext = new JobContext(partner.jobs?.curDriver);
            Job originalPartnerJob = partner.CurJob;

            // EN: SSC training expects both pawns to share one cell before RJW setup and animation start.
            // CN: SSC 调教要求双方在进入 RJW 初始化和动画前先站到同一格上。
            IntVec3 destination = forcedCell ?? partner.Position;
            if (partner.Position != destination)
            {
                partner.Position = destination;
                // 位置通知不能结束任务；接收者的旧工作由后续 StartJob 统一交接。
                partner.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
                if (!context.IsCurrent || !PartnerUnchanged(partner, originalPartnerJob, partnerContext)) return false;
            }

            if (actor.Position != destination)
            {
                // 发起者只移动位置，不在此结束调教；否则引擎会立即重新选择吃饭或加工工作。
                actor.Position = destination;
                actor.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
                if (!context.IsCurrent || !PartnerUnchanged(partner, originalPartnerJob, partnerContext)) return false;
            }

            // 仅停止本次配对的旧寻路；逐端复查，避免停止通知期间新接管任务的路径。
            actor.pather.StopDead();
            if (!context.IsCurrent || !PartnerUnchanged(partner, originalPartnerJob, partnerContext)) return false;
            partner.pather.StopDead();
            return context.IsCurrent && PartnerUnchanged(partner, originalPartnerJob, partnerContext);
        }

        // 接收者原本可以没有任务；此时维持“仍无任务”即可，不要求构造有效驱动快照。
        private static bool PartnerUnchanged(Pawn partner, Job originalJob, JobContext context)
            => originalJob == null ? partner.CurJob == null : context.IsCurrent;

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

        /// <summary>为人格排泄交接接收任务；只允许原执行者复用，并保留目标预约直到发起任务结束。</summary>
        public static bool TryStartPersonalityExcretionReceiver(Pawn actor, Pawn partner, Job parentJob, JobDef receiverJobDef)
        {
            // 在同步位置、注册设备参与者、释放预约或复用接收 Job 之前拒绝后来者。
            Pawn activeActor = PersonalityExcretionJobUtility.GetActiveInitiator(partner);
            if (activeActor != null && activeActor != actor) return false;
            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] PE receiver start requested: actor={actor?.LabelShort ?? "null"}, partner={partner?.LabelShort ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}");
            // 原配对结束后，即使旧接收 Job 尚未退出，也必须重新绑定新的执行者。
            return TryStartReceiverJobCore(actor, partner, parentJob, receiverJobDef, false, false, activeActor != actor, null);
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
            // 参数或接收者状态无效时不开始交接，不移动角色也不改变预约。
            if (actor == null || partner == null || !partner.Spawned || receiverJobDef == null)
            {
                SSCLog.WarningImportant(
                    $"[SSC Receiver] Invalid receiver start request: actor={actor?.LabelShort ?? "null"}, " +
                    $"partner={partner?.LabelShort ?? "null"}, spawned={partner?.Spawned ?? false}, " +
                    $"receiverJob={receiverJobDef?.defName ?? "null"}");
                return false;
            }

            // 将调用方传入的任务与当前执行器对齐，阻止已被替换的回调借用新任务身份。
            var context = new JobContext(actor.jobs?.curDriver);
            if (!context.IsCurrent || !ReferenceEquals(context.Job, parentJob) || context.Target.Thing != partner)
                return false;
            // 复用接收器必须属于当前配对；在移动角色或释放预约之前拒绝其他人的接收器。
            if (!forceRestartExisting && partner.CurJobDef == receiverJobDef &&
                (!(partner.jobs.curDriver is rjw.JobDriver_SexBaseReciever existingReceiver) || existingReceiver.Partner != actor))
                return false;

            if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Core start: actor={actor.LabelShort}, partner={partner.LabelShort}, actorJob={actor.CurJobDef?.defName ?? "null"}, partnerJob={partner.CurJobDef?.defName ?? "null"}, receiverJob={receiverJobDef?.defName ?? "null"}, releaseReservation={releaseReservation}, forceRestart={forceRestartExisting}, playerForced={playerForced}, forcedCell={(forcedCell.HasValue ? forcedCell.Value.ToString() : "null")}");

            if (OnaholeCompatibilityUtility.IsPawnOnOnahole(partner))
            {
                // EN: Onahole targets should keep their own receiver Job if partner registration succeeds.
                // CN: Onahole 目标如果注册 partner 成功，就应继续保留自己的专用 receiver Job。
                var receiverContext = new JobContext(partner.jobs.curDriver);
                var receiver = partner.jobs.curDriver as rjw.JobDriver_SexBaseReciever;
                // 区分已有关系和本次新增关系，失败时只撤销自己新增的登记。
                bool alreadyRegistered = receiver?.parteners?.Contains(actor) == true;
                bool registered = OnaholeCompatibilityUtility.TryRegisterOnaholePartner(partner, actor);
                if (registered && receiverContext.IsCurrent && SyncPartnerPosition(context, partner, forcedCell))
                {
                    SSCLog.Important($"[SSC Onahole] Compatible receiver retained: {partner.LabelShort}. State: {OnaholeCompatibilityUtility.GetOnaholeReceiverStateReport(partner, receiverJobDef)}");
                    return true;
                }

                // 只撤销本次新增的关系，保留家具任务以及后继发起任务已接管的关系。
                if (!alreadyRegistered && receiverContext.IsCurrent &&
                    (context.IsCurrent || !HasCurrentInitiator(actor, partner)))
                    OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(partner, actor);

                // 家具专用接收任务持有自己的绑定状态，不能替换成 SSC_TrainingReceiver。
                // 打断它可能使家具在同一 tick 重新绑定角色，递归重启发起者的工作任务。
                SSCLog.WarningImportant($"[SSC Onahole] Partner registration failed; preserving the existing Onahole receiver. State: {OnaholeCompatibilityUtility.GetOnaholeReceiverStateReport(partner, receiverJobDef)}");
                return false;
            }

            // 保留任务地完成站位同步；只有原发起者仍有效时才继续预约交接。
            if (!SyncPartnerPosition(context, partner, forcedCell)) return false;

            if (releaseReservation && actor.Map != null && parentJob != null)
            {
                // 使用交接前保存的目标；对象池可能已将 parentJob 改为新的加工或吃饭任务。
                if (!context.IsCurrent) return false;
                ReservationManager reservations = context.Map.reservationManager;
                if (reservations.ReservedBy(context.Target, actor, context.Job))
                {
                    if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Releasing reservation: actor={actor.LabelShort}, target={context.Target.Thing?.Label ?? context.Target.Cell.ToString()}, parentJob={context.Job.def?.defName ?? "null"}");
                    reservations.Release(context.Target, actor, context.Job);
                }
            }

            if (!context.IsCurrent) return false;
            if (!forceRestartExisting && partner.CurJobDef == receiverJobDef)
            {
                // 重复请求可以复用同一配对，不能仅按 JobDef 接管另一位发起者的接收器。
                if (!(partner.jobs.curDriver is rjw.JobDriver_SexBaseReciever receiver) || receiver.Partner != actor)
                    return false;
                if (SSCLog.VerboseEnabled) SSCLog.Verbose($"[SSC Receiver] Partner already in receiver job: partner={partner.LabelShort}, receiverJob={receiverJobDef?.defName ?? "null"}");
                return true;
            }

            // 创建接收任务后立即固定编号；StartJob 返回时，此对象也可能已经回池并复用。
            Job receiverJob = forcedCell.HasValue
                ? JobMaker.MakeJob(receiverJobDef, actor, forcedCell.Value)
                : JobMaker.MakeJob(receiverJobDef, actor);

            receiverJob.playerForced = playerForced;
            int receiverJobId = receiverJob.loadID;
            try
            {
                partner.jobs.StartJob(receiverJob, JobCondition.InterruptForced);
            }
            catch (Exception ex)
            {
                // 外部启动逻辑可能安装接收器后抛异常；只清理本次留下的孤立接收器。
                CleanupOrphanReceiver(partner, context, receiverJob, receiverJobId);
                SSCLog.Error(
                    $"[SSC Receiver] Receiver job start threw: actor={actor.LabelShort}, " +
                    $"partner={partner.LabelShort}, receiverJob={receiverJobDef.defName}, error={ex}");
                return false;
            }

            if (!context.IsCurrent)
            {
                // 接收任务启动可能同步触发发起者换工作；交接不能以旧任务名义继续成功。
                CleanupOrphanReceiver(partner, context, receiverJob, receiverJobId);
                return false;
            }

            // 成功要求刚创建的任务、编号和配对全部匹配，不能把随后出现的同名任务当作成功。
            if (!ReferenceEquals(partner.CurJob, receiverJob) || receiverJob.loadID != receiverJobId ||
                !(partner.jobs.curDriver is rjw.JobDriver_SexBaseReciever currentReceiver) || currentReceiver.Partner != actor)
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

        /// <summary>只回收本次创建且未被后续发起任务接管的孤立接收器；不结束后来替换的任务。</summary>
        private static void CleanupOrphanReceiver(Pawn partner, JobContext context, Job receiverJob, int receiverJobId)
        {
            Pawn actor = context.Actor;
            // 先验证接收器仍是本次创建的实例；后继任务或池中复用对象均不归旧回调清理。
            if (!ReferenceEquals(partner.CurJob, receiverJob) || receiverJob.loadID != receiverJobId ||
                !(partner.jobs.curDriver is rjw.JobDriver_SexBaseReciever receiver) || receiver.Partner != actor)
                return;
            // 后续合法发起任务若已接管此配对，保留其接收器；否则只结束孤立接收任务。
            if (!context.IsCurrent && HasCurrentInitiator(actor, partner))
                return;
            partner.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
        }

        // 以当前驱动实际指向的对象确认接管关系，不根据角色身份或调教员指派推测。
        private static bool HasCurrentInitiator(Pawn actor, Pawn partner)
            => actor.jobs?.curDriver is rjw.JobDriver_SexBaseInitiator current && current.Partner == partner;
    }
}
