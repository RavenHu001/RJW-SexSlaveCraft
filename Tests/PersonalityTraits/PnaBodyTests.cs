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
                foreach(string name in BodyPnaNames)
                {
                    var def=new HediffDef{defName=name};
                    source.health.AddHediff(def).Severity=.4f;
                    DefDatabase<RecipeDef>.AllDefs.Add(new RecipeDef_PSTag{defName="Capture"+name,hediffToAdd=def});
                }
                var gel=Gel();gel.StorePawnData(source);
                Assert(!gel.hediffTags.Keys.Any(SSCPnaExposureUtility.IsBodyStateDef),"凝胶排除身体PNA状态");
                foreach(string name in BodyPnaNames)
                    Assert(source.health.hediffSet.hediffs.Any(h=>h.def.defName==name && h.Severity==.4f),"采集保留身体记录");
            }
            finally { DefDatabase<RecipeDef>.AllDefs=definitions; }
        });
        // 构造含更高 PNA 值的旧凝胶，验证植入跳过标签并保留宿主身体原值。
        Run("历史人格快照的PNA标签不覆写宿主身体",()=>
        {
            var host=Body("PNA host");var gel=Gel();
            foreach(string name in BodyPnaNames)
            {
                var def=new HediffDef{defName=name};host.health.AddHediff(def).Severity=.2f;
                gel.hediffTags[def]=.9f;
            }
            Assert(ExcretionUtility.InheritEverything(host,gel),"人格植入成功");
            foreach(string name in BodyPnaNames)
                Assert(host.health.hediffSet.hediffs.Single(h=>h.def.defName==name).Severity==.2f,"宿主PNA原值保留");
        });
        // 保留其他恢复步骤的末尾回调，核验协调发生在实际身份／标签恢复完成之后。
        Run("人格恢复结束后核对 PNA 且不执行输入",()=>
        {
            var host=Body("PNA final host");var gel=Gel();
            var marker=new HediffDef{defName="SSC_FinalRestoreMarker"};gel.hediffTags[marker]=.6f;
            int calls=0;SSCPnaExposureUtility.OnRefresh=p=>
            {
                Assert(p==host && p.health.hediffSet.GetFirstHediffOfDef(marker)?.Severity==.6f,"标签回调已经结束");calls++;
            };
            try { Assert(ExcretionUtility.InheritEverything(host,gel),"植入成功");Assert(calls==1,"最终身体核对一次"); }
            finally { SSCPnaExposureUtility.OnRefresh=null; }
        });
    }

    private static readonly string[] BodyPnaNames={"Hediff_PNA_Debuff","SSC_Hediff_PnaRootedLoad","PNA_Addiction",
        "Hediff_PNA_Combat_Buff","Hediff_PNA_Combat_Buff_Plus","Hediff_PNA_Consciousness_Record",
        "SSC_Purple_HyperLactation","SSC_Hediff_RabbitCloneLowPNA"};
}

namespace SexSlaveCraft
{
    // 健康系统与真实分类由 PnaLoad 运行生产实现；这里隔离健康边界，执行真实人格采集／恢复代码。
    public static class SSCPnaExposureUtility
    {
        public static bool IsBodyStateDef(HediffDef def)=>def!=null &&
            (def.defName=="Hediff_PNA_Debuff" || def.defName=="SSC_Hediff_PnaRootedLoad" || def.defName=="PNA_Addiction" ||
             def.defName=="Hediff_PNA_Combat_Buff" || def.defName=="Hediff_PNA_Combat_Buff_Plus" ||
             def.defName=="Hediff_PNA_Consciousness_Record" || def.defName=="SSC_Purple_HyperLactation" ||
             def.defName=="SSC_Hediff_RabbitCloneLowPNA");
        public static System.Action<Pawn> OnRefresh;
        public static void RefreshBodyState(Pawn pawn,bool forceRefresh=false)=>OnRefresh?.Invoke(pawn);
    }
}
