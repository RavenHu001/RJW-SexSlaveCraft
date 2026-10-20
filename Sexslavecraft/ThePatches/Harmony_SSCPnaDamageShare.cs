// AI 辅助生成：精确跟踪一次转移伤害，不中止原版受击／护盾流程。
using System;
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class Harmony_SSCPnaDamageShareContext
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(Thing __instance, DamageInfo dinfo, out IDisposable __state)
            => __state = SSCPnaDamageShareUtility.EnterTakeDamage(__instance, dinfo);

        // 被护盾吸收、其他 Prefix 跳过原函数以及异常退出均必须恢复外层帧。
        // 返回原异常，不能用上下文清理吞掉游戏或第三方抛出的错误。
        internal static Exception Finalizer(Exception __exception, IDisposable __state)
        {
            __state?.Dispose();
            return __exception;
        }
    }
}
