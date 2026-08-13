using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class PersonalityGelUtility
    {
        public static ThingDef GetPersonalityGelDefForPawn(Pawn pawn)
        {
            float severity = SSCBondUtility.GetChain(pawn)?.Severity ?? 0f;
            if (severity >= 0.8f) return SSCDefOf.SSC_PS_P_U;
            if (severity >= 0.5f) return SSCDefOf.SSC_PS_U;
            if (severity >= 0.3f) return SSCDefOf.SSC_PS_P;
            return SSCDefOf.SSC_PersonalitySlime;
        }

        public static bool IsSupportedPersonalityGel(ThingDef def)
        {
            return def == SSCDefOf.SSC_PersonalitySlime
                || def == SSCDefOf.SSC_PS_P
                || def == SSCDefOf.SSC_PS_U
                || def == SSCDefOf.SSC_PS_P_U
                || def == SSCDefOf.SSC_PersonalitySlime_Edited
                || def == SSCDefOf.SSC_PS_P_Edited
                || def == SSCDefOf.SSC_PS_U_Edited
                || def == SSCDefOf.SSC_PS_P_U_Edited;
        }

        public static ThingDef GetEditedThingDef(ThingDef sourceDef)
        {
            if (sourceDef == SSCDefOf.SSC_PersonalitySlime || sourceDef == SSCDefOf.SSC_PersonalitySlime_Edited) return SSCDefOf.SSC_PersonalitySlime_Edited;
            if (sourceDef == SSCDefOf.SSC_PS_P || sourceDef == SSCDefOf.SSC_PS_P_Edited) return SSCDefOf.SSC_PS_P_Edited;
            if (sourceDef == SSCDefOf.SSC_PS_U || sourceDef == SSCDefOf.SSC_PS_U_Edited) return SSCDefOf.SSC_PS_U_Edited;
            if (sourceDef == SSCDefOf.SSC_PS_P_U || sourceDef == SSCDefOf.SSC_PS_P_U_Edited) return SSCDefOf.SSC_PS_P_U_Edited;
            return null;
        }
    }
}
