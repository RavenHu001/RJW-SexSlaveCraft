using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public class CompProperties_AbilityPetFoxEmergencyTend : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityPetFoxEmergencyTend()
        {
            compClass = typeof(CompAbilityEffect_PetFoxEmergencyTend);
        }
    }

    public class CompAbilityEffect_PetFoxEmergencyTend : CompAbilityEffect
    {
        private static bool IsPatientOnSameMap(Pawn caster, Pawn patient)
        {
            return caster?.Spawned == true && patient?.Spawned == true &&
                patient != caster && !patient.Dead && patient.RaceProps.Humanlike &&
                patient.Faction == Faction.OfPlayer && patient.Map == caster.Map;
        }

        private static bool IsTendableWound(Hediff hediff)
        {
            return (hediff is Hediff_Injury || hediff is Hediff_MissingPart) && hediff.TendableNow();
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Pawn patient = target.Pawn;
            string reason;
            if (!IsPatientOnSameMap(caster, patient))
                reason = "SSC_PetFoxEmergencyTend_InvalidTarget";
            else if (!patient.health.hediffSet.hediffs.Any(IsTendableWound))
                reason = "SSC_PetFoxEmergencyTend_NoWounds";
            else if (!caster.CanReach(target, PathEndMode.Touch, Danger.Deadly))
                reason = "SSC_PetFoxEmergencyTend_UnreachableTarget";
            else
                return base.Valid(target, throwMessages);

            if (throwMessages) Messages.Message(reason.Translate(), caster, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 原版队列和接近过程都会调用，接触距离由实际施放入口检查。
            Pawn patient = target.Pawn;
            return IsPatientOnSameMap(parent.pawn, patient) && patient.health.hediffSet.hediffs.Any(IsTendableWound);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent.pawn;
            Pawn patient = target.Pawn;
            if (!IsPatientOnSameMap(caster, patient)) return;

            // 包扎回调可能修改健康列表。只处理快照中仍在患者身上且可包扎的伤口。
            var hediffs = patient.health.hediffSet.hediffs;
            var wounds = hediffs.Where(IsTendableWound).ToArray();
            float quality = TendUtility.CalculateBaseTendQuality(caster, patient, null);
            int treated = 0;
            foreach (Hediff wound in wounds)
                if (hediffs.Contains(wound) && IsTendableWound(wound))
                    wound.Tended(quality, 0.70f, treated++);
        }
    }
}
