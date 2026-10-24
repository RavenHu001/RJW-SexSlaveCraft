// AI 辅助生成：实际隔离生产 DLL 的公共入口、身体记录及牛特化消费者，不复制适配算法。
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>为第二批接线补充原生类型验证；既有保存、妊娠和克隆回归保留原有独立范围。</summary>
    private static void AbilityToggleAdapterTests()
    {
        Check("toggle batch2 production registry contains five original-storage adapters", () =>
        {
            Equal(5, SSCAbilityToggleUtility.Definitions.Count);
            Equal(2, SSCAbilityToggleUtility.Definitions.Count(d => d.Group == SSCAbilityToggleGroup.Body));
            Require(SSCAbilityToggleUtility.Find(SSCAbilityToggleUtility.MilkProduction).DefaultEnabled);
            Require(!SSCAbilityToggleUtility.Find(SSCAbilityToggleUtility.UterusFertility).DefaultEnabled);
            Require(SSCAbilityToggleUtility.Find(SSCAbilityToggleUtility.GelRendering).DefaultEnabled);
        });
        Check("toggle batch2 readonly missing bodies never create records or components", () =>
        {
            var p = NewPawn(); int count = p.health.hediffSet.hediffs.Count, before = notifications;
            foreach (string id in new[] { SSCAbilityToggleUtility.MilkProduction, SSCAbilityToggleUtility.UterusFertility, SSCAbilityToggleUtility.GelRendering })
            {
                var view = SSCAbilityToggleUtility.GetView(p, id); Require(!view.Visible && !view.CanChange);
                Require(!SSCAbilityToggleUtility.TrySet(p, id, true, view.Context, out _));
            }
            Equal(count, p.health.hediffSet.hediffs.Count); Equal(before, notifications); Require(p.TryGetComp<CompSexSlaveTraining>() == null);
        });
        Check("toggle batch2 fertility uses actual body record and native fertility calculation", () =>
        {
            var p = NewPawn(); var record = Start(p, 1); var comp = AttachToggleTraining(p);
            comp.abilityToggles.Set(SSCAbilityToggleUtility.UterusFertility, true);
            Require(!SSCAbilityToggleUtility.GetEnabled(p, SSCAbilityToggleUtility.UterusFertility)); Near(0, Factor(p));
            var view = SSCAbilityToggleUtility.GetView(p, SSCAbilityToggleUtility.UterusFertility);
            Require(SSCAbilityToggleUtility.TrySet(p, SSCAbilityToggleUtility.UterusFertility, true, view.Context, out var reason), reason);
            Require(record.FertilityEnabled); Near(2.5f, Factor(p));
            int before = notifications;
            Require(SSCAbilityToggleUtility.TrySet(p, SSCAbilityToggleUtility.UterusFertility, true, view.Context, out reason), reason);
            Equal(before, notifications); Near(1, record.Severity);
        });
        Check("toggle batch2 milk retains raw Comp preference and actual Cow benefit thresholds", () =>
        {
            var p = NewPawn(); var comp = AttachToggleTraining(p);
            // 仅需原泌乳存在条件；不构造泌乳计时或营养算法替身。
            p.health.hediffSet.hediffs.Add(new Hediff { def = SSCDefOf.SSC_Lactating_SubState, pawn = p });
            comp.specializationType = SexSlaveSpecializationType.Cow;
            // 对真实生产程序集的四档收益逐次关闭／重开；预期常量不替代实际倍率计算。
            foreach (var stage in new[] { (0f, .6f), (.2f, 1f), (.5f, 1.3f), (1f, 1.6f) })
            {
                comp.specializationProgress = stage.Item1;
                var view = SSCAbilityToggleUtility.GetView(p, SSCAbilityToggleUtility.MilkProduction);
                Require(SSCAbilityToggleUtility.TrySet(p, SSCAbilityToggleUtility.MilkProduction, false, view.Context, out var reason), reason);
                Require(!comp.milkProductionEnabled && !BusSpecializationUtility.CanAccelerateMilkProduction(p));
                Near(0, BusSpecializationUtility.GetCowLactationBoostMultiplier(p));
                Require(SSCAbilityToggleUtility.TrySet(p, SSCAbilityToggleUtility.MilkProduction, true, view.Context, out reason), reason);
                Require(comp.milkProductionEnabled && BusSpecializationUtility.CanAccelerateMilkProduction(p));
                Near(stage.Item2, BusSpecializationUtility.GetCowLactationBoostMultiplier(p));
            }
            Require(!comp.abilityToggles.HasEntries);
            comp.specializationType = SexSlaveSpecializationType.None;
            Near(0, BusSpecializationUtility.GetCowLactationBoostMultiplier(p)); Require(!BusSpecializationUtility.CanAccelerateMilkProduction(p));
        });
        Check("toggle batch2 original pregnancy blocks generic disabling with same reason", () =>
        {
            var p = NewPawn(); var record = Start(p, 1); Toggle(p, true); AttachToggleTraining(p);
            p.health.hediffSet.hediffs.Add(new Hediff { def = RimWorld.HediffDefOf.Pregnant, pawn = p });
            var view = SSCAbilityToggleUtility.GetView(p, SSCAbilityToggleUtility.UterusFertility);
            Require(!SSCUterusDevelopmentUtility.CanSetFertilityEnabled(p, false, out string expected));
            Require(!SSCAbilityToggleUtility.TrySet(p, SSCAbilityToggleUtility.UterusFertility, false, view.Context, out string actual));
            Equal(expected, actual); Require(record.FertilityEnabled);
        });
    }

    /// <summary>只挂载真实训练组件，不执行完整训练、地图或关系初始化。</summary>
    private static CompSexSlaveTraining AttachToggleTraining(Pawn pawn)
    {
        var comp = new CompSexSlaveTraining { parent = pawn };
        AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(pawn, new List<ThingComp> { comp });
        return comp;
    }
}
