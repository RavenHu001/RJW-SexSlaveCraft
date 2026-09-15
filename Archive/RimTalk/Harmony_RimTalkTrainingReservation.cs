// Archived 2026-09-15: excluded from the runtime build pending a complete redesign.
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    // EN: Drives the soft RimTalk reservation queue from the normal game tick.
    // CN: 使用正常游戏 Tick 推进 RimTalk 软依赖预约队列。
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    internal static class Harmony_RimTalkTrainingReservation
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            RimTalkCompatibilityUtility.Tick();
        }
    }
}
