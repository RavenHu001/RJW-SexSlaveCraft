using System.Collections.Generic;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

// EN: This file implements the ordinary training sex job.
// EN: The initiator drives the scene, while the receiver Job stays lightweight and only keeps the sex slave in sync.
// CN: 这个文件实现普通调教用的性行为 Job。
// CN: 发起者负责主导整段流程，接受者 Job 则保持轻量，只负责把性奴维持在同步状态。
namespace SexSlaveCraft
{
    // ================================================================
    // 主导者 Job
    // ================================================================
    public class JobDriver_Training : JobDriver_SexBaseInitiator
    {
        private readonly JobDef partnerJob = SSCDefOf.SSC_TrainingReceiver;
        private Pawn preparedTrainingTarget;
        private bool sceneStarted;
        private bool hasSceneRecord = true;
        private bool legacySceneProgress;
        private bool dailyOutcomeClaimed;
        private int approachIdleRetries;
        private const int ApproachRetryIntervalTicks = 60;
        private const int MaxApproachIdleRetries = 10;

        /// <summary>保存本任务实际占用的对象，使行走或准备阶段读档后的中断能释放原占用。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref preparedTrainingTarget, "sscPreparedTrainingTarget");
            Scribe_Values.Look(ref hasSceneRecord, "sscDailySceneRecord", false);
            Scribe_Values.Look(ref sceneStarted, "sscDailySceneStarted", false);
            // 沿用旧特化奖励存档键。现统一认领整次日常结算，避免重复 ProcessSex、
            // 评分或特化发奖；旧档中已领取的任务也不会在升级后补发战斗员经验。
            Scribe_Values.Look(ref dailyOutcomeClaimed, "sscTrainerProgressAwarded", false);
            // 行走中的存档可能已经停在 Goto toil，却没有活动寻路。
            // 保留有限重试次数，读档后继续修复而不会无限挂在同一任务。
            Scribe_Values.Look(ref approachIdleRetries, "sscApproachIdleRetries", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                legacySceneProgress = Sexprops != null && (orgasms > 0 || (duration > 0 && ticks_left < duration));
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!hasSceneRecord && legacySceneProgress) sceneStarted = true;
                hasSceneRecord = true;
            }
            // 升级旧档时任务目标仍属于本日常 Job；仅补领已有占用，不创建训练状态。
            if (Scribe.mode == LoadSaveMode.PostLoadInit && preparedTrainingTarget == null &&
                Partner?.TryGetComp<CompSexSlaveTraining>()?.isBeingTrained == true)
                preparedTrainingTarget = Partner;
        }

        /// <summary>统一守卫通过后预约日常目标；这里只执行正常预约，不再次否决已开始场景的收尾资格。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Partner, job, 1, 0, null, errorOnFailed);
        }

        /// <summary>构造日常调教的占用、移动、接收准备、场景执行和收益结算步骤，并注册中断条件。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            setup_ticks();
            this.FailOnDespawnedNullOrForbidden(iTarget);
            this.FailOn(() => pawn.Drafted || pawn.IsFighting());
            this.FailOn(() => Partner.IsFighting());
            this.FailOn(() => !pawn.CanReserve(Partner, 1, 0));
            // 候选选定后可能开课；在途及执行阶段重新检查，退出时沿用现有占用清理。
            this.FailOn(() => ProgressionEducationCompatibility.ShouldDeferAutomaticTraining(Partner, job.playerForced));
            AddFinishAction(condition => CleanupPreparedTraining());

            // EN: Step 1: mark the sex slave as already being trained before movement starts, so no other training job grabs the same pawn.
            // CN: 步骤 1：在走位前先把性奴标记为“已在被调教”，避免其他调教 Job 抢走同一个目标。
            yield return new Toil
            {
                initAction = delegate
                {
                    preparedTrainingTarget = Partner;
                    TrainingJobUtility.MarkTrainingStarted(Partner, false);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            // 日常目标仍可自由活动。与工作扫描共用 Touch，接近后由场景
            // 初始化同步双方位置。上场 RJW/UAP 动画若留下位置锁，会拦截
            // StartPath 而让原版 Goto toil 永久等待一次永远不会到来的到达通知。
            Toil approachTarget = Toils_Goto.GotoThing(iTarget, PathEndMode.Touch);
            System.Action startApproach = approachTarget.initAction;
            approachTarget.initAction = delegate
            {
                approachIdleRetries = 0;
                // 此时本人的旧场景已结束，释放其遗留锁再发起首次寻路。
                // 不碰目标身上的锁，以免影响目标当前合法的其他活动。
                UapRitualCompatibilityUtility.ReleasePositionLocks(pawn, null);
                startApproach();
            };
            approachTarget.tickAction = delegate
            {
                if (pawn.jobs?.curDriver != this || Partner == null) return;
                if (pawn.pather.MovingNow)
                {
                    approachIdleRetries = 0;
                    return;
                }

                // 读档恢复在走位 toil 中间时不会重跑 initAction。寻路若被旧锁
                // 拦截或后来被外部停止，每 60 tick 只补试一次，避免逐 tick 寻路。
                if (Find.TickManager.TicksGame % ApproachRetryIntervalTicks != 0) return;
                if (++approachIdleRetries > MaxApproachIdleRetries)
                {
                    // 外部持续阻止移动时结束这份无进展的 Job，让工作扫描
                    // 重新选择目标；现有短暂失败冷却防止同目标立即反复分配。
                    TrainingJobUtility.MarkValidationFailure(Partner, "SSC_TRAIN_PATH");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                UapRitualCompatibilityUtility.ReleasePositionLocks(pawn, null);
                pawn.pather.StartPath(new LocalTargetInfo(Partner), PathEndMode.Touch);
            };
            yield return approachTarget;

            // EN: Step 2: start the receiver Job so the sex slave enters the same daily training scene.
            // CN: 步骤 2：启动 receiver Job，让性奴进入同一段日常调教场景。
            Toil startPartnerJob = new Toil();
            startPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
            // 准备回调：启动接收任务；失败时清除训练占用，再中止本任务。
            startPartnerJob.initAction = delegate
            {
                // 进入交接时固定原任务和目标；后续 Partner 可能随 Job 对象复用而改变。
                var context = new TrainingJobUtility.JobContext(this);
                if (!context.IsCurrent) return;
                Pawn target = Partner;
                // 统一步骤守卫已在进入此回调前复查许可与指派，避免另外维护一套拒绝路径。
                bool started = TrainingJobUtility.TryStartDailyTrainingReceiver(pawn, target, job, partnerJob);
                // StartJob/传送通知可重入工作调度；旧回调不能清理新任务或新目标。
                if (!context.IsCurrent) return;
                if (!started)
                {
                    // 只有仍属于本次调教的失败才写冷却并清理占用；换工作已由上方直接返回。
                    TrainingJobUtility.MarkValidationFailure(target, "SSC_TRAIN_RECEIVER");
                    TrainingJobUtility.NotifyTrainingAborted(target);
                    if (context.IsCurrent) pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };
            yield return startPartnerJob;

            // EN: Step 3: run the actual RJW scene while keeping trainer and sex slave synchronized.
            // CN: 步骤 3：运行实际的 RJW 场景，并持续保持调教师和性奴同步。
            Toil sexToil = new Toil();
            sexToil.defaultCompleteMode = ToilCompleteMode.Never;
            sexToil.defaultDuration = duration;
            sexToil.handlingFacing = true;
            // 开始回调：同步位置与动作，检查兼容设备；Start 中止任务时立即返回。
            sexToil.initAction = delegate
            {
                // 场景初始化还会再次同步位置，必须像首次交接一样保护当前任务归属。
                var context = new TrainingJobUtility.JobContext(this);
                if (!context.IsCurrent) return;
                if (!TrainingJobUtility.SyncPartnerPosition(pawn, Partner))
                {
                    // 同步失败可以终止原调教，但不能终止回调期间刚接手的新工作。
                    if (context.IsCurrent) pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                TrainingJobUtility.EnsureAwake(Partner);

                if (Sexprops == null)
                {
                    CompSexSlaveTraining compMode = Partner.TryGetComp<CompSexSlaveTraining>();
                    TrainingActType mode = compMode?.selectedMode ?? TrainingActType.Auto;
                    Sexprops = SexUtility.SelectSextype(pawn, Partner, false, false);
                    // RJW 动作选择返回后复查，再读取目标和应用玩家指定模式。
                    if (!context.IsCurrent) return;
                    if (mode != TrainingActType.Auto)
                    {
                        // EN: The selected training act only overrides RJW when the player explicitly locked a training mode.
                        // CN: 只有玩家手动锁定调教模式时，才会覆写 RJW 自动选择的姿势。
                        RJWSexPropsUtility.ApplyTrainingAct(Sexprops, pawn, Partner, mode);
                    }
                }

                SSCLog.Verbose($"[SSC Debug] Training initAction: partner job driver = {Partner?.jobs?.curDriver?.GetType()?.Name ?? "null"}");
                if (OnaholeCompatibilityUtility.IsPawnOnOnahole(Partner))
                {
                    SSCLog.Important($"[SSC Onahole] Training initAction receiver state: {OnaholeCompatibilityUtility.GetOnaholeReceiverStateReport(Partner, partnerJob)}");
                }

                if (Partner?.jobs?.curDriver is JobDriver_SexBaseReciever recv)
                {
                    SSCLog.Verbose($"[SSC Debug] parteners count = {recv.parteners?.Count ?? -1}");
                }
                else
                {
                    SSCLog.WarningImportant("[SSC Debug] partner is NOT a JobDriver_SexBaseReciever — animation will NOT start!");
                }

                if (!TrainingJobUtility.TryValidateStartOrAbort(pawn, Partner, "SSC_TRAIN"))
                {
                    return;
                }

                // 家具兼容调用可能触发外部任务切换，先复查归属再执行失败清理。
                bool synchronized = OnaholeCompatibilityUtility.TrySynchronizeOnaholeSexProps(Partner, Sexprops);
                if (!context.IsCurrent) return;
                if (!synchronized)
                {
                    TrainingJobUtility.NotifyTrainingAborted(Partner);
                    if (context.IsCurrent) pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                string sceneSexType = Sexprops != null ? Sexprops.sexType.ToString() : "null";
                SSCLog.Verbose($"[SSC_TRAIN] Start daily training scene: trainer={pawn.LabelShort}, slave={Partner.LabelShort}, sexType={sceneSexType}, selectedMode={(Partner.TryGetComp<CompSexSlaveTraining>()?.selectedMode.ToString() ?? "null")}");
                Start();
                // RJW Start 完整返回且原任务仍在，才允许标记已开始并在以后结算。
                if (!context.IsCurrent) return;
                sceneStarted = true;
                Log.Message("[SSC Debug] Start() called.");
            };
            // 每帧回调：更新场景与双方体力，保持位置同步，并在计时结束后切换步骤。
            sexToil.tickAction = delegate
            {
                // EN: Keep the trainer and sex slave on the same cell so the daily training scene does not drift apart.
                // CN: 持续把调教师和性奴压在同一格里，避免整段日常调教场景越跑越散。
                if (pawn.Position != Partner.Position) pawn.Position = Partner.Position;
                if ((Find.TickManager.TicksGame + pawn.thingIDNumber) % ticks_between_hearts == 0)
                {
                    ThrowMetaIconF(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }

                SexTick(pawn, Partner);
                SexUtility.reduce_rest(Partner);
                SexUtility.reduce_rest(pawn, 2f);
                if (ticks_left <= 0) ReadyForNextToil();
            };
            // 收尾回调：释放 RJW 场景、设备关联和训练状态，不在此发放训练收益。
            sexToil.AddFinishAction(delegate
            {
                if (pawn.jobs?.curDriver != this) return;
                if (sceneStarted) base.End();
                OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(Partner, pawn);
                TrainingJobUtility.CleanupTrainingState(Partner);
            });
            sexToil.FailOn(() => !OnaholeCompatibilityUtility.IsValidTrainingReceiver(Partner, partnerJob));
            yield return sexToil;

            // EN: Step 4: once the scene is over, process sex first and then convert the result into daily training payout.
            // CN: 步骤 4：场景结束后，先结算性交，再把结果换算成日常调教收益。
            yield return new Toil
            {
                initAction = delegate
                {
                    if (!sceneStarted || ticks_left > 0 || pawn.jobs?.curDriver != this || dailyOutcomeClaimed ||
                        Sexprops == null || Partner == null || Partner.Dead || Partner.Destroyed) return;
                    // 先保存认领事实，覆盖外部回调重入及结算中途异常后的重复通知。
                    dailyOutcomeClaimed = true;
                    SexUtility.ProcessSex(Sexprops);
                    ExecuteConditioningOutcome();
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        /// <summary>任何退出路径都释放本 Job 建立的日常占用及设备关联；迟到回调不清理新任务或仪式。</summary>
        private void CleanupPreparedTraining()
        {
            if (preparedTrainingTarget == null || pawn.jobs?.curDriver != this) return;
            Pawn target = preparedTrainingTarget;
            preparedTrainingTarget = null;
            if (target.TryGetComp<CompSexSlaveTraining>()?.isRitualTraining == true) return;
            OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(target, pawn);
            TrainingJobUtility.CleanupTrainingState(target);
        }

        /// <summary>对仍存活的目标结算日常调教评分和对应部位经验，随后通知训练完成并启动冷却。</summary>
        private void ExecuteConditioningOutcome()
        {
            if (Partner == null || Partner.Dead || Partner.Destroyed) return;

            // EN: Step 1: remember the daily training score before the facade applies the actual outcome.
            // CN: 步骤 1：先记下这次日常调教分数，再进入正式结算。
            float score = ConditioningUtility.GetScore(pawn, Partner);
            ConditioningUtility.ExecuteOutcome(pawn, Partner, score);
            if (Sexprops != null)
            {
                // EN: Step 2: convert this training scene into the matching body-part experience payout.
                // CN: 步骤 2：把这次调教场景换算成对应部位的经验奖励。
                TrainingExpUtility.ApplyExperienceFromScore(Partner, Sexprops.sexType, score, 1.0f);
            }

            // EN: Step 3: close the training session and start cooldown tracking on the sex slave comp.
            // CN: 步骤 3：结束本次调教，并让性奴组件进入冷却计时。
            CompSexSlaveTraining compToggle = Partner.TryGetComp<CompSexSlaveTraining>();
            if (compToggle != null)
            {
                compToggle.Notify_TrainingCompleted();
            }

            // 整次认领已在进入结算前保存；受训者只领取当前方向的一份基础经验，
            // 施教者的训导官特色经验独立判断。RJW 普通双人经验补丁排除 SSC 日常 Job。
            SpecializationTrainingProgressUtility.NotifyTrainingCompleted(pawn, Partner, score);
            TrainerSpecializationProgressUtility.NotifyProvidedTrainingCompleted(pawn, Partner);

            string finalSexType = Sexprops != null ? Sexprops.sexType.ToString() : "null";
            SSCLog.Important($"[SSC_TRAIN] Daily training completed: trainer={pawn.LabelShort}, slave={Partner.LabelShort}, sexType={finalSexType}, score={score:F2}, cooldownStarted={(compToggle != null)}");
        }
    }

    // ================================================================
    // 接受者 Job
    // ================================================================
    public class JobDriver_TrainingReceiver : JobDriver_SexBaseRecieverLoved
    {
        /// <summary>初始化接收者的参与者和结束条件，预约发起者后进入轻量接收步骤。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            DoSetup();

            SSCLog.Verbose($"[SSC Debug] TrainingReceiver DoSetup complete. parteners count = {parteners?.Count ?? -1}");
            if (parteners != null)
            {
                foreach (Pawn partner in parteners)
                {
                    SSCLog.Verbose($"[SSC Debug]   partener: {partner?.Name}");
                }
            }

            yield return Toils_Reserve.Reserve(TargetIndex.A, 1, 0);
            yield return CreateSimpleSexToil();
        }

        /// <summary>创建由发起者控制时长的接收步骤；定期显示效果，结束时恢复衣着显示并刷新肖像。</summary>
        private Toil CreateSimpleSexToil()
        {
            Toil toil = new Toil();
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.socialMode = RandomSocialMode.Off;
            toil.handlingFacing = false;
            // 每帧回调：按角色错开的时间间隔显示效果，不自行推进接收任务。
            toil.tickAction = delegate
            {
                if ((Find.TickManager.TicksGame + pawn.HashOffset()) % ticks_between_hearts == 0)
                    ThrowMetaIconF(pawn.Position, pawn.Map, FleckDefOf.Heart);
            };
            // 收尾回调：恢复类人角色的衣着显示，并尝试使缓存肖像失效。
            toil.AddFinishAction(delegate
            {
                if (xxx.is_human(pawn))
                {
                    CompRJW compRJW = pawn.GetCompRJW();
                    if (compRJW != null)
                    {
                        compRJW.drawNude = false;
                        pawn.Drawer.renderer.SetAllGraphicsDirty();
                    }
                }

                try
                {
                    PortraitsCache.SetDirty(pawn);
                }
                catch
                {
                }
            });
            return toil;
        }
    }
}
