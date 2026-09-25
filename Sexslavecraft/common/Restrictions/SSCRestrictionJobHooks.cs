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

        /// <summary>先检查真实行为的统一许可；非行为的家具占用保留原路径，拒绝不运行预约副作用。</summary>
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

    [HarmonyPatch(typeof(SexUtility), nameof(SexUtility.ProcessSex))]
    internal static class SSCTrainerInitiatedSexProgressHook
    {
        /// <summary>只在 RJW 正常结算返回后领取普通双人行为经验；任务归属和次数由场景守卫核实。</summary>
        public static void Postfix(SexProps props, bool __runOriginal)
        {
            // 其他补丁可能跳过 RJW 原结算；Harmony 此时仍会调用后缀。
            // 未执行原方法就没有完成事件，不能凭旧的开始凭据领奖。
            if (!__runOriginal) return;

            // 日常调教、绑定仪式和人格排泄虽也调用 ProcessSex，仍各走自己
            // 的专用结算路径。此处仅转发通过普通任务认领的实际发起者。
            if (SSCRestrictionJobGuard.TryClaimOrdinarySexOutcome(props, out Pawn initiator, out Pawn recipient))
                TrainerSpecializationProgressUtility.NotifyInitiatedSexCompleted(initiator, recipient);
        }
    }

    [HarmonyPatch]
    internal static class SSCRestrictionQuickiePreparationHook
    {
        /// <summary>仅覆盖已核对会给另一方创建移动/等待的驱动，避免包装未知第三方任务的任意副作用。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(JobDriver_SexQuick), "MakeNewToils");
            yield return AccessTools.DeclaredMethod(typeof(JobDriver_BestialityForFemale), "MakeNewToils");
        }

        /// <summary>跟踪快速和宠物事件准备阶段创建的等待或移动任务，供拒绝或取消路径精确撤销。</summary>
        public static void Postfix(JobDriver_SexBaseInitiator __instance, ref IEnumerable<Toil> __result)
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
            if (SSCRestrictionExternalJobs.TryReserve(job, ___pawn, out bool externalAllowed) && !externalAllowed)
            {
                Messages.Message(SSCRestrictionExternalJobs.RejectionReason(job, ___pawn), ___pawn, MessageTypeDefOf.RejectInput, false);
                __result = false;
                return false;
            }
            // 原版在此方法内部才设置 playerForced；前缀必须显式提供命令来源，不提前改动候选 Job。
            if (!(SSCRestrictionJobContext.GetDriver(job, ___pawn) is JobDriver_Sex driver) ||
                !SSCRestrictionJobGuard.TryReserve(driver, out bool allowed, ordered: true) || allowed) return true;
            Messages.Message(SSCRestrictionJobGuard.RejectionReason(driver), ___pawn, MessageTypeDefOf.RejectInput, false);
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
            bool rejected = SSCRestrictionExternalJobs.TryReserve(job, pawn, out bool externalAllowed) && !externalAllowed;
            if (!rejected && SSCRestrictionJobContext.GetDriver(job, pawn) is JobDriver_Sex driver)
                rejected = SSCRestrictionJobGuard.TryReserve(driver, out bool allowed) && !allowed;
            if (!rejected) return;
            pawn.ClearReservationsForJob(job);
            JobMaker.ReturnToPool(job);
            __result = ThinkResult.NoJob;
        }
    }
}
