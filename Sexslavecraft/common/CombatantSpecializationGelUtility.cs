using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>战斗员普通进度和终极成果沿用共用人格载荷，仅专门处理同系标签互斥。</summary>
    public static class CombatantSpecializationGelUtility
    {
        public static bool IsCombatantTag(HediffDef def)
        {
            return def != null && (def == SSCDefOf.SSC_Hediff_Combatant ||
                def == SSCDefOf.SSC_Hediff_Combatant_Final);
        }

        public static void StoreExclusiveTags(CompPersonalityStore store, Pawn pawn)
        {
            if (store == null) return;
            if (SSCDefOf.SSC_Hediff_Combatant != null) store.RemoveTag(SSCDefOf.SSC_Hediff_Combatant);
            if (SSCDefOf.SSC_Hediff_Combatant_Final != null) store.RemoveTag(SSCDefOf.SSC_Hediff_Combatant_Final);
            if (pawn?.health?.hediffSet == null) return;
            if (CombatantSpecializationUtility.HasFinalState(pawn))
            {
                store.SetTag(SSCDefOf.SSC_Hediff_Combatant_Final, 1f);
                return;
            }
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp?.specializationType == SexSlaveSpecializationType.Combatant &&
                SSCDefOf.SSC_Hediff_Combatant != null &&
                pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant) != null)
                store.SetTag(SSCDefOf.SSC_Hediff_Combatant, OrdinarySeverity(comp.specializationProgress));
        }

        /// <summary>在宿主历史被替换之前移除其旧成果，不以普通严重度反推源人格进度。</summary>
        public static void RemoveAllStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            var states = pawn.health.hediffSet.hediffs;
            for (int i = states.Count - 1; i >= 0; i--)
                if (IsCombatantTag(states[i].def) ||
                    (SSCDefOf.SSC_Hediff_CombatOverdrive != null && states[i].def == SSCDefOf.SSC_Hediff_CombatOverdrive))
                    pawn.health.RemoveHediff(states[i]);
            // 永久技能随人格离开；临时强化仅清理，不加入人格标签。
            if (SSCDefOf.SSC_CombatOverdrive != null) pawn.abilities?.RemoveAbility(SSCDefOf.SSC_CombatOverdrive);
        }

        /// <summary>仅在完成快照采集后的抽取入口调用，不改变身份或绑定。</summary>
        public static void DetachAfterExtraction(Pawn pawn)
        {
            pawn?.TryGetComp<CompSexSlaveTraining>()?.ClearCombatantProgressAfterExtraction();
            RemoveAllStates(pawn);
        }

        public static void ApplyExclusiveTags(Pawn pawn, CompPersonalityStore data)
        {
            if (pawn?.health?.hediffSet == null || data == null) return;
            RemoveAllStates(pawn);
            if (SSCDefOf.SSC_Hediff_Combatant_Final != null && data.HasTag(SSCDefOf.SSC_Hediff_Combatant_Final))
            {
                pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final).Severity = 1f;
                return;
            }
            if (data.specializationType == SexSlaveSpecializationType.Combatant &&
                SSCDefOf.SSC_Hediff_Combatant != null && data.HasTag(SSCDefOf.SSC_Hediff_Combatant))
                pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant).Severity = OrdinarySeverity(data.specializationProgress);
        }

        private static float OrdinarySeverity(float progress)
        {
            return Math.Max(0.01f, CompSexSlaveTraining.NormalizeSpecializationProgress(progress));
        }
    }
}
