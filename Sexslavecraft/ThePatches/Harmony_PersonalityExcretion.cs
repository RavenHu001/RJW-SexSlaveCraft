using HarmonyLib;
using rjw;
using RimWorld;
using SexSlaveCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    // Patch 目标：RJW 的 SexUtility.ProcessSex (性行为结算方法)
    [HarmonyPatch(typeof(SexUtility), "ProcessSex")]
    public static class Patch_AnalSex_Aggravates_Excretion
    {
        // 使用 Postfix (后置补丁)，在性行为结算完成后执行
        public static void Postfix(SexProps props)
        {
            // 1. 安全检查
            if (props == null || props.partner == null) return;

            Pawn initiator = props.pawn;
            Pawn partner = props.partner;
            if (initiator != null && partner != null)
            {
                BusSpecializationUtility.TryGainBusProgressFromSex(initiator, partner);
                BusSpecializationUtility.TryGainBusProgressFromSex(partner, initiator);
            }

            // 2. 检查是否为肛交 (Anal)
            if (props.sexType == xxx.rjwSextype.Anal)
            {
                Pawn victim = partner;

                // 3. 检查受害者是否有“人格排泄准备中”的 Hediff
                // 【修改】使用 SSCDefOf 获取 Def，不再使用硬编码字符串
                Hediff hediff = victim.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreting);

                if (hediff != null)
                {
                    // 4. 加重严重度
                    // 每次肛交增加 0.1 (10%) 的进度
                    float increaseAmount = 0.10f;

                    hediff.Severity += increaseAmount;

                    // 可选：如果不希望严重度超过最大值（虽然 RimWorld 通常会自动限制，但显式限制更安全）
                    if (hediff.Severity > hediff.def.maxSeverity)
                    {
                        hediff.Severity = hediff.def.maxSeverity;
                    }

                    // 调试日志 (发布时可注释掉)
                    // Log.Message($"[SSC] {victim.LabelShort} 的人格排泄进度增加了 {increaseAmount} (当前: {hediff.Severity})");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Recipe_Surgery), nameof(Recipe_Surgery.AvailableOnNow))]
    public static class Patch_RabbitClone_BlocksPersonalityExcretionSurgery
    {
        public static void Postfix(Recipe_Surgery __instance, Thing thing, ref bool __result)
        {
            if (!__result || __instance?.recipe != SSCDefOf.SSC_InducePersonalityExcretion) return;
            if (thing is Pawn pawn && RabbitCloneUtility.IsRabbitClone(pawn))
            {
                __result = false;
            }
        }
    }
}
