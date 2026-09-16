using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // SSC 只接入床位查询；不修改 LovePartnerRelationUtility 的任何判断。
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedNow))]
    public static class Harmony_SSC_CanUseBedNow
    {
        public static void Prefix(Thing bedThing, Pawn sleeper, ref GuestStatus? guestStatusOverride)
        {
            SSCSharedBedUtility.AdjustGuestStatusForPartnerBed(bedThing, sleeper, ref guestStatusOverride);
        }
    }

    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.BedOwnerWillShare))]
    public static class Harmony_SSC_BedOwnerWillShare
    {
        public static void Postfix(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatus, ref bool __result)
        {
            if (!__result && !SSCSharedBedUtility.IsPrisoner(sleeper, guestStatus)
                && SSCSharedBedUtility.HasPartnerBedPermission(bed, sleeper))
                __result = bed.AnyUnownedSleepingSlot;
        }
    }

    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindBedFor), new[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?) })]
    public static class Harmony_SSC_FindBedFor
    {
        public static void Postfix(Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool ignoreOtherReservations, GuestStatus? guestStatus, ref Building_Bed __result)
        {
            if (SSCSharedBedUtility.PreserveSpecialRest(sleeper, __result)) return;
            if (SSCSharedBedUtility.TryGetPreferredPartnerBed(sleeper, traveler, checkSocialProperness,
                ignoreOtherReservations, guestStatus, out Building_Bed bed))
                __result = bed;
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.CanAssignTo))]
    public static class Harmony_SSC_BedCanAssignTo
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var isSlave = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsSlave));
            var replacement = AccessTools.Method(typeof(SSCSharedBedUtility), nameof(SSCSharedBedUtility.IsSlaveForBedAssignment));
            foreach (CodeInstruction instruction in instructions)
            {
                if (!instruction.Calls(isSlave))
                {
                    yield return instruction;
                    continue;
                }
                // 保留原版方法，唯独将这张床的奴隶分类判断替换为 SSC 的有限例外。
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "get_AssigningCandidates")]
    public static class Harmony_SSC_BedAssigningCandidates
    {
        public static void Postfix(CompAssignableToPawn_Bed __instance, ref IEnumerable<Pawn> __result)
        {
            Building_Bed bed = __instance?.parent as Building_Bed;
            if (bed?.Map == null || __result == null) return;
            IEnumerable<Pawn> extra = bed.Map.mapPawns.SlavesOfColonySpawned
                .Where(pawn => SSCSharedBedUtility.HasPartnerBedPermission(bed, pawn));
            __result = __result.Concat(extra).Distinct();
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedRelatedEffects")]
    public static class Harmony_SSC_RecordSharedSleep
    {
        public static void Postfix(Pawn p, Building_Bed bed, bool asleep, bool gainRest)
        {
            if (asleep && gainRest) SSCSharedBedMemory.RecordSleep(p, bed);
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
    public static class Harmony_SSC_ApplyBedThoughts
    {
        public static void Postfix(Pawn actor, Building_Bed bed)
        {
            SSCSharedBedMemory.RemoveNegativeRoomMemories(actor, bed);
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "FinalizeLayingJob")]
    public static class Harmony_SSC_FinishSharedSleep
    {
        public static void Postfix(Pawn pawn, Building_Bed bed)
        {
            SSCSharedBedMemory.FinishSleep(pawn, bed);
        }
    }
}
