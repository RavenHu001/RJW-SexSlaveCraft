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

        /// <summary>从本次地图快照枚举可指派的调教员。</summary>
        /// <remarks>原版 FreeColonists 复用临时列表，指派校验再次读取会清空重填；枚举前必须复制。</remarks>
        public static IEnumerable<Pawn> GetTrainerCandidates(Pawn sexSlave)
        {
            Map map = sexSlave?.Map;
            if (map == null) yield break;

            // 1.7候选由显式菜单说明工作状态；冻结版本仍要求预先启用工作。
            foreach (Pawn candidate in map.mapPawns.FreeColonists.ToList())
            {
#if SSC_RW17
                // 关系页允许随指派启用工作，保留工作禁止者供界面显示原因。
                if (!SSCTrainerWorkUtility.CanConsiderForAdd(sexSlave, candidate)) continue;
#elif SSC_TRAINER_WORK_TEST
                if (!SSCTrainerWorkUtility.CanConsider(sexSlave, candidate)) continue;
#else
                if (!CanAssignTrainerTo(sexSlave, candidate)) continue;
#endif
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
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            return SSCTrainerWorkUtility.GetState(candidate) == SSCTrainerWorkState.Active;
#else
            if (candidate == null || candidate.Dead || candidate.Destroyed || candidate.Downed
                || !SSCIdentityUtility.IsTrainer(candidate)) return false;
            if (candidate.workSettings == null || TrainSexSlaveWorkType == null) return false;
            return candidate.workSettings.WorkIsActive(TrainSexSlaveWorkType);
#endif
        }

        /// <summary>菜单和直接指派共用资格：保持自由殖民者范围、排除自身，再只读检查指派及所需授权能否提交。</summary>
        public static bool CanAssignTrainerTo(Pawn sexSlave, Pawn candidate)
        {
            if (!SSCIdentityUtility.IsSexSlave(sexSlave) || sexSlave == candidate || sexSlave.Map == null
                || !CanAssignAsTrainer(candidate)
                || !sexSlave.Map.mapPawns.FreeColonists.Contains(candidate)) return false;
            return SSCRestrictionTrainerAssignment.CanAssign(sexSlave, candidate);
        }

        /// <summary>读取有效指定调教员；停用或失效时返回 null，但不清空原始指派记录。</summary>
        /// <remarks>不检查工作开关、地图或倒地状态，避免临时工作条件影响同床与归属。</remarks>
        public static Pawn GetActiveAssignedTrainer(Pawn sexSlave)
        {
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            return GetActiveAssignedTrainers(sexSlave).FirstOrDefault();
#else
            Pawn trainer = sexSlave?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            return trainer != null && trainer != sexSlave && !trainer.Dead && !trainer.Destroyed
                && SSCIdentityUtility.IsTrainer(trainer) ? trainer : null;
#endif
        }

#if SSC_RW17 || SSC_TRAINER_WORK_TEST
        internal static IEnumerable<Pawn> GetActiveAssignedTrainers(Pawn sexSlave)
        {
            var comp = sexSlave?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) yield break;
            foreach (Pawn trainer in comp.CopyAssignedTrainers())
                if (HasActiveTrainerIdentity(sexSlave, trainer)) yield return trainer;
        }

        internal static bool IsActiveAssignedTrainer(Pawn sexSlave, Pawn trainer)
            => sexSlave?.TryGetComp<CompSexSlaveTraining>()?.HasAssignedTrainer(trainer) == true &&
                HasActiveTrainerIdentity(sexSlave, trainer);

        private static bool HasActiveTrainerIdentity(Pawn sexSlave, Pawn trainer)
            => CompSexSlaveTraining.IsRetainableTrainer(sexSlave, trainer) && SSCIdentityUtility.IsTrainer(trainer);
#endif

        /// <summary>返回指派显示名，并区分身份停用与工作暂停，保留失效指派供玩家调整。</summary>
        public static string GetAssignedTrainerLabel(Pawn sexSlave)
            => GetAssignedTrainerLabel(sexSlave, sexSlave?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer);

        /// <summary>按关系视图捕获的指派引用格式化状态。</summary>
        internal static string GetAssignedTrainerLabel(Pawn sexSlave, Pawn trainer)
        {
            if (trainer == null) return Strings.ITab_TrainerNone;
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            SSCTrainerWorkState state = SSCTrainerWorkUtility.GetState(trainer, sexSlave);
            if (state != SSCTrainerWorkState.Active)
                return "SSC_TrainerWork_AssignmentStatus".Translate(trainer.LabelShort, SSCTrainerWorkUtility.Reason(state));
#else
            if (GetActiveAssignedTrainer(sexSlave) == null)
                return "SSC_TrainerIdentity_InactiveAssignment".Translate(trainer.LabelShort);
            if (!CanAssignAsTrainer(trainer))
                return "SSC_TrainerIdentity_UnavailableAssignment".Translate(trainer.LabelShort);
#endif
            return trainer.LabelShort;
        }

        /// <summary>只读筛选自动工作的粗略候选；不查询最终许可，不修复状态，也不触发兼容迁移。</summary>
        /// <remarks>可能陈旧的仪式和训练占用标记留给完整准备入口恢复，避免先筛掉后永远无法修复。</remarks>
        public static bool IsPotentialTrainingTarget(Pawn targetPawn, Pawn trainer)
        {
            if (targetPawn == null || trainer == null || targetPawn == trainer || targetPawn.Dead
                || !SSCIdentityUtility.IsSexSlave(targetPawn)
                || !targetPawn.RaceProps.Humanlike || !SSCIdentityUtility.IsSupportedVanillaStatus(targetPawn)) return false;
            CompSexSlaveTraining comp = targetPawn.TryGetComp<CompSexSlaveTraining>();

            // 地图扫描的大多数 Pawn 并未指派给当前执行者。先比较保存引用
            // 和目标基础状态，只对真正相关的目标查询锁链与特化任职资格。
            if (comp == null || !IsAssignedTrainer(comp, trainer) || !comp.IsEnabled
                || comp.IsWaitingAfterFailedValidation || comp.IsOnCooldown) return false;
            if (trainer.Dead || trainer.Destroyed || !SSCIdentityUtility.IsTrainer(trainer)) return false;

            // 这里仍只做粗筛；最终许可、预约和兼容状态交给准备入口检查。
            if (ProgressionEducationCompatibility.ShouldDeferAutomaticTraining(targetPawn)) return false;
            return !comp.scheduledTrainingEnabled || comp.IsScheduledTrainingDayDue && comp.IsWithinScheduledTrainingWindow;
        }

        internal static bool IsAssignedTrainer(CompSexSlaveTraining comp, Pawn trainer)
        {
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            return comp?.HasAssignedTrainer(trainer) == true;
#else
            return trainer != null && comp?.selectedTrainer == trainer;
#endif
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

            // 先拒绝身份失格目标，不让候选查询修复其占用或触发兼容迁移。
            if (!SSCIdentityUtility.IsSexSlave(targetPawn))
            {
                reason = "SSC_Training_TargetIdentityRequired".Translate();
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

            // 自动工作避让实际课程成员；手动命令不受此兼容规则影响。
            if (ProgressionEducationCompatibility.ShouldDeferAutomaticTraining(targetPawn, forced)) return false;

            // 先修复已结束仪式的残留占用，再判断调教资格；自动和强制命令共用此入口。
            RecoverTrainingTargetState(targetPawn);
#if SSC_RW17
            if (!CanStartDailyTraining(targetPawn, trainer, forced, out reason) ||
                !CanReserveDailyTraining(trainer, targetPawn))
            {
                reason ??= Strings.Train_Reason_NotReservable;
                return false;
            }
            return TrainingJobUtility.TryValidateTarget(targetPawn, forced, "SSC_TRAIN", out reason) &&
                CanStartDailyTraining(targetPawn, trainer, forced, out reason);
        }

        // 原版 playerForced 会抢占他人的预约。日常调教的单槽无论命令来源都必须空闲或由本人持有。
        internal static bool CanReserveDailyTraining(Pawn trainer, Pawn target)
            => trainer != null && target != null && trainer.Map != null && target.Map == trainer.Map &&
                trainer.CanReserve(target, 1, 0);

        // 预约与首个 Toil 共用的只读检查。身体兼容准备只由 WorkGiver 路径执行。
        internal static bool CanStartDailyTraining(Pawn targetPawn, Pawn trainer, bool forced, out string reason)
        {
            reason = null;
            if (!SSCIdentityUtility.IsSexSlave(targetPawn))
            {
                reason = "SSC_Training_TargetIdentityRequired".Translate();
                return false;
            }
            if (!SSCIdentityUtility.IsSupportedVanillaStatus(targetPawn))
            {
                reason = Strings.Train_Reason_InvalidFaction;
                return false;
            }
            if (ProgressionEducationCompatibility.ShouldDeferAutomaticTraining(targetPawn, forced)) return false;
#endif
            CompSexSlaveTraining compToggle = targetPawn.TryGetComp<CompSexSlaveTraining>();
            if (compToggle == null || !compToggle.IsEnabled)
            {
                reason = Strings.Train_Reason_NotEnabled;
                return false;
            }

#if SSC_RW17
            if (BindingRitualStateUtility.HasActiveTargetRitual(targetPawn))
#else
            if (compToggle.isRitualTraining)
#endif
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
#if SSC_RW17
            if (SSCDailyTrainingOccupancy.IsBusy(targetPawn) || SSCDailyTrainingOccupancy.HasOtherReceiver(targetPawn, trainer))
#else
            if (compToggle.isBeingTrained)
#endif
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

#if !SSC_RW17
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

            // 身体/兼容入口可能调用外部补丁；返回后不能继续承认已失效的 SSC 身份。
            if (!SSCIdentityUtility.IsSexSlave(targetPawn))
            {
                reason = "SSC_Training_TargetIdentityRequired".Translate();
                return false;
            }
#endif

            return true;
        }

        /// <summary>恢复已失效的仪式和日常占用，保留仍有实际接收任务或有效仪式支撑的训练标记。</summary>
        private static void RecoverTrainingTargetState(Pawn targetPawn)
        {
            BindingRitualStateUtility.RecoverPawnState(targetPawn);
            CompSexSlaveTraining comp = targetPawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.isRitualTraining || !comp.isBeingTrained) return;
#if SSC_RW17
            if (SSCDailyTrainingOccupancy.IsBusy(targetPawn)) return;
#endif
            bool hasTrainingJob = targetPawn.CurJobDef == SSCDefOf.SSC_TrainingReceiver
                || targetPawn.CurJobDef == SSCDefOf.Training_Ritual;
            if (!hasTrainingJob) comp.isBeingTrained = false;
        }
    }
}
