using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

// EN: This patch permanently removes vanilla slave work restrictions after corruption has appeared at least once.
// EN: Historical maximum corruption marks a colony slave as an SSC sex slave even if current corruption later decays to zero.
// CN: 这个补丁会在奴隶至少出现过一次恶堕后永久移除原版工作限制。
// CN: 即使当前恶堕后来衰减到零，历史最高恶堕仍会把殖民地奴隶标记为 SSC 性奴。
namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(GuestUtility), nameof(GuestUtility.GetDisabledWorkTypes))]
    public static class Patch_GuestUtility_GetDisabledWorkTypes
    {
        public static void Postfix(Pawn_GuestTracker guest, ref List<WorkTypeDef> __result)
        {
            // EN: Only colony slaves are relevant here. Prisoners and free colonists keep vanilla work behavior.
            // CN: 这里只有殖民地奴隶才是目标；囚犯和自由殖民者继续走原版工作逻辑。
            Pawn pawn = Traverse.Create(guest).Field("pawn").GetValue<Pawn>();
            if (pawn == null || !pawn.IsSlaveOfColony)
            {
                return;
            }

            if (!SSCIdentityUtility.HasEstablishedSexSlaveState(pawn) || __result == null || __result.Count == 0)
            {
                return;
            }

            // EN: Once corruption has ever existed, clear GuestUtility's disabled-work list so SSC can open the full work set.
            // CN: 只要历史上出现过恶堕，就清空 GuestUtility 算出的禁用工作列表，让 SSC 放开完整工作集。
            __result.Clear();
        }
    }
}
