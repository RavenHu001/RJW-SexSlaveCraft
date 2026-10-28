// AI 辅助生成：运行 XML 到生产涂覆处理器的接线回归；不模拟原版护甲随机或 XML 继承。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private const string PrimitiveThings = "Defs/ThingsDefs/SSC_PnaPrimitiveWeapons.xml";
    private const string PrimitiveDamages = "Defs/DemageDefs/SSC_PnaPrimitiveDamage.xml";
    private const string PrimitiveManeuvers = "Defs/ManeuverDefs/SSC_PnaPrimitiveManeuvers.xml";

    private static void RunPrimitiveWeaponXmlTests()
    {
        // 从真实 XML 读取剂量，再执行生产处理器；物理边界只提供成功伤口或阻挡结果。
        foreach (var row in new[]
        {
            (Damage:"SSC_Damage_PnaStaffStab", Dose:.15, Power:5f, Injury:"Stab"),
            (Damage:"SSC_Damage_PnaDartStab", Dose:.06, Power:3f, Injury:"Stab"),
            (Damage:"SSC_Damage_PnaBolt", Dose:.10, Power:7f, Injury:"Cut")
        })
        {
            Run("XML 剂量经生产处理器在开放伤口后输入 " + row.Damage, () =>
            {
                var extension = PrimitiveExtension(row.Damage);
                Near(row.Dose, extension.baseLoad);
                var pawn = CoatedPawn();
                var shot = CoatedShot(extension.baseLoad, row.Power);
                shot.Def.Extension = extension;
                var worker = CoatedWorker(row.Injury);
                var result = worker.Apply(shot, pawn);
                Near(row.Dose, SSCPnaExposureUtility.GetLoad(pawn));
                Near(row.Power, result.totalDamageDealt);
                Equal(1, worker.ApplyCalls);
            });
        }
    }

    private static SSCPnaCoatingExtension PrimitiveExtension(string damage)
    {
        var extension = LoadDef(PrimitiveDamages, damage).Element("modExtensions").Elements().Single();
        Equal("SexSlaveCraft.SSCPnaCoatingExtension", extension.Attribute("Class").Value);
        Equal("Hediff_PNA_Consciousness_Record", extension.Element("recordHediff").Value);
        return new SSCPnaCoatingExtension
        {
            baseLoad=(float)Number(extension.Element("baseLoad")),
            recordHediff=new HediffDef { defName=extension.Element("recordHediff").Value }
        };
    }
}
