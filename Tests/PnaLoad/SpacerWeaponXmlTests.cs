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
        Run("太空研究承接工业及精密装配，设备和布局明确", () =>
        {
            var d = LoadDef("Defs/ResearchDefs/SSC_PnaSpacerWeapons.xml", "SSC_RES_PNAShapeControl");
            Near(3000, Number(d.Element("baseCost"))); Equal("Spacer", d.Element("techLevel").Value);
            Assert(d.Element("prerequisites").Elements().Select(e => e.Value).SequenceEqual(new[] { "SSC_RES_PNAIndustrialWeapons", "Fabrication" }), "不用废案、充能或高级装配前置");
            Equal("HiTechResearchBench", d.Element("requiredResearchBuilding").Value);
            Equal("MultiAnalyzer", d.Element("requiredResearchFacilities").Elements().Single().Value);
            var projects = Directory.GetFiles(SscTestPaths.Combine(root, "Defs/ResearchDefs"), "*.xml")
                .SelectMany(f => XDocument.Load(f).Root.Elements()).Where(e => e.Name == "ResearchProjectDef");
            Equal(1, projects.Count(e => (string)e.Element("researchViewX") == "4" && (string)e.Element("researchViewY") == "4.5"));
        });
        foreach (var row in new[] {
            (Name:"SSC_Weapon_PnaSilkWhip", Steel:20, Plasteel:20, Components:1, Pna:10, Work:24000, Skill:6, Mass:.8),
            (Name:"SSC_Weapon_PnaPulseGun", Steel:30, Plasteel:40, Components:2, Pna:12, Work:45000, Skill:7, Mass:2.6),
            (Name:"SSC_Weapon_PnaProjectorGun", Steel:35, Plasteel:50, Components:3, Pna:15, Work:60000, Skill:8, Mass:3.6) })
            Run("太空固定配方及制造耗液 " + row.Name, () =>
            {
                var d = LoadDef(SpacerThings, row.Name); Equal("Spacer", d.Element("techLevel").Value);
                var cost = d.Element("costList"); Equal(5, cost.Elements().Count());
                Near(row.Steel, Number(cost.Element("Steel"))); Near(row.Plasteel, Number(cost.Element("Plasteel")));
                Near(10, Number(cost.Element("Chemfuel"))); Near(row.Components, Number(cost.Element("ComponentSpacer")));
                Near(row.Pna, Number(cost.Element("Purple_Aphrodisiac_Nanofluid_Plus")));
                var stats = d.Element("statBases"); Near(row.Work, Number(stats.Element("WorkToMake")));
                Near(row.Mass, Number(stats.Element("Mass"))); Near(100, Number(stats.Element("MaxHitPoints")));
                Assert(stats.Element("SSC_PnaWeaponPotency") == null, "效力不另设副本");
                var recipe = d.Element("recipeMaker"); Equal("SSC_RES_PNAShapeControl", recipe.Element("researchPrerequisite").Value);
                Near(row.Skill, Number(recipe.Element("skillRequirements").Element("Crafting")));
                var users = recipe.Element("recipeUsers"); Equal("False", users.Attribute("Inherit").Value);
                Equal("FabricationBench", users.Elements().Single().Value);
                var filter = recipe.Element("defaultIngredientFilter"); Equal("False", filter.Attribute("Inherit").Value);
                Assert(filter.Element("thingDefs").Elements().Select(e => e.Value).OrderBy(n => n)
                    .SequenceEqual(cost.Elements().Select(e => e.Name.LocalName).OrderBy(n => n)), "所有固定材料均可选");
                Assert(d.Element("comps") == null && d.Element("stuffCategories") == null, "无运行弹药、补液或材质组件");
                Equal("None", d.Element("tradeability").Value); Near(0, Number(d.Element("relicChance")));
                foreach (string key in new[] { "weaponTags", "tradeTags" })
                    Assert(!d.Element(key).HasElements && d.Element(key).Attribute("Inherit").Value == "False", "不投放商人／NPC");
            });
        Run("丝鞭只有纳米端，原版品质与专用动作接线完整", () =>
        {
            var d = LoadDef(SpacerThings, "SSC_Weapon_PnaSilkWhip");
            Equal("BaseMeleeWeapon_Sharp_Quality", d.Attribute("ParentName").Value);
            Assert(d.Element("modExtensions") == null, "无需额外品质标记");
            var tool = d.Element("tools").Elements().Single(); Near(1, Number(tool.Element("power")));
            Near(.6, Number(tool.Element("armorPenetration"))); Near(1.2, Number(tool.Element("cooldownTime")));
            Assert(tool.Element("chanceFactor") == null, "不提高原版动作权重");
            var maneuver = LoadDef(SpacerManeuvers, "SSC_Maneuver_PnaSilkWhip");
            Equal(tool.Element("capacities").Elements().Single().Value, maneuver.Element("requiredCapacity").Value);
            Equal("SexSlaveCraft.Verb_MeleeAttackSSCPnaSpacer", maneuver.Element("verb").Element("verbClass").Value);
            Equal("SSC_Damage_PnaSilkWhip", maneuver.Element("verb").Element("meleeDamageDef").Value);
            var offsets = d.Element("equippedStatOffsets"); Near(3, Number(offsets.Element("MeleeHitChance")));
            foreach (var row in new[] { (Key:"SuppressionPower", Value:.2), (Key:"TerrorSource", Value:15.0) })
            { Near(row.Value, Number(offsets.Element(row.Key))); Equal("Ludeon.RimWorld.Ideology", offsets.Element(row.Key).Attribute("MayRequire").Value); }
        });
        foreach (var row in new[] {
            (Weapon:"SSC_Weapon_PnaPulseGun", Projectile:"SSC_Projectile_PnaPulse", Damage:"SSC_Damage_PnaPulse", Ap:.7, Speed:90, Range:25.9, Warmup:.4, Cooldown:1.0, Count:3, Accuracy:new[]{.90,.80,.55,.25}),
            (Weapon:"SSC_Weapon_PnaProjectorGun", Projectile:"SSC_Projectile_PnaProjection", Damage:"SSC_Damage_PnaProjection", Ap:.9, Speed:120, Range:42.9, Warmup:1.3, Cooldown:2.1, Count:1, Accuracy:new[]{.55,.85,.95,.90}) })
            Run("太空两枪射击与普通防身入口 " + row.Weapon, () =>
            {
                var d = LoadDef(SpacerThings, row.Weapon); Equal("BaseMakeableGun", d.Attribute("ParentName").Value);
                Assert(d.Element("modExtensions") == null, "普通防身保留品质影响");
                var tool = d.Element("tools").Elements().Single(); Equal("Blunt", tool.Element("capacities").Elements().Single().Value);
                Near(9, Number(tool.Element("power"))); Near(2, Number(tool.Element("cooldownTime"))); Assert(tool.Element("armorPenetration") == null, "自动防身穿甲");
                var verb = d.Element("verbs").Elements().Single(); Equal("Verb_Shoot", verb.Element("verbClass").Value);
                Equal(row.Projectile, verb.Element("defaultProjectile").Value); Near(row.Range, Number(verb.Element("range")));
                Near(row.Warmup, Number(verb.Element("warmupTime"))); Near(row.Count, Number(verb.Element("burstShotCount")));
                if (row.Count == 3) Near(5, Number(verb.Element("ticksBetweenBurstShots")));
                Near(0, Number(verb.Element("muzzleFlashScale"))); Near(row.Cooldown, Number(d.Element("statBases").Element("RangedWeapon_Cooldown")));
                var keys = new[] { "AccuracyTouch", "AccuracyShort", "AccuracyMedium", "AccuracyLong" };
                for (int i=0; i<keys.Length; i++) Near(row.Accuracy[i], Number(d.Element("statBases").Element(keys[i])));
                var projectile = LoadDef(SpacerThings, row.Projectile); Equal("BaseBullet", projectile.Attribute("ParentName").Value);
                Equal("SexSlaveCraft.Projectile_SSCPnaTrail", projectile.Element("thingClass").Value);
                var trail = projectile.Element("comps").Elements().Single(); Equal("SexSlaveCraft.CompProperties_SSCPnaTrail", trail.Attribute("Class").Value);
                Equal("SSC_PnaTrail", trail.Element("fleckDef").Value); Equal("(0.4,0.4)", projectile.Element("graphicData").Element("drawSize").Value);
                var p = projectile.Element("projectile"); Equal(row.Damage, p.Element("damageDef").Value);
                Near(1, Number(p.Element("damageAmountBase"))); Near(row.Ap, Number(p.Element("armorPenetrationBase"))); Near(row.Speed, Number(p.Element("speed")));
            });

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
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Run("太空四语键及自动配方描述完整 " + language, () =>
            {
                foreach (var row in new[] { (Kind:"ThingDef",Count:11), (Kind:"DamageDef",Count:6), (Kind:"ToolCapacityDef",Count:1), (Kind:"ResearchProjectDef",Count:2), (Kind:"RecipeDef",Count:3) })
                {
                    var data=XDocument.Load(SscTestPaths.Combine(root,$"Languages/{language}/DefInjected/{row.Kind}/SSC_PnaSpacerWeapons.xml")).Root;
                    Equal(row.Count,data.Elements().Count()); Equal(row.Count,data.Elements().Select(e=>e.Name).Distinct().Count());
                    Assert(data.Elements().All(e=>!string.IsNullOrWhiteSpace(e.Value)),"翻译非空");
                    foreach(var death in data.Elements().Where(e=>e.Name.LocalName.EndsWith(".deathMessage"))) Equal(1,death.Value.Split("{0}").Length-1);
                }
                var recipes=XDocument.Load(SscTestPaths.Combine(root,$"Languages/{language}/DefInjected/RecipeDef/SSC_PnaSpacerWeapons.xml")).Root;
                foreach(string name in new[]{"SSC_Weapon_PnaSilkWhip","SSC_Weapon_PnaPulseGun","SSC_Weapon_PnaProjectorGun"}) Assert(recipes.Element("Make_"+name+".description")!=null,"原版自动配方键");
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
