// AI 辅助生成
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>定向孕育只负责一次采样提交；妊娠及婴儿仍由原后端与第四步负责。</summary>
    public static class FertilityAbilityUtility
    {
        public const string AbilityName = "SSC_FertilityConception";
        public const int CooldownTicks = 7 * GenDate.TicksPerDay;
        public static AbilityDef Definition => DefDatabase<AbilityDef>.GetNamedSilentFail(AbilityName);
        private static readonly ConditionalWeakTable<Job, Context> requests = new();
        private static readonly System.Collections.Generic.HashSet<Pawn> submitting = new();

        /// <summary>原命令的进程内归属，不序列化；未成功的施法在读档后取消，不能重授旧请求权限。</summary>
        public sealed class Context
        {
            internal readonly Pawn Caster, Target;
            internal readonly CompSexSlaveTraining Comp;
            internal readonly object Token;
            internal readonly Hediff_SSCUterusDevelopment Uterus;
            internal readonly Ability Ability;
            internal readonly Job Job;
            internal readonly int JobId;
            internal Context(Pawn caster, Pawn target, Ability ability, Job job)
            { Job = job; JobId = job.loadID; Caster = caster; Target = target; Ability = ability; Comp = caster?.TryGetComp<CompSexSlaveTraining>();
              Token = SSCPersonalityRuntimeUtility.Capture(caster); Uterus = SSCUterusDevelopmentUtility.Find(caster); }
        }
        /// <summary>原选取时登记，替换对象池遗留键；loadID同时使旧上下文失效。</summary>
        internal static void Register(Job job, Ability ability, Pawn target)
        { requests.Remove(job); requests.Add(job, new Context(ability.pawn, target, ability, job)); }
        /// <summary>只读取原Job的凭据，不为未登记或读档任务重新Capture。</summary>
        internal static Context Request(Job job) => job != null && requests.TryGetValue(job, out var c) ? c : null;
        /// <summary>人格、组件、身体、实际技能与任务世代须全部属于原提交。</summary>
        internal static bool IsCurrent(Context c) => c != null && c.Caster?.TryGetComp<CompSexSlaveTraining>() == c.Comp &&
            c.Comp != null && SSCPersonalityRuntimeUtility.IsCurrent(c.Caster, c.Token) && !SSCPersonalityRuntimeUtility.IsMutating(c.Caster) &&
            ReferenceEquals(SSCUterusDevelopmentUtility.Find(c.Caster), c.Uterus) && c.Uterus != null &&
            c.Caster.abilities?.GetAbility(Definition) == c.Ability && c.Job.loadID == c.JobId &&
            c.Job.ability == c.Ability && c.Job.GetTarget(TargetIndex.A).Pawn == c.Target;

        /// <summary>持有与可用分开；孕期不移除Ability，不用自然双人受孕筛选排除对象。</summary>
        public static bool CanUse(Pawn p, out string reason)
        {
            reason = "SSC_FertilitySkill_Unavailable";
            if (p == null || p.Dead || p.Destroyed || !p.Spawned || p.abilities == null || SSCPersonalityRuntimeUtility.IsMutating(p)) return false;
            if (!FertilitySpecializationUtility.HasFinalState(p) || !FertilitySpecializationUtility.MeetsConditions(p, out _)) return false;
            if (!ModsConfig.BiotechActive) { reason = "SSC_FertilitySkill_Biotech"; return false; }
            if (SSCUterusDevelopmentUtility.Find(p)?.FertilityEnabled != true) { reason = "SSC_FertilitySkill_Disabled"; return false; }
            if (SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(p)) { reason = "SSC_FertilitySkill_Pregnant"; return false; }
            // 只保留施法者的实际器官及明确绝育前置；目标的性别、器官及不育均不参与此查询。
            if (p.health.hediffSet.HasHediffPreventsPregnancy() || p.SterileGenes() ||
                !p.health.hediffSet.hediffs.Any(h => h.pawn == p && TrainingExpUtility.IsPresentPart(p, h.Part) && Genital_Helper.is_vagina(h)))
            { reason = "SSC_FertilitySkill_Body"; return false; }
            reason = null; return true;
        }
        /// <summary>同地图全图选取，无距离、视线、预约或可达性条件。</summary>
        public static bool CanTarget(Pawn caster, Pawn target, out string reason)
        {
            if (!CanUse(caster, out reason)) return false;
            reason = "SSC_FertilitySkill_Target";
            if (target == null || target == caster || target.Dead || target.Destroyed || !target.Spawned || target.Map != caster.Map ||
                !SSCCultureGenderUtility.IsAdult(target) || target.HostileTo(caster) || caster.HostileTo(target)) return false;
            reason = null; return true;
        }
        /// <summary>公共void入口之后核对实际落地结果；异常发生于落地之后也不能退还成功冷却。</summary>
        internal static bool TryConceive(Context c)
        {
            if (!IsCurrent(c) || c.Ability is not Ability_FertilityConception { Submitting: true } ||
                !CanTarget(c.Caster, c.Target, out _) || !submitting.Add(c.Caster)) return false;
            try
            {
                try { PregnancyHelper.StartVanillaPregnancy(c.Caster, c.Target, geneticMother: c.Caster); }
                catch (Exception ex) { Log.Warning("[SSC] Fertility conception callback: " + ex.GetType().Name); }
                return c.Caster.health.hediffSet.hediffs.OfType<HediffWithParents>().Any(h =>
                    h is Hediff_Pregnant && h.Mother == c.Caster && h.Father == c.Target);
            }
            finally { submitting.Remove(c.Caster); }
        }
        /// <summary>有终极成果就保留技能；失去身体资格只由CanCast禁用。</summary>
        public static void Maintain(Pawn p)
        {
            if (Scribe.mode != LoadSaveMode.Inactive && Scribe.mode != LoadSaveMode.PostLoadInit) return;
            var def = Definition;
            if (def == null || p?.abilities == null) return;
            if (!FertilitySpecializationUtility.HasFinalState(p)) { Remove(p); return; }
            if (p.abilities.GetAbility(def) == null) p.abilities.GainAbility(def);
        }
        /// <summary>只撤销本技能，不调整其他技能、妊娠或绑定。</summary>
        public static void Remove(Pawn p) { if (Definition != null) p?.abilities?.RemoveAbility(Definition); }
        /// <summary>凝胶携带绝对结束时间，存放加工期间仍流逝；活体权威仍是原版Ability。</summary>
        public static int CaptureCooldown(Pawn p)
        {
            if (!FertilitySpecializationUtility.HasFinalState(p)) return 0;
            int remaining = Definition == null ? 0 : p?.abilities?.GetAbility(Definition)?.CooldownTicksRemaining ?? 0;
            return remaining > 0 ? Find.TickManager.TicksGame + remaining : 0;
        }
        /// <summary>植入时立即授予并恢复原截止时间；身体失格不清除该冷却。</summary>
        public static void RestoreCooldown(Pawn p, int deadline)
        {
            Maintain(p);
            var ability = Definition == null ? null : p?.abilities?.GetAbility(Definition);
            if (ability == null) return;
            int remaining = Math.Max(0, deadline - Find.TickManager.TicksGame);
            if (remaining > 0) ability.StartCooldown(remaining); else ability.ResetCooldown();
        }
    }
}
