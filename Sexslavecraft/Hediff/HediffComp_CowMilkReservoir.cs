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

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref charge, "charge", CustomProps.initialCharge);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            charge = Mathf.Max(charge, CustomProps.initialCharge);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!Pawn.IsHashIntervalTick(600)) return;

            if (HumanCattleBridgeUtility.IsLoaded)
            {
                charge = 0f;
                return;
            }

            CompSexSlaveTraining trainingComp = Pawn?.TryGetComp<CompSexSlaveTraining>();
            if (trainingComp != null && !trainingComp.milkProductionEnabled) return;

            TickChargeGrowth();
            TransferToBreastIfNeeded();
        }

        public float AddCharge(float amount)
        {
            if (amount <= 0f) return 0f;
            float oldCharge = charge;
            charge = Mathf.Min(charge + amount, CustomProps.fullChargeAmount);
            return charge - oldCharge;
        }

        public void SetCharge(float value)
        {
            charge = Mathf.Clamp(value, 0f, CustomProps.fullChargeAmount);
        }

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

            Traverse.Create(comp).Field<float>("charge").Value = breastCharge + transferAmount;
            charge -= transferAmount;
        }
    }
}
