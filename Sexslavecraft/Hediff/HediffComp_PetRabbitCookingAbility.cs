using Verse;

namespace SexSlaveCraft
{
    public class HediffComp_PetRabbitCookingAbility : HediffComp_GiveAbility
    {
        internal static HediffDef EffectDef =>
            DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetRabbitCarefulCooking");

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            Pawn pawn = parent.pawn;
            if (PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit)) return;

            HediffDef effectDef = EffectDef;
            if (effectDef == null) return;
            Hediff effect = pawn?.health?.hediffSet?.GetFirstHediffOfDef(effectDef);
            if (effect != null) pawn.health.RemoveHediff(effect);
        }
    }
}
