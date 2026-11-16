using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_DuplicatedFrom))]
    internal static class Harmony_SexSlaveTraitDuplication
    {
        // 所有 Comp 处理完复制通知后，先补继承印记所需的身份，再按副本状态处理特性和心情。
        private static void Postfix(Pawn __instance, Pawn source)
        {
            if (SSCIdentityUtility.TryRestoreCopiedSlaveIdentity(source, __instance)) return;
            SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(__instance);
            SSCCorruptionStages.NotifyContextChanged(__instance);
        }
    }
}
