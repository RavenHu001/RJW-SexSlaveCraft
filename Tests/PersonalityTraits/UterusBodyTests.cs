// AI 辅助生成：运行真实采集／加工／植入及定义分类代码，游戏健康列表仍由原套件边界提供。
#if SSC_RW17
using System.Collections.Generic;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>身体开发不进入人格载荷；异常旧标签既不补建，也不提升宿主原有进度。</summary>
    private static void RunUterusBodyCases()
    {
        Run("子宫开发即使列为标签配方也不随人格采集", () =>
        {
            var original = DefDatabase<RecipeDef>.AllDefs;
            try
            {
                var def = new HediffDef { defName = SSCUterusDevelopmentUtility.DefName };
                var source = Body("uterus source"); var record = source.health.AddHediff(def); record.Severity = .43f;
                var other = new HediffDef { defName = "SSC_UnrelatedTag" }; source.health.AddHediff(other).Severity = .2f;
                DefDatabase<RecipeDef>.AllDefs = new List<RecipeDef> {
                    new RecipeDef_PSTag { hediffToAdd = def }, new RecipeDef_PSTag { hediffToAdd = other } };
                var data = Gel(); data.StorePawnData(source); var copy = Gel(); copy.CopyFrom(data);
                Assert(!data.HasTag(def) && !copy.HasTag(def), "采集及加工均无身体开发标签");
                Assert(copy.HasTag(other), "普通标签仍然保存");
                Assert(source.health.hediffSet.GetFirstHediffOfDef(def) == record && record.Severity == .43f, "原身体状态保留");
            }
            finally { DefDatabase<RecipeDef>.AllDefs = original; }
        });
        Run("人格加工复制剔除外部子宫标签并保留普通标签", () =>
        {
            var def = new HediffDef { defName = SSCUterusDevelopmentUtility.DefName };
            var other = new HediffDef { defName = "SSC_OtherCopyTag" };
            var data = Gel(); data.hediffTags[def] = 1f; data.hediffTags[other] = .4f;
            var copy = Gel(); copy.CopyFrom(data);
            Assert(!copy.HasTag(def) && copy.HasTag(other), "副本只排除身体开发标签");
            Assert(data.HasTag(def), "不改写来源载荷");
        });
        foreach (bool hasHostRecord in new[] { false, true })
            Run("人格植入忽略外部子宫标签，宿主已有记录=" + hasHostRecord, () =>
            {
                var def = new HediffDef { defName = SSCUterusDevelopmentUtility.DefName };
                var host = Body("uterus host"); var record = hasHostRecord ? host.health.AddHediff(def) : null;
                if (record != null) record.Severity = .23f;
                var data = Gel(); data.hediffTags[def] = 1f;
                // 直接植入未加工的外部载荷，不能让 CopyFrom 的过滤掩盖恢复入口的遗漏。
                Assert(ExcretionUtility.InheritEverything(host, data), "人格仍正常植入");
                Assert(host.health.hediffSet.GetFirstHediffOfDef(def) == record, "宿主记录不补建、不替换");
                if (record != null) Assert(record.Severity == .23f, "不合并外部更高进度");
            });
        RunStomachBodyCases();
        RunOrgasmSensitivityBodyCases();
    }


}
#endif
