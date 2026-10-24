// AI 辅助生成
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Bill_Medical), nameof(Bill_Medical.PawnAllowedToStartAnew))]
    public static class Harmony_UterusSurgeryAdmission
    {
        /// <summary>医生接单时复查旧账单，只收紧本手术且不覆盖原版拒绝。</summary>
        public static void Postfix(Bill_Medical __instance, Pawn pawn, ref bool __result)
        {
            if (!__result || __instance.recipe != SSCDefOf.SSC_Surgery_StartUterusDevelopment) return;
            // 部位必须仍是原账单选择的当前 Torso；不替旧账单改绑躯干或重新选择目标。
            // 菜单的 AvailableOnNow 只负责身体候选；接单必须读取完整报告，防止禁用项绕过资格。
            var patient = __instance.GiverPawn;
            __result = __instance.Part != null &&
                __instance.recipe.Worker.AvailableReport(patient, __instance.Part).Accepted &&
                SSCUterusSurgeryUtility.DoctorReport(pawn, patient).Accepted;
        }
    }
}
