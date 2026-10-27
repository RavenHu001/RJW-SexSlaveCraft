// AI 辅助生成：将正式 XML 的攻击引用接入生产效力／投送入口；不模拟原版完整 Def 加载器。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private const string SpacerThings = "Defs/ThingsDefs/SSC_PnaSpacerWeapons.xml";
    private const string SpacerDamages = "Defs/DemageDefs/SSC_PnaSpacerDamage.xml";
    private const string SpacerManeuvers = "Defs/ManeuverDefs/SSC_PnaSpacerManeuvers.xml";

    private static void RunSpacerWeaponXmlTests()
    {

        // 从正式武器追踪至 Maneuver／投射物及剂量扩展，然后运行生产处理器。
        // 身份、剂量、修复是真实生产代码；护甲和最终写伤仍由本宿主提供可观察边界。
        foreach (var row in new[] { (Name:"SSC_Weapon_PnaSilkWhip", Dose:.20, Repair:1.0), (Name:"SSC_Weapon_PnaPulseGun", Dose:.10, Repair:.5), (Name:"SSC_Weapon_PnaProjectorGun", Dose:.25, Repair:1.25) })
        {
            Run("正式XML效力读取及普通穿透 " + row.Name, () =>
            {
                var pair = SpacerAttackFromXml(row.Name); Near(row.Dose, Potency(pair.Weapon));
                var p = CoatedPawn(); var result = new DamageWorker_SSCPnaSpacer().Apply(pair.Shot,p);
                Near(row.Dose, SSCPnaExposureUtility.GetLoad(p)); Near(1,result.totalDamageDealt); Equal(1,ArmorUtility.Calls);
            });
            Run("正式XML有效链无甲耗吸收修复 " + row.Name, () =>
            {
                var pair = SpacerAttackFromXml(row.Name); var p = SpacerSlave(); var injury = SpacerInjury(p,10);
                ArmorUtility.Boundary = (Pawn _,float amount,ref DamageDef damage) => 0;
                var result = new DamageWorker_SSCPnaSpacer().Apply(pair.Shot,p);
                Near(row.Dose,SSCPnaExposureUtility.GetLoad(p)); Near(row.Repair,injury.Healed); Equal(0,ArmorUtility.Calls); Assert(!result.wounded,"无创伤");
            });
            Run("正式XML主人优先免疫 " + row.Name, () =>
            {
                var pair = SpacerAttackFromXml(row.Name); var p = SpacerSlave(); p.IsMaster=true; var injury=SpacerInjury(p,10);
                new DamageWorker_SSCPnaSpacer().Apply(pair.Shot,p); AssertNoMasterDrugState(p); Near(0,injury.Healed); Equal(0,ArmorUtility.Calls);
            });
            Run("正式XML普通目标挡甲不施药 " + row.Name, () =>
            {
                var pair = SpacerAttackFromXml(row.Name); var p=CoatedPawn(); p.health.capacities.Consciousness=3;
                ArmorUtility.Boundary=(Pawn _,float amount,ref DamageDef damage)=>0;
                new DamageWorker_SSCPnaSpacer().Apply(pair.Shot,p); AssertNoCoatingDrug(p); Equal(1,ArmorUtility.Calls);
            });
        }
        Run("太空三发由正式XML驱动逐发投送及修复", () =>
        {
            var pair=SpacerAttackFromXml("SSC_Weapon_PnaPulseGun"); var p=SpacerSlave(); var injury=SpacerInjury(p,10);
            int count=(int)Number(LoadDef(SpacerThings,"SSC_Weapon_PnaPulseGun").Element("verbs").Elements().Single().Element("burstShotCount"));
            for(int i=0;i<count;i++) new DamageWorker_SSCPnaSpacer().Apply(pair.Shot,p);
            Near(.3,SSCPnaExposureUtility.GetLoad(p)); Near(1.5,injury.Healed); Equal(3,SSCPnaWeaponEffectsUtility.Absorptions.Count);
        });
    }

    private static (ThingDef Weapon, DamageInfo Shot) SpacerAttackFromXml(string name)
    {
        var xml=LoadDef(SpacerThings,name); var verb=xml.Element("verbs")?.Elements().Single();
        var attack=verb==null ? xml.Element("tools").Elements().Single() : LoadDef(SpacerThings,verb.Element("defaultProjectile").Value).Element("projectile");
        string damageName=verb==null ? LoadDef(SpacerManeuvers,"SSC_Maneuver_PnaSilkWhip").Element("verb").Element("meleeDamageDef").Value : attack.Element("damageDef").Value;
        var damage=LoadDef(SpacerDamages,damageName); Assert(damage.Attribute("ParentName")==null,"不继承旧涂覆流程");
        Equal(typeof(DamageWorker_SSCPnaSpacer).FullName,damage.Element("workerClass").Value); Equal("Sharp",damage.Element("armorCategory").Value);
        var ext=damage.Element("modExtensions").Elements().Single(); Equal(typeof(SSCPnaSpacerExtension).FullName,ext.Attribute("Class").Value);
        Equal("Hediff_PNA_Consciousness_Record",ext.Element("recordHediff").Value); Equal(2,ext.Elements().Count());
        if(verb!=null) Equal("true",damage.Element("isRanged").Value);
        var def=new DamageDef{defName=damageName,workerClass=typeof(DamageWorker_SSCPnaSpacer),Extension=new SSCPnaSpacerExtension{baseLoad=(float)Number(ext.Element("baseLoad")),recordHediff=CoatingResistance}};
        var weapon=PotencyRanged(def);
        if(verb==null)
        {
            var capacity=new ToolCapacityDef();
            DefDatabase<ManeuverDef>.AllDefs=new List<ManeuverDef>{new ManeuverDef{requiredCapacity=capacity,verb=new VerbProperties{meleeDamageDef=def}}};
            weapon=new ThingDef{IsMeleeWeapon=true,tools=new List<Tool>{new Tool{capacities=new List<ToolCapacityDef>{capacity}}}};
        }
        return (weapon,new DamageInfo{Def=def,Amount=(float)Number(attack.Element(verb==null?"power":"damageAmountBase")),ArmorPenetrationInt=(float)Number(attack.Element(verb==null?"armorPenetration":"armorPenetrationBase"))});
    }
}
