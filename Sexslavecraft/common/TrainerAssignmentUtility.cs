using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

// EN: This file decides who may train a given sex slave.
// EN: Permission is delegated to the unified restriction policy; this file coordinates assignment and normal work conditions.
// CN: 这个文件负责决定“谁可以调教这个性奴”。
// CN: 许可交给统一限制策略；这里协调指定调教员、工作类型和目标可用性，不另设主人保护算法。
namespace SexSlaveCraft
{
    public static class TrainerAssignmentUtility
    {
        private static readonly WorkTypeDef TrainSexSlaveWorkType = DefDatabase<WorkTypeDef>.GetNamedSilentFail("TrainSexSlave");

        /// <summary>日常调教复用统一许可和资格检查；自动工作只交给指定者，手动主人命令不受其他指派排除。</summary>
        public static bool IsAllowedTrainer(Pawn sexSlave, Pawn master, bool forced = false)
        {
            return SSCRestrictionTrainingUtility.Evaluate(
                SSCRestrictionTrainingUtility.CreateRequest(master, sexSlave, false), !forced).Allowed;
        }

        /// <summary>从本次分类快照枚举可重新指定的调教员，不以旧 selectedTrainer 锁住候选列表。</summary>
        /// <remarks>原版 FreeColonists 复用临时列表，指派校验再次读取会清空重填；枚举前必须复制。</remarks>
        public static IEnumerable<Pawn> GetTrainerCandidates(Pawn sexSlave)
        {
            Map map = sexSlave?.Map;
            if (map == null) yield break;

            // EN: Candidate scan only returns free colonists who already have the TrainSexSlave work type enabled.
            // CN: 候选扫描只返回自由殖民者里那些已经启用“调教”工作类型的人。
            foreach (Pawn candidate in map.mapPawns.FreeColonists.ToList())
            {
                if (!CanAssignTrainerTo(sexSlave, candidate)) continue;
                yield return candidate;
            }
        }

        /// <summary>指定者选择锁定只读取新配置的调教条目；旧开放字段与公交车不再单独决定锁定。</summary>
        public static Pawn GetForcedMaster(Pawn sexSlave)
        {
            return SSCRestrictionTrainingUtility.GetForcedTrainer(sexSlave);
        }

        /// <summary>检查指派所需身份、存活状态和原有工作开关；工作关闭不抹除调教员身份。</summary>
        public static bool CanAssignAsTrainer(Pawn candidate)
        {
            if (candidate == null || candidate.Dead || candidate.Destroyed || candidate.Downed
                || !SSCIdentityUtility.IsTrainer(candidate)) return false;
            if (candidate.workSettings == null || TrainSexSlaveWorkType == null) return false;
            return candidate.workSettings.WorkIsActive(TrainSexSlaveWorkType);
        }

        /// <summary>菜单和直接指派共用资格：保持自由殖民者范围、排除自身，再只读检查指派及所需授权能否提交。</summary>
        public static bool CanAssignTrainerTo(Pawn sexSlave, Pawn candidate)
        {
            if (sexSlave == null || sexSlave == candidate || sexSlave.Map == null
                || !CanAssignAsTrainer(candidate)
                || !sexSlave.Map.mapPawns.FreeColonists.Contains(candidate)) return false;
            return SSCRestrictionTrainerAssignment.CanAssign(sexSlave, candidate);
        }

        /// <summary>读取有效指定调教员；停用或失效时返回 null，但不清空原始指派记录。</summary>
        /// <remarks>不检查工作开关、地图或倒地状态，避免临时工作条件影响同床与归属。</remarks>
        public static Pawn GetActiveAssignedTrainer(Pawn sexSlave)
        {
            Pawn trainer = sexSlave?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            return trainer != null && trainer != sexSlave && !trainer.Dead && !trainer.Destroyed
                && SSCIdentityUtility.IsTrainer(trainer) ? trainer : null;
        }

        /// <summary>返回指派显示名，并区分身份停用与工作暂停，保留失效指派供玩家调整。</summary>
        public static string GetAssignedTrainerLabel(Pawn sexSlave)
        {
            Pawn trainer = sexSlave?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            if (trainer == null) return Strings.ITab_TrainerNone;
            if (GetActiveAssignedTrainer(sexSlave) == null)
                return "SSC_TrainerIdentity_InactiveAssignment".Translate(trainer.LabelShort);
            if (!CanAssignAsTrainer(trainer))
                return "SSC_TrainerIdentity_UnavailableAssignment".Translate(trainer.LabelShort);
            return trainer.LabelShort;
        }

        /// <summary>只读筛选自动工作的粗略候选；不查询最终许可，不修复状态，也不触发兼容迁移。</summary>
        /// <remarks>可能陈旧的仪式和训练占用标记留给完整准备入口恢复，避免先筛掉后永远无法修复。</remarks>
        public static bool IsPotentialTrainingTarget(Pawn targetPawn, Pawn trainer)
        {
            if (targetPawn == null || trainer == null || targetPawn == trainer || targetPawn.Dead
                || !targetPawn.RaceProps.Humanlike || !SSCIdentityUtility.IsSupportedVanillaStatus(targetPawn)
                || GetActiveAssignedTrainer(targetPawn) != trainer) return false;
            CompSexSlaveTraining comp = targetPawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !comp.IsEnabled || comp.IsWaitingAfterFailedValidation || comp.IsOnCooldown) return false;
            return !comp.scheduledTrainingEnabled || comp.IsScheduledTrainingDayDue && comp.IsWithinScheduledTrainingWindow;
        }

        /// <summary>完整准备日常训练对象：恢复残留占用、检查现行许可与工作条件，最后执行兼容修复和身体校验。</summary>
        /// <returns>目标可接受训练时返回 true；否则返回 false，并通过 reason 提供首个拒绝原因。</returns>
        /// <remarks>此入口包含恢复与兼容迁移副作用；纯候选枚举必须使用 IsPotentialTrainingTarget。</remarks>
        public static bool TryPrepareTrainingTarget(Pawn targetPawn, Pawn trainer, bool forced, out string reason)
        {
            reason = null;
            if (targetPawn == null || trainer == null)
            {
                reason = Strings.RJW_Short_TargetNull;
                return false;
            }

            if (!targetPawn.RaceProps.Humanlike)
            {
                reason = Strings.Train_Reason_NotHumanlike;
                return false;
            }

            if (targetPawn.Dead || targetPawn == trainer)
            {
                reason = Strings.Train_Reason_DeadOrSelf;
                return false;
            }

            if (!SSCIdentityUtility.IsTrainer(trainer))
            {
                reason = "SSC_TrainerIdentity_Required".Translate();
                return false;
            }

            if (!SSCIdentityUtility.IsSupportedVanillaStatus(targetPawn))
            {
                reason = Strings.Train_Reason_InvalidFaction;
                return false;
            }

            // 先修复已结束仪式的残留占用，再判断调教资格；自动和强制命令共用此入口。
            RecoverTrainingTargetState(targetPawn);
            CompSexSlaveTraining compToggle = targetPawn.TryGetComp<CompSexSlaveTraining>();
            if (compToggle == null || !compToggle.IsEnabled)
            {
                reason = Strings.Train_Reason_NotEnabled;
                return false;
            }

            if (compToggle.isRitualTraining)
            {
                reason = Strings.Train_Reason_RitualBusy;
                return false;
            }

            if (compToggle.IsWaitingAfterFailedValidation)
            {
                reason = Strings.Train_Reason_ValidationCooldown;
                return false;
            }

            // EN: Automatic work obeys the saved timetable. A player-forced order may start outside the window,
            // but it still uses the same cooldown and all normal target-safety checks.
            // CN: 自动工作遵守已保存的排班；玩家强制命令可以越过时段限制，但仍保留冷却与目标安全检查。
            if (!forced && compToggle.scheduledTrainingEnabled)
            {
                if (!compToggle.IsScheduledTrainingDayDue)
                {
                    reason = "SSC_Train_Reason_ScheduleDay".Translate(compToggle.scheduledTrainingIntervalDays);
                    return false;
                }

                if (!compToggle.IsWithinScheduledTrainingWindow)
                {
                    reason = "SSC_Train_Reason_OutsideSchedule".Translate(
                        compToggle.scheduledTrainingHour.ToString("00"),
                        compToggle.ScheduledTrainingEndHour.ToString("00"));
                    return false;
                }
            }

            // EN: A sex slave already inside TrainingReceiver or Training_Ritual is busy and should not be pulled away.
            // CN: 已经处于 TrainingReceiver 或 Training_Ritual 的性奴正在被使用，不能再次拉去调教。
            if (compToggle.isBeingTrained)
            {
                reason = Strings.Train_Reason_AlreadyBeingTrained;
                return false;
            }

            if (compToggle.IsOnCooldown)
            {
                int ticksLeft = CompSexSlaveTraining.CooldownTicks - (Find.TickManager.TicksGame - compToggle.lastTrainingTick);
                reason = Strings.Train_Reason_Cooldown(ticksLeft.ToStringTicksToPeriod());
                return false;
            }

            SSCTrainingAdmission admission = SSCRestrictionTrainingUtility.Evaluate(
                SSCRestrictionTrainingUtility.CreateRequest(trainer, targetPawn, false), !forced);
            if (!admission.Allowed)
            {
                reason = admission.Reason;
                return false;
            }

            if (!trainer.CanReserve(targetPawn, 1, -1, null, forced))
            {
                reason = Strings.Train_Reason_NotReservable;
                return false;
            }

            // EN: Run the RJW compatibility validator only after every cheap SSC gate passed.
            // Automatic WorkGiver scans touch many pawns repeatedly; validating disabled,
            // cooling-down, scheduled-later, or trainer-locked pawns caused log spam and
            // let compatibility cleanup mutate pawns that were not actual training targets.
            // CN: 仅在所有低成本 SSC 条件通过后再执行 RJW 兼容校验。自动 WorkGiver
            // 会反复扫描大量 Pawn；过早校验会刷日志，还可能改动并非实际目标的 Pawn。
            if (!TrainingJobUtility.TryValidateTarget(targetPawn, forced, "SSC_TRAIN", out reason))
            {
                return false;
            }

            return true;
        }

        /// <summary>恢复已失效的仪式和日常占用，保留仍有实际接收任务或有效仪式支撑的训练标记。</summary>
        private static void RecoverTrainingTargetState(Pawn targetPawn)
        {
            BindingRitualStateUtility.RecoverPawnState(targetPawn);
            CompSexSlaveTraining comp = targetPawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.isRitualTraining || !comp.isBeingTrained) return;
            bool hasTrainingJob = targetPawn.CurJobDef == SSCDefOf.SSC_TrainingReceiver
                || targetPawn.CurJobDef == SSCDefOf.Training_Ritual;
            if (!hasTrainingJob) comp.isBeingTrained = false;
        }
    }
}
