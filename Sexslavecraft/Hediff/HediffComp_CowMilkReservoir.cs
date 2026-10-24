// AI 辅助维护：第二批仅统一原泌乳偏好读取，储量、结算与转奶规则保持既有实现。
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_CowMilkReservoir : HediffCompProperties
    {
        public float fullChargeAmount = 0.35f;
        public float initialCharge = 0f;
        public int ticksToFullCharge = 15000;
        public float nutritionPerDay = 0.45f;

        /// <summary>登记储液组件类型；容量、营养和充满时长仍采用既有配置。</summary>
        public HediffCompProperties_CowMilkReservoir()
        {
            compClass = typeof(HediffComp_CowMilkReservoir);
        }
    }

    public class HediffComp_CowMilkReservoir : HediffComp
    {
        private float charge;

        public HediffCompProperties_CowMilkReservoir CustomProps => (HediffCompProperties_CowMilkReservoir)props;

        public float CurrentCharge => charge;

        /// <summary>继续保存原 charge 字段，不把现存储量或生产选择搬入能力字典。</summary>
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref charge, "charge", CustomProps.initialCharge);
        }

        /// <summary>添加时只保证初始储量下限，不据开关另行清空或填满。</summary>
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            charge = Mathf.Max(charge, CustomProps.initialCharge);
        }

        /// <summary>每600 tick按原顺序处理接管、停产、增长及转奶；关闭时保留现量且不扣营养。</summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!Pawn.IsHashIntervalTick(600)) return;

            // 外部接管优先于 SSC 开关：继续清空 SSC 储液并退出，不声称可控制第三方产奶。
            if (HumanCattleBridgeUtility.IsLoaded)
            {
                charge = 0f;
                return;
            }

            // 共用原字段读取，缺训练组件仍默认生产；关闭会同时跳过增长和转奶。
            CompSexSlaveTraining trainingComp = Pawn?.TryGetComp<CompSexSlaveTraining>();
#if SSC_RW17 || SSC_ABILITY_TOGGLE_MILK
            if (!SSCAbilityToggleUtility.GetMilkProductionEnabled(trainingComp))
#else
            if (trainingComp != null && !trainingComp.milkProductionEnabled)
#endif
                return;

            TickChargeGrowth();
            TransferToBreastIfNeeded();
        }

        /// <summary>按原容量上限接收外部储量并返回实际增加量；生产开关不阻断原显式加量接口。</summary>
        public float AddCharge(float amount)
        {
            if (amount <= 0f) return 0f;
            float oldCharge = charge;
            charge = Mathf.Min(charge + amount, CustomProps.fullChargeAmount);
            return charge - oldCharge;
        }

        /// <summary>沿原上下限设置储量，供既有业务使用，不在此附加生产许可。</summary>
        public void SetCharge(float value)
        {
            charge = Mathf.Clamp(value, 0f, CustomProps.fullChargeAmount);
        }

        /// <summary>维持原600 tick产量和营养算法；满量不生产，缺粮按现有余粮比例增长。</summary>
        private void TickChargeGrowth()
        {
            if (charge >= CustomProps.fullChargeAmount) return;

            float amountToGain = (CustomProps.fullChargeAmount / Mathf.Max(1, CustomProps.ticksToFullCharge)) * 600f;
            float nutritionCost = (CustomProps.nutritionPerDay / 60000f) * 600f;

            if (Pawn.needs?.food != null)
            {
                if (Pawn.needs.food.CurLevel >= nutritionCost)
                {
                    Pawn.needs.food.CurLevel -= nutritionCost;
                    charge = Mathf.Min(charge + amountToGain, CustomProps.fullChargeAmount);
                }
                else if (nutritionCost > 0f && Pawn.needs.food.CurLevel > 0f)
                {
                    float scale = Pawn.needs.food.CurLevel / nutritionCost;
                    Pawn.needs.food.CurLevel = 0f;
                    charge = Mathf.Min(charge + amountToGain * scale, CustomProps.fullChargeAmount);
                }
            }
            else
            {
                charge = Mathf.Min(charge + amountToGain, CustomProps.fullChargeAmount);
            }
        }

        /// <summary>仅向当前原泌乳组件的剩余容量转入储量，不复制组件或建立第二套乳汁记录。</summary>
        private void TransferToBreastIfNeeded()
        {
            if (charge <= 0f || Pawn?.health?.hediffSet == null) return;

            Hediff lactating = Pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState);
            HediffComp_PermanentLactating comp = lactating?.TryGetComp<HediffComp_PermanentLactating>();
            if (comp == null) return;

            float breastCharge = Traverse.Create(comp).Field<float>("charge").Value;
            float breastCapacity = comp.CustomProps.fullChargeAmount;
            if (breastCharge >= breastCapacity) return;

            float transferAmount = Mathf.Min(charge, breastCapacity - breastCharge);
            if (transferAmount <= 0f) return;

            // 沿既有私有奶量字段扣增对应两端储量，开关校验已在本次 tick 的统一入口完成。
            Traverse.Create(comp).Field<float>("charge").Value = breastCharge + transferAmount;
            charge -= transferAmount;
        }
    }
}
