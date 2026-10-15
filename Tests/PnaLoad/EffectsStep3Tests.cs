using System;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 直接运行生产伤害工人和现有健康补丁；游戏替身只提供部位回调及显示边界。
    private static void RunEffectsStep3Tests()
    {
        Run("第三步有链三部位只活化一次但保留每部位治疗且无伤", () =>
        {
            var p = WeaponSlave(); var injury = WeaponInjury(p); var shot = Shot(); shot.Parts = 3;
            var worker = new DamageWorker_LowTier(); worker.Apply(shot, p);
            Near(.075, SSCPnaExposureUtility.GetLoad(p)); Near(15, injury.Healed); Equal(0, worker.InjuryCalls);
            Equal(1, MoteMaker.Sent.Count(t => t == "SSC_Pna_WeaponActivated"));
            Equal(3, MoteMaker.Sent.Count(t => t == "SSC_Pna_WeaponHealed"));
            AssertNoLegacyBuff(p); Assert(Record(p) == null && SSCPnaExposureUtility.GetAddiction(p) == null && p.needs.PnaNeed == null, "只有性奴负荷");
        });
        Run("第三步健康性奴仍有活化反馈且不虚报治疗", () =>
        {
            var p = WeaponSlave(); var worker = new DamageWorker_LowTier(); worker.Apply(Shot(), p);
            Equal(1, MoteMaker.Sent.Count); Equal("SSC_Pna_WeaponActivated", MoteMaker.Sent.Single()); Equal(0, worker.InjuryCalls);
        });
        Run("第三步有链同tick两次命中分别活化", () =>
        {
            var p = WeaponSlave(); var worker = new DamageWorker_LowTier(); worker.Apply(Shot(), p); worker.Apply(Shot(), p);
            Near(.15, SSCPnaExposureUtility.GetLoad(p)); Equal(2, MoteMaker.Sent.Count); Equal(0, worker.InjuryCalls);
        });
        Run("第三步满值性奴继续命中不增加成瘾仍保留反馈", () =>
        {
            var p = WeaponSlave(); Input(p, 1); var worker = new DamageWorker_LowTier(); worker.Apply(Shot(), p);
            Near(1, SSCPnaExposureUtility.GetLoad(p)); Assert(SSCPnaExposureUtility.GetAddiction(p) == null, "无成瘾");
            Equal(1, MoteMaker.Sent.Count); Equal(0, worker.InjuryCalls);
        });
        Run("第三步性奴剂量使用活化治疗前意识且快照继续复用", () =>
        {
            var p = WeaponSlave(); p.health.capacities.Consciousness = 3;
            WeaponInjury(p).OnHeal = () => p.health.capacities.Consciousness = 5;
            var shot = Shot(); var worker = new DamageWorker_LowTier(); worker.Apply(shot, p); worker.Apply(shot, p);
            Near(.05, SSCPnaExposureUtility.GetLoad(p)); Equal(0, worker.InjuryCalls);
        });
        Run("第三步完全偏转不活化不治疗不飘字", () =>
        {
            var p = WeaponSlave(); var injury = WeaponInjury(p); var shot = Shot(); shot.Deflected = true;
            var worker = new DamageWorker_LowTier(); worker.Apply(shot, p);
            Near(0, injury.Healed); Near(0, SSCPnaExposureUtility.GetLoad(p)); Equal(0, MoteMaker.Sent.Count); Equal(0, worker.InjuryCalls);
        });
        Run("第三步失效残链仍走普通伤害且没有性奴飘字", () =>
        {
            var p = NewPawn(); p.Map = new Map(); p.Spawned = true;
            p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { pawn = p, def = ChainDef });
            var worker = new DamageWorker_LowTier(); worker.Apply(Shot(), p);
            Near(.075, SSCPnaExposureUtility.GetLoad(p)); Assert(Record(p) != null, "普通载体"); Equal(1, worker.InjuryCalls); Equal(0, MoteMaker.Sent.Count);
        });
        Run("第三步主人有链保留治疗无伤但不活化", () =>
        {
            var p = WeaponSlave(); p.IsMaster = true; p.health.capacities.Consciousness = 3; var injury = WeaponInjury(p);
            var shot = Shot(); ((PNA_DWExtension)shot.Def.Extension).recordHediff.defName = "Hediff_PNA_Consciousness_Record";
            var worker = new DamageWorker_LowTier(); worker.Apply(shot, p);
            Near(5, injury.Healed); Equal(0, worker.InjuryCalls); AssertNoMasterDrugState(p);
            Equal("SSC_Pna_WeaponHealed", MoteMaker.Sent.Single());
        });
        Run("第三步无链主人仍承受原伤害且清理遗留药效", () =>
        {
            var p = NewPawn(); Raw(p, .8f); p.IsMaster = true; var worker = new DamageWorker_LowTier(); worker.Apply(Shot(), p);
            Equal(1, worker.InjuryCalls); AssertNoMasterDrugState(p); Equal(0, MoteMaker.Sent.Count);
        });
        Run("第三步负荷回调解绑不向受保护命中追加枪伤或活化提示", () =>
        {
            var p = WeaponSlave(); var chain = p.health.hediffSet.hediffs.OfType<Hediff_ChainOfSexSlave>().Single(); var injury = WeaponInjury(p);
            p.health.OnAdded = h => { if (SSCPnaExposureUtility.IsBodyLoadDef(h.def)) p.health.RemoveHediff(chain); };
            var shot = Shot(); shot.Parts = 3;
            var worker = new DamageWorker_LowTier(); worker.Apply(shot, p);
            Assert(Record(p) != null && !SSCPnaExposureUtility.IsRooted(p), "回调转普通");
            Near(0, injury.Healed); Equal(0, worker.InjuryCalls); Equal(0, MoteMaker.Sent.Count);
        });
        Run("第三步嵌套命中保护不串到其他身体且命中结束后释放", () =>
        {
            var outer = WeaponSlave(); var inner = NewPawn(); var worker = new DamageWorker_LowTier();
            worker.BeforePart = p => { worker.BeforePart = null; worker.Apply(Shot(), inner); };
            worker.Apply(Shot(), outer); Equal(1, worker.InjuryCalls);
            Near(.075, SSCPnaExposureUtility.GetLoad(outer)); Near(.075, SSCPnaExposureUtility.GetLoad(inner));
            worker.Apply(Shot(), inner); Equal(2, worker.InjuryCalls); Near(.15, SSCPnaExposureUtility.GetLoad(inner));
        });
        Run("第三步输入回调新建有效链立即阻止普通枪伤", () =>
        {
            var p = NewPawn(); p.health.OnAdded = h => { if (SSCPnaExposureUtility.IsBodyLoadDef(h.def)) Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn()); };
            var worker = new DamageWorker_LowTier(); worker.Apply(Shot(), p);
            Assert(RootedRecord(p) != null, "回调转性奴"); Equal(0, worker.InjuryCalls);
        });
        Run("第三步治疗回调成为主人不显示活化且仍清药无伤", () =>
        {
            var p = WeaponSlave(); WeaponInjury(p).OnHeal = () => { p.IsMaster = true; SSCPnaExposureUtility.RefreshBodyState(p); };
            var worker = new DamageWorker_LowTier(); worker.Apply(Shot(), p);
            AssertNoMasterDrugState(p); Equal(0, worker.InjuryCalls); Equal("SSC_Pna_WeaponHealed", MoteMaker.Sent.Single());
        });
        Run("第三步高阶兼容入口也输入性奴负荷且不再发旧强化", () =>
        {
            var p = WeaponSlave(); var injury = WeaponInjury(p); var shot = Shot(); shot.Parts = 2;
            ((PNA_DWExtension)shot.Def.Extension).fixedSeverity = .5f;
            var worker = new DamageWorker_HighTier(); worker.Apply(shot, p);
            Near(.25, SSCPnaExposureUtility.GetLoad(p)); Near(10, injury.Healed); Equal(0, worker.InjuryCalls); AssertNoLegacyBuff(p);
            Equal("SSC_Pna_WeaponActivated", MoteMaker.Sent.Single());
        });
        Run("第三步离图输入不创建地图飘字", () =>
        {
            var p = WeaponSlave(); p.Spawned = false; p.Map = null; var injury = WeaponInjury(p);
            new DamageWorker_LowTier().Apply(Shot(), p); Near(.075, SSCPnaExposureUtility.GetLoad(p)); Near(5, injury.Healed); Equal(0, MoteMaker.Sent.Count);
        });
        // 从存档恢复的旧实例绕过添加补丁，读档收尾清理，不把旧严重度折算成新负荷。
        foreach (bool rooted in new[] { false, true }) Run("第三步只有旧强化的身体读档清理 " + rooted, () =>
        {
            var p = rooted ? WeaponSlave() : NewPawn();
            foreach (string name in new[] { "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus" }) AddBodyState(p, name, .7f);
            AddBodyState(p, "SSC_Purple_HyperLactation", .6f); AddBodyState(p, "SSC_Hediff_RabbitCloneLowPNA", .4f);
            SSCPnaGameComponent.NormalizePawn(p); SSCPnaGameComponent.NormalizePawn(p);
            AssertNoLegacyBuff(p); Assert(SSCPnaExposureUtility.GetBodyRecord(p) == null, "不转换成负荷");
            Equal(rooted ? 3 : 2, p.health.hediffSet.hediffs.Count); Near(.6, p.health.hediffSet.hediffs.Single(h => h.def.defName == "SSC_Purple_HyperLactation").Severity);
        });
        Run("第三步旧强化清理保留当前合法负荷成瘾满足度与半批时间", () =>
        {
            var p = NewPawn(); Input(p, .4f); AddOldAddiction(p, .35f); p.needs.PnaNeed = .27f; Tick(p, 50); var load = Record(p);
            foreach (string name in new[] { "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus" }) AddBodyState(p, name, .7f);
            SSCPnaGameComponent.NormalizePawn(p); AssertNoLegacyBuff(p); Assert(Record(p) == load, "保留载体");
            Near(.4, SSCPnaExposureUtility.GetLoad(p)); Near(.35, SSCPnaExposureUtility.GetAddiction(p).Severity); Near(.27, p.needs.PnaNeed.Value); Equal(50, BodyComp(p).PendingTicks);
        });
        Run("第三步移除旧强化的回调绑定按最终锁链协调", () =>
        {
            var p = NewPawn(); Input(p, .4f); AddOldAddiction(p, .35f); AddBodyState(p, "Hediff_PNA_Combat_Buff", .7f);
            p.health.OnRemoved = h => { if (SSCPnaExposureUtility.IsLegacyCombatBuffDef(h.def)) Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn()); };
            SSCPnaExposureUtility.RefreshBodyState(p); AssertNoLegacyBuff(p); Assert(RootedRecord(p) != null, "最终性奴载体");
            Assert(SSCPnaExposureUtility.GetAddiction(p) == null && p.needs.PnaNeed == null, "清除成瘾需求"); Near(.4, SSCPnaExposureUtility.GetLoad(p));
        });
        foreach (bool rooted in new[] { false, true }) Run("第三步直接添加两类旧强化均被现有补丁拒绝 " + rooted, () =>
        {
            var p = rooted ? WeaponSlave() : NewPawn();
            foreach (string name in new[] { "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus" }) p.health.AddHediff(new HediffDef { defName = name });
            AssertNoLegacyBuff(p); Assert(SSCPnaExposureUtility.GetBodyRecord(p) == null, "不创建药效");
        });
        Run("第三步枪械XML停用旧强化引用但保留兼容定义和高阶禁用", () =>
        {
            var defs = XDocument.Load(System.IO.Path.Combine(root, "Defs/DemageDefs/DemageDef_Gun.xml"));
            Assert(!defs.Descendants("combatBuff").Any(), "不再配置旧强化");
            foreach (string name in new[] { "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus" }) LoadDef("Defs/HediffDefs/HediffOfBuff.xml", name);
            var guns = XDocument.Load(System.IO.Path.Combine(root, "Defs/ThingsDefs/SSC_Gun.xml"));
            Assert(!guns.Root.Elements().Any(d => (string)d.Element("defName") == "Gun_PNA_High" || (string)d.Element("defName") == "Bullet_PNA_High"), "高阶未开放");
            foreach (string lang in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                string relative = "DefInjected/ThingDef/SSC_Gun.xml";
                string source = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Languages", lang, relative));
                Equal(source, System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Sexslavecraft/Languages", lang, relative)), "枪械镜像");
                Assert(!string.IsNullOrWhiteSpace(XDocument.Parse(source).Root.Element("Gun_PNA_Low.description")?.Value), "四语枪械说明");
            }
        });
    }

    private static Pawn WeaponSlave()
    {
        var p = NewPawn(); p.Spawned = true; p.Map = new Map(); Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn()); return p;
    }
    private static Hediff_Injury WeaponInjury(Pawn p)
    {
        var injury = new Hediff_Injury { pawn = p, def = new HediffDef { defName = "Injury" } }; p.health.hediffSet.hediffs.Add(injury); return injury;
    }
    private static void AssertNoLegacyBuff(Pawn p) => Assert(!p.health.hediffSet.hediffs.Any(h => SSCPnaExposureUtility.IsLegacyCombatBuffDef(h.def)), "无旧强化");
}
