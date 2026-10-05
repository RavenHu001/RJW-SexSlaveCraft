using System;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>猫终极技能的共同资格、分支效果及人格迁移；不承担旧猫终极状态迁移。</summary>
    public static class PetCatAbilityUtility
    {
        public const float DefaultRange = 6f;

        private static bool HasFinalQualification(Pawn pawn)
        {
            HediffDef finalDef = PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetCat);
            return finalDef != null && pawn?.health?.hediffSet?.hediffs.Any(h =>
                h.def == finalDef && h.Severity >= 0.01f) == true;
        }

        public static bool CanUse(Pawn caster, out string reason)
        {
            reason = "SSC_PetCatComfortRequiresFinal";
            if (!HasFinalQualification(caster)) return false;
            reason = "SSC_PetCatComfortInvalidCaster";
            if (caster.Destroyed || caster.Dead || caster.Downed || !caster.Spawned || caster.MentalState != null ||
                (SSCDefOf.SSC_PersonalityExcreted_Done != null &&
                 caster.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done))) return false;
            // 终极成果不依赖当前培养方向、SSC 身份、绑定或研究；不要求征召。
            reason = null;
            return true;
        }

        public static bool CanApply(Pawn caster, Pawn target, out string reason, float range = DefaultRange)
        {
            if (!CanUse(caster, out reason)) return false;
            reason = "SSC_PetCatComfortInvalidTarget";
            if (target == null || target == caster || target.Destroyed || target.Dead || !target.Spawned ||
                target.health?.hediffSet == null || target.RaceProps?.Humanlike != true ||
                target.Faction != Faction.OfPlayer || target.Map != caster.Map ||
                float.IsNaN(range) || float.IsInfinity(range) || range < 0f ||
                !caster.Position.InHorDistOf(target.Position, range) ||
                !GenSight.LineOfSight(caster.Position, target.Position, caster.Map)) return false;

            // 读取实际阵营而不检查 HostileTo，确保己方狂暴等敌对行为不会被误拒。
            // 有精神状态时不要求心情需求；无状态时才检查激励所需的心情需求。
            if (target.MentalState == null && target.needs?.mood == null) return false;
            reason = null;
            return true;
        }

        public static bool IsEffectConfigured(HediffDef encouragement, int durationTicks)
        {
            return encouragement != null && durationTicks > 0 && encouragement.hediffClass != null &&
                typeof(HediffWithComps).IsAssignableFrom(encouragement.hediffClass) &&
                encouragement.comps?.Any(c => c is HediffCompProperties_Disappears) == true;
        }

        public static bool Apply(Pawn caster, Pawn target, HediffDef encouragement, int durationTicks,
            float range = DefaultRange)
        {
            if (!IsEffectConfigured(encouragement, durationTicks) || !CanApply(caster, target, out _, range))
                return false;

            // 所有当前 MentalState 均通过原版恢复入口结束，保留恢复通知和 PostEnd。
            // 不检查类型或来源，也不查找、删除紧张性昏迷或其他疾病 Hediff。
            var mentalState = target.MentalState;
            if (mentalState != null)
            {
                mentalState.RecoverFromState();
                return true;
            }

            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(encouragement);
            Hediff effect = existing ?? target.health.AddHediff(encouragement);
            HediffComp_Disappears timer = effect?.TryGetComp<HediffComp_Disappears>();
            if (timer == null)
            {
                // 配置损坏时不留下永久激励。正常定义在执行前已验证带有倒计时组件。
                if (existing == null && effect != null) target.health.RemoveHediff(effect);
                return false;
            }

            // 多只猫施放仍只保留一份激励，重新施放刷新原实例与计时，不叠加属性。
            timer.ticksToDisappear = durationTicks;
            foreach (Hediff duplicate in target.health.hediffSet.hediffs
                .Where(h => h != effect && h.def == encouragement).ToList())
                target.health.RemoveHediff(duplicate);
            return true;
        }

        /// <summary>抽取前保存绝对结束时间；人格凝胶存放或加工时冷却继续流逝。</summary>
        public static int CaptureCooldownDeadline(Pawn pawn)
        {
            if (!HasFinalQualification(pawn) || SSCDefOf.SSC_PetCatComfort == null) return 0;
            Ability ability = pawn.abilities?.GetAbility(SSCDefOf.SSC_PetCatComfort);
            int remaining = ability?.CooldownTicksRemaining ?? 0;
            return remaining > 0 ? Find.TickManager.TicksGame + remaining : 0;
        }

        public static void RemoveAbility(Pawn pawn)
        {
            if (SSCDefOf.SSC_PetCatComfort != null) pawn?.abilities?.RemoveAbility(SSCDefOf.SSC_PetCatComfort);
        }

        /// <summary>成功采集快照后再清源身体，防止空壳保留技能或重新生成普通猫状态。</summary>
        public static void DetachAfterExtraction(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            HediffDef ordinary = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetCat);
            HediffDef final = PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetCat);
            foreach (Hediff h in pawn.health.hediffSet.hediffs
                .Where(h => h.def == ordinary || h.def == final).ToList()) pawn.health.RemoveHediff(h);
            RemoveAbility(pawn);
            pawn.TryGetComp<CompSexSlaveTraining>()?.ClearPetCatProgressAfterExtraction();
        }

        /// <summary>植入完成时立即授予技能并恢复冷却，避免等待组件授予期间出现可用空窗。</summary>
        public static void RestoreCooldown(Pawn pawn, int deadline)
        {
            if (!HasFinalQualification(pawn) || SSCDefOf.SSC_PetCatComfort == null || pawn?.abilities == null)
            {
                RemoveAbility(pawn);
                return;
            }
            Ability ability = pawn.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort);
            if (ability == null)
            {
                pawn.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
                ability = pawn.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort);
            }
            if (ability == null) return;
            int remaining = Math.Max(0, deadline - Find.TickManager.TicksGame);
            if (remaining > 0) ability.StartCooldown(remaining);
            else ability.ResetCooldown();
        }
    }
}
