// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>生育特化的资格、阶段和推进；身体子宫、人格成果与生育开关分别读取。</summary>
    public static class FertilitySpecializationUtility
    {
        public const string OrdinaryName = "SSC_Hediff_Fertility";
        public const string FinalName = "SSC_Hediff_Fertility_Final";
        public const string ResearchName = "SSC_RES_Fertility";
        public static HediffDef OrdinaryDef => DefDatabase<HediffDef>.GetNamedSilentFail(OrdinaryName);
        public static HediffDef FinalDef => DefDatabase<HediffDef>.GetNamedSilentFail(FinalName);
        private static readonly HashSet<Pawn> syncing = new HashSet<Pawn>();

        /// <summary>只查成果，不以当前身体或当前培养方向撤销人格持有的终极事实。</summary>
        public static bool HasFinalState(Pawn pawn) => FinalDef != null && pawn?.health?.hediffSet.HasHediff(FinalDef) == true;
        /// <summary>仅识别本方向两种人格健康标签，供精确保存与通用恢复过滤使用。</summary>
        public static bool IsTag(HediffDef def) => def?.defName == OrdinaryName || def?.defName == FinalName;

        /// <summary>失格暂停；不将生育关闭、无主或没有印记当作培养失败。</summary>
        public static bool MeetsConditions(Pawn pawn, out string reason)
        {
            reason = null;
            if (!SSCIdentityUtility.IsSexSlave(pawn)) reason = "SSC_Fertility_Identity".Translate();
            else if (!SSCCultureGenderUtility.IsAdult(pawn)) reason = "SSC_Fertility_Adult".Translate();
            else if (SSCUterusDevelopmentUtility.Find(pawn)?.IsComplete != true) reason = "SSC_Fertility_Uterus".Translate();
            else if (SSCDefOf.SSC_PersonalityExcreted_Done != null && pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done))
                reason = "SSC_Fertility_Identity".Translate();
            return reason == null;
        }

        /// <summary>研究只限制新选择；点击时仍复查当前人格上下文。</summary>
        public static bool CanSelect(Pawn pawn, out string reason)
        {
            if (!MeetsConditions(pawn, out reason)) return false;
            if (HasFinalState(pawn)) reason = "SSC_Fertility_Finalized".Translate();
            else if (!ResearchUtils.IsResearchFinished(DefDatabase<ResearchProjectDef>.GetNamedSilentFail(ResearchName)))
                reason = "SSC_Fertility_Research".Translate();
            return reason == null;
        }

        /// <summary>以组件权威进度映射档位，公共完成容差也用于实际收益。</summary>
        public static int ProgressStage(float progress) => !float.IsFinite(progress) ? 0 :
            progress >= CompSexSlaveTraining.SpecializationCompletionProgress ? 4 : progress >= .8f ? 3 : progress >= .5f ? 2 : progress >= .2f ? 1 : 0;
        /// <summary>失格返回-1；终极成果优先于普通当前方向，不从历史进度激活效果。</summary>
        public static int Stage(Pawn pawn)
        {
            if (!MeetsConditions(pawn, out _)) return -1;
            if (HasFinalState(pawn)) return 5;
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            return comp?.specializationType == SexSlaveSpecializationType.Fertility ? ProgressStage(comp.specializationProgress) : -1;
        }
        /// <summary>读取照料者的当前配置，作用于活动受益者而非照料者的学习Stat。</summary>
        public static float CareFactor(Pawn pawn)
        {
            int stage = Stage(pawn);
            var factors = (HasFinalState(pawn) ? FinalDef : OrdinaryDef)?.GetModExtension<FertilitySpecializationExtension>()?.careFactors;
            return stage >= 0 && factors != null && stage < factors.Count ? factors[stage] : 1f;
        }

        /// <summary>统一调教与特色活动共用；完成后不继续领进度。</summary>
        public static float TryGainProgress(Pawn pawn, float amount)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp?.specializationType != SexSlaveSpecializationType.Fertility || !float.IsFinite(amount) || amount <= 0f ||
                !MeetsConditions(pawn, out _) || SSCPersonalityRuntimeUtility.IsMutating(pawn) || HasFinalState(pawn)) return 0f;
            // 进度是人格组件的唯一权威；坏数值先规范化，普通Hediff严重度不回灌。
            float before = CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress);
            if (before >= CompSexSlaveTraining.SpecializationCompletionProgress) return 0f;
            comp.specializationProgress = before;
            comp.AddSpecializationProgress(amount);
            Sync(pawn);
            return comp.specializationProgress - before;
        }

        /// <summary>幂等协调健康表现；终极实例不重建，失格时由CurStage返回中性效果。</summary>
        public static void Sync(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || pawn.health?.hediffSet == null || OrdinaryDef == null || !syncing.Add(pawn)) return;
            try
            {
                if (comp.specializationType != SexSlaveSpecializationType.Fertility && !pawn.health.hediffSet.hediffs.Any(h => IsTag(h.def))) return;
                bool final = HasFinalState(pawn);
                // 终极优先且保留首个实例，只清重复/不再生效的普通表现。
                Hediff keep = null;
                foreach (var h in pawn.health.hediffSet.hediffs.ToArray())
                {
                    if (!IsTag(h.def)) continue;
                    bool wanted = final ? h.def == FinalDef : comp.specializationType == SexSlaveSpecializationType.Fertility && h.def == OrdinaryDef;
                    if (wanted && keep == null) keep = h;
                    else pawn.health.RemoveHediff(h);
                }
                if (keep == null && comp.specializationType == SexSlaveSpecializationType.Fertility)
                    keep = pawn.health.AddHediff(OrdinaryDef);
                if (keep != null)
                {
                    float severity = final ? 1f : Math.Max(.01f, CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress));
                    if (keep.Severity != severity) keep.Severity = severity;
                    // 身份/身体资格可能变而严重度未变，仍需刷新阶段属性缓存。
                    (keep as Hediff_FertilitySpecialization)?.Refresh();
                }
            }
            finally { syncing.Remove(pawn); }
        }
    }
}
