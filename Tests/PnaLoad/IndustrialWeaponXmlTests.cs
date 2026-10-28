// AI 辅助生成：工业 XML 接线、真实剂量入口和既有武器隔离回归。
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;

internal static partial class Program
{
    private const string IndustrialDamages = "Defs/DemageDefs/SSC_PnaIndustrialDamage.xml";

    private static void RunIndustrialWeaponXmlTests()
    {
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
        }
        Run("速射三针各自读取命中前意识，不固定合计24点", () =>
        {
            var pawn = CoatedPawn(); var worker = CoatedWorker("Stab");
            pawn.health.capacities.Consciousness = 1; worker.Apply(CoatedShot(.08f, 2), pawn);
            pawn.health.capacities.Consciousness = .5f; worker.Apply(CoatedShot(.08f, 2), pawn);
            pawn.health.capacities.Consciousness = .25f; worker.Apply(CoatedShot(.08f, 2), pawn);
            Near(.56, SSCPnaExposureUtility.GetLoad(pawn)); Equal(3, worker.ApplyCalls);
        });
    }
}
