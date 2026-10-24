// AI 辅助生成：真实阶段、原版生育因子、健康说明和 Scribe 的永久泌乳中和回归。
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>转发生产 Postfix；测试 Harmony 2 不读取游戏 Harmony 3 的补丁特性。</summary>
    private static void LactationStagePostfix(Hediff __instance, ref HediffStage __result)
        => Harmony_LactationFertilityControl.Postfix(__instance, ref __result);

    /// <summary>在正常业务回归前安装真实原版 CurStage getter 的后置补丁。</summary>
    private static void InstallLactationFertilityControl(Harmony harmony)
        => harmony.Patch(AccessTools.PropertyGetter(typeof(Hediff), nameof(Hediff.CurStage)),
            postfix: new HarmonyMethod(typeof(Program), nameof(LactationStagePostfix)));

    /// <summary>复用已由原生 XML 加载的泌乳定义，验证状态变化、角色隔离和原生计算的统一性。</summary>
    private static void LactationFertilityControlTests(string repo)
    {
        var lactation = DefDatabase<HediffDef>.GetNamed(Harmony_LactationFertilityControl.LactationDefName);
        HediffStage source = lactation.stages.Single();
        foreach (var state in new[] { (-1f, false, .05f), (.01f, false, 0f), (.7f, false, 0f), (1f, false, 0f), (1f, true, 2.5f) })
            Check("lactation override native state matrix: " + state, () =>
            {
                var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation);
                if (state.Item1 > 0) Start(pawn, state.Item1);
                if (state.Item2) Toggle(pawn, true);
                Near(state.Item3, Factor(pawn)); Near(state.Item2 ? 1f : .05f, milk.CurStage.fertilityFactor);
                Near(.05f, source.fertilityFactor);
                // 原版属性详细说明也读取同一有效阶段，开启后不再把泌乳列为抑制项。
                string explanation = new StatPart_FertilityByHediffs().ExplanationPart(StatRequest.For(pawn));
                Equal(!state.Item2, explanation.Contains(lactation.LabelCap));
            });
        Check("lactation override isolates pawns and preserves all other stage fields", () =>
        {
            var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation); Start(pawn, 1); Toggle(pawn, true);
            var otherPawn = NewPawn(); var otherMilk = AttachLactationStage(otherPawn, lactation);
            HediffStage effective = milk.CurStage; Require(!ReferenceEquals(source, effective));
            foreach (var field in typeof(HediffStage).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.Name != nameof(HediffStage.fertilityFactor)) Equal(field.GetValue(source), field.GetValue(effective));
            Equal(source, otherMilk.CurStage); Near(.05f, Factor(otherPawn)); Near(2.5f, Factor(pawn));
        });
        Check("lactation override follows toggles immediately without query side effects", () =>
        {
            var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation); var record = Start(pawn, 1);
            for (int i = 0; i < 3; ++i)
            {
                Toggle(pawn, true); int before = notifications;
                for (int query = 0; query < 20; ++query) { Near(1, milk.CurStage.fertilityFactor); Near(2.5f, Factor(pawn)); }
                Equal(before, notifications); Near(1, record.Severity); Toggle(pawn, false);
                Near(.05f, milk.CurStage.fertilityFactor); Near(0, Factor(pawn));
            }
        });
        Check("lactation override restores source after uterine record removal", () =>
        {
            var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation); var record = Start(pawn, 1); Toggle(pawn, true);
            Near(2.5f, Factor(pawn)); pawn.health.RemoveHediff(record);
            Equal(source, milk.CurStage); Near(.05f, Factor(pawn));
        });
        Check("lactation override rejects incomplete or invalid torso records", () =>
        {
            var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation); var record = Start(pawn, 1); Toggle(pawn, true);
            record.Severity = .7f; Equal(source, milk.CurStage); Near(0, Factor(pawn));
            record.Severity = 1; Toggle(pawn, true); record.Part = null;
            Equal(source, milk.CurStage); Near(.05f, Factor(pawn));
        });
        Check("lactation override ignores detached milk and respects remaining fertility modifiers", () =>
        {
            var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation); Start(pawn, 1); Toggle(pawn, true);
            var other = new Hediff { pawn = pawn, def = new HediffDef { defName = "OtherSuppression", stages = new() { new() { fertilityFactor = .4f } } } };
            pawn.health.hediffSet.hediffs.Add(other); Near(1, Factor(pawn));
            other.def.stages[0].fertilityFactor = 0; Near(0, Factor(pawn));
            pawn.health.hediffSet.hediffs.Remove(other); pawn.health.RemoveHediff(milk);
            Equal(source, milk.CurStage); Near(2.5f, Factor(pawn));
        });
        Check("lactation override respects changing compatibility config without restoring hardcoded suppression", () =>
        {
            var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation); Start(pawn, 1); Toggle(pawn, true);
            float original = source.fertilityFactor;
            try
            {
                source.fertilityFactor = .2f; Near(1, milk.CurStage.fertilityFactor); Toggle(pawn, false); Near(.2f, milk.CurStage.fertilityFactor);
                // 模拟 HumanCattle 对实际源阶段的中和；再次开关不能把 ×1 改回 ×0.05。
                source.fertilityFactor = 1; Toggle(pawn, true); Equal(source, milk.CurStage); Near(2.5f, Factor(pawn));
                Toggle(pawn, false); Equal(source, milk.CurStage); Near(1, milk.CurStage.fertilityFactor);
                source.fertilityFactor = 1.2f; Toggle(pawn, true); Equal(source, milk.CurStage); Near(3, Factor(pawn));
            }
            finally { source.fertilityFactor = original; }
        });
        foreach (bool enabled in new[] { false, true })
            Check("lactation override real Scribe round-trip derives effect: " + enabled, () =>
            {
                var pawn = NewPawn(); AttachLactationStage(pawn, lactation); Start(pawn, 1); if (enabled) Toggle(pawn, true);
                var loaded = Load(Save(pawn)); var milk = loaded.health.hediffSet.GetFirstHediffOfDef(lactation);
                Near(enabled ? 1 : .05f, milk.CurStage.fertilityFactor); Near(enabled ? 2.5f : 0, Factor(loaded));
            });
        Check("lactation override old save missing uterine toggle stays disabled", () =>
        {
            var pawn = NewPawn(); AttachLactationStage(pawn, lactation); Start(pawn, 1); Toggle(pawn, true); string path = Save(pawn);
            Edit(path, doc => doc.Descendants("sscUterusFertilityEnabled").Remove());
            var loaded = Load(path); Near(.05f, loaded.health.hediffSet.GetFirstHediffOfDef(lactation).CurStage.fertilityFactor); Near(0, Factor(loaded));
        });
        Check("lactation override pregnancy still blocks switching to infertility", () =>
        {
            var pawn = NewPawn(); var milk = AttachLactationStage(pawn, lactation); Start(pawn, 1); Toggle(pawn, true);
            var pregnancy = new Hediff { pawn = pawn, def = HediffDefOf.PregnantHuman, Severity = .4f };
            pawn.health.hediffSet.hediffs.Add(pregnancy);
            Require(!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, false, out _));
            Near(2.5f, Factor(pawn)); Near(1, milk.CurStage.fertilityFactor); Near(.4f, pregnancy.Severity);
        });
        Check("lactation override queries preserve actual milk component and production state", () =>
        {
            // 不调用依赖游戏 Harmony 3 的 CompPostMake；奶量及累计字段用反射置为已运行状态。
            var pawn = SurgeryPawn(); var milk = new HediffWithComps { pawn = pawn, def = lactation, Severity = 1, comps = new() };
            var component = new HediffComp_PermanentLactating { parent = milk, props = lactation.comps.Single() };
            milk.comps.Add(component); pawn.health.AddHediff(milk); Start(pawn, 1);
            var charge = AccessTools.Field(typeof(HediffComp_PermanentLactating), "charge");
            var pending = AccessTools.Field(typeof(HediffComp_PermanentLactating), "pendingProductionTicks");
            charge.SetValue(component, .08f); pending.SetValue(component, 35);
            foreach (bool production in new[] { false, true })
            {
                pawn.GetComp<CompSexSlaveTraining>().milkProductionEnabled = production;
                Toggle(pawn, true); Near(2.5f, Factor(pawn)); Near(1, milk.CurStage.fertilityFactor);
                Toggle(pawn, false); Near(0, Factor(pawn)); Near(.05f, milk.CurStage.fertilityFactor);
                Near(.08f, (float)charge.GetValue(component)); Equal(35, (int)pending.GetValue(component));
                Equal(component, milk.comps.Single()); Equal(production, pawn.GetComp<CompSexSlaveTraining>().milkProductionEnabled);
            }
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("lactation override health display follows state in language: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                var pawn = SurgeryPawn(); var milk = AttachLactationStage(pawn, lactation); Start(pawn, 1); Toggle(pawn, true);
                Equal("×1", FertilityEntry(milk).ValueString);
                string note = "SSC_HediffFertility_LactationOverrideNote".Translate();
                Require(note.Contains("抑制已解除") && milk.TipStringExtra.Contains(note));
                Require(!milk.TipStringExtra.Contains("SSC_HediffFertility_"));
                Toggle(pawn, false); Equal("×0.05", FertilityEntry(milk).ValueString); Require(!milk.TipStringExtra.Contains(note));
            });
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
    }
}
