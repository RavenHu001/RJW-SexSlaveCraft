using System;
using RimWorld;
using Verse;
using Verse.AI;

// EN: This file is the rules layer behind `与主人同床`.
// EN: It decides when a sex slave may treat the master as a valid bed partner, which bed should be used, and which thought stage should be shown the next morning.
// CN: 这个文件是“与主人同床”系统背后的规则层。
// CN: 它决定性奴何时可以把主人视为合法同床对象、该睡哪张床，以及第二天应该显示哪一档同床想法。
namespace SexSlaveCraft
{
    public static class SSCSharedBedUtility
    {
        private const float MinCorruptionForSharedBed = 0.001f;

        private static bool ShouldTreatAsRegularGuestForBeds(Pawn sleeper, GuestStatus? guestStatusOverride)
        {
            if (sleeper == null || !HasMeaningfulCorruption(sleeper)) return false;
            GuestStatus effectiveStatus = guestStatusOverride ?? sleeper.GuestStatus ?? GuestStatus.Guest;
            return effectiveStatus == GuestStatus.Slave;
        }

        private static T InvokeWithGuestStatusBypass<T>(Func<T> action)
        {
            if (SSCBedGuestStatusBypass.Active) return action();

            SSCBedGuestStatusBypass.Active = true;
            try
            {
                return action();
            }
            finally
            {
                SSCBedGuestStatusBypass.Active = false;
            }
        }

        public static bool HasMeaningfulCorruption(Pawn pawn)
        {
            // EN: Shared-bed rules only start after corruption is no longer effectively zero.
            // CN: “与主人同床”规则只有在恶堕不再接近零时才开始生效。
            Need_Corruption need = pawn?.needs?.TryGetNeed<Need_Corruption>();
            return need != null && need.CurLevelPercentage > MinCorruptionForSharedBed;
        }

        public static float GetCorruption(Pawn pawn)
        {
            return pawn?.needs?.TryGetNeed<Need_Corruption>()?.CurLevelPercentage ?? 0f;
        }

        public static bool HasFlawedLovers(Pawn first, Pawn second)
        {
            return first != null && second != null && SSCDefOf.SSC_FlawedLovers != null && first.relations.DirectRelationExists(SSCDefOf.SSC_FlawedLovers, second);
        }

        public static bool TryGetResolvedMaster(Pawn pawn, out Pawn master)
        {
            master = null;
            if (pawn == null) return false;

            // EN: Step 1: ChainOfSexSlave has priority, because the slave chain is the strongest owner signal in SSC.
            // CN: 步骤 1：优先读取 ChainOfSexSlave，因为锁链是 SSC 里最强的主人归属信号。
            master = SSCBondUtility.GetResolvedMaster(pawn);
            if (master != null)
            {
                return true;
            }

            return false;
        }

        public static bool AreConsideredBedPartners(Pawn first, Pawn second)
        {
            if (first == null || second == null) return false;
            // EN: `SSC_FlawedLovers` is the strongest shared-bed bond and should always count first.
            // CN: `SSC_FlawedLovers` 是最强的同床关系，只要存在就应该优先成立。
            if (HasFlawedLovers(first, second)) return true;

            return (TryGetResolvedMaster(first, out Pawn firstMaster) && firstMaster == second && HasMeaningfulCorruption(first))
                || (TryGetResolvedMaster(second, out Pawn secondMaster) && secondMaster == first && HasMeaningfulCorruption(second));
        }

        public static bool TryGetSharedBedContext(Pawn pawn, out Pawn master, out Pawn bondedPawn, out Building_Bed bed)
        {
            master = null;
            bondedPawn = null;
            bed = pawn?.CurrentBed();
            if (pawn == null || bed == null || bed.SleepingSlotsCount <= 1) return false;

            // EN: Step 1: inspect the current bed occupants and look for a valid master / sex-slave pairing in the same bed.
            // CN: 步骤 1：检查当前床上的所有占用者，看看其中有没有合法的“主人 / 性奴”同床组合。
            foreach (Pawn other in bed.CurOccupants)
            {
                if (other == null || other == pawn) continue;

                if (TryGetResolvedMaster(pawn, out Pawn pawnMaster) && pawnMaster == other && HasMeaningfulCorruption(pawn))
                {
                    master = other;
                    bondedPawn = pawn;
                    return true;
                }

                if (TryGetResolvedMaster(other, out Pawn otherMaster) && otherMaster == pawn && HasMeaningfulCorruption(other))
                {
                    master = pawn;
                    bondedPawn = other;
                    return true;
                }
            }

            return false;
        }

        public static int GetSharedBedThoughtStage(Pawn bondedPawn, Pawn master)
        {
            if (bondedPawn == null || master == null) return -1;
            // EN: `SSC_FlawedLovers` jumps straight to the highest `与主人同床` stage.
            // CN: `SSC_FlawedLovers` 会直接跳到“与主人同床”的最高阶段。
            if (HasFlawedLovers(bondedPawn, master)) return 5;

            float corruption = GetCorruption(bondedPawn);
            if (corruption >= 0.5f) return 4;
            if (corruption >= 0.3f) return 3;

            int opinion = bondedPawn.relations?.OpinionOf(master) ?? 0;
            if (opinion < 30) return 0;
            if (opinion < 50) return 1;
            return 2;
        }

        public static bool CanUseMasterBed(Building_Bed bed, Pawn sleeper, bool checkSocialProperness, GuestStatus? guestStatusOverride = null)
        {
            // EN: This is the main gate that lets a corrupted sex slave use the master's owned bed outside vanilla romance rules.
            // CN: 这是“让恶堕性奴绕过原版恋人规则去睡主人床”的主判定入口。
            // EN: Step 1: reject anything that is not a corrupted sex slave with a resolved master.
            // CN: 步骤 1：先排除所有“不属于恶堕性奴且无法解析出主人”的情况。
            if (bed == null || sleeper == null) return false;
            if (!HasMeaningfulCorruption(sleeper)) return false;
            if (!TryGetResolvedMaster(sleeper, out Pawn master) || master == null) return false;
            if (!bed.OwnersForReading.Contains(master)) return false;
            if (!bed.Spawned || bed.Map != sleeper.MapHeld || bed.IsBurning() || bed.Medical) return false;
            if (!RestUtility.CanUseBedEver(sleeper, bed.def)) return false;
            if (bed.CompAssignableToPawn.IdeoligionForbids(sleeper)) return false;
            if (checkSocialProperness && !bed.IsSociallyProper(sleeper, false)) return false;
            if (bed.ForPrisoners) return false;

            // EN: Step 2: respect vanilla ownership / occupied-slot rules so SSC only overrides partner logic, not bed capacity rules.
            // CN: 步骤 2：继续遵守原版“床位归属 / 占用格”规则，SSC 这里只覆盖伴侣逻辑，不覆盖床位容量逻辑。
            int? assignedSleepingSlot;
            bool isOwner = bed.IsOwner(sleeper, out assignedSleepingSlot);
            int? currentSleepingSlot;
            bool alreadyInBed = sleeper.CurrentBed(out currentSleepingSlot) == bed;
            if (!bed.AnyUnoccupiedSleepingSlot && !isOwner && !alreadyInBed) return false;
            if (!isOwner && !bed.AnyUnownedSleepingSlot && !alreadyInBed) return false;
            if (alreadyInBed && currentSleepingSlot != assignedSleepingSlot && isOwner) return false;

            // EN: Step 3: keep the usual forbidden / colony access checks for the final bed usage decision.
            // CN: 步骤 3：在最后决策时继续保留原版的禁止访问和殖民地访问检查。
            if (sleeper.IsColonist && (guestStatusOverride ?? sleeper.GuestStatus) != GuestStatus.Prisoner)
                {
                Job curJob = sleeper.CurJob;
                if ((curJob == null || !curJob.ignoreForbidden) && !sleeper.Downed && bed.IsForbidden(sleeper)) return false;
            }

            return true;
        }

        public static bool TryAllowBedAsNonSlaveGuest(Building_Bed bed, Pawn sleeper, bool checkSocialProperness, GuestStatus? guestStatusOverride, out bool result)
        {
            result = false;
            if (SSCBedGuestStatusBypass.Active || !ShouldTreatAsRegularGuestForBeds(sleeper, guestStatusOverride)) return false;
            if (!CanUseRegularBedAsCorruptedSlave(bed, sleeper)) return true;

            result = InvokeWithGuestStatusBypass<bool>(() => RestUtility.CanUseBedNow(bed, sleeper, checkSocialProperness, false, GuestStatus.Guest));
            return true;
        }

        public static bool TryAllowOwnerShareAsNonSlaveGuest(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatusOverride, out bool result)
        {
            result = false;
            if (SSCBedGuestStatusBypass.Active || !ShouldTreatAsRegularGuestForBeds(sleeper, guestStatusOverride)) return false;
            if (!CanUseRegularBedAsCorruptedSlave(bed, sleeper)) return true;

            result = InvokeWithGuestStatusBypass<bool>(() => RestUtility.BedOwnerWillShare(bed, sleeper, GuestStatus.Guest));
            return true;
        }

        public static bool TryGetPreferredMasterBed(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus, out Building_Bed bed)
        {
            bed = null;
            if (sleeper == null) return false;
            if (!TryGetResolvedMaster(sleeper, out Pawn master) || master?.ownership?.OwnedBed == null) return false;

            // EN: When vanilla bed search fails, this uses the master's owned bed as SSC's preferred fallback result.
            // CN: 当原版找床失败时，这里会把主人的床作为 SSC 的首选回退结果。
            Building_Bed masterBed = master.ownership.OwnedBed;
            Pawn actor = traveler ?? sleeper;
            if (!CanUseMasterBed(masterBed, sleeper, checkSocialProperness, guestStatus)) return false;
            if (!actor.CanReach(masterBed, PathEndMode.OnCell, Danger.Deadly)) return false;
            if (!ignoreOtherReservations && !actor.CanReserve(masterBed, masterBed.SleepingSlotsCount, 0)) return false;

            bed = masterBed;
            return true;
        }

        public static bool ShouldPreferMasterBed(Pawn sleeper, GuestStatus? guestStatus = null)
        {
            if (sleeper == null || !HasMeaningfulCorruption(sleeper)) return false;
            if (!TryGetResolvedMaster(sleeper, out Pawn master) || master?.ownership?.OwnedBed == null) return false;

            GuestStatus effectiveStatus = guestStatus ?? sleeper.GuestStatus ?? GuestStatus.Guest;
            return effectiveStatus == GuestStatus.Slave;
        }

        public static bool CanAssignToMasterBed(Building_Bed bed, Pawn pawn, out string reason)
        {
            reason = null;
            if (bed == null || pawn == null) return false;
            if (!HasMeaningfulCorruption(pawn) || !TryGetResolvedMaster(pawn, out Pawn master) || master == null) return false;
            if (!bed.OwnersForReading.Contains(master)) return false;
            if (bed.ForPrisoners || bed.Medical) return false;
            if (bed.SleepingSlotsCount <= 1) return false;
            if (bed.ForSlaves) return false;

            int maxOwners = bed.SleepingSlotsCount;
            if (maxOwners > 0 && !bed.OwnersForReading.Contains(pawn) && bed.OwnersForReading.Count >= maxOwners)
            {
                reason = "该床位拥有者已满。";
                return false;
            }

            if (bed.CompAssignableToPawn.IdeoligionForbids(pawn))
            {
                reason = "意识形态不允许该目标使用这张床。";
                return false;
            }

            return true;
        }

        public static bool CanUseRegularBedAsCorruptedSlave(Building_Bed bed, Pawn sleeper)
        {
            if (bed == null || sleeper == null) return false;
            if (!HasMeaningfulCorruption(sleeper)) return false;
            if (bed.ForPrisoners || bed.ForSlaves || bed.Medical) return false;
            return true;
        }

        public static bool TryFindNormalBedAsNonSlaveGuest(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus, out Building_Bed bed)
        {
            bed = null;
            if (SSCBedGuestStatusBypass.Active || !ShouldTreatAsRegularGuestForBeds(sleeper, guestStatus)) return false;

            Pawn actor = traveler ?? sleeper;
            Building_Bed bestBed = null;
            float bestDistance = float.MaxValue;
            foreach (Building_Bed candidate in sleeper.MapHeld.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            {
                if (!CanUseRegularBedAsCorruptedSlave(candidate, sleeper)) continue;

                bool canUse = InvokeWithGuestStatusBypass<bool>(() => RestUtility.CanUseBedNow(candidate, sleeper, checkSocialProperness, ignoreOtherReservations, GuestStatus.Guest));
                if (!canUse) continue;
                if (!actor.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly)) continue;
                if (!ignoreOtherReservations && !actor.CanReserve(candidate, candidate.SleepingSlotsCount, 0)) continue;

                float distance = actor.Position.DistanceToSquared(candidate.Position);
                if (distance >= bestDistance) continue;

                bestBed = candidate;
                bestDistance = distance;
            }

            bed = bestBed;
            return bed != null;
        }

        public static bool ShouldIgnoreNegativeSleepMood(Pawn pawn, Building_Bed bed)
        {
            if (pawn == null || bed == null) return false;
            if (!TryGetSharedBedContext(pawn, out _, out _, out Building_Bed sharedBed)) return false;
            return sharedBed == bed;
        }
    }
}
