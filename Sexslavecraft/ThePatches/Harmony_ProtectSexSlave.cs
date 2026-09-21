using HarmonyLib;
using rjw;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>RJW 生命周期适配层：只安排检查时机，所有许可均交给统一限制系统。</summary>
    /// <remarks>
    /// 阶段3C已删除旧白名单、玩家命令豁免和旧保护策略回退。保持类名便于现有补丁定位，
    /// 不代表仍使用锁链保护算法。派生驱动自行覆盖的预约方法由动态预约补丁处理。
    /// </remarks>
    [HarmonyPatch(typeof(JobDriver_Sex), "TryMakePreToilReservations")]
    public static class Patch_JobDriver_Sex_ProtectChainOfSexSlave
    {
        /// <summary>在原预约发生前查询统一守卫；拒绝只返回预约失败，不在安装驱动期间递归结束任务。</summary>
        public static bool Prefix(JobDriver_Sex __instance, ref bool __result)
        {
            if (!SSCRestrictionJobGuard.TryReserve(__instance, out bool allowed) || allowed) return true;
            __result = false;
            return false;
        }

        /// <summary>每次进入下一步骤前检查尚未开始的请求，覆盖行走后创建接收任务的时机。</summary>
        /// <remarks>独立原版及专项准备驱动也在这里路由；不认识的普通工作不受影响。</remarks>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(JobDriver), "TryActuallyStartNextToil")]
        public static bool NextToil_Prefix(JobDriver __instance)
        {
            if (__instance is JobDriver_Sex driver)
                return !SSCRestrictionJobGuard.TryCheck(driver, "BeforeToil", out bool allowed) || allowed;
            return SSCRestrictionExternalJobs.CheckBeforeToil(__instance);
        }

        /// <summary>实际场景开始前再次校验，拦住绕过常规步骤直接调用 Start 的路径。</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), "Start")]
        public static bool Start_Prefix(JobDriver_SexBaseInitiator __instance)
            => !SSCRestrictionJobGuard.TryCheck(__instance, "Start", out bool allowed) || allowed;

        /// <summary>仅原生 Start 确实执行后保存开始凭据，并触发与该任务绑定的一次性事件通知。</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), "Start")]
        public static void Start_Postfix(JobDriver_SexBaseInitiator __instance, bool __runOriginal)
            => SSCRestrictionJobGuard.TryMarkStarted(__instance, __runOriginal);

        /// <summary>未开始且已被拒绝的场景不执行原生 End；真正开始的场景保留其正常收尾。</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), "End")]
        public static bool End_Prefix(JobDriver_SexBaseInitiator __instance)
            => !SSCRestrictionJobGuard.TryEnd(__instance, out bool allowed) || allowed;
    }
}
