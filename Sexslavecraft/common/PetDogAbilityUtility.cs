using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>狗终极驯导的资格、原版直接驯服、旧成果授予及人格冷却迁移。</summary>
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
                target.Map != caster.Map || target.RaceProps?.Animal != true || target.Faction != null ||
                target.MentalState != null || target.health?.hediffSet == null) return false;
            // 原版意识资格允许可醒来的睡眠与有意识的倒地；不额外治疗或结束精神状态。
            reason = "SSC_PetDogTameUnconsciousTarget";
            if (target.health.capacities?.CanBeAwake != true) return false;
            reason = "SSC_PetDogTameUntamableTarget";
            // 使用本机原版资格接口，保留野性、动物类型及 Scaria 等原版限制。
            // 不把食物、驯服指定或操作者工作技能门槛带入终极主动技能。
            if (!TameUtility.CanTame(target)) return false;
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
            if (caster.Position != target.Position) return false;
            reason = null;
            return true;
        }

        /// <summary>同格预热完成后直接走原版驯服成功链，不调用随机驯服尝试或狗工作通知。</summary>
        public static bool Apply(Pawn caster, Pawn target)
        {
            if (!CanApply(caster, target, out _)) return false;
            // DoRecruit 对动物执行正常阵营转换、命名、驯服记录、关系及成功反馈。
            // 不另给培养经验、技能经验或工作后事件；第三方阻止转换时报告无实效。
            InteractionWorker_RecruitAttempt.DoRecruit(caster, target);
            return target.Faction == Faction.OfPlayer;
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
