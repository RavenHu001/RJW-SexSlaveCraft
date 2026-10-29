// AI 辅助生成
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>各档育儿倍率由XML配置；普通与终极使用同一阶段索引。</summary>
    public sealed class FertilitySpecializationExtension : DefModExtension
    {
        public List<float> careFactors;
    }
    /// <summary>普通表现从组件派生，终极Def保存成果；当前身体失格不删除成果。</summary>
    public sealed class Hediff_FertilitySpecialization : Hediff
    {
        private static readonly HediffStage neutral = new HediffStage();
        private int lastStage = -2;
        /// <summary>实时按组件阶段和当前身体资格返回配置；离体、失格或被终极覆盖时中性化。</summary>
        public override HediffStage CurStage
        {
            get
            {
                if (pawn?.health?.hediffSet.hediffs.Contains(this) != true) return neutral;
                int stage = FertilitySpecializationUtility.Stage(pawn);
                if (stage < 0 || (def.defName == FertilitySpecializationUtility.OrdinaryName && FertilitySpecializationUtility.HasFinalState(pawn))) return neutral;
                return def.stages[def.defName == FertilitySpecializationUtility.FinalName ? 0 : stage];
            }
        }
        /// <summary>资格变化即使未改严重度，也使原版属性缓存失效。</summary>
        internal void Refresh()
        {
            int current = FertilitySpecializationUtility.Stage(pawn);
            if (current == lastStage) return;
            lastStage = current;
            pawn.health.Notify_HediffChanged(this);
        }
    }
}
