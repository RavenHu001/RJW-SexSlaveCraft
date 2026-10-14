using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>运行实际人格采集／植入代码，验证 PNA 只归属身体的最小排除规则。</summary>
    private static void RunPnaBodyCases()
    {
        // 将 PNA 放进通用配方标签候选，验证采集仍显式过滤，且不删除原身体记录。
        Run("人格采集不会带走PNA负荷和依赖",()=>
        {
            // 临时隔离配方定义库，finally 恢复原列表，避免污染后续人格测试。
            var definitions=DefDatabase<RecipeDef>.AllDefs;
            try
            {
                var source=Body("PNA source");
                DefDatabase<RecipeDef>.AllDefs=new List<RecipeDef>();
                foreach(string name in new[]{"Hediff_PNA_Debuff","SSC_Hediff_PnaRootedLoad","PNA_Addiction"})
                {
                    var def=new HediffDef{defName=name};
                    source.health.AddHediff(def).Severity=.4f;
                    DefDatabase<RecipeDef>.AllDefs.Add(new RecipeDef_PSTag{defName="Capture"+name,hediffToAdd=def});
                }
                var gel=Gel();gel.StorePawnData(source);
                Assert(!gel.hediffTags.Keys.Any(SSCPnaExposureUtility.IsBodyStateDef),"凝胶排除身体PNA状态");
                foreach(string name in new[]{"Hediff_PNA_Debuff","SSC_Hediff_PnaRootedLoad","PNA_Addiction"})
                    Assert(source.health.hediffSet.hediffs.Any(h=>h.def.defName==name && h.Severity==.4f),"采集保留身体记录");
            }
            finally { DefDatabase<RecipeDef>.AllDefs=definitions; }
        });
        // 构造含更高 PNA 值的旧凝胶，验证植入跳过标签并保留宿主身体原值。
        Run("历史人格快照的PNA标签不覆写宿主身体",()=>
        {
            var host=Body("PNA host");var gel=Gel();
            foreach(string name in new[]{"Hediff_PNA_Debuff","SSC_Hediff_PnaRootedLoad","PNA_Addiction"})
            {
                var def=new HediffDef{defName=name};host.health.AddHediff(def).Severity=.2f;
                gel.hediffTags[def]=.9f;
            }
            Assert(ExcretionUtility.InheritEverything(host,gel),"人格植入成功");
            foreach(string name in new[]{"Hediff_PNA_Debuff","SSC_Hediff_PnaRootedLoad","PNA_Addiction"})
                Assert(host.health.hediffSet.hediffs.Single(h=>h.def.defName==name).Severity==.2f,"宿主PNA原值保留");
        });
    }
}

namespace SexSlaveCraft
{
    // 健康系统由 PnaLoad 直接运行生产工具验证；这里隔离其静态分类接口，验证真实人格采集／恢复代码。
    public static class SSCPnaExposureUtility
    {
        public static bool IsBodyStateDef(HediffDef def)=>def!=null &&
            (def.defName=="Hediff_PNA_Debuff" || def.defName=="SSC_Hediff_PnaRootedLoad" || def.defName=="PNA_Addiction");
    }
}
