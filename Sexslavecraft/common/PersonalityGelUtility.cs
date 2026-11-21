using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class PersonalityGelUtility
    {
        public static ThingDef GetPersonalityGelDefForPawn(Pawn pawn)
        {
#if SSC_RW17
            // 无主按实际阶段选用已有凝胶 Def，加工时自然取得同档的塑形贴图。
            // 有主继续使用既有的凝胶严重度分档，其中最高档起点是 80%。
            int ownerlessStage = Hediff_SSCOwnerlessSlave.GetActiveStage(pawn);
            if (ownerlessStage > 0)
                return ownerlessStage switch
                {
                    4 => SSCDefOf.SSC_PS_P_U,
                    3 => SSCDefOf.SSC_PS_U,
                    2 => SSCDefOf.SSC_PS_P,
                    _ => SSCDefOf.SSC_PersonalitySlime
                };
#endif
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
