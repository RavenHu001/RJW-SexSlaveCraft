using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunRefinementTests()
    {
        Run("信息栏去除旧概率等条目并保留原版有效条目", () =>
        {
            var def = InfoDrug(.4f);
            def.NativeInfoEntries.Add(InfoEntry(StatCategoryDefOf.DrugAddiction, "Addictiveness"));
            def.NativeInfoEntries.Add(InfoEntry(StatCategoryDefOf.DrugAddiction, "MinimumToleranceForAddiction"));
            def.NativeInfoEntries.Add(InfoEntry(StatCategoryDefOf.Drug, "SafeDoseInterval"));
            var recovery = InfoEntry(StatCategoryDefOf.DrugAddiction, "AddictionRecoveryTime");
            var refill = InfoEntry(StatCategoryDefOf.DrugAddiction, "AddictionNeedOffset");
            var mass = InfoEntry(StatCategoryDefOf.Basics, "Mass");
            def.NativeInfoEntries.AddRange(new[] { recovery, refill, mass });
            var entries = def.SpecialDisplayStats(default).ToList();
            Equal(5, entries.Count); Assert(entries.Contains(recovery) && entries.Contains(refill) && entries.Contains(mass), "原条目实例保留");
            Equal("20%", entries.Single(e => e.LabelCap == "SSC_Pna_InfoLoadPerDose").ValueString);
            Equal("100%", entries.Single(e => e.LabelCap == "SSC_Pna_InfoAddictionThreshold").ValueString);
            Assert(entries.Last().Report.Contains("97%") && entries.Last().Report.Contains("20%"), "末期规则取核心常量");
        });
        Run("信息栏每次读取当前剂量配置且不按物品名硬编码", () =>
        {
            var def = InfoDrug(.6f); def.defName = "ExternalPnaVariant";
            Equal("30%", def.SpecialDisplayStats(default).First().ValueString);
            ((IngestionOutcomeDoer_SSCPna)def.ingestible.outcomeDoers.Single()).severity = 1.4f;
            Equal("70%", def.SpecialDisplayStats(default).First().ValueString);
        });
        Run("信息栏缺少专用摄入时交还原版展示", () =>
        {
            var def = new ThingDef_SSCPna { ingestible = null };
            var original = InfoEntry(StatCategoryDefOf.DrugAddiction, "Addictiveness"); def.NativeInfoEntries.Add(original);
            Equal(original, def.SpecialDisplayStats(default).Single());
        });
        Run("信息栏过滤同时限定条目类别和非StatDef身份", () =>
        {
            var def = InfoDrug(.4f);
            var otherCategory = InfoEntry(StatCategoryDefOf.Basics, "Addictiveness");
            var actualStat = InfoEntry(StatCategoryDefOf.DrugAddiction, "Addictiveness"); actualStat.stat = new object();
            def.NativeInfoEntries.AddRange(new[] { otherCategory, actualStat });
            var entries = def.SpecialDisplayStats(default).ToList(); Assert(entries.Contains(otherCategory) && entries.Contains(actualStat), "不误删同名有效属性");
        });
        Run("枪击非末期加深0.01且多部位只计算一次", () =>
        {
            var pawn = PnaAddict(.2f); var worker = new DamageWorker_LowTier(); var shot = Shot(); shot.Parts = 3;
            worker.Apply(shot, pawn); Near(.71, SSCPnaExposureUtility.GetAddiction(pawn).Severity); Near(.075, SSCPnaExposureUtility.GetLoad(pawn));
            worker.Apply(shot, pawn); Near(.72, SSCPnaExposureUtility.GetAddiction(pawn).Severity); Near(.15, SSCPnaExposureUtility.GetLoad(pawn));
        });
        Run("枪击基础及末期合计0.21且只写一次", () =>
        {
            var pawn = NewPawn(); Raw(pawn, 1.8f);
            var addiction = new SupplyAddiction { pawn = pawn, def = AddictionDef, Severity = .4f };
            pawn.health.hediffSet.hediffs.Add(addiction); addiction.Writes = 0;
            var shot = Shot(); shot.Parts = 3; new DamageWorker_LowTier().Apply(shot, pawn);
            Near(.61, addiction.Severity); Equal(1, addiction.Writes); Near(.975, SSCPnaExposureUtility.GetLoad(pawn));
        });
        Run("枪击未成瘾且未满负荷时不提前建立", () =>
        {
            var pawn = NewPawn(); new DamageWorker_LowTier().Apply(Shot(), pawn);
            Assert(SSCPnaExposureUtility.GetAddiction(pawn) == null && SSCPnaExposureUtility.GetNeed(pawn) == null, "未满无成瘾需求");
        });
        Run("枪击首次满负荷不对本次新成瘾追加0.21", () =>
        {
            var pawn = NewPawn(); Raw(pawn, 1.9f);
            pawn.health.OnAdded = h => { if (SSCPnaExposureUtility.IsAddictionDef(h.def)) h.Severity = .35f; };
            new DamageWorker_LowTier().Apply(Shot(), pawn); Near(.35, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
        });
        Run("满负荷枪击仍强化已有成瘾并沿原版上限钳制", () =>
        {
            var pawn = PnaAddict(.2f); Raw(pawn, 2f); var worker = new DamageWorker_LowTier();
            worker.Apply(Shot(), pawn); Near(.91, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
            worker.Apply(Shot(), pawn); Near(1, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
        });
        Run("无有效输入的偏转命中不加深成瘾", () =>
        {
            var pawn = PnaAddict(.2f); var shot = Shot(); shot.Deflected = true;
            new DamageWorker_LowTier().Apply(shot, pawn); Near(.7, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
        });
        foreach (bool master in new[] { false, true })
            Run("枪击基础加深回调后继续核对保护 master=" + master, () =>
            {
                var pawn = NewPawn(); var addiction = new SupplyAddiction { pawn = pawn, def = AddictionDef, Severity = .4f };
                pawn.health.hediffSet.hediffs.Add(addiction);
                addiction.OnWrite = () => { if (master) pawn.IsMaster = true; else Hediff_ChainOfSexSlave.AddToPawn(pawn, NewPawn()); };
                new DamageWorker_LowTier().Apply(Shot(), pawn);
                Assert(SSCPnaExposureUtility.GetAddiction(pawn) == null && SSCPnaExposureUtility.GetNeed(pawn) == null, "清成瘾及需求");
                Assert(master ? SSCPnaExposureUtility.GetBodyRecord(pawn) == null : RootedRecord(pawn) != null, "最终药效保护");
            });
        Run("药物定义使用局部展示类且研究门槛引用现有SSC项目", () =>
        {
            foreach (string name in new[] { "Purple_Aphrodisiac_Nanofluid", "Purple_Aphrodisiac_Nanofluid_Plus" })
                Equal("SexSlaveCraft.ThingDef_SSCPna", (string)LoadDef("Defs/ThingsDefs/Nanofluid.xml", name).Attribute("Class"));
            Equal("SSC_RES_PNASimpleLauncher", (string)LoadDef("Defs/ThingsDefs/SSC_Gun.xml", "Gun_PNA_Low").Element("recipeMaker").Element("researchPrerequisite"));
            var prerequisites = LoadDef("Defs/ResearchDefs/ResearchProjectDef.xml", "SSC_RES_PNASimpleLauncher").Element("prerequisites").Elements().Select(e => e.Value).ToArray();
            Assert(prerequisites.SequenceEqual(new[] { "SSC_RES_PNASimpleApp", "PrecisionRifling" }), "原研究前置保持");
        });
    }

    private static ThingDef_SSCPna InfoDrug(float severity)
    {
        var def = new ThingDef_SSCPna(); def.ingestible.outcomeDoers.Add(new IngestionOutcomeDoer_SSCPna { severity = severity }); return def;
    }
    private static StatDrawEntry InfoEntry(StatCategoryDef category, string label) => new(category, label, "value", "report", 0);
}
