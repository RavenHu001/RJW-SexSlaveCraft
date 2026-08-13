using RimWorld;
using rjw;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

// EN: This file drives the Binding Ritual from the initiator side.
// EN: It keeps the master and sex slave at the ritual spot, swaps ritual phases, and decides when the Binding Ritual is truly finished.
// CN: 这个文件实现“绑定仪式”发起者一侧的主流程。
// CN: 它负责把主人和性奴固定在仪式点、切换仪式阶段，并判断绑定仪式何时真正完成。
namespace SexSlaveCraft
{
    public class JobDriver_RitualTraining : JobDriver_SexBaseInitiator
    {
        private bool phaseSceneStarted;
        private bool phaseRanToCompletion;

        protected Pawn Slave => (Pawn)job.targetA.Thing;
        protected LocalTargetInfo RitualSpot => job.targetB;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref phaseSceneStarted, "sscPhaseSceneStarted", false);
            Scribe_Values.Look(ref phaseRanToCompletion, "sscPhaseRanToCompletion", false);
        }

        private void SwitchSexPhase(int phaseIndex)
        {
            // EN: Each ritualPhase maps to one fixed act in the Binding Ritual sequence.
            // CN: 绑定仪式里的每个 ritualPhase，都对应一段固定的性交姿势顺序。
            RitualTrainingUtility.ApplyPhaseToSexProps(Sexprops, pawn, Slave, phaseIndex);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            setup_ticks();
            this.FailOnDespawnedOrNull(TargetIndex.A);

            var lord = pawn.GetLord();
            IntVec3 spotCell = pawn.Position;
            if (lord?.LordJob is LordJob_Ritual ritual) spotCell = ritual.selectedTarget.Cell;
            job.targetB = new LocalTargetInfo(spotCell);

            // EN: Step 1: walk to the ritual spot selected for this Binding Ritual.
            // CN: 步骤 1：走到这次绑定仪式选定的仪式点。
            yield return Toils_Goto.GotoCell(spotCell, PathEndMode.OnCell);

            // EN: Step 2: stack the master and sex slave together, load the current ritual phase, and assign the receiver Job.
            // CN: 步骤 2：把主人和性奴压到同一格，读取当前仪式阶段，并给性奴分配 receiver Job。
            Toil prepare = new Toil();
            prepare.defaultCompleteMode = ToilCompleteMode.Instant;
            prepare.initAction = delegate
            {
                Pawn slave = Slave;
                if (slave == null || !slave.Spawned) return;
                if (!TrainingJobUtility.TryValidateStartOrAbort(pawn, slave, "SSC_RITUAL"))
                {
                    return;
                }

                IntVec3 spot = RitualSpot.Cell;
                TrainingJobUtility.SyncPartnerPosition(pawn, slave, spot);

                if (Sexprops == null)
                    Sexprops = SexUtility.SelectSextype(pawn, slave, false, false);

                // EN: Load the current ritualPhase so the resumed Binding Ritual continues from the right act instead of restarting from phase 0.
                // CN: 读取当前 ritualPhase，保证恢复中的绑定仪式会从正确姿势继续，而不是重新回到 phase 0。
                TrainingJobUtility.MarkTrainingStarted(slave, true);
                var trainingComp = slave.TryGetComp<CompSexSlaveTraining>();
                int phase = trainingComp?.ritualPhase ?? 0;  // 保持当前阶段
                SwitchSexPhase(phase);

                SSCLog.Verbose($"[SSC_RITUAL] Prepare phase: master={pawn.LabelShort}, slave={slave.LabelShort}, ritualPhase={phase}, ritualSpot={spot}");

                if (!TrainingJobUtility.TryStartBindingRitualReceiver(pawn, slave, job, SSCDefOf.SSC_TrainingReceiver, spot))
                {
                    TrainingJobUtility.NotifyTrainingAborted(slave);
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };
            yield return prepare;

            // EN: Step 3: run the ritual sex Toil, same-cell, same-phase, and same Start() timing as daily training.
            // CN: 步骤 3：进入仪式性交 Toil，流程与日常调教相近，但要维持仪式位置、仪式阶段和 Start() 时机。
            Toil sexToil = new Toil();
            sexToil.defaultCompleteMode = ToilCompleteMode.Never;
            sexToil.handlingFacing = true;
            sexToil.initAction = delegate
            {
                Pawn slave = Slave;

                TrainingJobUtility.SyncPartnerPosition(pawn, slave, job.targetB.Cell);
                TrainingJobUtility.EnsureAwake(slave);

                if (!TrainingJobUtility.TryValidateStartOrAbort(pawn, slave, "SSC_RITUAL"))
                {
                    return;
                }

                if (!OnaholeCompatibilityUtility.TrySynchronizeOnaholeSexProps(slave, Sexprops))
                {
                    TrainingJobUtility.NotifyTrainingAborted(slave);
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                string ritualSexType = Sexprops != null ? Sexprops.sexType.ToString() : "null";
                SSCLog.Verbose($"[SSC_RITUAL] Start ritual scene: master={pawn.LabelShort}, slave={slave.LabelShort}, ritualPhase={slave.TryGetComp<CompSexSlaveTraining>()?.ritualPhase ?? -1}, sexType={ritualSexType}");
                // EN: Start() must run in initAction so RJW and the receiver Job enter the Binding Ritual scene together.
                // CN: Start() 必须在 initAction 里调用，才能让 RJW 和 receiver Job 同步进入绑定仪式场景。
                Start();
                phaseSceneStarted = true;
                RimTalkCompatibilityUtility.NotifySexStarted(
                    pawn,
                    slave,
                    $"Binding Ritual phase {(slave.TryGetComp<CompSexSlaveTraining>()?.ritualPhase ?? 0) + 1} of 6",
                    ritualSexType);

                SSCLog.Verbose($"[SSC Ritual] Start() called. pawn IsAnimating = {RitualTrainingUtility.IsAnimating(pawn)}");
                if (OnaholeCompatibilityUtility.IsPawnOnOnahole(slave))
                    SSCLog.Important($"[SSC Onahole] Ritual initAction receiver state: {OnaholeCompatibilityUtility.GetOnaholeReceiverStateReport(slave, SSCDefOf.SSC_TrainingReceiver)}");

                // EN: If the usual animation hook misses, start a fallback Binding Ritual animation by hand.
                // CN: 如果常规动画 Hook 没有接上，就在这里手动补启动一次绑定仪式动画。
                if (!RitualTrainingUtility.IsAnimating(pawn))
                {
                    RitualTrainingUtility.TryStartFallbackAnimation(pawn, slave, Bed, animTicks =>
                    {
                        ApplyAnimationTicks(animTicks);

                        if (slave?.jobs?.curDriver is JobDriver_Sex receiverSex)
                        {
                            receiverSex.ticks_left = animTicks;
                            receiverSex.sex_ticks = animTicks;
                            receiverSex.orgasmStartTick = animTicks;
                            receiverSex.duration = animTicks;
                        }
                    });
                }
            };
            sexToil.tickAction = delegate
            {
                Pawn slave = Slave;
                if (pawn.Position != job.targetB.Cell) pawn.Position = job.targetB.Cell;
                if (slave != null && slave.Position != job.targetB.Cell) slave.Position = job.targetB.Cell;

                SexTick(pawn, slave);
                SexUtility.reduce_rest(slave);
                SexUtility.reduce_rest(pawn, 2f);

                if (ticks_left <= 0)
                {
                    phaseRanToCompletion = true;
                    ReadyForNextToil();
                }
            };
            sexToil.AddFinishAction(OnPhaseComplete);
            yield return sexToil;
        }

        private void OnPhaseComplete()
        {
            if (phaseSceneStarted &&
                phaseRanToCompletion &&
                Sexprops != null &&
                Sexprops.pawn != null &&
                Sexprops.partner != null)
            {
                try { SexUtility.ProcessSex(Sexprops); }
                catch (Exception ex) { Log.Error($"[SSC] 结算冲突: {ex.Message}"); }
            }
            base.End();

            Pawn slave = Slave;
            if (slave == null) return;
            OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(slave, pawn);

            // EN: Clear the nude render state left by RJW before the next ritual phase or ritual letter fires.
            // CN: 在下一阶段或仪式结算信件出现前，先清掉 RJW 留下的裸体渲染状态。
            CompRJW comp = slave.GetCompRJW();
            if (comp != null && comp.drawNude)
            {
                comp.drawNude = false;
                slave.Drawer.renderer.SetAllGraphicsDirty();
            }

            if (!phaseSceneStarted || !phaseRanToCompletion)
            {
                SSCLog.WarningImportant(
                    $"[SSC_RITUAL] Phase aborted without progression: master={pawn.LabelShort}, " +
                    $"slave={slave.LabelShort}, sceneStarted={phaseSceneStarted}, " +
                    $"ranToCompletion={phaseRanToCompletion}");
                return;
            }

            var trainingComp = slave.TryGetComp<CompSexSlaveTraining>();
            if (trainingComp == null) return;

            // EN: Each finished sex phase advances ritualPhase; phase 6 means the whole Binding Ritual is complete.
            // CN: 每完成一次性交阶段，ritualPhase 都会推进；推进到 6 就代表整场绑定仪式完成。
            trainingComp.ritualPhase++;
            SSCLog.Verbose($"[SSC Ritual] 阶段完成，ritualPhase 推进至 {trainingComp.ritualPhase}");

            if (trainingComp.ritualPhase >= 6)
            {
                trainingComp.Notify_TrainingCompleted();
                RitualOutcomeEffectWorker_SSCBinding.IsRitualCompletedSuccessfully = true;
                pawn.GetLord()?.ReceiveMemo("SSC_Training_Finished");
                SSCLog.Important($"[SSC_RITUAL] Binding Ritual finished: master={pawn.LabelShort}, slave={slave.LabelShort}, finalPhase={trainingComp.ritualPhase}");
            }
            else
            {
                SSCLog.Verbose($"[SSC_RITUAL] Binding Ritual phase advanced: master={pawn.LabelShort}, slave={slave.LabelShort}, nextPhase={trainingComp.ritualPhase}");
            }
        }

        private void ApplyAnimationTicks(int animationTicks)
        {
            ticks_left = animationTicks;
            sex_ticks = animationTicks;
            orgasmStartTick = animationTicks;
            duration = animationTicks;
        }


    }
}
