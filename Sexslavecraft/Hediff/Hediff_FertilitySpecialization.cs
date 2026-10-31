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
    public sealed class Hediff_FertilitySpecialization : HediffWithComps
    {
        private static readonly HediffStage neutral = new HediffStage();
        private int lastStage = -2;
        /// <summary>失格只暂停现有终极成果，不删除或改写其保存状态。</summary>
        public bool IsInactiveFinal => def?.defName == FertilitySpecializationUtility.FinalName && FertilitySpecializationUtility.Stage(pawn) < 0;
        /// <summary>原特化名称保持不变，失效时通过原生括号标签明确标记。</summary>
        public override string LabelInBrackets => IsInactiveFinal ? "SSC_Fertility_InactiveLabel".Translate().ToString() : base.LabelInBrackets;
        /// <summary>健康说明实时提示当前缺失资格；恢复身体前置后自动回到原描述。</summary>
        public override string Description => IsInactiveFinal && !FertilitySpecializationUtility.MeetsConditions(pawn, out string reason)
            ? "SSC_Fertility_InactiveDescription".Translate(reason) + "\n\n" + base.Description : base.Description;
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
