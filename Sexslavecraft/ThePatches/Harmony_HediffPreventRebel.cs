using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using SexSlaveCraft;



namespace SexSlaveCraft
{
    // =============================================================
    // 1. 阻止参与越狱 (Prison Break)
    // =============================================================
    [HarmonyPatch(typeof(PrisonBreakUtility), "CanParticipateInPrisonBreak")]
    public static class Patch_PrisonBreakUtility_CanParticipateInPrisonBreak
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            // 修正点1：加上 "Utils." 前缀
            if (!__result || !Utils.HasChainHediff(pawn))
                return;

            __result = false;
        }
    }

    // =============================================================
    // 2. 阻止参与奴隶叛乱 (Slave Rebellion)
    // =============================================================
    [HarmonyPatch(typeof(SlaveRebellionUtility), "CanParticipateInSlaveRebellion")]
    public static class Patch_SlaveRebellionUtility_CanParticipateInSlaveRebellion
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            // 修正点1：加上 "Utils." 前缀
            if (!__result || !Utils.HasChainHediff(pawn))
                return;

            __result = false;
        }
    }

    // =============================================================
    // 3. 拦截狂暴 (Berserk) 并强制回满压制条
    // =============================================================
    [HarmonyPatch(typeof(MentalStateHandler), "TryStartMentalState")]
    public static class Patch_MentalStateHandler_TryStartMentalState
    {
        public static bool Prefix(MentalStateHandler __instance, MentalStateDef stateDef, Pawn ___pawn, ref bool __result)
        {
            if (stateDef != MentalStateDefOf.Berserk)
            {
                return true;
            }

            // 修正点1：加上 "Utils." 前缀
            if (!Utils.HasChainHediff(___pawn))
            {
                return true;
            }

            // --- 拦截生效 ---
            // 修改：使用本地化字符串
            Messages.Message(
                Strings.Message_BerserkSuppressed(___pawn.LabelShort),
                ___pawn,
                MessageTypeDefOf.NeutralEvent,
                historical: false
            );

            Need_Suppression suppressionNeed = ___pawn.needs?.TryGetNeed<Need_Suppression>();
            if (suppressionNeed != null)
            {
                suppressionNeed.CurLevelPercentage = 1.0f;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Need_Suppression), nameof(Need_Suppression.NeedInterval))]
    public static class Patch_Need_Suppression_NeedInterval
    {
        public static bool Prefix(Need_Suppression __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (!Utils.HasChainHediff(pawn))
            {
                return true;
            }

            return false;
        }
    }

    // =============================================================
    // 通用辅助方法
    // =============================================================
    public static class Utils
    {
        public static bool HasChainHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return false;
            }

            // 修正点2：确认你的 DefOf 类名
            // 假设你在之前的步骤里定义的是 "SSCDefOf" 且变量名为 "ChainOfSexSlave"
            return pawn.health.hediffSet.HasHediff(SSCDefOf.ChainOfSexSlave);
        }
    }
}
