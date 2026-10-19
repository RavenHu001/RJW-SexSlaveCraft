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
    private const string PrimitiveResearch = "SSC_RES_PNAPrimitiveWeapons";

    private static void RunPrimitiveWeaponXmlTests()
    {
        Run("原始武器研究在简易应用后独立解锁且坐标不重叠", () =>
        {
            var research = LoadDef("Defs/ResearchDefs/ResearchProjectDef.xml", PrimitiveResearch);
            Equal("Medieval", research.Element("techLevel").Value);
            Near(800, Number(research.Element("baseCost")));
            Assert(research.Element("prerequisites").Elements().Select(e => e.Value)
                .SequenceEqual(new[] { "SSC_RES_PNASimpleApp" }), "不要求浓缩或工业研究");
            Near(2, Number(research.Element("researchViewX")));
            Near(4.5, Number(research.Element("researchViewY")));
            var others = XDocument.Load(SscTestPaths.Combine(root, "Defs/ResearchDefs/ResearchProjectDef.xml"))
                .Root.Elements().Where(d => (string)d.Element("defName") != PrimitiveResearch);
            Assert(others.All(d => (double?)d.Element("researchViewX") != 2 ||
                (double?)d.Element("researchViewY") != 4.5), "新研究不占用既有坐标");
        });

        var weapons = new[]
        {
            (Name:"SSC_Weapon_PnaSpikeStaff", Wood:30, Steel:15, Pna:10, Work:4000, Skill:3),
            (Name:"SSC_Weapon_PnaBlowpipe", Wood:20, Steel:5, Pna:6, Work:2400, Skill:2),
            (Name:"SSC_Weapon_PnaLightCrossbow", Wood:45, Steel:25, Pna:10, Work:8000, Skill:4)
        };
        foreach (var weapon in weapons)
        {
            Run("原始武器配方中世纪可达、仅制造耗液 " + weapon.Name, () =>
            {
                var def = LoadDef(PrimitiveThings, weapon.Name);
                Equal("Medieval", def.Element("techLevel").Value);
                Near(weapon.Work, Number(def.Element("statBases").Element("WorkToMake")));
                var cost = def.Element("costList");
                Equal(3, cost.Elements().Count());
                Near(weapon.Wood, Number(cost.Element("WoodLog")));
                Near(weapon.Steel, Number(cost.Element("Steel")));
                Near(weapon.Pna, Number(cost.Element("Purple_Aphrodisiac_Nanofluid")));
                Assert(def.Element("stuffCategories") == null && def.Element("costStuffCount") == null,
                    "固定材料不开放其他材质倍率");
                var recipe = def.Element("recipeMaker");
                Equal(PrimitiveResearch, recipe.Element("researchPrerequisite").Value);
                Near(weapon.Skill, Number(recipe.Element("skillRequirements").Element("Crafting")));
                var benches = recipe.Element("recipeUsers");
                Equal("False", benches.Attribute("Inherit").Value);
                Assert(benches.Elements().Select(e => e.Value).SequenceEqual(weapon.Skill == 2
                    ? new[] { "CraftingSpot", "FueledSmithy", "ElectricSmithy" }
                    : new[] { "FueledSmithy", "ElectricSmithy" }), "替换继承工作台，无机械加工台");
                Assert(def.Element("comps") == null, "不引入补液、充能或装填组件");
                Equal("None", def.Element("tradeability").Value);
                Assert(!def.Element("weaponTags").HasElements && !def.Element("tradeTags").HasElements,
                    "不接入 NPC 或商人标签");
                Equal(0, (int)def.Element("relicChance"));
            });
            Run("原始武器普通柄击不连接携液入口 " + weapon.Name, () =>
            {
                var tools = LoadDef(PrimitiveThings, weapon.Name).Element("tools").Elements();
                var blunt = tools.Single(t => t.Element("capacities").Elements().Any(c => c.Value == "Blunt"));
                Near(2, Number(blunt.Element("power")));
                Near(0, Number(blunt.Element("armorPenetration")));
                Near(2, Number(blunt.Element("cooldownTime")));
                Assert(blunt.Element("capacities").Elements().Select(e => e.Value).SequenceEqual(new[] { "Blunt" }),
                    "柄击不使用涂覆能力或原版 Poke 的内部刺击");
                Assert(tools.All(t => t.Element("extraMeleeDamages") == null), "不使用独立额外 PNA 命中");
            });
        }

        Run("短矛通过独立能力和动作接入主伤害", () =>
        {
            var staff = LoadDef(PrimitiveThings, weapons[0].Name);
            Near(2, Number(staff.Element("equippedStatOffsets").Element("MeleeHitChance")));
            var spike = staff.Element("tools").Elements().First();
            Near(5, Number(spike.Element("power")));
            Near(.12, Number(spike.Element("armorPenetration")));
            Near(1.8, Number(spike.Element("cooldownTime")));
            Near(2, Number(spike.Element("chanceFactor")));
            string capacity = spike.Element("capacities").Elements().Single().Value;
            Equal("SSC_ToolCapacity_PnaStab", LoadDef(PrimitiveManeuvers, capacity).Element("defName").Value);
            var maneuver = LoadDef(PrimitiveManeuvers, "SSC_Maneuver_PnaStab");
            Equal(capacity, maneuver.Element("requiredCapacity").Value);
            Equal("SexSlaveCraft.Verb_MeleeAttackSSCPna", maneuver.Element("verb").Element("verbClass").Value);
            Equal("SSC_Damage_PnaStaffStab", maneuver.Element("verb").Element("meleeDamageDef").Value);
            var filter = staff.Element("recipeMaker").Element("defaultIngredientFilter");
            Equal("False", filter.Attribute("Inherit").Value);
            Assert(filter.Element("disallowedThingDefs") == null && filter.Element("thingDefs").Elements()
                .Select(e => e.Value).SequenceEqual(staff.Element("costList").Elements().Select(e => e.Name.LocalName)),
                "覆盖默认禁木材筛选，原液和固定材料均可选");
        });

        foreach (var row in new[]
        {
            (Name:weapons[1].Name, Projectile:"SSC_Projectile_PnaDart", Damage:"SSC_Damage_PnaDartStab",
                Power:3d, Ap:.05, Range:17.9, Warmup:.5, Cooldown:1d, Accuracy:new[]{.65,.45,.20,.10}),
            (Name:weapons[2].Name, Projectile:"SSC_Projectile_PnaBolt", Damage:"SSC_Damage_PnaBolt",
                Power:7d, Ap:.18, Range:30.9, Warmup:1.5, Cooldown:2.5, Accuracy:new[]{.50,.80,.80,.60})
        })
            Run("远程武器主射击完整连接至携液弹丸 " + row.Name, () =>
            {
                var def = LoadDef(PrimitiveThings, row.Name);
                var verb = def.Element("verbs").Elements().Single();
                Equal("Verb_Shoot", verb.Element("verbClass").Value);
                Equal(1, (int)verb.Element("burstShotCount"));
                Near(row.Warmup, Number(verb.Element("warmupTime")));
                Near(row.Range, Number(verb.Element("range")));
                Equal(row.Projectile, verb.Element("defaultProjectile").Value);
                var projectile = LoadDef(PrimitiveThings, row.Projectile).Element("projectile");
                Equal(row.Damage, projectile.Element("damageDef").Value);
                Near(row.Power, Number(projectile.Element("damageAmountBase")));
                Near(row.Ap, Number(projectile.Element("armorPenetrationBase")));
                Assert(projectile.Element("extraDamages") == null, "同次物理命中内结算药效");
                var stats = def.Element("statBases");
                Near(row.Cooldown, Number(stats.Element("RangedWeapon_Cooldown")));
                var names = new[] { "AccuracyTouch", "AccuracyShort", "AccuracyMedium", "AccuracyLong" };
                for (int i=0;i<names.Length;i++) Near(row.Accuracy[i], Number(stats.Element(names[i])));
            });

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
            Run("XML 携液命中被阻挡不建立抗性或施药 " + row.Damage, () =>
            {
                var pawn = CoatedPawn(); pawn.health.capacities.Consciousness=3;
                var worker = CoatedWorker(row.Injury);
                worker.PhysicalApply = (info,target) => new DamageWorker.DamageResult { deflected=true };
                var shot = CoatedShot(PrimitiveExtension(row.Damage).baseLoad, row.Power);
                worker.Apply(shot, pawn);
                AssertNoCoatingDrug(pawn);
            });
        }

        Run("新武器不额外声明自动生成配方或改变原版能力", () =>
        {
            var defs = Directory.GetFiles(SscTestPaths.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
                .SelectMany(f => XDocument.Load(f).Root.Elements()).ToArray();
            foreach (var weapon in weapons)
                Assert(!defs.Any(d => (string)d.Element("defName") == "Make_"+weapon.Name), "避免配方重复注册");
            Equal(3, defs.Count(d => d.Name == "DamageDef" && ((string)d.Element("defName"))?.StartsWith("SSC_Damage_Pna") == true));
            Equal(1, defs.Count(d => d.Name == "ManeuverDef" && (string)d.Element("requiredCapacity") == "SSC_ToolCapacity_PnaStab"));
            Assert(!defs.Any(d => d.Name == "ManeuverDef" && (string)d.Element("requiredCapacity") == "Stab"), "不改原版刺击能力");
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Run("原始武器四语文本和死亡占位符 " + language, () =>
            {
                var groups = new[] { "ThingDef", "DamageDef", "ToolCapacityDef", "ResearchProjectDef" };
                foreach (string kind in groups)
                {
                    var data = XDocument.Load(SscTestPaths.Combine(root, $"Languages/{language}/DefInjected/{kind}/SSC_PnaPrimitiveWeapons.xml")).Root;
                    Assert(data.Elements().All(e => !string.IsNullOrWhiteSpace(e.Value)), "翻译不为空");
                    Equal(data.Elements().Count(), data.Elements().Select(e => e.Name).Distinct().Count());
                    Equal(kind == "ThingDef" ? 12 : kind == "DamageDef" ? 6 : kind == "ToolCapacityDef" ? 1 : 2,
                        data.Elements().Count());
                    foreach (var death in data.Elements().Where(e => e.Name.LocalName.EndsWith(".deathMessage")))
                        Equal(1, death.Value.Split("{0}").Length-1);
                    if (kind == "ThingDef")
                        foreach (var weapon in weapons)
                        {
                            Assert(data.Element(weapon.Name+".label") != null && data.Element(weapon.Name+".description") != null,
                                "武器标签及说明齐备");
                        }
                }
            });
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
