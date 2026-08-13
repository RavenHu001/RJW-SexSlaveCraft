using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class GelatinizationHealthSanitizer
    {
        public static bool SanitizeMissingPartInjuries(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
            {
                return false;
            }

            bool changed = false;
            var hediffs = pawn.health.hediffSet.hediffs;

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_MissingPart missingPart &&
                    missingPart.lastInjury != null &&
                    missingPart.lastInjury.injuryProps == null)
                {
                    missingPart.lastInjury = HediffDefOf.SurgicalCut;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
