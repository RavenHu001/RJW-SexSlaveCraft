// AI 辅助生成：直接执行实际储液组件；这里只提供引擎时钟、需求及反射字段边界。
using System;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>验证原布尔值仍控制普通生产与牛储液，不改变暂停、现存奶量和外部接管规则。</summary>
    private static void MilkToggleConsumerTests()
    {
        Run("缺训练组件仍按原默认生产", () =>
        {
            var comp = Create(); comp.Pawn.Training = null; Tick(comp, 60); ExpectProduction(comp, 60);
        });
        Run("关闭泌乳保留现存奶量且仍能挤取", () =>
        {
            var comp = Create(); Tick(comp, 600); float amount = comp.CurrentCharge, food = comp.Pawn.needs.food.CurLevel;
            comp.Pawn.Training.milkProductionEnabled = false; Tick(comp, 1200);
            Near(amount, comp.CurrentCharge, "停产保留奶量"); Near(food, comp.Pawn.needs.food.CurLevel, "停产不扣营养");
            Near(amount / 2, comp.GreedyConsume(amount / 2), "原挤取入口不受生产开关阻断");
        });
        Run("牛储液关闭保留现量、不扣营养也不转入乳房，重开恢复", () =>
        {
            var milk = Create(); milk.parent.def = SSCDefOf.SSC_Lactating_SubState;
            milk.parent.comps.Add(milk); milk.Pawn.health.hediffSet.hediffs.Add(milk.parent);
            // 把实际两种生产组件挂在同一身体，才能验证关闭时不发生原储液到乳房的转移。
            var reservoir = Reservoir(milk.Pawn); reservoir.SetCharge(.1f);
            milk.Pawn.Training.milkProductionEnabled = false; float food = milk.Pawn.needs.food.CurLevel;
            ReservoirTick(reservoir); Near(.1f, reservoir.CurrentCharge, "储液保留");
            Near(0, milk.CurrentCharge, "关闭不转奶"); Near(food, milk.Pawn.needs.food.CurLevel, "关闭不扣营养");
            milk.Pawn.Training.milkProductionEnabled = true; ReservoirTick(reservoir);
            Near(.114f, milk.CurrentCharge, "恢复原600 tick产量与转奶"); Near(0, reservoir.CurrentCharge, "转奶扣储量");
            Near(food - .0045f, milk.Pawn.needs.food.CurLevel, "恢复原营养消耗");
        });
        Run("牛储液无训练组件仍生产，满量不扣营养", () =>
        {
            var p = new Pawn { Training = null }; var reservoir = Reservoir(p); ReservoirTick(reservoir);
            Near(.014f, reservoir.CurrentCharge, "缺组件默认生产");
            reservoir.SetCharge(.35f); float food = p.needs.food.CurLevel; ReservoirTick(reservoir);
            Near(.35f, reservoir.CurrentCharge, "满容量保持"); Near(food, p.needs.food.CurLevel, "满量不扣营养");
        });
        Run("HumanCattle 接管仍先于牛储液开关并清空 SSC 储量", () =>
        {
            var reservoir = Reservoir(new Pawn()); reservoir.SetCharge(.2f);
            reservoir.Pawn.Training.milkProductionEnabled = false; HumanCattleBridgeUtility.IsLoaded = true;
            ReservoirTick(reservoir); Near(0, reservoir.CurrentCharge, "保留接管优先级");
        });
        Run("关闭牛储液仍保存读取原 charge 字段", () =>
        {
            var reservoir = Reservoir(new Pawn()); reservoir.SetCharge(.2f); reservoir.Pawn.Training.milkProductionEnabled = false;
            // 本宿主仅做字段字典往返；真实 XML／交叉引用不在此模型内宣称通过。
            Scribe.mode = LoadSaveMode.Saving; reservoir.CompExposeData();
            var restored = Reservoir(new Pawn()); Scribe.mode = LoadSaveMode.LoadingVars; restored.CompExposeData();
            Near(.2f, restored.CurrentCharge, "原储液字段未迁移");
        });
    }

    /// <summary>创建实际储液生产组件，参数采用既有默认值，不模拟储液算法。</summary>
    private static HediffComp_CowMilkReservoir Reservoir(Pawn pawn)
        => new HediffComp_CowMilkReservoir { props = new HediffCompProperties_CowMilkReservoir(), parent = new Hediff { pawn = pawn } };

    /// <summary>推进到原600 tick相位，调用真实组件结算入口。</summary>
    private static void ReservoirTick(HediffComp_CowMilkReservoir reservoir)
    {
        reservoir.Pawn.Tick = -reservoir.Pawn.HashOffset; float severity = 0; reservoir.CompPostTick(ref severity);
    }
}
