// AI 辅助生成：真实游戏 TickInterval、RJW 原始时长/Initialize 与生产补丁转发。
// 此文件刻意使用链接的同名生产补丁类，以 Harmony 2 构建；业务组件仍来自生产 DLL。
#pragma warning disable CS0436
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static HungerCategory pregnancyHunger;

    /// <summary>测试 Harmony 2 以无特性转发器安装生产逻辑，避免独立 CLR 读取游戏 Harmony 3 类特性。</summary>
    private static void PregnancyFlowTests(Harmony harmony)
    {
        var native = AccessTools.Method(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.TickInterval));
        Check("batch4 actual native IL matches unique rate anchor and preserves branch labels", () =>
        {
            var original = PatchProcessor.GetOriginalInstructions(native).ToList();
            int store = original.FindIndex(c => c.opcode == OpCodes.Div) + 1;
            // 给实际锚的存储指令加上可观察标签，确认插入不会让分支跳过新增计算。
            var label = new DynamicMethod("pregnancy_labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
            original[store].labels.Add(label);
            var rewritten = Harmony_UterusNativePregnancy.Transpiler(original.Select(c => new CodeInstruction(c))).ToList();
            Equal(original.Count + 2, rewritten.Count);
            Equal(1, rewritten.Count(c => c.Calls(AccessTools.Method(typeof(SSCUterusPregnancyUtility), nameof(SSCUterusPregnancyUtility.ScaleNativeRate)))));
            Require(rewritten[store].opcode == OpCodes.Ldarg_0 && rewritten[store].labels.Contains(label));
            Require(!rewritten[store + 2].labels.Contains(label));
        });
        foreach (bool duplicate in new[] { false, true })
            Check("batch4 missing/nonunique IL anchor leaves entire original unchanged: " + duplicate, () =>
            {
                var codes = duplicate ? PatchProcessor.GetOriginalInstructions(native).ToList() : new List<CodeInstruction> { new(OpCodes.Ret) };
                if (duplicate) codes.AddRange(PatchProcessor.GetOriginalInstructions(native));
                var rewritten = Harmony_UterusNativePregnancy.Transpiler(codes).ToList();
                Equal(codes.Count, rewritten.Count); Require(codes.Zip(rewritten).All(pair => ReferenceEquals(pair.First, pair.Second)));
                Equal(1, errors.Count); Require(errors[0].Contains("保留原版孕期")); errors.Clear();
            });
        harmony.Patch(native, transpiler: new HarmonyMethod(typeof(Program), nameof(NativePregnancyTranspiler)));
        harmony.Patch(AccessTools.Method(typeof(Hediff_BasePregnancy), "GetGestationPeriod"),
            postfix: new HarmonyMethod(typeof(Program), nameof(RjwPregnancyPostfix)));
        harmony.Patch(AccessTools.Method(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.StartLabor)),
            prefix: new HarmonyMethod(typeof(Program), nameof(LaborBoundary)));
        harmony.Patch(AccessTools.Method(typeof(Hediff_Pregnant), "NotifyPlayerOfTrimesterPassing"),
            prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Need_Food), nameof(Need_Food.CurCategory)),
            prefix: new HarmonyMethod(typeof(Program), nameof(PregnancyHungerBoundary)));
        Current.Game.storyteller = Bare<Storyteller>(); Current.Game.storyteller.difficulty = new Difficulty { babiesAreHealthy = true };
        human.race.gestationPeriodDays = 20;
        DefDatabase<HediffDef>.Add(new HediffDef { defName = "FertilityEnhancer", hediffClass = typeof(Hediff) });
        foreach (string name in new[] { "Pregnant", "PregnantHuman" })
        foreach (var category in new[] { HungerCategory.Fed, HungerCategory.Hungry, HungerCategory.Starving })
            Check("batch4 actual native Tick applies 1.25 once and keeps hunger: " + name + "/" + category, () =>
            {
                pregnancyHunger = category; var normal = PregnancyMother(false); var boosted = PregnancyMother(true);
                var a = (Hediff_Pregnant)NewPregnancy(normal, name); var b = (Hediff_Pregnant)NewPregnancy(boosted, name);
                a.Severity = b.Severity = .1f; a.TickInterval(1200); b.TickInterval(1200);
                float gain = a.GestationProgress - .1f;
                Require(gain > 0); Near(.1f + gain * 1.25f, b.GestationProgress);
                Near(1200, a.ageTicks); Near(1200, b.ageTicks);
                // 不读取修改后的进度为下一轮缩短基准；每 Tick 只加同一正常速度倍率。
                b.TickInterval(1200); Near(.1f + gain * 2.5f, b.GestationProgress);
            });
        Check("batch4 native boosted final Tick calls labor immediately and removes pregnancy", () =>
        {
            pregnancyHunger = HungerCategory.Fed; var p = PregnancyMother(true);
            var h = (Hediff_Pregnant)NewPregnancy(p, "PregnantHuman"); h.Severity = .9989f;
            laborCalls = 0; h.TickInterval(1200); Equal(1, laborCalls); Require(!p.health.hediffSet.hediffs.Contains(h));
        });
        foreach (string name in new[] { "RJW_pregnancy", "RJW_pregnancy_beast" })
        foreach (bool boosted in new[] { false, true })
            Check("batch4 actual RJW Initialize writes fresh noncompounding times: " + name + "/" + boosted, () =>
            {
                var p = PregnancyMother(boosted); var h = (Hediff_BasePregnancy)NewPregnancy(p, name, false);
                h.babies = new() { NewPawn() }; RJWPregnancySettings.normal_pregnancy_duration = 1;
                h.Initialize(p, null, default); Near(boosted ? 960000 : 1200000, h.p_end_tick - h.p_start_tick);
                Require(p.health.hediffSet.hediffs.Contains(h));
                if (!boosted) Toggle(p, true);
                for (int i = 0; i < 3; i++)
                {
                    Current.Game.tickManager.DebugSetTicksGame(1000 * (i + 1));
                    h.Initialize(p, null, default); Near(boosted ? 960000 : 1200000, h.p_end_tick - h.p_start_tick);
                    Near(1000 * (i + 1), h.p_start_tick);
                }
                // 原 RJW 设置与 FertilityEnhancer 继续参与它自己的原始时长计算。
                RJWPregnancySettings.normal_pregnancy_duration = .5f;
                p.health.AddHediff(DefDatabase<HediffDef>.GetNamed("FertilityEnhancer"));
                h.Initialize(p, null, default); Near(boosted ? 384000 : 480000, h.p_end_tick - h.p_start_tick);
                RJWPregnancySettings.normal_pregnancy_duration = 1;
            });
        Check("batch4 absent comp never modifies native or RJW calculations", () =>
        {
            var p = NewPawn(); var a = new Hediff_Pregnant { pawn = p, def = HediffDefOf.Pregnant };
            Near(4, SSCUterusPregnancyUtility.ScaleNativeRate(4, a));
            var b = new Hediff_HumanlikePregnancy { pawn = p, def = DefDatabase<HediffDef>.GetNamed("RJW_pregnancy") };
            Near(100, SSCUterusPregnancyUtility.ScaleRjwDuration(100, b));
        });
        PregnancyCreationFlowTests(harmony);
    }

    /// <summary>建立含真实食物需求的母体；食物类别单独控制，正常增长倍率仍执行原版方法。</summary>
    private static Pawn PregnancyMother(bool boosted)
    {
        var p = NewPawn(); Start(p, 1); if (boosted) Toggle(p, true);
        // 无地图的最小宿主跳过需求构造器的种族初始饱食度，食物类别由下方边界提供。
        var food = Bare<Need_Food>(); AccessTools.Field(typeof(Need), "pawn").SetValue(food, p);
        food.def = new NeedDef { defName = "Food" };
        AccessTools.Field(typeof(Pawn_NeedsTracker), "needs").SetValue(p.needs, new List<Need> { food });
        return p;
    }

    /// <summary>只提供食物类别作为前提；BodyResourceGrowthSpeed 与 HungerMultiplier 保留原生实现。</summary>
    private static bool PregnancyHungerBoundary(ref HungerCategory __result) { __result = pregnancyHunger; return false; }
    /// <summary>保留 Tick 的临产分派与移除，只隔离后续地图、信件和婴儿出生生命周期。</summary>
    private static bool LaborBoundary() { laborCalls++; return false; }
    /// <summary>无业务转发器：执行生产 Transpiler，不复制指令匹配或倍率逻辑。</summary>
    private static IEnumerable<CodeInstruction> NativePregnancyTranspiler(IEnumerable<CodeInstruction> instructions)
        => Harmony_UterusNativePregnancy.Transpiler(instructions);
    /// <summary>无业务转发器：执行生产 RJW Postfix。</summary>
    private static void RjwPregnancyPostfix(Hediff_BasePregnancy __instance, ref float __result)
        => Harmony_UterusRjwPregnancy.Postfix(__instance, ref __result);

    /// <summary>实际执行两类 RJW 创建/缺胎儿重建及原版分支，只隔离遗传和完整婴儿生成。</summary>
    private static void PregnancyCreationFlowTests(Harmony harmony)
    {
        foreach (var method in new[] { typeof(Hediff_BasePregnancy), typeof(Hediff_HumanlikePregnancy), typeof(Hediff_BestialPregnancy) }
            .Select(t => AccessTools.Method(t, "GenerateBabies"))
            .Select(m => AccessTools.DeclaredMethod(m.DeclaringType, m.Name)).Distinct())
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(GeneratePregnancyBabiesBoundary)));
        harmony.Patch(AccessTools.Method(typeof(Hediff_BasePregnancy), nameof(Hediff_BasePregnancy.SelectDnaGivingParent)),
            prefix: new HarmonyMethod(typeof(Program), nameof(DnaParentBoundary)));
        harmony.Patch(AccessTools.Method(typeof(PregnancyUtility), nameof(PregnancyUtility.GetInheritedGeneSet), new[] { typeof(Pawn), typeof(Pawn) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(PregnancyGenesBoundary)));
        foreach (bool boosted in new[] { false, true })
        {
            Check("batch4 RJW vanilla branch adds native pregnancy and accelerates only once: " + boosted, () =>
            {
                pregnancyHunger = HungerCategory.Fed; var p = PregnancyMother(boosted);
                PregnancyHelper.StartVanillaPregnancy(p, NewPawn());
                var h = (Hediff_Pregnant)p.health.hediffSet.hediffs.Single(x => x.def == HediffDefOf.PregnantHuman);
                Equal(boosted, h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
                h.Severity = .1f; h.TickInterval(1200); Near(boosted ? .10125f : .101f, h.GestationProgress);
            });
            foreach (bool beast in new[] { false, true })
                Check("batch4 actual RJW generic Create and empty-babies reinitialize keep original grant: " + beast + "/" + boosted, () =>
                {
                    var p = PregnancyMother(boosted); var father = NewPawn();
                    var h = beast ? (Hediff_BasePregnancy)Hediff_BasePregnancy.Create<Hediff_BestialPregnancy>(p, father, default)
                        : Hediff_BasePregnancy.Create<Hediff_HumanlikePregnancy>(p, father, default);
                    Equal(boosted, h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
                    Near(boosted ? 960000 : 1200000, h.p_end_tick - h.p_start_tick);
                    if (!boosted) Toggle(p, true);
                    h.babies.Clear(); AccessTools.Method(typeof(Hediff_BasePregnancy), "InitializeIfNoBabies").Invoke(h, null);
                    Near(boosted ? 960000 : 1200000, h.p_end_tick - h.p_start_tick);
                    Equal(1, h.babies.Count);
                });
        }
    }

    /// <summary>只提供已生成的婴儿，保留 Create/PostMake/Add/Initialize/原始时长计算与写入。</summary>
    private static bool GeneratePregnancyBabiesBoundary(Hediff_BasePregnancy __instance)
    { __instance.babies = new() { NewPawn() }; return false; }
    /// <summary>遗传来源在本专项中固定，不验证独立的种族遗传选择系统。</summary>
    private static bool DnaParentBoundary(ref Hediff_BasePregnancy.DnaGivingParent __result)
    { __result = Hediff_BasePregnancy.DnaGivingParent.Mother; return false; }
    /// <summary>原版妊娠分支仍真实创建/加入/设置父母；遗传内容作为外部边界。</summary>
    private static bool PregnancyGenesBoundary(ref GeneSet __result) { __result = new GeneSet(); return false; }
}
