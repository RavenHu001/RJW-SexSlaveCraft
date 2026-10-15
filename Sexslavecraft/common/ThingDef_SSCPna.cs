// AI 辅助生成：PNA 药液的原生信息栏适配，不参与摄入或健康结算。
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仅由两种 PNA 药液引用，保留原版有效信息并补充负荷成瘾规则。</summary>
    public sealed class ThingDef_SSCPna : ThingDef
    {
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            // 直接读取当前 Doer，避免重复维护剂量或在查看信息时推进身体状态。
            // 外部定义若已移除专用摄入入口，原版概率分支可能重新生效，展示也交还原版。
            var outcome = ingestible?.outcomeDoers?.Find(doer => doer is IngestionOutcomeDoer_SSCPna)
                as IngestionOutcomeDoer_SSCPna;
            bool usesPnaLoad = outcome != null && SSCPnaMath.IsFinite(outcome.severity) && outcome.severity > 0f;
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                // 原版这三项是临时生成的命名条目，没有 StatDef。限定类别及原版翻译名，
                // 仅过滤不适用的概率／耐受／安全间隔，保留恢复、需求和其他物品属性。
                if (usesPnaLoad && IsObsoleteDrugEntry(entry)) continue;
                yield return entry;
            }
            if (!usesPnaLoad) yield break;

            // 信息卡显示每份基础剂量；真实摄入的体型／耐受修正仍由原摄入流程执行。
            // 满负荷门槛与末期强化共用核心常量，不建立另一套状态或保存值。
            yield return new StatDrawEntry(StatCategoryDefOf.Drug,
                "SSC_Pna_InfoLoadPerDose".Translate(),
                (outcome.severity / SSCPnaMath.MaximumSeverity).ToStringPercent(),
                "SSC_Pna_InfoLoadPerDoseDesc".Translate(), 2480);
            yield return new StatDrawEntry(StatCategoryDefOf.DrugAddiction,
                "SSC_Pna_InfoAddictionThreshold".Translate(), 1f.ToStringPercent(),
                "SSC_Pna_InfoAddictionThresholdDesc".Translate(
                    ((float)SSCPnaMath.HighLoadThreshold).ToStringPercent(),
                    SSCPnaMath.TerminalAddictionIncrease.ToStringPercent()), 2428);
        }

        private static bool IsObsoleteDrugEntry(StatDrawEntry entry)
        {
            // 使用当前语言的原版键生成比较文本，切换语言后无需保存或刷新自建缓存。
            if (entry.stat != null) return false;
            if (entry.category == StatCategoryDefOf.DrugAddiction)
                return entry.LabelCap == "Addictiveness".Translate().CapitalizeFirst().ToString() ||
                    entry.LabelCap == "MinimumToleranceForAddiction".Translate().CapitalizeFirst().ToString();
            return entry.category == StatCategoryDefOf.Drug &&
                entry.LabelCap == "SafeDoseInterval".Translate().CapitalizeFirst().ToString();
        }
    }
}
