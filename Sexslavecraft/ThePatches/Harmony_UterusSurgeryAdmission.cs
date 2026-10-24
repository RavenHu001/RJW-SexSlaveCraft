// AI 辅助生成
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Bill_Medical), nameof(Bill_Medical.PawnAllowedToStartAnew))]
    public static class Harmony_UterusSurgeryAdmission
    {
        /// <summary>医生接单时复查旧账单，只收紧两项身体开发手术且不覆盖原版拒绝。</summary>
        public static void Postfix(Bill_Medical __instance, Pawn pawn, ref bool __result)
        {
            if (!__result) return;
            bool uterus = __instance.recipe == SSCDefOf.SSC_Surgery_StartUterusDevelopment;
            bool stomach = __instance.recipe == SSCDefOf.SSC_Surgery_StartStomachDevelopment;
            if (!uterus && !stomach) return;
            // 胃部与躯干均使用账单原目标；禁止接单时悄悄重选部位绕过资格变化。
            var patient = __instance.GiverPawn;
            __result = __instance.Part != null &&
                __instance.recipe.Worker.AvailableReport(patient, __instance.Part).Accepted &&
                (uterus ? SSCUterusSurgeryUtility.DoctorReport(pawn, patient).Accepted
                    : SSCStomachSurgeryUtility.DoctorReport(pawn, patient).Accepted);
        }
    }
}
