using HarmonyLib;
using RimWorld;
using rjw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    // 🔥 拦截 RJW 每次性行为结束（高潮/完事）的终极结算中心
    [HarmonyPatch(typeof(SexUtility), nameof(SexUtility.ProcessSex))]
    public static class Patch_ProcessSex_LactationBoost
    {
        public static void Postfix(SexProps props)
        {
            if (props == null || props.pawn == null || props.partner == null) return;
            if (props.dictionaryKey == null) return; // dictionaryKey 为 null 时跳过，避免后续访问 interaction/resolved 导致 NRE

            // 0. 检查是否为动物×动物（双方都是动物） - 跳过处理
            if (xxx.is_animal(props.pawn) && xxx.is_animal(props.partner)) return;

            // 1. 判断是否为“精液直饮”体位 (内部注入，无需消耗宿主营养)
            bool isDirectFeed = (props.sexType == xxx.rjwSextype.Vaginal ||
                                 props.sexType == xxx.rjwSextype.Oral ||
                                 props.sexType == xxx.rjwSextype.Anal);

            // 2. 获取本次性行为的评分
            float score;
            bool isHumanToAnimal = (xxx.is_human(props.pawn) && xxx.is_animal(props.partner)) ||
                                   (xxx.is_animal(props.pawn) && xxx.is_human(props.partner));
            
            if (isHumanToAnimal)
            {
                // 兽交使用固定评分值 50 分（对应 7.5% 加速）
                score = 50f;
            }
            else
            {
                // 正常性行为使用 ConditioningUtility 评分
                score = ConditioningUtility.GetScore(props.pawn, props.partner);
            }

            // 3. 计算基础催化增量 (Boost) 
            // 评分越高，高潮越猛，最高瞬间拔高 30% (0.3) 的进度
            float baseBoost = Mathf.Clamp((score / 100f) * 0.15f, 0.05f, 0.30f);

            // 4. 将催化剂打入双方体内 (分别计算营养扣除)
            ApplyBoostWithLogic(props, props.pawn, baseBoost, isDirectFeed);
            ApplyBoostWithLogic(props, props.partner, baseBoost, isDirectFeed);
        }

        // ==============================================================
        // 核心注入引擎 (包含营养度等价交换算法)
        // ==============================================================
        private static void ApplyBoostWithLogic(SexProps props, Pawn pawn, float boostAmount, bool isDirectFeed)
        {
            if (pawn == null || pawn.health == null) return;

            if (!BusSpecializationUtility.CanAccelerateMilkProduction(pawn)) return;
            bool useDoopLactation = HumanCattleBridgeUtility.IsLoaded;

            float stageMultiplier = BusSpecializationUtility.GetCowLactationBoostMultiplier(pawn);
            if (stageMultiplier <= 0f) return;

            float finalBoost = boostAmount * stageMultiplier;
            float nutritionDiscountFactor = GetNutritionDiscountFactorFromEjaculation(props, pawn, isDirectFeed);
            float expectedMilk = 0f;
            float overflowMilk = 0f;

            // -----------------------------------------------------------------
            // A. 催熟自定义的纳米液 (SSC_Purple_HyperLactation)
            // -----------------------------------------------------------------
            Hediff hyperLactation = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Purple_HyperLactation"));
            if (hyperLactation != null)
            {
                float actualBoost = finalBoost;
                var comp = hyperLactation.TryGetComp<HediffComp_SeverityProduct>();

                if (comp != null && !isDirectFeed)
                {
                    // 非直饮体位：强行抽干宿主胃里的食物换取加速
                    if (pawn.needs != null && pawn.needs.food != null)
                    {
                        // 这波加速需要多少营养 = 加速比例 * (每天消耗营养 / 每天基础产出)
                        float nutritionRequired = finalBoost * (comp.Props.nutritionPerDay / comp.Props.severityPerDay) * nutritionDiscountFactor;

                        if (pawn.needs.food.CurLevel >= nutritionRequired)
                        {
                            pawn.needs.food.CurLevel -= nutritionRequired; // 全额扣除
                        }
                        else
                        {
                            // 营养不足：榨干最后一点饭，按比例折算加速
                            float availableFood = pawn.needs.food.CurLevel;
                            actualBoost = availableFood / ((comp.Props.nutritionPerDay / comp.Props.severityPerDay) * nutritionDiscountFactor);
                            pawn.needs.food.CurLevel = 0f; // 彻底饿扁
                        }
                    }
                }

                hyperLactation.Severity += actualBoost; // 注入最终折算的进度
            }

            // -----------------------------------------------------------------
            // B. 催熟永久泌乳期 (SSC_Lactating_SubState)
            // -----------------------------------------------------------------
            if (!useDoopLactation)
            {
                Hediff subLactating = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Lactating_SubState"));
                if (subLactating != null)
                {
                    // 🔥 这里已经精准换成了我们刚写的专属类 HediffComp_PermanentLactating
                    var lactatingComp = subLactating.TryGetComp<HediffComp_PermanentLactating>();
                    if (lactatingComp != null)
                    {
                        float actualBoost = finalBoost;

                        if (!isDirectFeed && pawn.needs != null && pawn.needs.food != null)
                        {
                            // 原版母乳强制设定：充满一次母乳(100%) 约消耗 0.5 营养度
                            float fakeNutritionCost = finalBoost * 0.5f * nutritionDiscountFactor;
                            if (pawn.needs.food.CurLevel >= fakeNutritionCost)
                            {
                                pawn.needs.food.CurLevel -= fakeNutritionCost;
                            }
                            else
                            {
                                actualBoost = pawn.needs.food.CurLevel / (0.5f * nutritionDiscountFactor);
                                pawn.needs.food.CurLevel = 0f;
                            }
                        }

                        expectedMilk += actualBoost;

                        // ⚠️ 黑客手段：因为基类 charge 是私有的，所以依然必须用 Traverse 暴力修改
                        float currentCharge = Traverse.Create(lactatingComp).Field<float>("charge").Value;
                        float targetCharge = currentCharge + actualBoost;

                        // 新的充盈度不能超过设定的 CustomProps.fullChargeAmount 上限
                        float newCharge = Mathf.Min(targetCharge, lactatingComp.CustomProps.fullChargeAmount);
                        overflowMilk += Mathf.Max(0f, targetCharge - lactatingComp.CustomProps.fullChargeAmount);

                        // 把催熟的奶量塞回去
                        Traverse.Create(lactatingComp).Field<float>("charge").Value = newCharge;
                    }
                }

                HediffComp_CowMilkReservoir reservoirComp = BusSpecializationUtility.GetCowMilkReservoirComp(pawn);
                if (reservoirComp != null)
                {
                    expectedMilk += finalBoost;
                    float addedToReservoir = reservoirComp.AddCharge(finalBoost);
                    overflowMilk += Mathf.Max(0f, finalBoost - addedToReservoir);
                }

                TrySpawnOverflowMilk(pawn, overflowMilk);
                BusSpecializationUtility.TryGainCowProgressFromMilkAmount(pawn, expectedMilk);
            }
        }

        private static void TrySpawnOverflowMilk(Pawn pawn, float overflowMilk)
        {
            if (pawn?.MapHeld == null || overflowMilk <= 0f) return;

            ThingDef milkDef = DefDatabase<ThingDef>.GetNamedSilentFail("EM_HumanMilk")
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("Milk");
            if (milkDef == null) return;

            float nutritionPerItem = milkDef.GetStatValueAbstract(StatDefOf.Nutrition);
            if (nutritionPerItem <= 0f) return;

            int stackCount = Mathf.FloorToInt(overflowMilk / nutritionPerItem);
            if (stackCount <= 0) return;

            Thing milkThing = ThingMaker.MakeThing(milkDef);
            milkThing.stackCount = stackCount;
            GenPlace.TryPlaceThing(milkThing, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
        }

        private static float GetNutritionDiscountFactorFromEjaculation(SexProps props, Pawn targetPawn, bool isDirectFeed)
        {
            if (isDirectFeed) return 0.30f;

            float cumAmount = 0f;
            cumAmount = Mathf.Max(cumAmount, GetFloatByName(props, "cumAmount"));
            cumAmount = Mathf.Max(cumAmount, GetFloatByName(props, "semenAmount"));
            cumAmount = Mathf.Max(cumAmount, GetFloatByName(props, "fluidAmount"));

            bool hasInternalCum = GetBoolByName(props, "isCoreLovin") || GetBoolByName(props, "isCoreSex") || GetBoolByName(props, "isReceiverInseminated");
            if (cumAmount <= 0f && hasInternalCum)
            {
                cumAmount = 0.35f;
            }

            if (cumAmount <= 0f) return 1f;
            if (cumAmount >= 1f) return 0.20f;
            if (cumAmount >= 0.5f) return 0.45f;
            return 0.70f;
        }

        private static float GetFloatByName(object target, string memberName)
        {
            if (target == null) return 0f;
            try
            {
                object value = Traverse.Create(target).Field(memberName).GetValue();
                if (value is float f) return f;
                if (value is double d) return (float)d;
            }
            catch
            {
            }

            try
            {
                object value = Traverse.Create(target).Property(memberName).GetValue();
                if (value is float f) return f;
                if (value is double d) return (float)d;
            }
            catch
            {
            }

            return 0f;
        }

        private static bool GetBoolByName(object target, string memberName)
        {
            if (target == null) return false;
            try
            {
                object value = Traverse.Create(target).Field(memberName).GetValue();
                if (value is bool b) return b;
            }
            catch
            {
            }

            try
            {
                object value = Traverse.Create(target).Property(memberName).GetValue();
                if (value is bool b) return b;
            }
            catch
            {
            }

            return false;
        }
    }
}
