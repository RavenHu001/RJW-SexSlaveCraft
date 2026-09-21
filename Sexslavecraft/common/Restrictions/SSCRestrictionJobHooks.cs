using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    [HarmonyPatch]
    internal static class SSCRestrictionReservationHook
    {
        /// <summary>覆盖已加载 RJW 派生驱动自己声明的预约方法；基类由原适配器转接，避免重复补丁。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type type in GenTypes.AllTypes)
            {
                if (type == typeof(JobDriver_Sex) || !typeof(JobDriver_Sex).IsAssignableFrom(type)) continue;
                MethodInfo method = type.GetMethod("TryMakePreToilReservations", BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(bool) }, null);
                if (method != null && !method.IsAbstract) yield return method;
            }
        }

        /// <summary>先检查本批统一许可；后续批次保持原预约路径，拒绝时不运行任何预约副作用。</summary>
        public static bool Prefix(JobDriver_Sex __instance, ref bool __result)
        {
            if (!SSCRestrictionJobGuard.TryReserve(__instance, out bool allowed) || allowed) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Sex), nameof(JobDriver_Sex.ExposeData))]
    internal static class SSCRestrictionSceneSaveHook
    {
        /// <summary>跟随 RJW 驱动保存实际开始凭据，让单人与双人场景恢复后遵守相同的准备和收尾边界。</summary>
        public static void Postfix(JobDriver_Sex __instance) => SSCRestrictionJobGuard.ExposeData(__instance);
    }

    [HarmonyPatch(typeof(JobDriver_SexQuick), "MakeNewToils")]
    internal static class SSCRestrictionQuickiePreparationHook
    {
        /// <summary>跟踪快速任务准备阶段创建的等待任务，供统一拒绝路径精确撤销。</summary>
        public static void Postfix(JobDriver_SexQuick __instance, ref IEnumerable<Toil> __result)
        {
            if (__result != null) __result = SSCRestrictionJobGuard.TrackPreparation(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    internal static class SSCRestrictionOrderedJobHook
    {
        /// <summary>玩家下令时先查统一许可，拒绝发生在清空原队列及预约之前，避免原版将许可拒绝记为预约异常。</summary>
        public static bool Prefix(Job job, Pawn ___pawn, ref bool __result)
        {
            // 原版在此方法内部才设置 playerForced；前缀必须显式提供命令来源，不提前改动候选 Job。
            if (!(SSCRestrictionJobContext.GetDriver(job, ___pawn) is JobDriver_Sex driver) ||
                !SSCRestrictionJobGuard.TryReserve(driver, out bool allowed, ordered: true) || allowed) return true;
            Messages.Message("SSC_Restrictions_CommandRejected".Translate(), ___pawn, MessageTypeDefOf.RejectInput, false);
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ThinkNode_JobGiver), nameof(ThinkNode_JobGiver.TryIssueJobPackage))]
    internal static class SSCRestrictionAutomaticJobHook
    {
        /// <summary>AI 候选产生后先查统一许可；被拒绝时返回无任务，让思考树继续其他工作而非进入预约失败循环。</summary>
        public static void Postfix(Pawn pawn, ref ThinkResult __result)
        {
            Job job = __result.Job;
            if (!(SSCRestrictionJobContext.GetDriver(job, pawn) is JobDriver_Sex driver) ||
                !SSCRestrictionJobGuard.TryReserve(driver, out bool allowed) || allowed) return;
            pawn.ClearReservationsForJob(job);
            JobMaker.ReturnToPool(job);
            __result = ThinkResult.NoJob;
        }
    }
}
