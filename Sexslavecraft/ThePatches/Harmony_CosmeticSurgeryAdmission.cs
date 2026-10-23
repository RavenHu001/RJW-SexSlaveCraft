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
            // 只收紧本配方的原版允许结果；不放行原版已拒绝的医生，也不影响其他手术。
            if (!__result || __instance.recipe != SSCDefOf.SSC_Surgery_BeautyEnhancement) return;
            // 接单时同时复查患者和医生，补足旧账单未重新打开清单时的业务资格检查。
            var patient = __instance.GiverPawn;
            __result = SSCCosmeticUtility.RecipientReport(patient, true, pawn?.Map).Accepted &&
                SSCCosmeticUtility.DoctorReport(pawn, patient).Accepted;
        }
    }
}
