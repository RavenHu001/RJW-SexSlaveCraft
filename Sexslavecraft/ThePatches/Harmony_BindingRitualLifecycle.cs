using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // 旧档会保存 RitualBehaviorWorker 的具体类型。钩住 LordJob 生命周期，
    // 可以同时修复旧仪式和新仪式，不要求替换已存档的 worker。
    [HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.Cleanup))]
    public static class Harmony_BindingRitualCleanup
    {
        /// <summary>在整场结果处理之后、参与者 Job 收尾之前，调用统一入口解除绑定仪式状态。</summary>
        public static void Prefix(LordJob_Ritual __instance)
        {
            // 原版先 ApplyOutcome，再清理 Lord；此处不会提前清零结算所需的阶段。
            // 在 Pawn Job 收尾前解除本场状态，阻止迟到的阶段回调继续推进。
            BindingRitualStateUtility.EndRitual(__instance);
        }
    }

    [HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.PostCleanup))]
    public static class Harmony_BindingRitualPostCleanup
    {
        /// <summary>在原版后置清理完成后再次执行可重复的收尾，复核本场是否仍有残留状态。</summary>
        public static void Postfix(LordJob_Ritual __instance)
        {
            BindingRitualStateUtility.EndRitual(__instance);
        }
    }

    [HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.Notify_PawnLost))]
    public static class Harmony_BindingRitualPawnLost
    {
        /// <summary>在原版删除角色分配前处理主人或目标离场；普通观众离场不解除主从占用。</summary>
        /// <param name="p">原版通知中即将失去角色分配的参与者，参数名与 Harmony 注入目标一致。</param>
        public static void Prefix(LordJob_Ritual __instance, Pawn p)
        {
            if (!BindingRitualStateUtility.IsBindingRitual(__instance)) return;
            // 原版会在此方法内移除角色分配。必须在移除前处理，否则整场 Cleanup
            // 再找 slave 时可能已经找不到；普通观众离场不结束主从的运行状态。
            if (p == __instance.PawnWithRole("master") || p == __instance.PawnWithRole("slave"))
            {
                BindingRitualStateUtility.EndRitual(__instance);
            }
        }
    }
}
