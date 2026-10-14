// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>每份原始剂量乘实际份数，当前启用的原摄入修正只应用一次。</summary>
    public sealed class IngestionOutcomeDoer_SSCPna : IngestionOutcomeDoer
    {
        // severity 是每份原始严重度，满负荷为 2；修正开关沿用原版摄入接口默认值。
        public float severity;
        public ChemicalDef toleranceChemical;
        public bool divideByBodySize;
        public bool multiplyByGeneToleranceFactors;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            // 先校验身体和实际份数，避免对失效健康载体调用原版化学修正。
            if (Scribe.mode != LoadSaveMode.Inactive || !SSCPnaExposureUtility.IsValidTarget(pawn) ||
                ingestedCount <= 0 || !SSCPnaMath.IsFinite(severity) || severity <= 0f) return;
            // 每份剂量只修正一次；原负荷 Doer 已由 XML 替换，不再叠加第二份效果。
            float effect = severity;
            AddictionUtility.ModifyChemicalEffectForToleranceAndBodySize(pawn, toleranceChemical, ref effect,
                multiplyByGeneToleranceFactors, divideByBodySize);
            // 乘实际摄入份数后换成归一化负荷；用 double 中间值防止批量计算溢出。
            double dose = effect * (double)ingestedCount / SSCPnaMath.MaximumSeverity;
            if (double.IsNaN(dose) || double.IsInfinity(dose) || dose <= 0d) return;
            // PrePostIngested 先于 Doer；已有依赖的成瘾品已经由原版增加严重度并补充需求。
            // 只判断当前摄入组件，不增加缓存或另写服药算法；首次建立仍交给共享入口。
            CompDrug drug = ingested?.TryGetComp<CompDrug>();
            bool nativeRefill = drug?.Props.Addictive == true &&
                SSCPnaExposureUtility.IsAddictionDef(drug.Props.chemical?.addictionHediff) &&
                SSCPnaExposureUtility.GetAddiction(pawn) != null;
            SSCPnaExposureUtility.ApplyExposure(pawn, (float)System.Math.Min(dose, float.MaxValue),
                SSCPnaExposureSource.Ingestion, refillIngestionNeed: !nativeRefill);
        }
    }
}
