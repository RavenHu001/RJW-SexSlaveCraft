// AI 辅助生成：挂接1.7原版旅队每名成员的定时服药检查。
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只挂个人重载；旅队遍历与120tick节拍继续由原版上层入口负责。</summary>
    [HarmonyPatch(typeof(CaravanDrugPolicyUtility), "TryTakeScheduledDrugs", new[] { typeof(Pawn), typeof(Caravan) })]
    internal static class Harmony_PnaCaravanMaintenance
    {
        /// <summary>原政策处理完毕后尝试阶段维持；其他补丁跳过原方法时不追加摄入。</summary>
        private static void Postfix(Pawn pawn, Caravan caravan, bool __runOriginal)
        {
            // Postfix也会经过原版提前return，因此资格、成员关系与目标负荷由适配器自行完整复查。
            if (__runOriginal) SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan);
        }
    }
}
