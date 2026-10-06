using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>狗终极驯导的资格、原版驯服／己方训练、旧成果授予及人格冷却迁移。</summary>
    public static class PetDogAbilityUtility
    {
        private static bool HasFinalQualification(Pawn pawn)
        {
            HediffDef final = PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetDog);
            return final != null && pawn?.health?.hediffSet?.hediffs.Any(h =>
                h.def == final && h.Severity >= 0.01f) == true;
        }

        private static bool IsHollow(Pawn pawn)
        {
            return SSCDefOf.SSC_PersonalityExcreted_Done != null &&
                pawn?.health?.hediffSet?.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) == true;
        }

        /// <summary>施放只依赖保留的终极成果与当前身体状态，不要求培养方向、绑定或研究。</summary>
        public static bool CanUse(Pawn caster, out string reason)
        {
            reason = "SSC_PetDogTameRequiresFinal";
            if (!HasFinalQualification(caster)) return false;
            reason = "SSC_PetDogTameInvalidCaster";
            // 原版成功接口使用施放者阵营招募目标；限定玩家阵营以保证驯服结果归玩家。
            if (caster.Destroyed || caster.Dead || caster.Downed || !caster.Spawned ||
                caster.Faction != Faction.OfPlayer || caster.MentalState != null || IsHollow(caster)) return false;
            reason = null;
            return true;
        }

        public static bool CanTarget(Pawn caster, Pawn target, out string reason)
        {
            if (!CanUse(caster, out reason)) return false;
            reason = "SSC_PetDogTameInvalidTarget";
            if (target == null || target == caster || target.Destroyed || target.Dead || !target.Spawned ||
                target.Map != caster.Map || target.RaceProps?.Animal != true ||
                (target.Faction != null && target.Faction != Faction.OfPlayer) ||
                target.MentalState != null || target.health?.hediffSet == null) return false;
            // 原版意识资格允许可醒来的睡眠与有意识的倒地；不额外治疗或结束精神状态。
            reason = "SSC_PetDogTameUnconsciousTarget";
            if (target.health.capacities?.CanBeAwake != true) return false;
            if (target.Faction == Faction.OfPlayer)
            {
                reason = "SSC_PetDogTameMissingEffect";
                if (SSCDefOf.SSC_Job_PetDogTrain == null) return false;
                reason = "SSC_PetDogTameNoTrainingTarget";
                if (GetNextTraining(target) == null) return false;
            }
            else
            {
                reason = "SSC_PetDogTameUntamableTarget";
                // 野生分支沿用原版可驯服资格，不带入食物、指定或操作者工作技能门槛。
                if (!TameUtility.CanTame(target)) return false;
            }
            reason = null;
            return true;
        }

        public static bool CanSelectTarget(Pawn caster, Pawn target, out string reason)
        {
            if (!CanTarget(caster, target, out reason)) return false;
            reason = "SSC_PetDogTameUnreachableTarget";
            if (!caster.CanReach(target, PathEndMode.OnCell, Danger.Deadly)) return false;
            reason = null;
            return true;
        }

        public static bool CanApply(Pawn caster, Pawn target, out string reason)
        {
            if (!CanTarget(caster, target, out reason)) return false;
            reason = "SSC_PetDogTameInvalidTarget";
            if (caster.Position != target.Position || !MatchesJobBranch(caster, target)) return false;
            reason = null;
            return true;
        }

        /// <summary>按原版列表优先级取一个已勾选、仍需训练且符合当前动物资格的项目。</summary>
        public static TrainableDef GetNextTraining(Pawn animal)
        {
            if (animal?.training == null || animal.RaceProps?.Animal != true ||
                animal.RaceProps.animalType == AnimalType.Dryad) return null;
            // NextTrainableToTrain 只检查勾选及前置／剩余步骤；额外使用原版
            // CanAssignToTrain 检查种族、智力与当前体型，跳过残留的不合法勾选。
            foreach (TrainableDef trainable in TrainableUtility.TrainableDefsInListOrder)
                if (trainable != null && animal.training.GetWanted(trainable) &&
                    animal.training.CanBeTrained(trainable) && animal.training.CanAssignToTrain(trainable))
                    return trainable;
            return null;
        }

        /// <summary>执行中的任务用原版保存的 JobDef 固定分支，重新选取其他目标不受旧任务限制。</summary>
        public static bool MatchesJobBranch(Pawn caster, Pawn target)
        {
            Job job = caster?.CurJob;
            if (job == null || SSCDefOf.SSC_PetDogTame == null || job.ability?.def != SSCDefOf.SSC_PetDogTame ||
                job.GetTarget(TargetIndex.A).Pawn != target) return true;
            if (job.def == SSCDefOf.SSC_Job_PetDogTrain)
                return target?.Faction == Faction.OfPlayer;
            return job.def == SSCDefOf.SSC_PetDogTame.jobDef && target?.Faction == null;
        }

        /// <summary>同格预热结束后完成一个当前有效分支，不调用普通训练／驯服尝试或狗工作通知。</summary>
        public static bool Apply(Pawn caster, Pawn target)
        {
            if (!CanApply(caster, target, out _)) return false;
            if (target.Faction == null)
            {
                // 保留原版阵营转换、命名、关系、记录与反馈；不另发普通工作奖励。
                InteractionWorker_RecruitAttempt.DoRecruit(caster, target);
                return target.Faction == Faction.OfPlayer;
            }

            // 项目按实际生效时的勾选和资格重选；只完成一项，不同时完成其后解锁项目。
            // trainer=null 避免完成服从时自动把施放者设为动物主人；现有主人也不改变。
            TrainableDef training = GetNextTraining(target);
            if (training == null) return false;
            Pawn_TrainingTracker tracker = target.training;
            tracker.Train(training, null, complete: true);
            // 已学项目可能仍缺补训步骤，不能仅凭 HasLearned 判为成功。
            // 第三方阻止实效时返回失败，由 Ability 退回本次新冷却。
            if (target.training != tracker || target.Faction != Faction.OfPlayer ||
                !tracker.HasLearned(training) || tracker.CanBeTrained(training)) return false;

            // 技能用独立的一天冷却，不拒绝近期普通训练；成功后更新原版工作间隔，
            // 防止普通训兽工作紧接着再操作。失败不消费该时间或互动次数。
            if (target.mindState != null)
            {
                target.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                target.mindState.interactionsToday++;
            }
            Messages.Message("SSC_PetDogTameTrainingSuccess".Translate(target.LabelShort, training.LabelCap),
                target, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        /// <summary>旧终极狗可能保存为普通 Hediff，直接按成果对账授予，不替换原状态。</summary>
        public static void Maintain(Pawn pawn)
        {
            if (pawn?.abilities == null || SSCDefOf.SSC_PetDogTame == null) return;
            if (Scribe.mode != LoadSaveMode.Inactive && Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (!HasFinalQualification(pawn) || pawn.Destroyed || pawn.Dead || IsHollow(pawn))
            {
                RemoveAbility(pawn);
                return;
            }
            // 保留既有实例及冷却，周期对账和新 Hediff 组件重复授予也不会重置技能。
            if (pawn.abilities.GetAbility(SSCDefOf.SSC_PetDogTame) == null)
                pawn.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
        }

        public static int CaptureCooldownDeadline(Pawn pawn)
        {
            if (!HasFinalQualification(pawn) || SSCDefOf.SSC_PetDogTame == null) return 0;
            int remaining = pawn.abilities?.GetAbility(SSCDefOf.SSC_PetDogTame)?.CooldownTicksRemaining ?? 0;
            return remaining > 0 ? Find.TickManager.TicksGame + remaining : 0;
        }

        public static void RemoveAbility(Pawn pawn)
        {
            if (SSCDefOf.SSC_PetDogTame != null) pawn?.abilities?.RemoveAbility(SSCDefOf.SSC_PetDogTame);
        }

        /// <summary>人格快照成功后移走源身体的狗成果及历史，其他方向交给各自迁移入口。</summary>
        public static void DetachAfterExtraction(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            HediffDef ordinary = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetDog);
            HediffDef final = PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetDog);
            foreach (Hediff h in pawn.health.hediffSet.hediffs
                .Where(h => h.def == ordinary || h.def == final).ToList()) pawn.health.RemoveHediff(h);
            RemoveAbility(pawn);
            pawn.TryGetComp<CompSexSlaveTraining>()?.ClearPetDogProgressAfterExtraction();
        }

        /// <summary>植入完成后立即授予并恢复绝对截止时间，凝胶存放与加工期间继续计时。</summary>
        public static void RestoreCooldown(Pawn pawn, int deadline)
        {
            if (!HasFinalQualification(pawn) || SSCDefOf.SSC_PetDogTame == null || pawn?.abilities == null || IsHollow(pawn))
            {
                RemoveAbility(pawn);
                return;
            }
            Ability ability = pawn.abilities.GetAbility(SSCDefOf.SSC_PetDogTame);
            if (ability == null)
            {
                pawn.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
                ability = pawn.abilities.GetAbility(SSCDefOf.SSC_PetDogTame);
            }
            if (ability == null) return;
            int remaining = Math.Max(0, deadline - Find.TickManager.TicksGame);
            if (remaining > 0) ability.StartCooldown(remaining);
            else ability.ResetCooldown();
        }
    }
}
