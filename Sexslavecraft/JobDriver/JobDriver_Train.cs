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

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Partner, job, 1, 0, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            setup_ticks();
            this.FailOnDespawnedNullOrForbidden(iTarget);
            this.FailOn(() => pawn.Drafted || pawn.IsFighting());
            this.FailOn(() => Partner.IsFighting());
            this.FailOn(() => !pawn.CanReserve(Partner, 1, 0));

            // EN: Step 1: mark the sex slave as already being trained before movement starts, so no other training job grabs the same pawn.
            // CN: 步骤 1：在走位前先把性奴标记为“已在被调教”，避免其他调教 Job 抢走同一个目标。
            yield return new Toil
            {
                initAction = delegate
                {
                    RimTalkCompatibilityUtility.ReserveTrainingJob(pawn, Partner);
                    TrainingJobUtility.MarkTrainingStarted(Partner, false);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            yield return Toils_Goto.GotoThing(iTarget, PathEndMode.OnCell);

            // EN: Step 2: start the receiver Job so the sex slave enters the same daily training scene.
            // CN: 步骤 2：启动 receiver Job，让性奴进入同一段日常调教场景。
            Toil startPartnerJob = new Toil();
            startPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
            startPartnerJob.initAction = delegate
            {
                if (!TrainingJobUtility.TryStartDailyTrainingReceiver(pawn, Partner, job, partnerJob))
                {
                    TrainingJobUtility.MarkValidationFailure(Partner, "SSC_TRAIN_RECEIVER");
                    TrainingJobUtility.NotifyTrainingAborted(Partner);
                    RimTalkCompatibilityUtility.ReleaseTrainingJob(pawn, Partner);
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };
            yield return startPartnerJob;

            // EN: Step 3: run the actual RJW scene while keeping trainer and sex slave synchronized.
            // CN: 步骤 3：运行实际的 RJW 场景，并持续保持调教师和性奴同步。
            Toil sexToil = new Toil();
            sexToil.defaultCompleteMode = ToilCompleteMode.Never;
            sexToil.defaultDuration = duration;
            sexToil.handlingFacing = true;
            sexToil.initAction = delegate
            {
                TrainingJobUtility.SyncPartnerPosition(pawn, Partner);
                TrainingJobUtility.EnsureAwake(Partner);

                if (Sexprops == null)
                {
                    CompSexSlaveTraining compMode = Partner.TryGetComp<CompSexSlaveTraining>();
                    TrainingActType mode = compMode?.selectedMode ?? TrainingActType.Auto;
                    Sexprops = SexUtility.SelectSextype(pawn, Partner, false, false);
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

                if (!OnaholeCompatibilityUtility.TrySynchronizeOnaholeSexProps(Partner, Sexprops))
                {
                    TrainingJobUtility.NotifyTrainingAborted(Partner);
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                string sceneSexType = Sexprops != null ? Sexprops.sexType.ToString() : "null";
                SSCLog.Verbose($"[SSC_TRAIN] Start daily training scene: trainer={pawn.LabelShort}, slave={Partner.LabelShort}, sexType={sceneSexType}, selectedMode={(Partner.TryGetComp<CompSexSlaveTraining>()?.selectedMode.ToString() ?? "null")}");
                Start();
                RimTalkCompatibilityUtility.NotifyTrainingSceneStarted(pawn, Partner, sceneSexType);
                Log.Message("[SSC Debug] Start() called.");
            };
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
            sexToil.AddFinishAction(delegate
            {
                base.End();
                OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(Partner, pawn);
                TrainingJobUtility.CleanupTrainingState(Partner);
                RimTalkCompatibilityUtility.ReleaseTrainingJob(pawn, Partner);
            });
            sexToil.FailOn(() => !OnaholeCompatibilityUtility.IsValidTrainingReceiver(Partner, partnerJob));
            yield return sexToil;

            // EN: Step 4: once the scene is over, process sex first and then convert the result into daily training payout.
            // CN: 步骤 4：场景结束后，先结算性交，再把结果换算成日常调教收益。
            yield return new Toil
            {
                initAction = delegate
                {
                    SexUtility.ProcessSex(Sexprops);
                    ExecuteConditioningOutcome();
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private void ExecuteConditioningOutcome()
        {
            if (Partner == null || Partner.Dead) return;

            // EN: Step 1: remember the daily training score before the facade applies the actual outcome.
            // CN: 步骤 1：先记下这次日常调教分数，再进入正式结算。
            float score = ConditioningUtility.GetScore(pawn, Partner);
            ConditioningUtility.ExecuteOutcome(pawn, Partner);
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

            string finalSexType = Sexprops != null ? Sexprops.sexType.ToString() : "null";
            SSCLog.Important($"[SSC_TRAIN] Daily training completed: trainer={pawn.LabelShort}, slave={Partner.LabelShort}, sexType={finalSexType}, score={score:F2}, cooldownStarted={(compToggle != null)}");
        }
    }

    // ================================================================
    // 接受者 Job
    // ================================================================
    public class JobDriver_TrainingReceiver : JobDriver_SexBaseRecieverLoved
    {
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

        private Toil CreateSimpleSexToil()
        {
            Toil toil = new Toil();
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.socialMode = RandomSocialMode.Off;
            toil.handlingFacing = false;
            toil.tickAction = delegate
            {
                if ((Find.TickManager.TicksGame + pawn.HashOffset()) % ticks_between_hearts == 0)
                    ThrowMetaIconF(pawn.Position, pawn.Map, FleckDefOf.Heart);
            };
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
