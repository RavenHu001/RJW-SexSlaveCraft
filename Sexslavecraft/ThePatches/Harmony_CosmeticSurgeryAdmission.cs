// AI 辅助生成
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Bill_Medical), nameof(Bill_Medical.PawnAllowedToStartAnew))]
    public static class Harmony_CosmeticSurgeryAdmission
    {
        public static void Postfix(Bill_Medical __instance, Pawn pawn, ref bool __result)
        {
            if (!__result || __instance.recipe != SSCDefOf.SSC_Surgery_BeautyEnhancement) return;
            var patient = __instance.GiverPawn;
            __result = SSCCosmeticUtility.RecipientReport(patient, true, pawn?.Map).Accepted &&
                SSCCosmeticUtility.DoctorReport(pawn, patient).Accepted;
        }
    }
}
