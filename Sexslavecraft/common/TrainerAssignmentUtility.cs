using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

// EN: This file decides who may train a given sex slave.
// EN: It merges ChainOfSexSlave owner rules, manual trainer assignment, worktype checks, and target availability checks.
// CN: 这个文件负责决定“谁可以调教这个性奴”。
// CN: 它会合并 ChainOfSexSlave 主人规则、手动指定 trainer、工作类型检查，以及目标可用性检查。
namespace SexSlaveCraft
{
    public static class TrainerAssignmentUtility
    {
        private static readonly WorkTypeDef TrainSexSlaveWorkType = DefDatabase<WorkTypeDef>.GetNamedSilentFail("TrainSexSlave");

        public static bool IsAllowedTrainer(Pawn sexSlave, Pawn master)
        {
            if (sexSlave == null || master == null) return false;

            CompSexSlaveTraining comp = sexSlave.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return false;

            // EN: "Allow others" and public-use states are true opt-outs from the
            // exclusive trainer lock. Keeping selectedTrainer active here made the
            // UI promise broader access while the WorkGiver still rejected everyone.
            // CN: “允许其他人”和公交车状态必须真正解除独占调教师限制；否则界面虽已
            // 放开，WorkGiver 最终仍会按 selectedTrainer 拒绝其他所有人。
            if (comp.AllowsOthersForTrainingOrSex ||
                comp.IsBusSpecialized ||
                BusSpecializationUtility.HasAnyBusState(sexSlave))
            {
                return true;
            }

            // EN: ChainOfSexSlave can force the current master unless the pawn is in the Bus state.
            // CN: 除了公交车状态外，ChainOfSexSlave 可以强制指定当前唯一合法的主人。
            Pawn forcedMaster = GetForcedMaster(sexSlave);
            if (forcedMaster != null && forcedMaster != master)
            {
                return false;
            }

            if (comp.selectedTrainer == null) return true;
            return comp.selectedTrainer == master;
        }

        public static IEnumerable<Pawn> GetTrainerCandidates(Pawn sexSlave)
        {
            Map map = sexSlave?.Map;
            if (map == null) yield break;

            // EN: Candidate scan only returns free colonists who already have the TrainSexSlave work type enabled.
            // CN: 候选扫描只返回自由殖民者里那些已经启用“调教”工作类型的人。
            Pawn forcedMaster = GetForcedMaster(sexSlave);
            foreach (Pawn candidate in map.mapPawns.FreeColonists)
            {
                if (!CanAssignAsTrainer(candidate)) continue;
                if (forcedMaster != null && candidate != forcedMaster) continue;
                yield return candidate;
            }
        }

        public static Pawn GetForcedMaster(Pawn sexSlave)
        {
            if (sexSlave?.health?.hediffSet == null) return null;
            CompSexSlaveTraining comp = sexSlave.TryGetComp<CompSexSlaveTraining>();
            if (comp != null && (comp.AllowsOthersForTrainingOrSex || comp.IsBusSpecialized)) return null;
            if (BusSpecializationUtility.HasAnyBusState(sexSlave)) return null;

            return SSCBondUtility.GetBoundMaster(sexSlave);
        }

        public static bool CanAssignAsTrainer(Pawn candidate)
        {
            if (candidate == null || candidate.Dead || candidate.Downed) return false;
            if (candidate.workSettings == null || TrainSexSlaveWorkType == null) return false;
            return candidate.workSettings.WorkIsActive(TrainSexSlaveWorkType);
        }

        public static bool IsTrainingTargetAvailable(Pawn targetPawn, Pawn trainer, bool forced)
        {
            return TryGetTrainingTargetFailureReason(targetPawn, trainer, forced, out _);
        }

        public static bool TryGetTrainingTargetFailureReason(Pawn targetPawn, Pawn trainer, bool forced, out string reason)
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

            if (!SSCIdentityUtility.IsSupportedVanillaStatus(targetPawn))
            {
                reason = Strings.Train_Reason_InvalidFaction;
                return false;
            }

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
                bool isActiveTrainingJob = targetPawn.CurJobDef == SSCDefOf.SSC_TrainingReceiver || targetPawn.CurJobDef == SSCDefOf.Training_Ritual;
                if (isActiveTrainingJob)
                {
                    reason = Strings.Train_Reason_AlreadyBeingTrained;
                    return false;
                }

                compToggle.isBeingTrained = false;
            }

            if (compToggle.IsOnCooldown)
            {
                int ticksLeft = CompSexSlaveTraining.CooldownTicks - (Find.TickManager.TicksGame - compToggle.lastTrainingTick);
                reason = Strings.Train_Reason_Cooldown(ticksLeft.ToStringTicksToPeriod());
                return false;
            }

            if (!IsAllowedTrainer(targetPawn, trainer))
            {
                reason = Strings.Train_Reason_TrainerLocked;
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
    }
}
