// AI 辅助生成：实际 HediffStatsUtility 与健康悬停展示，隔离图形，不替换倍率或展示算法。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using System.Xml;

internal static partial class Program
{
    /// <summary>通过无业务逻辑转发器安装生产 Postfix，避免测试 Harmony 2 读取游戏 Harmony 3 特性。</summary>
    private static void FertilityDisplayPostfix(Hediff instance, ref IEnumerable<StatDrawEntry> __result)
        => Harmony_HediffFertilityDisplay.Postfix(instance, ref __result);

    /// <summary>保留真实基础悬停生成，只转发生产解除抑制说明。</summary>
    private static void LactationFertilityTipPostfix(Hediff __instance, ref string __result)
        => Harmony_LactationFertilityTip.Postfix(__instance, ref __result);

    /// <summary>查找新增生育行，数值取自真实生产补丁生成的原生 StatDrawEntry。</summary>
    private static StatDrawEntry FertilityEntry(Hediff hediff)
        => HediffStatsUtility.SpecialDisplayStats(hediff.CurStage, hediff)
            .Single(entry => entry.LabelCap == (StatDefOf.Fertility?.LabelCap.ToString() ?? "SSC_HediffFertility_Label".Translate().ToString()));

    /// <summary>只装载原始泌乳阶段，隔离与本展示无关且依赖游戏 Harmony 3 的产奶组件初始化。</summary>
    private static Hediff AttachLactationStage(Pawn pawn, HediffDef definition)
    {
        var record = new Hediff { pawn = pawn, def = definition, Severity = 1f };
        pawn.health.AddHediff(record);
        return record;
    }

    /// <summary>验证当前身体状态与兼容后的真实阶段值，完整保留原版其他属性条目。</summary>
    private static void HediffFertilityDisplayTests(string repo, Harmony harmony)
    {
        // 前面的完全凝胶化用例隔离过成功消息翻译；本组必须撤掉该边界，使用真实单参数格式化。
        harmony.Unpatch(AccessTools.Method(typeof(TranslatorFormattedStringExtensions), "Translate", new[] { typeof(string), typeof(NamedArgument) }),
            HarmonyPatchType.Prefix, harmony.Id);
        harmony.Unpatch(AccessTools.Method(typeof(GrammarResolverSimpleStringExtensions), "Formatted", new[] { typeof(string), typeof(NamedArgument) }),
            HarmonyPatchType.Prefix, harmony.Id);
        // 旧属性数学夹具只需定义名和基础值；健康提示还需要与实际游戏一样的非空属性标签。
        foreach (string name in stageStats)
        {
            var definition = DefDatabase<StatDef>.GetNamed(name);
            definition.label ??= name;
        }
        harmony.Patch(AccessTools.Method(typeof(HediffStatsUtility), nameof(HediffStatsUtility.SpecialDisplayStats)),
            postfix: new HarmonyMethod(typeof(Program), nameof(FertilityDisplayPostfix)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Hediff), nameof(Hediff.TipStringExtra)),
            postfix: new HarmonyMethod(typeof(Program), nameof(LactationFertilityTipPostfix)));
        StatCategoryDefOf.CapacityEffects ??= new StatCategoryDef { defName = "CapacityEffects", label = "身体影响" };
        StatDefOf.Fertility = new StatDef { defName = "Fertility", label = "原版生育标签" };
        foreach (var state in new[] { (.01f, false, "×0"), (.3f, false, "×0"), (.7f, false, "×0"), (1f, false, "×0"), (1f, true, "×2.5") })
            Check("health fertility native entry: " + state, () =>
            {
                var pawn = SurgeryPawn(); var record = Start(pawn, state.Item1); if (state.Item2) Toggle(pawn, true);
                int notices = notifications;
                Equal(state.Item3, FertilityEntry(record).ValueString);
                string tip = record.TipStringExtra;
                Require(tip.Contains("原版生育标签") && tip.Contains(state.Item3));
                Equal(1, tip.Split("原版生育标签").Length - 1); Equal(notices, notifications); Near(state.Item1, record.Severity);
            });
        Check("health fertility updates with toggle and preserves original stat entries", () =>
        {
            var pawn = SurgeryPawn(); var record = Start(pawn, 1);
            var entries = HediffStatsUtility.SpecialDisplayStats(record.CurStage, record).ToList();
            Require(entries.Count > 1); Toggle(pawn, true); Equal("×2.5", FertilityEntry(record).ValueString);
            Toggle(pawn, false); Equal("×0", FertilityEntry(record).ValueString);
        });
        // 读取实际永久泌乳节点及组件；不手写 0.05 的生产配置，不触发完整模组启动扫描。
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/HediffOfTrain.xml"));
        var lactation = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.SelectSingleNode("Defs/HediffDef[defName='SSC_Lactating_SubState']"), false);
        DefDatabase<HediffDef>.Add(lactation); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Check("health permanent lactation displays actual factor even when production is disabled", () =>
        {
            var pawn = SurgeryPawn(); var h = AttachLactationStage(pawn, lactation); Near(.05f, h.CurStage.fertilityFactor);
            Equal("×0.05", FertilityEntry(h).ValueString);
            pawn.GetComp<CompSexSlaveTraining>().milkProductionEnabled = false;
            Equal("×0.05", FertilityEntry(h).ValueString); Require(h.TipStringExtra.Contains("×0.05"));
        });
        Check("health permanent lactation reflects neutralized compatibility factor", () =>
        {
            var pawn = SurgeryPawn(); var h = AttachLactationStage(pawn, lactation); float original = h.CurStage.fertilityFactor;
            try { h.CurStage.fertilityFactor = 1; Equal("×1", FertilityEntry(h).ValueString); }
            finally { h.CurStage.fertilityFactor = original; }
        });
        Check("health fertility reports individual modifiers instead of the combined pawn result", () =>
        {
            var pawn = SurgeryPawn(); var record = Start(pawn, 1); Toggle(pawn, true); var milk = AttachLactationStage(pawn, lactation);
            Near(2.5f, Factor(pawn)); Equal("×2.5", FertilityEntry(record).ValueString); Equal("×1", FertilityEntry(milk).ValueString);
        });
        Check("health fertility leaves unrelated hediff and stage-only preview unchanged", () =>
        {
            var pawn = SurgeryPawn(); var def = new HediffDef { defName = "OtherFertilityFixture", stages = new() { new HediffStage { fertilityFactor = .6f } } };
            var unrelated = new Hediff { pawn = pawn, def = def, Severity = 1 };
            Require(!HediffStatsUtility.SpecialDisplayStats(unrelated.CurStage, unrelated).Any(entry => entry.LabelCap == "原版生育标签"));
            Require(!HediffStatsUtility.SpecialDisplayStats(unrelated.CurStage, null).Any(entry => entry.LabelCap == "原版生育标签"));
        });
        Check("health fertility missing Biotech stat uses localized fallback label", () =>
        {
            var original = StatDefOf.Fertility;
            try { StatDefOf.Fertility = null; Equal("×0", FertilityEntry(Start(SurgeryPawn())).ValueString); }
            finally { StatDefOf.Fertility = original; }
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("health fertility keyed display: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                var pawn = SurgeryPawn(); var record = Start(pawn, 1); Toggle(pawn, true);
                Equal("×2.5", FertilityEntry(record).ValueString);
                Require(!record.TipStringExtra.Contains("SSC_HediffFertility_"));
            });
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
    }
}
