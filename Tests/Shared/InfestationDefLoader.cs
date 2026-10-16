// 测试资源入口：直接读取生产 XML，两个宿主共用；不复制侵染数值表。
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
internal static class InfestationDefLoader
{
    public static void Load(string root)
    {
        var defs=XDocument.Load(Path.Combine(root,"Defs/ThoughtDefs/SSC_DesireInfestation.xml")).Root.Elements("ThoughtDef")
            .Select(node=>new ThoughtDef {
                defName=(string)node.Element("defName"),
                thoughtClass=(string)node.Element("thoughtClass")=="Thought_MemorySocial"?typeof(Thought_MemorySocial):typeof(Thought_Memory),
                durationDays=Number(node,"durationDays"),stackLimit=(int)node.Element("stackLimit"),
                stackLimitForSameOtherPawn=(int)node.Element("stackLimitForSameOtherPawn"),
                lerpOpinionToZeroAfterDurationPct=node.Element("lerpOpinionToZeroAfterDurationPct")==null?.7f:Number(node,"lerpOpinionToZeroAfterDurationPct"),
                stages=node.Element("stages").Elements("li").Select(stage=>new ThoughtStage {
                    label=(string)stage.Element("label"),description=(string)stage.Element("description"),
                    baseMoodEffect=Number(stage,"baseMoodEffect"),baseOpinionOffset=Number(stage,"baseOpinionOffset")
                }).ToList()
            }).ToList();
        SSCDefOf.SSC_DesireInfestation_Mood=defs.Single(d=>d.defName=="SSC_DesireInfestation_Mood");
        SSCDefOf.SSC_DesireInfestation_Opinion=defs.Single(d=>d.defName=="SSC_DesireInfestation_Opinion");
    }
    private static float Number(XElement node,string key)=>float.Parse((string)node.Element(key)??"0",CultureInfo.InvariantCulture);
}
