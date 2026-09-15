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
        private bool phaseFinishHandled;
        private Lord ritualLord;

        /// <summary>读取当前仪式任务 A 目标所指向的接收角色。</summary>
        protected Pawn Slave => (Pawn)job.targetA.Thing;
        /// <summary>读取当前阶段使用的仪式位置，供双方站位同步。</summary>
        protected LocalTargetInfo RitualSpot => job.targetB;

        /// <summary>在阶段 Job 开始前记录所属 Lord 并验证主从归属；无有效仪式时返回 false。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            ritualLord = pawn.GetLord();
            return HasActiveRitual();
        }

        /// <summary>保存或恢复阶段执行标记与所属 Lord，让读档后的回调保留原场次和防重入状态。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref phaseSceneStarted, "sscPhaseSceneStarted", false);
            Scribe_Values.Look(ref phaseRanToCompletion, "sscPhaseRanToCompletion", false);
            Scribe_Values.Look(ref phaseFinishHandled, "sscPhaseFinishHandled", false);
            Scribe_References.Look(ref ritualLord, "sscRitualLord");
        }

        /// <summary>运行时检查本 Job 是否仍对应目标当前的有效仪式，并在需要时认领旧档引用。</summary>
        /// <returns>主从角色、目标占用及本 Job 的 Lord 全部匹配时返回 true。</returns>
        private bool HasActiveRitual()
        {
            // 旧档中的 Job 没有 Lord 引用，在第一次运行检查时认领；不在
            // MakeNewToils 的读档枚举阶段修改状态，避免引用尚未恢复就误清零。
            if (ritualLord == null) ritualLord = pawn.GetLord();
            CompSexSlaveTraining training = Slave?.TryGetComp<CompSexSlaveTraining>();
            if (training?.bindingRitualLord == null && training?.isRitualTraining == true)
            {
                BindingRitualStateUtility.RecoverPawnState(Slave);
            }
            return training?.bindingRitualLord == ritualLord
                && training?.isRitualTraining == true
                && BindingRitualStateUtility.IsActiveRitualFor(pawn, Slave, ritualLord);
        }

        /// <summary>根据从零开始的阶段索引设置当前场景动作；不推进仪式阶段计数。</summary>
        private void SwitchSexPhase(int phaseIndex)
        {
            // EN: Each ritualPhase maps to one fixed act in the Binding Ritual sequence.
            // CN: 绑定仪式里的每个 ritualPhase，都对应一段固定的性交姿势顺序。
            RitualTrainingUtility.ApplyPhaseToSexProps(Sexprops, pawn, Slave, phaseIndex);
        }

        /// <summary>构造移动、准备及执行阶段的 Toil，并注册有效性检查与覆盖整份 Job 的收尾回调。</summary>
        /// <remarks>枚举可能发生在读档期间，因此仪式状态恢复放在运行时回调中执行。</remarks>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            setup_ticks();
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !HasActiveRitual());
            // 全局结束回调覆盖走路和准备阶段。有效仪式中的一次 Job 中断仍可重试；
            // 若仪式已结束则修复占用，不触发成功结算或日常训练冷却。
            AddFinishAction(condition => BindingRitualStateUtility.RecoverPawnState(Slave));

            var lord = ritualLord ?? pawn.GetLord();
            IntVec3 spotCell = job.targetB.IsValid ? job.targetB.Cell : pawn.Position;
            if (lord?.LordJob is LordJob_Ritual ritual) spotCell = ritual.selectedTarget.Cell;
            job.targetB = new LocalTargetInfo(spotCell);

            // EN: Step 1: walk to the ritual spot selected for this Binding Ritual.
            // CN: 步骤 1：走到这次绑定仪式选定的仪式点。
            yield return Toils_Goto.GotoCell(spotCell, PathEndMode.OnCell);

            // EN: Step 2: stack the master and sex slave together, load the current ritual phase, and assign the receiver Job.
            // CN: 步骤 2：把主人和性奴压到同一格，读取当前仪式阶段，并给性奴分配 receiver Job。
            Toil prepare = new Toil();
            prepare.defaultCompleteMode = ToilCompleteMode.Instant;
            // 准备回调：校验本场主从关系，恢复阶段动作并创建仪式接收任务。
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
            // 开始回调：再次校验并同步设备；仅在 Start 后任务仍有效时记录阶段开始和启用动画。
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
                if (pawn.jobs.curDriver != this) return;
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
                    // 动画启动回调：将返回的时长同步到主从双方驱动，避免阶段结束时间不一致。
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
            // 每帧回调：维持双方仪式站位，更新时间和体力；计时耗尽才标记阶段完整执行。
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

        /// <summary>仅一次地收尾当前场景；完整执行且仍属于有效仪式时推进阶段，末阶段发送完成 memo。</summary>
        /// <remarks>中断不推进阶段；防止 memo 同步清理引起重入，并隔离旧 Job 对新仪式的迟到回调。</remarks>
        private void OnPhaseComplete()
        {
            // 最后阶段的 memo 可同步触发整场结算和 Job.Cleanup，再次进入此方法。
            // 在任何 RJW 副作用前置位，确保同一个阶段只处理一次。
            if (phaseFinishHandled) return;
            phaseFinishHandled = true;
            Pawn slave = Slave;
            if (slave == null) return;
            CompSexSlaveTraining training = slave.TryGetComp<CompSexSlaveTraining>();
            if (training?.bindingRitualLord != null && training.bindingRitualLord != ritualLord) return;

            bool completedActivePhase = phaseSceneStarted && phaseRanToCompletion && HasActiveRitual();
            try
            {
                if (completedActivePhase && Sexprops?.pawn != null && Sexprops.partner != null)
                {
                    try { SexUtility.ProcessSex(Sexprops); }
                    catch (Exception ex) { Log.Error($"[SSC] 结算冲突: {ex.Message}"); }
                }

                if (phaseSceneStarted && Sexprops != null) base.End();
                OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(slave, pawn);

                CompRJW receiverComp = slave.GetCompRJW();
                if (receiverComp != null && receiverComp.drawNude)
                {
                    receiverComp.drawNude = false;
                    slave.Drawer.renderer.SetAllGraphicsDirty();
                }

                if (!completedActivePhase || !BindingRitualStateUtility.TryCompletePhase(pawn, slave, ritualLord))
                {
                    SSCLog.Verbose($"[SSC_RITUAL] 阶段中断，不推进: master={pawn.LabelShort}, slave={slave.LabelShort}");
                    return;
                }

                int completedPhases = training.ritualPhase;
                SSCLog.Verbose($"[SSC Ritual] 阶段完成: {completedPhases}/{BindingRitualStateUtility.PhaseCount}");
                if (completedPhases == BindingRitualStateUtility.PhaseCount)
                {
                    // 结果处理器按本场 Lord 领取一次结算资格，随后生命周期补丁收尾。
                    ritualLord.ReceiveMemo("SSC_Training_Finished");
                    SSCLog.Important($"[SSC_RITUAL] Binding Ritual finished: master={pawn.LabelShort}, slave={slave.LabelShort}");
                }
            }
            finally
            {
                BindingRitualStateUtility.RecoverPawnState(slave);
            }
        }

        /// <summary>将回退动画的时长同步到本 Job 的 RJW 计时字段，使场景结束时机保持一致。</summary>
        private void ApplyAnimationTicks(int animationTicks)
        {
            ticks_left = animationTicks;
            sex_ticks = animationTicks;
            orgasmStartTick = animationTicks;
            duration = animationTicks;
        }


    }
}
