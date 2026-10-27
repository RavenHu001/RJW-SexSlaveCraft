// AI 辅助生成：执行真实采集、加工和植入过滤，模型只替代游戏身体容器。
#if SSC_RW17
using System.Collections.Generic;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>微械指令属于身体；合法配方和异常外部标签均不能使其流入新人格。</summary>
    private static void RunOrgasmSensitivityBodyCases()
    {
        Run("高潮敏感度不随标签配方采集或人格加工", () =>
        {
            var recipes = DefDatabase<RecipeDef>.AllDefs;
            try
            {
                var def = new HediffDef { defName = "SSC_OrgasmSensitivity" }; var source = Body("sensitivity source");
                var record = source.health.AddHediff(def); record.Severity = 4;
                var other = new HediffDef { defName = "SSC_OrdinarySensitivityTestTag" }; source.health.AddHediff(other).Severity = .4f;
                DefDatabase<RecipeDef>.AllDefs = new List<RecipeDef> { new RecipeDef_PSTag { hediffToAdd = def }, new RecipeDef_PSTag { hediffToAdd = other } };
                var data = Gel(); data.StorePawnData(source); var copy = Gel(); copy.CopyFrom(data);
                Assert(!data.HasTag(def) && !copy.HasTag(def) && copy.HasTag(other), "只排除身体微械指令");
                Assert(source.health.hediffSet.GetFirstHediffOfDef(def) == record && record.Severity == 4, "来源身体仍保存四档");
            }
            finally { DefDatabase<RecipeDef>.AllDefs = recipes; }
        });
        Run("人格加工剔除外部敏感度标签且不改写来源", () =>
        {
            var def = new HediffDef { defName = "SSC_OrgasmSensitivity" }; var other = new HediffDef { defName = "SSC_KeepSensitivityTestTag" };
            var data = Gel(); data.hediffTags[def] = 4; data.hediffTags[other] = .4f;
            var copy = Gel(); copy.CopyFrom(data);
            Assert(!copy.HasTag(def) && copy.HasTag(other) && data.HasTag(def), "身体过滤不扩大为全部 SSC 标签");
        });
        foreach (bool existing in new[] { false, true })
            Run("人格植入保留当前身体敏感度，原有记录=" + existing, () =>
            {
                var def = new HediffDef { defName = "SSC_OrgasmSensitivity" }; var host = Body("sensitivity host");
                var record = existing ? host.health.AddHediff(def) : null; if (record != null) record.Severity = 1;
                var data = Gel(); data.hediffTags[def] = 4;
                Assert(ExcretionUtility.InheritEverything(host, data), "植入继续正常执行");
                Assert(host.health.hediffSet.GetFirstHediffOfDef(def) == record, "不创建或替换宿主身体记录");
                if (record != null) Assert(record.Severity == 1, "外部四档标签不能覆盖宿主一档");
            });
    }
}
#endif
