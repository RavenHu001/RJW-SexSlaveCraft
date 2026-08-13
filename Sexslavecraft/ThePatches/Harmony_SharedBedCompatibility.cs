using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

// EN: These patches inject SSC's `与主人同床` logic into vanilla bed checks.
// EN: They let a master and a chained sex slave count as valid bed partners even when vanilla love-partner logic would say no.
// CN: 这些补丁把 SSC 的“与主人同床”规则接入原版床位判定。
// CN: 即使原版恋人逻辑不给过，它们也能让主人和锁链性奴被视为合法同床对象。
namespace SexSlaveCraft
{
    internal static class SSCBedGuestStatusBypass
    {
        internal static bool Active;
    }

    [HarmonyPatch(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.LovePartnerRelationExists))]
    public static class Harmony_SSC_LovePartnerRelationExists
    {
        public static void Postfix(Pawn first, Pawn second, ref bool __result)
        {
            // EN: Use SSCSharedBedUtility here so `与主人同床` can count as a valid bed-partner relation.
            // CN: 这里调用 SSCSharedBedUtility，是为了让“与主人同床”关系也能被视为合法同床关系。
            if (!__result && SSCSharedBedUtility.AreConsideredBedPartners(first, second))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedNow))]
    public static class Harmony_SSC_CanUseBedNow
    {
        public static void Postfix(Thing bedThing, Pawn sleeper, bool checkSocialProperness, GuestStatus? guestStatusOverride, ref bool __result)
        {
            Building_Bed bed = bedThing as Building_Bed;
            if (__result || bed == null) return;

            if (SSCSharedBedUtility.TryAllowBedAsNonSlaveGuest(bed, sleeper, checkSocialProperness, guestStatusOverride, out bool allowAsGuest))
            {
                __result = allowAsGuest;
                if (__result) return;
            }

            // EN: If vanilla says no, SSCSharedBedUtility gets one extra chance to allow the master's bed for the chained sex slave.
            // CN: 如果原版判定失败，就再给 SSCSharedBedUtility 一次机会，让锁链性奴合法使用主人的床。
            if (SSCSharedBedUtility.CanUseMasterBed(bed, sleeper, checkSocialProperness, guestStatusOverride))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.BedOwnerWillShare))]
    public static class Harmony_SSC_BedOwnerWillShare
    {
        public static void Postfix(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatus, ref bool __result)
        {
            if (!__result && SSCSharedBedUtility.TryAllowOwnerShareAsNonSlaveGuest(bed, sleeper, guestStatus, out bool allowAsGuest))
            {
                __result = allowAsGuest;
                if (__result) return;
            }

            if (!__result && SSCSharedBedUtility.CanUseMasterBed(bed, sleeper, false, guestStatus))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindBedFor), new[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?) })]
    public static class Harmony_SSC_FindBedFor
    {
        public static void Postfix(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus, ref Building_Bed __result)
        {
            if (SSCSharedBedUtility.ShouldPreferMasterBed(sleeper, guestStatus)
                && SSCSharedBedUtility.TryGetPreferredMasterBed(sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, out Building_Bed preferredBed))
            {
                __result = preferredBed;
                return;
            }

            if (__result != null) return;

            if (SSCSharedBedUtility.TryFindNormalBedAsNonSlaveGuest(sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, out Building_Bed regularBed))
            {
                __result = regularBed;
                return;
            }

            // EN: When vanilla bed search finds nothing, SSC may still return the master's preferred bed for this sex slave.
            // CN: 当原版找床失败时，SSC 仍然可以为这个性奴补上一张偏好的主人床位。
            if (SSCSharedBedUtility.TryGetPreferredMasterBed(sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, out Building_Bed bed))
            {
                __result = bed;
            }
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.CanAssignTo))]
    public static class Harmony_SSC_BedCanAssignTo
    {
        public static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn, ref AcceptanceReport __result)
        {
            Building_Bed bed = __instance?.parent as Building_Bed;
            if (__result.Accepted || bed == null) return;
            if (SSCSharedBedUtility.CanAssignToMasterBed(bed, pawn, out string reason))
            {
                __result = AcceptanceReport.WasAccepted;
                return;
            }

            if (!string.IsNullOrEmpty(reason))
            {
                __result = reason;
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

            List<Pawn> extraCandidates = bed.Map.mapPawns?.SlavesOfColonySpawned
                ?.Where(pawn => SSCSharedBedUtility.CanAssignToMasterBed(bed, pawn, out _))
                .ToList();
            if (extraCandidates == null || extraCandidates.Count == 0) return;

            __result = __result.Concat(extraCandidates).Distinct();
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
    public static class Harmony_SSC_ApplyBedThoughts
    {
        public static void Postfix(Pawn actor, Building_Bed bed)
        {
            if (!SSCSharedBedUtility.ShouldIgnoreNegativeSleepMood(actor, bed)) return;

            actor?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDefIf(ThoughtDefOf.SleptInBedroom, thought => thought.MoodOffset() < 0f);
            actor?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDefIf(ThoughtDefOf.SleptInBarracks, thought => thought.MoodOffset() < 0f);
        }
    }
}
