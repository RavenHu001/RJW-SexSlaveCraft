// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>生育普通进度和终极成果沿用共用人格载荷，仅专门处理同系标签互斥。</summary>
    public static class FertilitySpecializationGelUtility
    {
        /// <summary>只匹配数据库中的本方向普通/终极标签，避免清理其他人格成果。</summary>
        public static bool IsTag(HediffDef def)
        {
            return def != null && (def == FertilitySpecializationUtility.OrdinaryDef ||
                def == FertilitySpecializationUtility.FinalDef);
        }

        /// <summary>以组件进度采集普通态，终极优先；不把当前失格当作人格没有成果。</summary>
        public static void StoreExclusiveTags(CompPersonalityStore store, Pawn pawn)
        {
            if (store == null) return;
            if (FertilitySpecializationUtility.OrdinaryDef != null) store.RemoveTag(FertilitySpecializationUtility.OrdinaryDef);
            if (FertilitySpecializationUtility.FinalDef != null) store.RemoveTag(FertilitySpecializationUtility.FinalDef);
            if (pawn?.health?.hediffSet == null) return;
            if (FertilitySpecializationUtility.HasFinalState(pawn))
            {
                store.SetTag(FertilitySpecializationUtility.FinalDef, 1f);
                return;
            }
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp?.specializationType == SexSlaveSpecializationType.Fertility &&
                FertilitySpecializationUtility.OrdinaryDef != null &&
                pawn.health.hediffSet.GetFirstHediffOfDef(FertilitySpecializationUtility.OrdinaryDef) != null)
                store.SetTag(FertilitySpecializationUtility.OrdinaryDef, OrdinarySeverity(comp.specializationProgress));
        }

        /// <summary>在宿主历史被替换之前移除其旧成果，不以普通严重度反推源人格进度。</summary>
        public static void RemoveAllStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            var states = pawn.health.hediffSet.hediffs;
            for (int i = states.Count - 1; i >= 0; i--)
                if (IsTag(states[i].def)) pawn.health.RemoveHediff(states[i]);
        }

        /// <summary>仅在完成快照采集后的抽取入口调用，不改变身份或绑定。</summary>
        public static void DetachAfterExtraction(Pawn pawn)
        {
            pawn?.TryGetComp<CompSexSlaveTraining>()?.ClearFertilityProgressAfterExtraction();
            RemoveAllStates(pawn);
        }

        /// <summary>来源整体覆盖同系标签，避免宿主旧进度通过通用最大值合并混入。</summary>
        public static void ApplyExclusiveTags(Pawn pawn, CompPersonalityStore data)
        {
            if (pawn?.health?.hediffSet == null || data == null) return;
            RemoveAllStates(pawn);
            if (FertilitySpecializationUtility.FinalDef != null && data.HasTag(FertilitySpecializationUtility.FinalDef))
            {
                pawn.health.AddHediff(FertilitySpecializationUtility.FinalDef).Severity = 1f;
                return;
            }
            if (data.specializationType == SexSlaveSpecializationType.Fertility &&
                FertilitySpecializationUtility.OrdinaryDef != null && data.HasTag(FertilitySpecializationUtility.OrdinaryDef))
                pawn.health.AddHediff(FertilitySpecializationUtility.OrdinaryDef).Severity = OrdinarySeverity(data.specializationProgress);
        }

        /// <summary>显示严重度至少为1%，但实际零进度与完成判断仍读取组件。</summary>
        private static float OrdinarySeverity(float progress)
        {
            return Math.Max(0.01f, CompSexSlaveTraining.NormalizeSpecializationProgress(progress));
        }
    }
}
