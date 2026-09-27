using HarmonyLib;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>维持人格排泄接收任务，避免普通工作或需求任务由思考树自动抢占。</summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "ShouldStartJobFromThinkTree")]
    public static class PersonalityExcretionJobProtection
    {
        /// <summary>只改变自动候选的替换决定；候选回收、队列管理及显式结束仍由原版负责。</summary>
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (__result && PersonalityExcretionJobUtility.GetActiveInitiator(___pawn) != null) __result = false;
        }

    }
}
