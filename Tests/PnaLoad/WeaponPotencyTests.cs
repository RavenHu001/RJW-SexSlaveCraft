// AI 辅助生成：执行生产效力解析／StatWorker；原版 UI 绘制仍需 1.7 实机验收。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunWeaponPotencyTests()
    {
        foreach (var row in new[] { ("SSC_Weapon_PnaSpikeStaff", .15f), ("SSC_Weapon_PnaBlowpipe", .06f),
            ("SSC_Weapon_PnaLightCrossbow", .10f) })
            Run("效力从实际 XML 接线读取 " + row.Item1, () =>
            {
                var weapon = PotencyWeaponFromXml(row.Item1);
                var worker = new StatWorker_SSCPnaWeaponPotency();
                var request = StatRequest.For(weapon);
                Assert(worker.ShouldShowFor(request), "定义信息卡可见");
                Near(row.Item2, worker.GetValueUnfinalized(request));
                var instance = StatRequest.For(new Thing { def = weapon });
                Near(row.Item2, worker.GetValueUnfinalized(instance));
                float value = worker.GetValueUnfinalized(instance);
                worker.FinalizeValue(instance, ref value, true);
                Near(row.Item2, value);
                string explanation = worker.GetExplanationUnfinalized(instance, ToStringNumberSense.Absolute);
                Assert(explanation.Contains(row.Item2.ToStringPercent()), "报告包含当前基线");
                Equal(string.Empty, worker.GetExplanationFinalizePart(instance, ToStringNumberSense.Absolute, value));
                Assert(LoadDef(PrimitiveThings, row.Item1).Element("statBases").Element("SSC_PnaWeaponPotency") == null,
                    "没有复制一份 UI 剂量");
            });

        Run("效力随实际扩展修改更新，不缓存旧值", () =>
        {
            var weapon = PotencyWeaponFromXml("SSC_Weapon_PnaBlowpipe");
            var worker = new StatWorker_SSCPnaWeaponPotency();
            var extension = weapon.verbs[0].defaultProjectile.projectile.damageDef.GetModExtension<SSCPnaCoatingExtension>();
            Near(.06, worker.GetValueUnfinalized(StatRequest.For(weapon)));
            extension.baseLoad = .12f;
            Near(.12, worker.GetValueUnfinalized(StatRequest.For(weapon)));
        });
        Run("效力短矛排除柄击，只有柄击时隐藏", () =>
        {
            var weapon = PotencyWeaponFromXml("SSC_Weapon_PnaSpikeStaff");
            Near(.15, Potency(weapon));
            weapon.tools.RemoveAt(0);
            Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(weapon, out _), "不能从普通 Blunt 猜测携液");
        });
        Run("效力继承原生显示开关", () =>
        {
            var weapon = PotencyWeaponFromXml("SSC_Weapon_PnaLightCrossbow");
            var worker = new StatWorker_SSCPnaWeaponPotency { NativeVisibility = false };
            Assert(!worker.ShouldShowFor(StatRequest.For(weapon)), "原生隐藏优先");
        });
        Run("效力空定义、普通武器、投射物和非武器隐藏", () =>
        {
            var worker = new StatWorker_SSCPnaWeaponPotency();
            foreach (var def in new[] { null, new ThingDef(), new ThingDef { IsRangedWeapon = true },
                new ThingDef { projectile = new ProjectileProperties { damageDef = PotencyDamage(.15f) } } })
            {
                var req = StatRequest.For(def);
                Assert(!worker.ShouldShowFor(req), "无有效武器入口");
                Near(0, worker.GetValueUnfinalized(req));
                Equal(string.Empty, worker.GetExplanationUnfinalized(req, ToStringNumberSense.Absolute));
            }
        });
        Run("效力其他处理器不误用涂覆说明", () =>
        {
            foreach (var type in new[] { typeof(DamageWorker_AddInjury) })
            {
                var damage = PotencyDamage(.15f); damage.workerClass = type;
                Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(PotencyRanged(damage), out _), "限定涂覆处理器");
            }
        });
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Run("效力拒绝损坏药量 " + invalid, () =>
                Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(PotencyRanged(PotencyDamage(invalid)), out _), "无误导值"));
        Run("效力缺少扩展、抗性定义或投射物时隐藏", () =>
        {
            var damage = PotencyDamage(.15f);
            damage.GetModExtension<SSCPnaCoatingExtension>().recordHediff = null;
            Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(PotencyRanged(damage), out _), "无抗性定义");
            damage.Extension = null;
            Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(PotencyRanged(damage), out _), "无扩展");
            var weapon = PotencyRanged(damage); weapon.verbs[0].defaultProjectile = null;
            Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(weapon, out _), "无投射物");
        });
        Run("效力复用真实剂量上限", () => Near(.5, Potency(PotencyRanged(PotencyDamage(float.MaxValue)))));
        Run("效力相同多入口不相加，不同药量不随意取第一个", () =>
        {
            var weapon = PotencyRanged(PotencyDamage(.06f));
            weapon.verbs.Add(PotencyRanged(PotencyDamage(.06f)).verbs[0]);
            Near(.06, Potency(weapon));
            weapon.verbs.Add(PotencyRanged(PotencyDamage(.10f)).verbs[0]);
            Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(weapon, out _), "冲突不伪装统一基线");
        });
        Run("效力查询和报告不会读取或改变身体状态", () =>
        {
            var weapon = PotencyWeaponFromXml("SSC_Weapon_PnaSpikeStaff");
            var pawn = CoatedPawn();
            Raw(pawn, .8f);
            pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, def = CoatingResistance, RawSeverity = 9f });
            pawn.health.capacities.Consciousness = .1f;
            pawn.IsMaster = true;
            Find.Selector.SingleSelectedThing = pawn;
            var before = pawn.health.hediffSet.hediffs.Select(h => (h, h.Severity)).ToArray();
            int notifications = pawn.health.Notifications;
            int messages = Messages.Sent.Count, motes = MoteMaker.Sent.Count;
            Scribe.mode = LoadSaveMode.LoadingVars;
            var worker = new StatWorker_SSCPnaWeaponPotency();
            for (int i = 0; i < 50; i++)
            {
                var req = StatRequest.For(weapon);
                Assert(worker.ShouldShowFor(req), "不依赖存读档或所选目标");
                Near(.15, worker.GetValueUnfinalized(req));
                worker.GetExplanationUnfinalized(req, ToStringNumberSense.Absolute);
            }
            Equal(before.Length, pawn.health.hediffSet.hediffs.Count);
            foreach (var entry in before)
            {
                Assert(pawn.health.hediffSet.hediffs.Contains(entry.h), "健康实例保留");
                Near(entry.Severity, entry.h.Severity);
            }
            Equal(notifications, pawn.health.Notifications);
            Equal(messages, Messages.Sent.Count); Equal(motes, MoteMaker.Sent.Count);
            Find.Selector.SingleSelectedThing = null;
        });
    }

    private static float Potency(ThingDef weapon)
    {
        Assert(SSCPnaWeaponPotencyUtility.TryGetBaseline(weapon, out float value), "应识别携液武器");
        return value;
    }

    private static DamageDef PotencyDamage(float amount) => new DamageDef
    {
        workerClass = typeof(DamageWorker_SSCPnaCoated),
        Extension = new SSCPnaCoatingExtension { baseLoad = amount, recordHediff = CoatingResistance }
    };

    private static ThingDef PotencyRanged(DamageDef damage) => new ThingDef
    {
        IsRangedWeapon = true,
        verbs = new List<VerbProperties> { new VerbProperties { defaultProjectile = new ThingDef
            { projectile = new ProjectileProperties { damageDef = damage } } } }
    };

    // 只把本批 XML 引用转成宿主边界对象；不冒充完整原版 XML 继承或补丁加载器。
    private static ThingDef PotencyWeaponFromXml(string name)
    {
        var xml = LoadDef(PrimitiveThings, name);
        var capacities = new Dictionary<string, ToolCapacityDef>();
        ToolCapacityDef Capacity(string id)
        {
            if (!capacities.TryGetValue(id, out var value)) capacities[id] = value = new ToolCapacityDef();
            return value;
        }
        var maneuvers = XDocument.Load(SscTestPaths.Combine(root, PrimitiveManeuvers)).Root.Elements("ManeuverDef")
            .Select(m => new ManeuverDef
            {
                requiredCapacity = Capacity(m.Element("requiredCapacity").Value),
                verb = new VerbProperties { meleeDamageDef = new DamageDef
                {
                    workerClass = typeof(DamageWorker_SSCPnaCoated),
                    Extension = PrimitiveExtension(m.Element("verb").Element("meleeDamageDef").Value)
                } }
            }).ToList();
        maneuvers.Add(new ManeuverDef { requiredCapacity = Capacity("Blunt"),
            verb = new VerbProperties { meleeDamageDef = new DamageDef() } });
        DefDatabase<ManeuverDef>.AllDefs = maneuvers;
        return new ThingDef
        {
            defName = name, IsRangedWeapon = xml.Element("verbs") != null, IsMeleeWeapon = xml.Element("verbs") == null,
            tools = xml.Element("tools").Elements().Select(t => new Tool
                { capacities = t.Element("capacities").Elements().Select(c => Capacity(c.Value)).ToList() }).ToList(),
            verbs = xml.Element("verbs")?.Elements().Select(v => new VerbProperties
            {
                defaultProjectile = new ThingDef { projectile = new ProjectileProperties { damageDef = new DamageDef
                {
                    workerClass = typeof(DamageWorker_SSCPnaCoated),
                    Extension = PrimitiveExtension(LoadDef(PrimitiveThings, v.Element("defaultProjectile").Value)
                        .Element("projectile").Element("damageDef").Value)
                } } }
            }).ToList()
        };
    }
}
