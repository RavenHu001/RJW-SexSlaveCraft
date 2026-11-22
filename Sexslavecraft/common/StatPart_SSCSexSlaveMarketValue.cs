// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>把当前性奴化阶段的市值系数接入原版人物估价。</summary>
    public sealed class StatPart_SSCSexSlaveMarketValue : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val) => val *= GetFactor(req, out _);

        public override string ExplanationPart(StatRequest req)
        {
            float factor = GetFactor(req, out Hediff progress);
            return factor == 1f ? null : $"{progress.def.LabelCap} ×{factor:0.##}";
        }

        private float GetFactor(StatRequest req, out Hediff progress)
        {
            // 原版人物估价用种族 Def 计算基础值，通用 Hediff statFactors 看不到人物。
            // 在原版估价的最终处理阶段用完整人物请求读取唯一生效的进度载体。
            progress = req.Thing is Pawn pawn ? TrainingOutcomeUtility.GetSexSlaveProgress(pawn) : null;
            HediffStage stage = progress?.CurStage;
            // 旧锁链低于一阶时，原版仍回退到第一个 CurStage，零阶不应拿到一阶系数。
            if (stage != null && stage.minSeverity > 0f && progress.Severity >= stage.minSeverity)
                return stage.statFactors.GetStatValueFromList(parentStat, 1f);
            return 1f;
        }
    }
}
