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

        public override void CompPostMake()
        {
            CurrentCharge = Mathf.Min(CustomProps?.initialCharge ?? FullChargeAmount, FullChargeAmount);
            Traverse.Create(this).Field<int>("ticksSinceLastSuckled").Value = 0;
        }

        // 🔥 我们重写 (Override) 原版的每帧跳动逻辑
        public override void CompPostTick(ref float severityAdjustment)
        {
            Traverse.Create(this).Field<int>("ticksSinceLastSuckled").Value = 0;
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (HumanCattleBridgeUtility.IsLoaded)
            {
                HumanCattleBridgeUtility.ApplyDoopMode(this);
                return;
            }

            CompSexSlaveTraining trainingComp = Pawn?.TryGetComp<CompSexSlaveTraining>();
            if (trainingComp != null && !trainingComp.milkProductionEnabled)
            {
                Traverse.Create(this).Field<int>("ticksSinceLastSuckled").Value = 0;
                return;
            }

            // ⚠️ 绝不调用 base.CompPostTick / CompPostTickInterval！
            // 原版会在这里计算“如果10天不吸奶，就删除该状态”。我们直接抛弃它，彻底锁死！

            int safeDelta = Mathf.Max(1, delta);

            // 采用 TPS 极致优化：每 60 tick 结算一次；更大的 interval 也按真实 delta 结算
            if (safeDelta >= 60 || Pawn.IsHashIntervalTick(60))
            {
                float currentCharge = CurrentCharge;
                float fullAmount = FullChargeAmount;

                if (currentCharge < fullAmount)
                {
                    float amountToGain = (fullAmount / TicksToFullCharge) * safeDelta;

                    float nutritionCost = (NutritionPerDay / 60000f) * safeDelta;

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
