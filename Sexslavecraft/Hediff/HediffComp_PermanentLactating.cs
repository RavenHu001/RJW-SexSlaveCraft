using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace SexSlaveCraft
{
    // ==============================================================
    // 1. 自定义 XML 属性：继承原版产奶属性，额外加入营养消耗
    // ==============================================================
    public class HediffCompProperties_PermanentLactating : HediffCompProperties_Lactating
    {
        // 每天为了产奶需要消耗多少营养度 (0.3 相当于多吃半顿饭)
        public float nutritionPerDay = 0.3f;

        public HediffCompProperties_PermanentLactating()
        {
            this.compClass = typeof(HediffComp_PermanentLactating);
        }
    }

    // ==============================================================
    // 2. 核心逻辑组件：永久泌乳 + 饥饿榨取
    // ==============================================================
    public class HediffComp_PermanentLactating : HediffComp_Lactating
    {
        private const int ProductionTickInterval = 60;
        private int pendingProductionTicks;

        public HediffCompProperties_PermanentLactating CustomProps
        {
            get
            {
                if (props is HediffCompProperties_PermanentLactating typedProps)
                {
                    return typedProps;
                }

                return parent?.def?.comps?.OfType<HediffCompProperties_PermanentLactating>()?.FirstOrDefault();
            }
        }

        private float FullChargeAmount
        {
            get
            {
                if (props is HediffCompProperties_Chargeable chargeableProps)
                {
                    return chargeableProps.fullChargeAmount;
                }

                return CustomProps?.fullChargeAmount ?? 1f;
            }
        }

        private int TicksToFullCharge
        {
            get
            {
                if (props is HediffCompProperties_Chargeable chargeableProps)
                {
                    return chargeableProps.ticksToFullCharge;
                }

                return CustomProps?.ticksToFullCharge ?? 60000;
            }
        }

        private string LabelInBracketsFormat
        {
            get
            {
                if (props is HediffCompProperties_Chargeable chargeableProps)
                {
                    return chargeableProps.labelInBrackets;
                }

                return CustomProps?.labelInBrackets ?? "{0}";
            }
        }

        private float NutritionPerDay => CustomProps?.nutritionPerDay ?? 0.3f;

        public float CurrentCharge
        {
            get
            {
                float rawCharge = Traverse.Create(this).Field<float>("charge").Value;
                float clampedCharge = Mathf.Clamp(rawCharge, 0f, FullChargeAmount);
                if (!Mathf.Approximately(rawCharge, clampedCharge))
                {
                    Traverse.Create(this).Field<float>("charge").Value = clampedCharge;
                }

                return clampedCharge;
            }
            set => Traverse.Create(this).Field<float>("charge").Value = Mathf.Clamp(value, 0f, FullChargeAmount);
        }

        /// <summary>初始化奶量与生产计时，保留永久泌乳状态。</summary>
        public override void CompPostMake()
        {
            pendingProductionTicks = 0;
            CurrentCharge = Mathf.Min(CustomProps?.initialCharge ?? FullChargeAmount, FullChargeAmount);
            Traverse.Create(this).Field<int>("ticksSinceLastSuckled").Value = 0;
        }

        /// <summary>保留不足一批的生产时间；旧存档从零累计，不补发历史漏算产量。</summary>
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref pendingProductionTicks, "sscPendingProductionTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingProductionTicks = Mathf.Clamp(pendingProductionTicks, 0, ProductionTickInterval - 1);
            }
        }

        // 🔥 我们重写 (Override) 原版的每帧跳动逻辑
        public override void CompPostTick(ref float severityAdjustment)
        {
            Traverse.Create(this).Field<int>("ticksSinceLastSuckled").Value = 0;
        }

        /// <summary>累计有效生产时间后分批结算，产奶与营养消耗使用相同的实际经过时间。</summary>
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (HumanCattleBridgeUtility.IsLoaded)
            {
                pendingProductionTicks = 0;
                HumanCattleBridgeUtility.ApplyDoopMode(this);
                return;
            }

            CompSexSlaveTraining trainingComp = Pawn?.TryGetComp<CompSexSlaveTraining>();
            if (trainingComp != null && !trainingComp.milkProductionEnabled)
            {
                pendingProductionTicks = 0;
                Traverse.Create(this).Field<int>("ticksSinceLastSuckled").Value = 0;
                return;
            }

            // ⚠️ 绝不调用 base.CompPostTick / CompPostTickInterval！
            // 原版会在这里计算“如果10天不吸奶，就删除该状态”。我们直接抛弃它，彻底锁死！

            if (delta <= 0) return;

            // 基类提供直接读取奶量的属性；满容量检查无需在每次调用时反射查找字段。
            if (Charge >= FullChargeAmount)
            {
                pendingProductionTicks = 0;
                return;
            }

            // delta 仅包含本次调用的时间；必须累积被跳过的调用，不能依赖哈希时点补算。
            long elapsedTicks = (long)pendingProductionTicks + delta;
            if (elapsedTicks < ProductionTickInterval)
            {
                pendingProductionTicks = (int)elapsedTicks;
                return;
            }

            // 本批即使缺粮也已经结算，不能在补食后兑现停产期间的时间。
            pendingProductionTicks = 0;
            float currentCharge = CurrentCharge;
            float fullAmount = FullChargeAmount;

            if (currentCharge < fullAmount)
            {
                float amountToGain = (fullAmount / TicksToFullCharge) * elapsedTicks;

                float nutritionCost = (NutritionPerDay / 60000f) * elapsedTicks;

                // 分批生产可能跨过容量上限，只为实际能储存的奶量消耗营养。
                float remainingCapacity = fullAmount - currentCharge;
                if (amountToGain > remainingCapacity)
                {
                    nutritionCost *= remainingCapacity / amountToGain;
                    amountToGain = remainingCapacity;
                }

                if (Pawn.needs != null && Pawn.needs.food != null)
                {
                    if (Pawn.needs.food.CurLevel >= nutritionCost)
                    {
                        Pawn.needs.food.CurLevel -= nutritionCost;
                        currentCharge = Mathf.Min(currentCharge + amountToGain, fullAmount);
                    }
                    else if (nutritionCost > 0f && Pawn.needs.food.CurLevel > 0f)
                    {
                        float scale = Pawn.needs.food.CurLevel / nutritionCost;
                        Pawn.needs.food.CurLevel = 0f;
                        currentCharge = Mathf.Min(currentCharge + amountToGain * scale, fullAmount);
                    }
                }
                else
                {
                    currentCharge = Mathf.Min(currentCharge + amountToGain, fullAmount);
                }

                CurrentCharge = currentCharge;
            }

            Traverse.Create(this).Field<int>("ticksSinceLastSuckled").Value = 0;
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            HumanCattleBridgeUtility.CleanupBridge(Pawn);
        }

        // 修改 UI 面板提示，让玩家知道没饭吃了会停产
        public override string CompLabelInBracketsExtra
        {
            get
            {
                float currentCharge = CurrentCharge;
                float fullAmount = Mathf.Max(FullChargeAmount, 0.001f);
                string progress = (currentCharge / fullAmount * 100f).ToString("F0") + "%";

                CompSexSlaveTraining trainingComp = Pawn?.TryGetComp<CompSexSlaveTraining>();
                if (trainingComp != null && !trainingComp.milkProductionEnabled)
                {
                    return Strings.Lactating_Disabled(progress);
                }

                if (Pawn.needs?.food != null && Pawn.needs.food.CurLevel < (NutritionPerDay / 60000f * 60f))
                {
                    return Strings.Lactating_Stalled(progress);
                }

                return $"乳汁充盈度 {progress}";
            }
        }
    }
}
