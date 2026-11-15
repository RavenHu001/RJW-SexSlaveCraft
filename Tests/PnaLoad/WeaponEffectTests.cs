// AI 辅助生成：通过现役涂覆／太空入口，核对一次有效投送对应一次受药显示。
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunWeaponEffectTests()
    {
        Run("涂覆独立命中的受药反馈与目标一一对应", () =>
        {
            var pawn = CoatedPawn();
            var worker = CoatedWorker();
            worker.PhysicalApply = (_, _) =>
            {
                var result = BoundaryWound(pawn, 3);
                result.hediffs.AddRange(BoundaryWound(pawn, 2).hediffs);
                result.totalDamageDealt += 2;
                return result;
            };
            worker.Apply(CoatedShot(), pawn);
            Equal(1, SSCPnaWeaponEffectsUtility.Absorptions.Count);
            Input(pawn, 1);
            Equal(1, SSCPnaWeaponEffectsUtility.Absorptions.Count);
            worker.Apply(CoatedShot(), pawn);
            Equal(2, SSCPnaWeaponEffectsUtility.Absorptions.Count);
            Assert(SSCPnaWeaponEffectsUtility.Absorptions.All(target => target == pawn), "反馈对应实际目标");
        });
        Run("太空转主人时停止修复和受药反馈", () =>
        {
            var pawn = SpacerSlave();
            var injury = SpacerInjury(pawn, 10);
            pawn.health.OnAdded = hediff =>
            {
                if (SSCPnaExposureUtility.IsBodyLoadDef(hediff.def)) pawn.IsMaster = true;
            };
            new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(), pawn);
            Near(0, injury.Healed);
            AssertNoMasterDrugState(pawn);
            Equal(0, SSCPnaWeaponEffectsUtility.Absorptions.Count);
        });
        Run("涂覆转钝伤无受药反馈", () =>
        {
            var pawn = CoatedPawn();
            CoatedWorker("Bruise").Apply(CoatedShot(), pawn);
            AssertNoCoatingDrug(pawn);
            Equal(0, SSCPnaWeaponEffectsUtility.Absorptions.Count);
        });
    }
}
