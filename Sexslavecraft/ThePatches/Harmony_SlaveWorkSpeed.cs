using HarmonyLib;
using RimWorld;
using Verse;

// EN: This patch permanently disables vanilla slave work-speed handling after corruption has appeared at least once.
// EN: Historical maximum corruption keeps the pawn classified as an SSC sex slave after current corruption decays.
// CN: 这个补丁会在 Pawn 至少出现过一次恶堕后永久关闭原版奴隶工作速度处理。
// CN: 当前恶堕衰减后，历史最高恶堕仍会让该 Pawn 保持 SSC 性奴身份。
namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(StatPart_Slave), "ActiveFor")]
    public static class Patch_StatPart_Slave_ActiveFor
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            // EN: Only pawns already marked by vanilla as slave-controlled need this override.
            // CN: 这里只有那些已经被原版判定为“走奴隶工作速度逻辑”的 Pawn 才需要继续覆盖。
            if (!__result || !(t is Pawn pawn))
            {
                return;
            }

            // EN: Any non-zero historical maximum permanently disables the vanilla slave work-speed StatPart.
            // CN: 只要历史最高恶堕大于零，就永久关闭原版奴隶工作速度 StatPart。
            if (SSCIdentityUtility.HasEstablishedSexSlaveState(pawn))
            {
                __result = false;
            }
        }
    }
}
