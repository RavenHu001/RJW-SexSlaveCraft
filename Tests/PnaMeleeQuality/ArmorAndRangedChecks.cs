// AI 辅助生成：正式 XML 的原生穿甲／信息卡及远程品质检查；属性基础输入隔离，原版品质与弹丸算法实际执行。
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

internal static partial class Program
{
    private static float externalMeleeFactor = 1f;
    private static readonly Dictionary<Thing, Dictionary<StatDef, float>> BaseStats = new();
    private static readonly List<object> RangedAudit = new();

    private static void RunArmorAndRangedChecks(string root, string gameRoot, Harmony harmony)
    {
        // 只隔离属性基础输入／缓存，FinalizeValue、品质组件、穿甲、加权信息卡及弹丸算法仍为真实游戏实现。
        harmony.Patch(AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue),
            new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(ReadStat)));
        harmony.Patch(AccessTools.Method(typeof(StatWorker), nameof(StatWorker.GetValue),
            new[] { typeof(Thing), typeof(bool), typeof(int) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(ReadNativeStat)));

        var rangedXml = XDocument.Load(Path.Combine(gameRoot, "Data/Core/Defs/Stats/Stats_Weapons_Ranged.xml")).Root;
        StatDefOf.RangedWeapon_DamageMultiplier = ReadStatDef(rangedXml.Elements().Single(d => (string)d.Element("defName") == "RangedWeapon_DamageMultiplier"));
        StatDefOf.RangedWeapon_ArmorPenetrationMultiplier = ReadStatDef(rangedXml.Elements().Single(d => (string)d.Element("defName") == "RangedWeapon_ArmorPenetrationMultiplier"));
        var accuracy = ReadStatDef(rangedXml.Elements().Single(d => (string)d.Attribute("Name") == "AccuracyBase"));
        var sharp = new DamageArmorCategoryDef { defName = "Sharp" };
        var damage = new DamageDef { defName = "TestQualityDamage", armorCategory = sharp };
        float[] meleeFactors = { .8f, .9f, 1f, 1.1f, 1.2f, 1.45f, 1.65f };
        foreach (var row in new[] {
            (File:"SSC_PnaPrimitiveWeapons.xml", Name:"SSC_Weapon_PnaSpikeStaff", Power:5f, Ap:.12f),
            (File:"SSC_PnaIndustrialWeapons.xml", Name:"SSC_Weapon_PnaShortWhip", Power:3f, Ap:.20f),
            (File:"SSC_PnaSpacerWeapons.xml", Name:"SSC_Weapon_PnaSilkWhip", Power:1f, Ap:.60f) })
        {
            var source = ReadThing(root, row.File, row.Name);
            foreach (QualityCategory quality in Enum.GetValues<QualityCategory>())
                Check($"原生品质伤害、穿甲和信息卡一致 {row.Name} {quality}", () =>
                {
                    var weapon = WeaponFromXml(source, quality, damage);
                    var tool = weapon.def.tools[0]; var verb = tool.Maneuvers.Single().verb;
                    Near(row.Power * meleeFactors[(int)quality], verb.AdjustedMeleeDamageAmount(tool, null, weapon, null));
                    Near(row.Ap * meleeFactors[(int)quality], verb.AdjustedArmorPenetration(tool, null, weapon, null));
                    // Verb 包装入口不能二次补品质。
                    var wrapper = new Verb_MeleeAttackDamage { verbProps = verb, tool = tool };
                    var equipment = new CompEquippable { parent = weapon }; wrapper.verbTracker = new VerbTracker(equipment);
                    Near(row.Ap * meleeFactors[(int)quality], verb.AdjustedArmorPenetration(wrapper, null));
                    var worker = new StatWorker_MeleeAverageArmorPenetration();
                    var pairs = VerbUtility.GetAllVerbProperties(weapon.def.Verbs, weapon.def.tools).Where(p => p.verbProps.IsMeleeAttack).ToArray();
                    float sum = 0f, weight = 0f;
                    foreach (var pair in pairs)
                    {
                        float w = pair.verbProps.AdjustedMeleeSelectionWeight(pair.tool, null, weapon, null, false);
                        sum += w * pair.verbProps.AdjustedArmorPenetration(pair.tool, null, weapon, null); weight += w;
                    }
                    Near(sum / weight, worker.GetValueUnfinalized(StatRequest.For(weapon)));
                    string explanation = worker.GetExplanationUnfinalized(StatRequest.For(weapon), ToStringNumberSense.Absolute);
                    if (!explanation.Contains((row.Ap * meleeFactors[(int)quality]).ToStringPercent())) throw new Exception("信息卡未显示工作端品质穿甲");
                    // 无实物的原版重载没有品质参数，定义卡应保留普通品质基线。
                    Near(row.Ap, verb.AdjustedArmorPenetration(tool, null, weapon.def, null, null));
                });
        }
        Check("外部倍率只保留一次且零穿甲不被凭空提高", () =>
        {
            var source = ReadThing(root, "SSC_PnaPrimitiveWeapons.xml", "SSC_Weapon_PnaSpikeStaff");
            var weapon = WeaponFromXml(source, QualityCategory.Legendary, damage); var tool = weapon.def.tools[0]; var verb = tool.Maneuvers.Single().verb;
            externalMeleeFactor = 1.25f;
            try
            {
                Near(5 * 1.25f * 1.65f, verb.AdjustedMeleeDamageAmount(tool, null, weapon, null));
                Near(.12f * 1.25f * 1.65f, verb.AdjustedArmorPenetration(tool, null, weapon, null));
                tool.armorPenetration = 0; Near(0, verb.AdjustedArmorPenetration(tool, null, weapon, null));
                tool.armorPenetration = -1; Near(5 * 1.25f * .015f * 1.65f, verb.AdjustedArmorPenetration(tool, null, weapon, null));
            }
            finally { externalMeleeFactor = 1; }
        });
        Check("缺失品质按普通且普通武器仍由原版处理", () =>
        {
            var def = MakeWeapon("OrdinaryQualityControl"); def.tools[0].armorPenetration = .2f;
            var weapon = WithQuality(def, QualityCategory.Legendary); var verb = new VerbProperties { verbClass = typeof(Verb_MeleeAttackDamage) };
            Near(.2f * 1.65f, verb.AdjustedArmorPenetration(def.tools[0], null, weapon, null));
            def = MakeWeapon("PnaWithoutQuality"); def.tools[0].armorPenetration = .2f;
            weapon = new ThingWithComps { def = def }; BaseStats[weapon] = new();
            Near(.2f, verb.AdjustedArmorPenetration(def.tools[0], null, weapon, null));
        });
        Check("品质曲线从实际StatDef读取，不写死七档常量", () =>
        {
            var part = StatDefOf.MeleeWeapon_DamageMultiplier.parts.OfType<StatPart_Quality>().Single();
            var field = AccessTools.Field(typeof(StatPart_Quality), "factorLegendary"); var saved = field.GetValue(part);
            try
            {
                field.SetValue(part, 1.8f);
                var weapon = WeaponFromXml(ReadThing(root, "SSC_PnaPrimitiveWeapons.xml", "SSC_Weapon_PnaSpikeStaff"), QualityCategory.Legendary, damage);
                var tool = weapon.def.tools[0]; var verb = tool.Maneuvers.Single().verb;
                Near(5 * 1.8f, verb.AdjustedMeleeDamageAmount(tool, null, weapon, null));
                Near(.12f * 1.8f, verb.AdjustedArmorPenetration(tool, null, weapon, null));
            }
            finally { field.SetValue(part, saved); }
        });

        // 六把现役远程武器：原版取整和穿甲系数实际执行；仅审计，不改变生产远程规则。
        float[] rangedFactors = { .9f, 1f, 1f, 1f, 1f, 1.25f, 1.5f };
        float[] accuracyFactors = { .8f, .9f, 1f, 1.1f, 1.2f, 1.35f, 1.5f };
        foreach (var row in new[] {
            (File:"SSC_PnaPrimitiveWeapons.xml", Name:"SSC_Weapon_PnaBlowpipe"),
            (File:"SSC_PnaPrimitiveWeapons.xml", Name:"SSC_Weapon_PnaLightCrossbow"),
            (File:"SSC_PnaIndustrialWeapons.xml", Name:"SSC_Weapon_PnaNeedleRepeater"),
            (File:"SSC_PnaIndustrialWeapons.xml", Name:"SSC_Weapon_PnaNeedleRifle"),
            (File:"SSC_PnaSpacerWeapons.xml", Name:"SSC_Weapon_PnaPulseGun"),
            (File:"SSC_PnaSpacerWeapons.xml", Name:"SSC_Weapon_PnaProjectorGun") })
        {
            var source = ReadThing(root, row.File, row.Name);
            var projectile = ReadThing(root, row.File, source.Element("verbs").Elements().Single().Element("defaultProjectile").Value).Element("projectile");
            int baseDamage = (int)projectile.Element("damageAmountBase"); float baseAp = Number(projectile.Element("armorPenetrationBase"));
            var prop = new ProjectileProperties { damageDef = damage };
            AccessTools.Field(typeof(ProjectileProperties), "damageAmountBase").SetValue(prop, baseDamage);
            AccessTools.Field(typeof(ProjectileProperties), "armorPenetrationBase").SetValue(prop, baseAp);
            foreach (QualityCategory quality in Enum.GetValues<QualityCategory>())
                Check($"远程原版伤害穿甲精度及普通防身品质 {row.Name} {quality}", () =>
                {
                    var def = new ThingDef { defName = row.Name, category = ThingCategory.Item, equipmentType = EquipmentType.Primary, tools = new List<Tool> { new Tool { power = 9 } } };
                    AccessTools.Field(typeof(ThingDef), "verbs").SetValue(def, new List<VerbProperties> { new VerbProperties { verbClass = typeof(Verb_Shoot) } });
                    var weapon = WithQuality(def, quality); float factor = rangedFactors[(int)quality];
                    int actualDamage = prop.GetDamageAmount(weapon); float actualAp = prop.GetArmorPenetration(weapon);
                    Near(UnityEngine.Mathf.RoundToInt(baseDamage * factor), actualDamage); Near(baseAp * factor, actualAp);
                    Near(9 * meleeFactors[(int)quality], def.tools[0].AdjustedBaseMeleeDamageAmount(weapon, damage));
                    var accuracyValues = new List<float>();
                    foreach (string key in new[] { "AccuracyTouch", "AccuracyShort", "AccuracyMedium", "AccuracyLong" })
                    {
                        float value = Number(source.Element("statBases").Element(key));
                        var req = StatRequest.For(weapon); accuracy.Worker.FinalizeValue(req, ref value, true);
                        Near(Math.Clamp(Number(source.Element("statBases").Element(key)) * accuracyFactors[(int)quality], .01f, 1f), value);
                        accuracyValues.Add(value);
                    }
                    RangedAudit.Add(new { weapon = row.Name, quality = quality.ToString(), damage = actualDamage, armorPenetration = actualAp, accuracy = accuracyValues });
                });
        }
        var output = Path.Combine(root, ".builds/pna-weapons/weapon-quality-native"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "ranged-quality.json"), JsonSerializer.Serialize(RangedAudit, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static XElement ReadThing(string root, string file, string name) => XDocument.Load(Path.Combine(root, "1.7/Defs/ThingsDefs", file)).Root.Elements().Single(d => (string)d.Element("defName") == name);
    private static float Number(XElement node) => float.Parse(node.Value, CultureInfo.InvariantCulture);

    private static StatDef ReadStatDef(XElement source)
    {
        var def = new StatDef { defName = (string)source.Element("defName") ?? "TestAccuracy", defaultBaseValue = 1, workerClass = typeof(StatWorker), minValue = 0, maxValue = float.MaxValue };
        if (source.Element("minValue") is { } min) def.minValue = Number(min);
        if (source.Element("maxValue") is { } max) def.maxValue = Number(max);
        def.parts = new List<StatPart>();
        foreach (var xml in source.Element("parts").Elements())
        {
            var part = new StatPart_Quality { parentStat = def };
            foreach (var e in xml.Elements()) AccessTools.Field(typeof(StatPart_Quality), e.Name.LocalName).SetValue(part, Number(e));
            def.parts.Add(part);
        }
        return def;
    }

    private static ThingWithComps WithQuality(ThingDef def, QualityCategory quality)
    {
        var weapon = new ThingWithComps { def = def };
        var comp = new CompQuality { parent = weapon }; AccessTools.Field(typeof(CompQuality), "qualityInt").SetValue(comp, quality);
        AccessTools.Field(typeof(ThingWithComps), "compQuality").SetValue(weapon, comp); BaseStats[weapon] = new(); return weapon;
    }

    private static ThingWithComps WeaponFromXml(XElement xml, QualityCategory quality, DamageDef damage)
    {
        var def = MakeWeapon(xml.Element("defName").Value); def.tools.Clear();
        foreach (var item in xml.Element("tools").Elements())
        {
            var cap = new ToolCapacityDef { defName = item.Element("capacities").Elements().First().Value, label = "contact" };
            var props = new VerbProperties { verbClass = typeof(Verb_MeleeAttackDamage), meleeDamageDef = damage };
            DefDatabase<ManeuverDef>.AllDefsListForReading.Add(new ManeuverDef { defName = def.defName + def.tools.Count, requiredCapacity = cap, verb = props });
            def.tools.Add(new Tool { label = item.Element("label").Value, capacities = new List<ToolCapacityDef> { cap }, power = Number(item.Element("power")), armorPenetration = Number(item.Element("armorPenetration")), cooldownTime = Number(item.Element("cooldownTime")), chanceFactor = item.Element("chanceFactor") is { } chance ? Number(chance) : 1 });
        }
        return WithQuality(def, quality);
    }

    private static bool ReadStat(Thing thing, StatDef stat, ref float __result)
    {
        if (!BaseStats.ContainsKey(thing)) return true;
        __result = stat == StatDefOf.MeleeWeapon_DamageMultiplier ? externalMeleeFactor : stat.defaultBaseValue;
        stat.Worker.FinalizeValue(StatRequest.For(thing), ref __result, true); return false;
    }
    private static bool ReadNativeStat(Thing thing, StatDef ___stat, ref float __result) => ReadStat(thing, ___stat, ref __result);
}
