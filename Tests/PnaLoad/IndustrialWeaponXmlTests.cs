// AI 辅助生成：工业 XML 接线、真实剂量入口和既有武器隔离回归。
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;

internal static partial class Program
{
    private const string IndustrialThings = "Defs/ThingsDefs/SSC_PnaIndustrialWeapons.xml";
    private const string IndustrialDamages = "Defs/DemageDefs/SSC_PnaIndustrialDamage.xml";

    private static void RunIndustrialWeaponXmlTests()
    {
        Run("工业研究三个前置及1600点，无废案或额外研究台门槛", () =>
        {
            var d = LoadDef("Defs/ResearchDefs/SSC_PnaIndustrialWeapons.xml", "SSC_RES_PNAIndustrialWeapons");
            Near(1600, Number(d.Element("baseCost"))); Equal("Industrial", d.Element("techLevel").Value);
            Assert(d.Element("prerequisites").Elements().Select(e => e.Value).SequenceEqual(new[] {
                "SSC_RES_PNAPrimitiveWeapons", "SSC_RES_PNAConcentration", "Machining" }), "独立工业链");
            Assert(d.Element("requiredResearchBuilding") == null, "无高科技台限制");
            var projects = Directory.GetFiles(SscTestPaths.Combine(root, "Defs/ResearchDefs"), "*.xml")
                .SelectMany(f => XDocument.Load(f).Root.Elements()).Where(e => e.Name == "ResearchProjectDef");
            Equal(1, projects.Count(e => (string)e.Element("researchViewX") == "3" && (string)e.Element("researchViewY") == "4.5"));
        });
        foreach (var row in new[] {
            (Name:"SSC_Weapon_PnaShortWhip", Steel:20, Fuel:20, Components:1, Pna:6, Work:8000, Skill:4, Mass:.8),
            (Name:"SSC_Weapon_PnaNeedleRepeater", Steel:45, Fuel:10, Components:3, Pna:8, Work:14000, Skill:5, Mass:2.0),
            (Name:"SSC_Weapon_PnaNeedleRifle", Steel:65, Fuel:15, Components:4, Pna:10, Work:18000, Skill:6, Mass:3.0) })
            Run("工业制造固定聚合物材料和加工台 " + row.Name, () =>
            {
                var d = LoadDef(IndustrialThings, row.Name); var cost = d.Element("costList");
                Equal(4, cost.Elements().Count()); Near(row.Steel, Number(cost.Element("Steel")));
                Near(row.Fuel, Number(cost.Element("Chemfuel"))); Near(row.Components, Number(cost.Element("ComponentIndustrial")));
                Near(row.Pna, Number(cost.Element("Purple_Aphrodisiac_Nanofluid_Plus")));
                var stats = d.Element("statBases"); Near(row.Work, Number(stats.Element("WorkToMake")));
                Near(row.Mass, Number(stats.Element("Mass"))); Near(100, Number(stats.Element("MaxHitPoints")));
                Assert(stats.Element("MarketValue") == null && stats.Element("SSC_PnaWeaponPotency") == null, "不重复配置效力或售价");
                var recipe = d.Element("recipeMaker");
                Equal("SSC_RES_PNAIndustrialWeapons", recipe.Element("researchPrerequisite").Value);
                Near(row.Skill, Number(recipe.Element("skillRequirements").Element("Crafting")));
                Assert(recipe.Element("recipeUsers").Elements().Select(e => e.Value).SequenceEqual(new[] { "TableMachining" }), "只在机械加工台制作");
                Assert(recipe.Element("defaultIngredientFilter").Element("thingDefs").Elements().Select(e => e.Value)
                    .OrderBy(e => e).SequenceEqual(cost.Elements().Select(e => e.Name.LocalName).OrderBy(e => e)), "材料过滤包括燃料和药液");
                Assert(d.Element("comps") == null && d.Element("stuffCategories") == null, "不增加装填供气或材质管理");
                Equal("None", d.Element("tradeability").Value); Near(0, Number(d.Element("relicChance")));
                Assert(!d.Element("weaponTags").HasElements && !d.Element("tradeTags").HasElements, "只由玩家制作");
            });
        Run("短鞭低创伤主攻击、独立动作与文化装备属性", () =>
        {
            var d = LoadDef(IndustrialThings, "SSC_Weapon_PnaShortWhip"); var tool = d.Element("tools").Elements().First();
            Near(3, Number(tool.Element("power"))); Near(.2, Number(tool.Element("armorPenetration")));
            Near(1.4, Number(tool.Element("cooldownTime"))); Near(3, Number(tool.Element("chanceFactor")));
            Equal("SSC_ToolCapacity_PnaWhip", tool.Element("capacities").Elements().Single().Value);
            var offsets = d.Element("equippedStatOffsets"); Near(3, Number(offsets.Element("MeleeHitChance")));
            foreach (var row in new[] { (Name:"SuppressionPower", Value:.20), (Name:"TerrorSource", Value:15.0) })
            { Near(row.Value, Number(offsets.Element(row.Name))); Equal("Ludeon.RimWorld.Ideology", offsets.Element(row.Name).Attribute("MayRequire").Value); }
            var maneuver = LoadDef("Defs/ManeuverDefs/SSC_PnaIndustrialManeuvers.xml", "SSC_Maneuver_PnaWhip");
            Equal("SexSlaveCraft.Verb_MeleeAttackSSCPnaWhip", maneuver.Element("verb").Element("verbClass").Value);
            Equal("SSC_Damage_PnaWhipCut", maneuver.Element("verb").Element("meleeDamageDef").Value);
            var handle = d.Element("tools").Elements().Last(); Near(2, Number(handle.Element("power")));
            Near(0, Number(handle.Element("armorPenetration"))); Equal("Blunt", handle.Element("capacities").Elements().Single().Value);
        });
        Run("所有武器不再声明品质伤害例外", () =>
        {
            var marked = Directory.GetFiles(SscTestPaths.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
                .SelectMany(f => XDocument.Load(f).Root.Elements()).Where(d => d.Element("modExtensions")?.Elements()
                    .Any(e => (string)e.Attribute("Class") == "SexSlaveCraft.SSCPnaFixedMeleeDamageExtension") == true)
                .Select(d => d.Element("defName").Value).OrderBy(n => n).ToArray();
            Assert(marked.Length == 0, "品质完全交由原版处理");
        });
        foreach (var row in new[] {
            (Name:"SSC_Weapon_PnaNeedleRepeater", Projectile:"SSC_Projectile_PnaRepeaterNeedle", Damage:"SSC_Damage_PnaRepeaterNeedle", Power:2.0, Ap:.3, Speed:70, Range:22.9, Warmup:.5, Cooldown:1.3, Count:3, Accuracy:new[]{.85,.70,.35,.20}),
            (Name:"SSC_Weapon_PnaNeedleRifle", Projectile:"SSC_Projectile_PnaRifleNeedle", Damage:"SSC_Damage_PnaRifleNeedle", Power:4.0, Ap:.4, Speed:90, Range:36.9, Warmup:1.5, Cooldown:2.5, Count:1, Accuracy:new[]{.55,.80,.90,.80}) })
            Run("针枪精度点射、防身伤害与尾迹 " + row.Name, () =>
            {
                var d = LoadDef(IndustrialThings, row.Name); var verb = d.Element("verbs").Elements().Single();
                Equal("Verb_Shoot", verb.Element("verbClass").Value); Equal(row.Projectile, verb.Element("defaultProjectile").Value);
                Near(row.Range, Number(verb.Element("range"))); Near(row.Warmup, Number(verb.Element("warmupTime")));
                Near(row.Count, Number(verb.Element("burstShotCount"))); if (row.Count == 3) Near(6, Number(verb.Element("ticksBetweenBurstShots")));
                Near(0, Number(verb.Element("muzzleFlashScale"))); Assert(verb.Element("soundCastTail") == null, "无火药枪尾声");
                var stats = d.Element("statBases"); Near(row.Cooldown, Number(stats.Element("RangedWeapon_Cooldown")));
                var keys = new[] { "AccuracyTouch", "AccuracyShort", "AccuracyMedium", "AccuracyLong" };
                for (int i = 0; i < keys.Length; i++) Near(row.Accuracy[i], Number(stats.Element(keys[i])));
                var tool = d.Element("tools").Elements().Single(); Near(9, Number(tool.Element("power"))); Near(2, Number(tool.Element("cooldownTime")));
                Equal("Blunt", tool.Element("capacities").Elements().Single().Value); Assert(tool.Element("armorPenetration") == null, "原版自动防身穿甲");
                var projectile = LoadDef(IndustrialThings, row.Projectile);
                Equal("SexSlaveCraft.Projectile_SSCPnaTrail", projectile.Element("thingClass").Value);
                Equal("SSC_PnaTrail", projectile.Element("comps").Elements().Single().Element("fleckDef").Value);
                var damage = projectile.Element("projectile"); Equal(row.Damage, damage.Element("damageDef").Value);
                Near(row.Power, Number(damage.Element("damageAmountBase"))); Near(row.Ap, Number(damage.Element("armorPenetrationBase"))); Near(row.Speed, Number(damage.Element("speed")));
            });
        foreach (var row in new[] {
            (Damage:"SSC_Damage_PnaWhipCut", Dose:.18f, Power:3f, Injury:"Cut"),
            (Damage:"SSC_Damage_PnaRepeaterNeedle", Dose:.08f, Power:2f, Injury:"Stab"),
            (Damage:"SSC_Damage_PnaRifleNeedle", Dose:.20f, Power:4f, Injury:"Stab") })
        {
            Run("工业XML真实剂量入口及效力显示 " + row.Damage, () =>
            {
                var extension = LoadDef(IndustrialDamages, row.Damage).Element("modExtensions").Elements().Single();
                Near(row.Dose, Number(extension.Element("baseLoad")));
                Equal("Hediff_PNA_Consciousness_Record", extension.Element("recordHediff").Value);
                var shot = CoatedShot((float)Number(extension.Element("baseLoad")), row.Power);
                shot.Def.workerClass = typeof(DamageWorker_SSCPnaCoated);
                var pawn = CoatedPawn(); var worker = CoatedWorker(row.Injury); worker.Apply(shot, pawn);
                Near(row.Dose, SSCPnaExposureUtility.GetLoad(pawn));
                Assert(SSCPnaWeaponPotencyUtility.TryGetBaseline(PotencyRanged(shot.Def), out float dose), "共用属性正确识别新伤害"); Near(row.Dose, dose);
            });
            Run("工业命中挡甲不施药 " + row.Damage, () =>
            {
                var pawn = CoatedPawn(); var worker = CoatedWorker(row.Injury);
                worker.PhysicalApply = (info,target) => new Verse.DamageWorker.DamageResult { deflected=true };
                worker.Apply(CoatedShot(row.Dose, row.Power), pawn); AssertNoCoatingDrug(pawn);
            });
        }
        Run("速射三针各自读取命中前意识，不固定合计24点", () =>
        {
            var pawn = CoatedPawn(); var worker = CoatedWorker("Stab");
            pawn.health.capacities.Consciousness = 1; worker.Apply(CoatedShot(.08f, 2), pawn);
            pawn.health.capacities.Consciousness = .5f; worker.Apply(CoatedShot(.08f, 2), pawn);
            pawn.health.capacities.Consciousness = .25f; worker.Apply(CoatedShot(.08f, 2), pawn);
            Near(.56, SSCPnaExposureUtility.GetLoad(pawn)); Equal(3, worker.ApplyCalls);
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Run("工业四语接线与死亡占位符 " + language, () =>
            {
                foreach (var row in new[] { (Kind:"ThingDef", Count:12), (Kind:"DamageDef", Count:6), (Kind:"ToolCapacityDef", Count:1), (Kind:"ResearchProjectDef", Count:2), (Kind:"RecipeDef", Count:3) })
                {
                    var data = XDocument.Load(SscTestPaths.Combine(root, $"Languages/{language}/DefInjected/{row.Kind}/SSC_PnaIndustrialWeapons.xml")).Root;
                    Equal(row.Count, data.Elements().Count()); Equal(row.Count, data.Elements().Select(e => e.Name).Distinct().Count());
                    Assert(data.Elements().All(e => !string.IsNullOrWhiteSpace(e.Value)), "完整翻译");
                    foreach (var death in data.Elements().Where(e => e.Name.LocalName.EndsWith(".deathMessage"))) Equal(1, death.Value.Split("{0}").Length - 1);
                }
            });
    }
}
