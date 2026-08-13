using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(JobDriver_Sex))]
    internal static class Patch_JobDriver_Sex_CorruptionAdd
    {
        // 前置：判断性高潮是否触发   Precondition: Check whether orgasm is triggered.
        [HarmonyPrefix]
        [HarmonyPatch("Orgasm")]
        public static void Orgasm_Prefix(JobDriver_Sex __instance, out bool __state)
        {
            __state = false;
            if (__instance.sex_ticks <= __instance.orgasmstick)
            {
                __state = true;
            }
        }

        // 后置：性高潮触发后，已进入锁链健康阶段的性奴增加恶堕值。
        [HarmonyPostfix]
        [HarmonyPatch("Orgasm")]
        public static void Orgasm_Postfix(JobDriver_Sex __instance, bool __state)
        {
            if (!__state) return;

            Pawn pawn = __instance.Partner;
            if (pawn == null) return;

            if (SSCIdentityUtility.GetSexSlaveStage(pawn) > 0)
            {
                float corruptionAmount = 0.003f; // 调用时的恶堕值   Corruption rate at the time of invocation.
                CorruptionUtility.AddCorruption(__instance.Partner, corruptionAmount);
                Log.Message($"[SexSlaveCorruption] {pawn.Name} increased corruption by {corruptionAmount}");
            }
        }
    }
}
