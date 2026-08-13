using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    // ==============================================================
    // 1. XML 参数定义
    // ==============================================================
    public class HediffCompProperties_SeverityProduct : HediffCompProperties
    {
        public ThingDef thingToSpawn;       // 产出什么物品
        public int spawnCount = 10;         // 每次满时产出的固定数量
        public float severityPerDay = 0.2f; // 每天自然增加多少严重度 (产出速度)

        // 🔥 新增：每天需要消耗多少营养度 (0.15 相当于小半顿饭)
        public float nutritionPerDay = 0.15f;

        public HediffCompProperties_SeverityProduct()
        {
            this.compClass = typeof(HediffComp_SeverityProduct);
        }
    }

    // ==============================================================
    // 2. 逻辑组件 (TPS 极致优化 + 营养消耗机制)
    // ==============================================================
    public class HediffComp_SeverityProduct : HediffComp
    {
        public HediffCompProperties_SeverityProduct Props => (HediffCompProperties_SeverityProduct)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 🔥 TPS 优化核心：每 60 帧 (现实1秒) 才执行一次真正的判断和扣除！
            if (Pawn.IsHashIntervalTick(60))
            {
                // 1. 如果进度还没满 (1.0)
                if (parent.Severity < 1.0f)
                {
                    // 注意：因为我们 60 帧才算一次，所以增量和消耗量都要直接乘以 60
                    float severityGain = (Props.severityPerDay / 60000f) * 60f;
                    float nutritionCost = (Props.nutritionPerDay / 60000f) * 60f;

                    // 营养度扣除与停工逻辑
                    if (Pawn.needs != null && Pawn.needs.food != null)
                    {
                        // 只有当小人目前的饱食度足够支付这 1 秒的消耗时，才推进进度
                        if (Pawn.needs.food.CurLevel >= nutritionCost)
                        {
                            Pawn.needs.food.CurLevel -= nutritionCost; // 抽走营养
                            parent.Severity += severityGain;           // 增加产出进度 (直接加给母体)
                        }
                        else
                        {
                            // 营养不良/饥饿时，彻底停止分泌 (不加 Severity)
                        }
                    }
                    else
                    {
                        // 如果这生物没有饱食度条（比如特殊的机械体），直接白嫖进度
                        parent.Severity += severityGain;
                    }
                }

                // 2. 检测是否满了 (>= 1.0)
                if (parent.Severity >= 1.0f)
                {
                    // 强制把严重度重置归零
                    parent.Severity = 0f;

                    // 3. 判断小人当前的位置并生成物品
                    if (Pawn.Map != null)
                    {
                        // 在正常地图中 -> 掉落在脚下
                        SpawnProductOnMap();
                    }
                    else
                    {
                        // 不在正常地图中 -> 检查是否在远行队里
                        Caravan caravan = Pawn.GetCaravan();
                        if (caravan != null)
                        {
                            SpawnProductInCaravan(caravan);
                        }
                    }
                }
            }
        }

        // --- 可视化面板进度 ---
        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (Props.thingToSpawn == null) return base.CompLabelInBracketsExtra;

                string progress = (parent.Severity * 100f).ToString("F0") + "%";

                // 提示判定：用除法看看当前饱食度还能不能撑过 1 秒
                if (Pawn.needs?.food != null && Pawn.needs.food.CurLevel < (Props.nutritionPerDay / 60000f * 60f))
                {
                    return Strings.Produce_ProgressStalled(progress);
                }

                return Strings.Produce_Progress(progress);
            }
        }

        // 内部辅助方法：在地图内生成
        private void SpawnProductOnMap()
        {
            if (Props.thingToSpawn == null) return;

            Thing thing = ThingMaker.MakeThing(Props.thingToSpawn);
            thing.stackCount = Props.spawnCount;

            GenPlace.TryPlaceThing(thing, Pawn.Position, Pawn.Map, ThingPlaceMode.Near);
        }

        // 内部辅助方法：在远行队中生成
        private void SpawnProductInCaravan(Caravan caravan)
        {
            if (Props.thingToSpawn == null) return;

            Thing thing = ThingMaker.MakeThing(Props.thingToSpawn);
            thing.stackCount = Props.spawnCount;

            // 直接将物品加入远行队的物资库存中
            CaravanInventoryUtility.GiveThing(caravan, thing);
        }
    }

    // ==============================================================
    // 3. 铁壁防多开入口
    // ==============================================================
    public static class HyperLactationUtility
    {
        /// <summary>
        /// 安全地给小人添加催情液产出状态。
        /// 如果已经有了，则彻底无视（防止无限叠加）。
        /// </summary>
        public static void TryAddLactation(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return;

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Purple_HyperLactation");
            if (def == null) return;

            Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);

            if (existingHediff != null)
            {
                return; // 已有，铁壁拦截
            }
            else
            {
                Hediff newHediff = pawn.health.AddHediff(def);
                newHediff.Severity = 0.01f; // 极小的初始值，让面板立刻显示 1%
            }
        }
    }
}