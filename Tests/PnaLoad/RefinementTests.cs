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
            var pawn = CoatedAddict(.2f); var worker = RefinementWeapon(); var shot = CoatedShot(.075f); shot.Parts = 3;
            worker.Apply(shot, pawn); Near(.71, SSCPnaExposureUtility.GetAddiction(pawn).Severity); Near(.075, SSCPnaExposureUtility.GetLoad(pawn));
            worker.Apply(shot, pawn); Near(.72, SSCPnaExposureUtility.GetAddiction(pawn).Severity); Near(.15, SSCPnaExposureUtility.GetLoad(pawn));
        });
        Run("枪击基础及末期合计0.21且只写一次", () =>
        {
            var pawn = CoatedPawn(); Raw(pawn, 1.8f);
            var addiction = new SupplyAddiction { pawn = pawn, def = AddictionDef, Severity = .4f };
            pawn.health.hediffSet.hediffs.Add(addiction); addiction.Writes = 0;
            var shot = CoatedShot(.075f); shot.Parts = 3; RefinementWeapon().Apply(shot, pawn);
            Near(.61, addiction.Severity); Equal(1, addiction.Writes); Near(.975, SSCPnaExposureUtility.GetLoad(pawn));
        });
        Run("枪击未成瘾且未满负荷时不提前建立", () =>
        {
            var pawn = CoatedPawn(); RefinementWeapon().Apply(CoatedShot(.075f), pawn);
            Assert(SSCPnaExposureUtility.GetAddiction(pawn) == null && SSCPnaExposureUtility.GetNeed(pawn) == null, "未满无成瘾需求");
        });
        Run("枪击首次满负荷不对本次新成瘾追加0.21", () =>
        {
            var pawn = CoatedPawn(); Raw(pawn, 1.9f);
            pawn.health.OnAdded = h => { if (SSCPnaExposureUtility.IsAddictionDef(h.def)) h.Severity = .35f; };
            RefinementWeapon().Apply(CoatedShot(.075f), pawn); Near(.35, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
        });
        Run("满负荷枪击仍强化已有成瘾并沿原版上限钳制", () =>
        {
            var pawn = CoatedAddict(.2f); Raw(pawn, 2f); var worker = RefinementWeapon();
            worker.Apply(CoatedShot(.075f), pawn); Near(.91, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
            worker.Apply(CoatedShot(.075f), pawn); Near(1, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
        });
        Run("无有效输入的偏转命中不加深成瘾", () =>
        {
            var pawn = CoatedAddict(.2f); var shot = CoatedShot(.075f); shot.Deflected = true;
            RefinementWeapon().Apply(shot, pawn); Near(.7, SSCPnaExposureUtility.GetAddiction(pawn).Severity);
        });
        foreach (bool master in new[] { false, true })
            Run("枪击基础加深回调后继续核对保护 master=" + master, () =>
            {
                var pawn = CoatedPawn(); var addiction = new SupplyAddiction { pawn = pawn, def = AddictionDef, Severity = .4f };
                pawn.health.hediffSet.hediffs.Add(addiction);
                // 边界替身直接改身份字段，显式补生产身份 setter 的身体协调通知。
                addiction.OnWrite = () => { if (master) { pawn.IsMaster = true; SSCPnaExposureUtility.RefreshBodyState(pawn); } else Hediff_ChainOfSexSlave.AddToPawn(pawn, NewPawn()); };
                RefinementWeapon().Apply(CoatedShot(.075f), pawn);
                Assert(SSCPnaExposureUtility.GetAddiction(pawn) == null && SSCPnaExposureUtility.GetNeed(pawn) == null, "清成瘾及需求");
                Assert(master ? SSCPnaExposureUtility.GetBodyRecord(pawn) == null : RootedRecord(pawn) != null, "最终药效保护");
            });
        Run("药物定义保留局部展示类", () =>
        {
            foreach (string name in new[] { "Purple_Aphrodisiac_Nanofluid", "Purple_Aphrodisiac_Nanofluid_Plus" })
                Equal("SexSlaveCraft.ThingDef_SSCPna", (string)LoadDef("Defs/ThingsDefs/Nanofluid.xml", name).Attribute("Class"));
        });
    }

    // 以真实新伤口作为药效准入，Parts 只提供多伤口边界，不复制原版护甲算法。
    private static DamageWorker_SSCPnaCoated RefinementWeapon() => new DamageWorker_SSCPnaCoated
    {
        PhysicalApply = (info, target) =>
        {
            if (info.Deflected) return new DamageWorker.DamageResult { deflected = true };
            var p = (Pawn)target; var result = BoundaryWound(p, info.Amount);
            for (int i = 1; i < info.Parts; i++)
            { var next = BoundaryWound(p, info.Amount); result.hediffs.AddRange(next.hediffs); result.totalDamageDealt += next.totalDamageDealt; }
            return result;
        }
    };
    private static Pawn CoatedAddict(float need)
    { var p = PnaAddict(need); p.RaceProps.body.AllParts.Add(new BodyPartRecord()); return p; }

    private static ThingDef_SSCPna InfoDrug(float severity)
    {
        var def = new ThingDef_SSCPna(); def.ingestible.outcomeDoers.Add(new IngestionOutcomeDoer_SSCPna { severity = severity }); return def;
    }
    private static StatDrawEntry InfoEntry(StatCategoryDef category, string label) => new(category, label, "value", "report", 0);
}
