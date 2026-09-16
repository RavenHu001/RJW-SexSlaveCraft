using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只定义 SSC 的额外床伴许可；床型、环境、容量和预留仍由原版检查。</summary>
    public static class SSCSharedBedUtility
    {
        private const float MinCorruptionForSharedBed = 0.001f;

        public static float GetCorruption(Pawn pawn)
        {
            return pawn?.needs?.TryGetNeed<Need_Corruption>()?.CurLevelPercentage ?? 0f;
        }

        public static bool HasMeaningfulCorruption(Pawn pawn)
        {
            return GetCorruption(pawn) > MinCorruptionForSharedBed;
        }

        /// <summary>未绑定时使用指定调教员；已有锁链时绝不回退到其他调教员。</summary>
        public static bool TryGetPartner(Pawn pawn, out Pawn partner, out bool isMaster)
        {
            partner = null;
            isMaster = false;
            if (!SSCIdentityUtility.IsSexSlave(pawn)) return false;
            Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(pawn);
            isMaster = chain != null;
            partner = isMaster ? chain.LinkedPawn : pawn.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            return partner != null && partner != pawn && !partner.Dead && !partner.Destroyed;
        }

        /// <summary>只为当前对象拥有的普通多人床提供额外许可，不向其他普通床扩散。</summary>
        public static bool HasPartnerBedPermission(Building_Bed bed, Pawn pawn)
        {
            return bed != null && !bed.Medical && !bed.ForPrisoners && !bed.ForSlaves
                && bed.SleepingSlotsCount > 1 && HasMeaningfulCorruption(pawn)
                && TryGetPartner(pawn, out Pawn partner, out _)
                && bed.OwnersForReading.Contains(partner);
        }

        public static bool IsPrisoner(Pawn pawn, GuestStatus? guestStatus)
        {
            return pawn != null && (pawn.IsPrisoner || guestStatus == GuestStatus.Prisoner);
        }

        /// <summary>只在这次目标床查询中放宽原版奴隶床分类，不修改 Pawn 身份。</summary>
        public static void AdjustGuestStatusForPartnerBed(Thing bedThing, Pawn sleeper, ref GuestStatus? guestStatusOverride)
        {
            if (sleeper == null || IsPrisoner(sleeper, guestStatusOverride)) return;
            if ((guestStatusOverride ?? sleeper.GuestStatus) != GuestStatus.Slave) return;
            if (HasPartnerBedPermission(bedThing as Building_Bed, sleeper))
                guestStatusOverride = GuestStatus.Guest;
        }

        /// <summary>分配界面仅放宽目标床的奴隶分类检查，保留原版体型等拒绝条件。</summary>
        public static bool IsSlaveForBedAssignment(Pawn pawn, CompAssignableToPawn_Bed assignable)
        {
            return pawn.IsSlave && !HasPartnerBedPermission(assignable.parent as Building_Bed, pawn);
        }

        public static bool TryGetPreferredPartnerBed(Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool ignoreOtherReservations, GuestStatus? guestStatus, out Building_Bed bed)
        {
            bed = null;
            if (sleeper == null || IsPrisoner(sleeper, guestStatus)
                || !TryGetPartner(sleeper, out Pawn partner, out _)) return false;
            Building_Bed candidate = partner.ownership?.OwnedBed;
            if (!HasPartnerBedPermission(candidate, sleeper)) return false;
            // 调用完整原版检查，SSC 不复制环境、寻路、禁用或预留规则。
            if (!RestUtility.IsValidBedFor(candidate, sleeper, traveler ?? sleeper, checkSocialProperness,
                false, ignoreOtherReservations, guestStatus)) return false;
            bed = candidate;
            return true;
        }

        public static bool PreserveSpecialRest(Pawn sleeper, Building_Bed vanillaBed)
        {
            return sleeper == null || sleeper.Deathresting || HealthAIUtility.ShouldSeekMedicalRest(sleeper)
                || (vanillaBed != null && (vanillaBed.Medical || vanillaBed.def == ThingDefOf.DeathrestCasket));
        }

        public static int GetSharedBedThoughtStage(Pawn pawn, Pawn partner)
        {
            if (pawn == null || partner == null) return -1;
            if (SSCDefOf.SSC_FlawedLovers != null
                && pawn.relations.DirectRelationExists(SSCDefOf.SSC_FlawedLovers, partner)) return 5;
            float corruption = GetCorruption(pawn);
            if (corruption >= 0.5f) return 4;
            if (corruption >= 0.3f) return 3;
            int opinion = pawn.relations?.OpinionOf(partner) ?? 0;
            return opinion < 30 ? 0 : opinion < 50 ? 1 : 2;
        }
    }
}
