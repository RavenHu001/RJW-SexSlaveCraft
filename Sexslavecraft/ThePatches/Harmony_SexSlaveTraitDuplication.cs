using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_DuplicatedFrom))]
    internal static class Harmony_SexSlaveTraitDuplication
    {
        // 等所有 Comp 都处理完复制通知，再按副本身份重算。没有训练 Comp 的副本也会清理。
        private static void Postfix(Pawn __instance)
        {
            SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(__instance);
        }
    }
}
