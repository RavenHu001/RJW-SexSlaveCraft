// AI 辅助生成：用实际游戏估价入口验证阶段倍率，不以 XML 字段断言代替结算。
using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void SexSlaveMarketValueTests(string repo)
    {
        Check("pawn market value and trade base follow the ownerless to bound handoff", () =>
        {
            var original = new XmlDocument();
            string game = Path.GetDirectoryName(typeof(Pawn).Assembly.Location);
            original.Load(Path.Combine(game, "Data/Core/Defs/Stats/Stats_Basics_General.xml"));
            var xml = new XmlDocument();
            xml.Load(Path.Combine(repo, "1.7/Patches/SSC_SexSlaveMarketValue.xml"));
            var patch = DirectXmlToObject.ObjectFromXml<PatchOperation>(xml.DocumentElement["Operation"], false);
            Require(patch.Apply(original));
            var partNode = original.SelectNodes("/Defs/StatDef[defName='MarketValue']/parts/li")
                .Cast<XmlNode>().Single(node => node.Attributes?["Class"]?.Value == "SexSlaveCraft.StatPart_SSCSexSlaveMarketValue");
            var stagePart = DirectXmlToObject.ObjectFromXml<StatPart>(partNode, false);

            var market = DefDatabase<StatDef>.GetNamed("MarketValue");
            var chainDef = SSCDefOf.ChainOfSexSlave;
            var previousMarket = StatDefOf.MarketValue;
            var previousWorkerClass = market.workerClass;
            var workerField = AccessTools.Field(typeof(StatDef), "workerInt");
            var previousWorker = workerField.GetValue(market);
            var previousParts = market.parts;
            var previousStages = chainDef.stages;
            var boundary = new Harmony("ssc.tests.sex-slave-market-value");
            try
            {
                // 保留原版实际人物估价、StatPart 与交易读取，只固定其余人物品质和偏移。
                StatDefOf.MarketValue = market;
                market.workerClass = typeof(StatWorker_MarketValue);
                market.parts = new List<StatPart> { stagePart };
                workerField.SetValue(market, null);
                boundary.Patch(AccessTools.Method(typeof(PriceUtility), nameof(PriceUtility.PawnQualityPriceFactor)),
                    prefix: new HarmonyMethod(typeof(Program), nameof(UnitPawnPriceFactor)));
                boundary.Patch(AccessTools.Method(typeof(PriceUtility), nameof(PriceUtility.PawnQualityPriceOffset)),
                    prefix: new HarmonyMethod(typeof(Program), nameof(ZeroPawnPriceOffset)));

                ImprintBoundaryDef("PNA_Addiction", typeof(Hediff));
                if (DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail("Body") == null)
                    DefDatabase<PawnRenderNodeTagDef>.Add(new PawnRenderNodeTagDef { defName = "Body" });
                xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/HediffOfSexSlave.xml"));
                var sourceChainDef = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement["HediffDef"], false);
                DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                // 其他印记测试共用锁链定义的引用。此处仅借用生产 XML 的阶段，并在退出时恢复。
                chainDef.stages = sourceChainDef.stages;

                var pawn = ImprintPawn(PawnIdentity.Slave);
                pawn.def.statBases = new List<StatModifier> { new() { stat = market, value = 1000f } };
                var trade = new Tradeable();
                trade.AddThing(pawn, Transactor.Trader);

                void VerifyValue(float expected)
                {
                    Near(expected, pawn.MarketValue);
                    Near(expected, trade.BaseMarketValue);
                }

                var ownerless = (Hediff_SSCOwnerlessSlave)pawn.health.AddHediff(
                    DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                ownerless.Severity = .9f;
                VerifyValue(2000f);

                var master = ImprintPawn(PawnIdentity.Master);
                Require(SSCBondUtility.BindNewWithProgress(master, pawn, ownerless, ownerless.Severity, () => true, out var chain));
                VerifyValue(500f);

                chain.Severity = MathF.BitDecrement(.1f);
                Near(1000f, pawn.MarketValue);
            }
            finally
            {
                boundary.UnpatchAll(boundary.Id);
                chainDef.stages = previousStages;
                market.parts = previousParts;
                market.workerClass = previousWorkerClass;
                workerField.SetValue(market, previousWorker);
                StatDefOf.MarketValue = previousMarket;
            }
        });
    }

    private static bool UnitPawnPriceFactor(ref float __result) { __result = 1f; return false; }
    private static bool ZeroPawnPriceOffset(ref float __result) { __result = 0f; return false; }
}
