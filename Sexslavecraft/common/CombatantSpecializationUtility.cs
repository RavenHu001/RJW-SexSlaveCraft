using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>普通战斗员只依赖当前方向和组件进度，不维护身份、绑定或锁链资格。</summary>
    public static class CombatantSpecializationUtility
    {
        private const float InitialSeverity = 0.01f;

        /// <summary>选择时只检查组件、现有页签支持范围及战斗员研究。</summary>
        public static bool CanSelect(Pawn pawn, out string reason)
        {
            reason = null;
            if (pawn?.TryGetComp<CompSexSlaveTraining>() == null ||
                !SSCIdentityUtility.IsSupportedVanillaStatus(pawn)) return false;
            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_RES_Combatant))
            {
                reason = Strings.ITab_SpecializationCombatantDisabledResearch;
                return false;
            }
            return true;
        }

        /// <summary>组件进度是唯一权威；重复调用不增长进度，也不重建已有状态。</summary>
        public static void Sync(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || SSCDefOf.SSC_Hediff_Combatant == null) return;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;
            if (comp.specializationType != SexSlaveSpecializationType.Combatant)
            {
                RemoveOrdinaryState(pawn);
                return;
            }

            comp.specializationProgress = CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress);
            Hediff ordinary = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant);
            // 修复重复标记，保留原实例；不能叠加两份被动属性。
            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff candidate = hediffs[i];
                if (candidate.def == SSCDefOf.SSC_Hediff_Combatant && candidate != ordinary)
                    pawn.health.RemoveHediff(candidate);
            }
            if (ordinary == null) ordinary = pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant);
            if (ordinary != null)
                ordinary.Severity = Math.Max(InitialSeverity, comp.specializationProgress);
        }

        /// <summary>方向退出时移除所有普通标记，历史进度由组件保留。</summary>
        public static void RemoveOrdinaryState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || SSCDefOf.SSC_Hediff_Combatant == null) return;
            Hediff ordinary;
            while ((ordinary = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant)) != null)
                pawn.health.RemoveHediff(ordinary);
        }
    }
}
