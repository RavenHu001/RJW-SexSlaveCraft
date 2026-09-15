using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed, failed;
    private static HediffCompProperties_PermanentLactating properties;

    /// <summary>使用实际 XML 参数验证生产组件，覆盖调用分段、停产和存档边界。</summary>
    private static int Main(string[] args)
    {
        string root = args.Length == 0 ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")) : Path.GetFullPath(args[0]);
        XElement definition = XDocument.Load(Path.Combine(root, "Defs/HediffDefs/HediffOfTrain.xml")).Root.Elements("HediffDef")
            .Single(def => (string)def.Element("defName") == "SSC_Lactating_SubState");
        XElement config = definition.Element("comps").Elements("li").Single(comp => (string)comp.Attribute("Class") == "SexSlaveCraft.HediffCompProperties_PermanentLactating");
        properties = new HediffCompProperties_PermanentLactating
        {
            fullChargeAmount = Read(config, "fullChargeAmount"), initialCharge = Read(config, "initialCharge"),
            ticksToFullCharge = (int)Read(config, "ticksToFullCharge"), nutritionPerDay = Read(config, "nutritionPerDay")
        };
        foreach (int rate in new[] { 1, 2, 3, 4, 5, 15, 60, 100, 250 })
            Run($"调用间隔 {rate}：配置时长内充满且营养消耗正确", () => FillAtRate(rate));
        Run("50 + 15 tick 跨过结算点时保留全部时间", MixedIntervals);
        Run("不足 60 tick 时不提前生产或扣营养", WaitForBatch);
        Run("关闭再开启不补算关闭前残余及停产时间", DisabledResetsTime);
        Run("满容量不积攒时间，挤奶后重新计时", FullResetsTime);
        Run("外部补满清理之前未结算时间", ExternalFillResetsTime);
        Run("食物不足时按比例生产", PartialFood);
        Run("饥饿期间不积攒可在补食后兑现的产量", StarvationDoesNotBankTime);
        Run("接近满容量时只为实际产量扣营养", CapacityLimitsNutrition);
        Run("没有食物需求的角色仍按时间生产", NoFoodNeed);
        Run("保存读取保留奶量及不足一批的时间", SaveRoundTrip);
        Run("旧存档缺少累计字段时从零累计", LegacySave);
        Run("HumanCattle 接管不运行 SSC 生产或遗留累计时间", HumanCattleTakeover);
        Run("零或负 delta 不制造生产时间", NonPositiveDelta);
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>读取实际定义中的生产参数，避免测试与 XML 分离。</summary>
    private static float Read(XElement config, string key) => float.Parse(config.Element(key).Value, CultureInfo.InvariantCulture);

    /// <summary>隔离可选依赖及存档状态，并汇总失败。</summary>
    private static void Run(string name, Action test)
    {
        HumanCattleBridgeUtility.IsLoaded = false;
        HumanCattleBridgeUtility.Calls = 0;
        Scribe.mode = LoadSaveMode.Inactive;
        Scribe.Data.Clear();
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }

    /// <summary>创建从实际 XML 初始奶量开始的角色；偏移避免依赖固定哈希相位。</summary>
    private static HediffComp_PermanentLactating Create()
    {
        var comp = new HediffComp_PermanentLactating { props = properties, parent = new Hediff { pawn = new Pawn { HashOffset = 17 } } };
        comp.CompPostMake();
        return comp;
    }

    /// <summary>传入真实经过的 tick 数；每日普通饥饿、额外催熟和储奶槽不在模型中。</summary>
    private static void Tick(HediffComp_PermanentLactating comp, int delta)
    {
        comp.Pawn.Tick += delta;
        float adjustment = 0;
        comp.CompPostTick(ref adjustment);
        comp.CompPostTickInterval(ref adjustment, delta);
        Near(0f, adjustment, "永久泌乳不应衰减", 0f);
    }

    /// <summary>验证配置充满时长与调用分段无关，且额外营养消耗相同。</summary>
    private static void FillAtRate(int rate)
    {
        var comp = Create();
        for (int elapsed = 0; elapsed < properties.ticksToFullCharge; elapsed += rate)
            Tick(comp, Math.Min(rate, properties.ticksToFullCharge - elapsed));
        Near(properties.fullChargeAmount, comp.CurrentCharge, "充满时长");
        Near(1f - properties.nutritionPerDay * properties.ticksToFullCharge / 60000f, comp.Pawn.needs.food.CurLevel, "完整一批营养", 0.00003f);
    }

    /// <summary>间隔切换跨过 60 时，所有已过去的时间都必须被结算。</summary>
    private static void MixedIntervals()
    {
        var comp = Create();
        for (int i = 0; i < 100; i++) { Tick(comp, 50); Tick(comp, 15); }
        ExpectProduction(comp, 6500);
    }

    /// <summary>保留分批执行，而非恢复成每次调用都生产。</summary>
    private static void WaitForBatch()
    {
        var comp = Create();
        Tick(comp, 59);
        ExpectProduction(comp, 0);
        Tick(comp, 1);
        ExpectProduction(comp, 60);
    }

    /// <summary>停产跨越任意时长后只能结算重新开启的生产时间。</summary>
    private static void DisabledResetsTime()
    {
        var comp = Create(); Tick(comp, 50);
        comp.Pawn.Training.milkProductionEnabled = false; Tick(comp, 600);
        comp.Pawn.Training.milkProductionEnabled = true; Tick(comp, 10);
        ExpectProduction(comp, 0);
        Tick(comp, 50); ExpectProduction(comp, 60);
    }

    /// <summary>模拟满奶等待后由原版消费入口取奶，防止满容量期间积攒下一批。</summary>
    private static void FullResetsTime()
    {
        var comp = Create(); comp.CurrentCharge = properties.fullChargeAmount;
        Tick(comp, 600); Tick(comp, 50);
        comp.GreedyConsume(properties.fullChargeAmount);
        Tick(comp, 10); ExpectProduction(comp, 0);
        Tick(comp, 50); ExpectProduction(comp, 60);
    }

    /// <summary>其他产量来源补满后清理原先半批时间。</summary>
    private static void ExternalFillResetsTime()
    {
        var comp = Create(); Tick(comp, 50);
        comp.CurrentCharge = properties.fullChargeAmount; Tick(comp, 1);
        comp.GreedyConsume(properties.fullChargeAmount);
        Tick(comp, 10); ExpectProduction(comp, 0);
        Tick(comp, 50); ExpectProduction(comp, 60);
    }

    /// <summary>不足一整批的食物只能换取相同比例的奶量。</summary>
    private static void PartialFood()
    {
        var comp = Create(); comp.Pawn.needs.food.CurLevel = properties.nutritionPerDay * 30f / 60000f;
        Tick(comp, 60);
        Near(properties.fullChargeAmount * 30f / properties.ticksToFullCharge, comp.CurrentCharge, "半批产量");
        Near(0f, comp.Pawn.needs.food.CurLevel, "食物归零");
    }

    /// <summary>缺粮批次结算完毕后不得在补食时补发。</summary>
    private static void StarvationDoesNotBankTime()
    {
        var comp = Create(); comp.Pawn.needs.food.CurLevel = 0f; Tick(comp, 600);
        Near(0f, comp.CurrentCharge, "饥饿停产");
        comp.Pawn.needs.food.CurLevel = 1f; Tick(comp, 60); ExpectProduction(comp, 60);
    }

    /// <summary>容量只剩半批时，奶量和营养必须一起截断。</summary>
    private static void CapacityLimitsNutrition()
    {
        var comp = Create(); comp.CurrentCharge = properties.fullChargeAmount * (1f - 30f / properties.ticksToFullCharge);
        Tick(comp, 60);
        Near(properties.fullChargeAmount, comp.CurrentCharge, "容量上限");
        Near(1f - properties.nutritionPerDay * 30f / 60000f, comp.Pawn.needs.food.CurLevel, "仅扣有效生产营养");
    }

    /// <summary>保留无食物需求或无 needs 跟踪器时的生产分支。</summary>
    private static void NoFoodNeed()
    {
        foreach (bool noTracker in new[] { false, true })
        {
            var comp = Create();
            if (noTracker) comp.Pawn.needs = null; else comp.Pawn.needs.food = null;
            Tick(comp, 60);
            Near(properties.fullChargeAmount * 60f / properties.ticksToFullCharge, comp.CurrentCharge, "无食物需求生产");
        }
    }

    /// <summary>保留父类奶量与本组件未结算时间，避免频繁存读档丢失进度。</summary>
    private static void SaveRoundTrip()
    {
        var comp = Create(); comp.CurrentCharge = 0.02f; Tick(comp, 50);
        Scribe.mode = LoadSaveMode.Saving; comp.CompExposeData();
        var restored = Load(); Tick(restored, 10);
        Near(0.02f + properties.fullChargeAmount * 60f / properties.ticksToFullCharge, restored.CurrentCharge, "读档接续奶量");
        Near(1f - properties.nutritionPerDay * 60f / 60000f, restored.Pawn.needs.food.CurLevel, "读档接续营养");
    }

    /// <summary>已有存档只有 charge 时不补发历史漏算产量。</summary>
    private static void LegacySave()
    {
        Scribe.Data["charge"] = 0.02f;
        var comp = Load(); Tick(comp, 59);
        Near(0.02f, comp.CurrentCharge, "旧存档奶量保持");
        Tick(comp, 1);
        Near(0.02f + properties.fullChargeAmount * 60f / properties.ticksToFullCharge, comp.CurrentCharge, "旧存档重新累计");
    }

    /// <summary>模拟基础字段读取与 PostLoadInit 阶段。</summary>
    private static HediffComp_PermanentLactating Load()
    {
        var comp = Create();
        Scribe.mode = LoadSaveMode.LoadingVars; comp.CompExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit; comp.CompExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        return comp;
    }

    /// <summary>接管分支必须提前退出；切换仅用于检查本组件累计状态的隔离。</summary>
    private static void HumanCattleTakeover()
    {
        var comp = Create(); Tick(comp, 50);
        HumanCattleBridgeUtility.IsLoaded = true; Tick(comp, 600);
        if (HumanCattleBridgeUtility.Calls != 1) throw new InvalidOperationException("未调用 HumanCattle 接管入口。");
        ExpectProduction(comp, 0);
        HumanCattleBridgeUtility.IsLoaded = false; Tick(comp, 10); ExpectProduction(comp, 0);
        Tick(comp, 50); ExpectProduction(comp, 60);
    }

    /// <summary>非正间隔不应被改成一个虚构的有效 tick。</summary>
    private static void NonPositiveDelta()
    {
        var comp = Create(); Tick(comp, 59);
        Tick(comp, 0); Tick(comp, -1); ExpectProduction(comp, 0);
        Tick(comp, 1); ExpectProduction(comp, 60);
    }

    /// <summary>从配置推导期望产量与营养，不复写生产分支。</summary>
    private static void ExpectProduction(HediffComp_PermanentLactating comp, int ticks)
    {
        Near(properties.fullChargeAmount * ticks / properties.ticksToFullCharge, comp.CurrentCharge, "按实际时间产奶");
        Near(1f - properties.nutritionPerDay * ticks / 60000f, comp.Pawn.needs.food.CurLevel, "按实际时间扣营养", 0.00003f);
    }

    /// <summary>允许单精度累计误差，但拒绝丢 tick、重复计时和无效数值。</summary>
    private static void Near(float expected, float actual, string message, float tolerance = 0.000002f)
    {
        if (!float.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message}：期望 {expected:R}，实际 {actual:R}");
    }
}
