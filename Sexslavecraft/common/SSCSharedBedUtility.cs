using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只定义 SSC 的额外床伴许可；床型、环境、容量和预留仍由原版检查。</summary>
    public static class SSCSharedBedUtility
    {
        private const float MinCorruptionForSharedBed = 0.001f;

        /// <summary>读取当前恶堕比例；角色或对应需求缺失时按零处理。</summary>
        public static float GetCorruption(Pawn pawn)
        {
            return pawn?.needs?.TryGetNeed<Need_Corruption>()?.CurLevelPercentage ?? 0f;
        }

        /// <summary>检查恶堕是否严格高于 0.1%，等于门槛时仍不授予额外同床许可。</summary>
        public static bool HasMeaningfulCorruption(Pawn pawn)
        {
            return GetCorruption(pawn) > MinCorruptionForSharedBed;
        }

        /// <summary>依次枚举有效主人和指定调教员；绑定不排除调教员，同一人只返回一次。</summary>
        /// <remarks>只解析 SSC 性奴的直接关系，不检查恶堕、床型或环境，也不改变调教员指派规则。</remarks>
        public static IEnumerable<Pawn> GetAllowedPartners(Pawn pawn)
        {
            if (!SSCIdentityUtility.IsSexSlave(pawn) || pawn.Dead || pawn.Destroyed) yield break;
            Pawn master = SSCBondUtility.GetChain(pawn)?.LinkedPawn;
            if (IsValidPartner(pawn, master)) yield return master;
            Pawn trainer = pawn.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            if (trainer != master && IsValidPartner(pawn, trainer)) yield return trainer;
        }

        /// <summary>排除自身、空引用和已死亡或销毁的对象。</summary>
        private static bool IsValidPartner(Pawn pawn, Pawn partner)
        {
            return partner != null && partner != pawn && !partner.Dead && !partner.Destroyed;
        }

        /// <summary>双向判断直接 SSC 同床关系，不递归扩展到对象的其他关系。</summary>
        public static bool HasSharedBedRelation(Pawn first, Pawn second)
        {
            return GetAllowedPartners(first).Contains(second) || GetAllowedPartners(second).Contains(first);
        }

        /// <summary>判断指定调教员关系是否有效，用于只从本床已分配性奴反查调教员标签。</summary>
        public static bool IsDesignatedTrainer(Pawn sexSlave, Pawn trainer)
        {
            return SSCIdentityUtility.IsSexSlave(sexSlave)
                && sexSlave.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer == trainer
                && GetAllowedPartners(sexSlave).Contains(trainer);
        }

        /// <summary>限定 SSC 额外许可的床位类别；完整环境和可用性仍由原版判断。</summary>
        public static bool IsOrdinarySharedBed(Building_Bed bed)
        {
            return bed != null && !bed.Medical && !bed.ForPrisoners && !bed.ForSlaves && bed.SleepingSlotsCount > 1;
        }

        /// <summary>检查原版奴隶床分类例外所需的性奴身份和恶堕门槛，排除囚犯。</summary>
        private static bool CanRelaxSlaveBedCategory(Pawn pawn)
        {
            return SSCIdentityUtility.IsSexSlave(pawn) && !pawn.IsPrisoner && HasMeaningfulCorruption(pawn);
        }

        /// <summary>本床存在直接床伴关系，且对应性奴达到恶堕门槛时提供双向共享许可。</summary>
        /// <remarks>不依赖哪一方先分配；普通原版奴隶不会因此获得床位身份覆盖。</remarks>
        public static bool HasPartnerBedPermission(Building_Bed bed, Pawn pawn)
        {
            if (!IsOrdinarySharedBed(bed) || pawn == null || pawn.IsPrisoner) return false;
            return bed.OwnersForReading.Any(owner =>
                (CanRelaxSlaveBedCategory(pawn) && GetAllowedPartners(pawn).Contains(owner))
                || (CanRelaxSlaveBedCategory(owner) && GetAllowedPartners(owner).Contains(pawn)));
        }

        /// <summary>保留符合条件的性奴已有普通多人床归属；不据此允许向空床新增分配。</summary>
        private static bool HasAssignedBedPermission(Building_Bed bed, Pawn pawn)
        {
            return IsOrdinarySharedBed(bed) && CanRelaxSlaveBedCategory(pawn) && bed.OwnersForReading.Contains(pawn);
        }

        /// <summary>同时检查实际囚犯身份和本次查询的身份参数，避免额外许可绕过囚犯床限制。</summary>
        public static bool IsPrisoner(Pawn pawn, GuestStatus? guestStatus)
        {
            return pawn != null && (pawn.IsPrisoner || guestStatus == GuestStatus.Prisoner);
        }

        /// <summary>只在这次目标床查询中放宽原版奴隶床分类，不修改 Pawn 身份。</summary>
        /// <param name="bedThing">原版正在检查的目标床，非床建筑不会获得许可。</param>
        /// <param name="sleeper">准备使用床位的角色；囚犯不进行覆盖。</param>
        /// <param name="guestStatusOverride">按引用调整的本次查询参数；仅有效身份为奴隶且符合额外许可时改为访客。</param>
        public static void AdjustGuestStatusForPartnerBed(Thing bedThing, Pawn sleeper, ref GuestStatus? guestStatusOverride)
        {
            if (!CanRelaxSlaveBedCategory(sleeper) || IsPrisoner(sleeper, guestStatusOverride)) return;
            if ((guestStatusOverride ?? sleeper.GuestStatus) != GuestStatus.Slave) return;
            Building_Bed bed = bedThing as Building_Bed;
            if (HasAssignedBedPermission(bed, sleeper) || HasPartnerBedPermission(bed, sleeper))
                guestStatusOverride = GuestStatus.Guest;
        }

        /// <summary>分配界面仅放宽目标床的奴隶分类检查，保留原版体型等拒绝条件。</summary>
        /// <returns>只保留本床已有归属或允许加入有效床伴；原版奴隶性奴不能先分配空殖民者床。</returns>
        public static bool IsSlaveForBedAssignment(Pawn pawn, CompAssignableToPawn_Bed assignable)
        {
            Building_Bed bed = assignable.parent as Building_Bed;
            bool allowed = IsOrdinarySharedBed(bed) && CanRelaxSlaveBedCategory(pawn)
                && (bed.OwnersForReading.Contains(pawn) || HasPartnerBedPermission(bed, pawn));
            return pawn.IsSlave && !allowed;
        }

        /// <summary>依次尝试角色自己的已分配床、主人床和调教员床；每一步均使用原版完整检查。</summary>
        /// <param name="sleeper">准备休息的性奴。</param>
        /// <param name="traveler">实际移动或搬运的角色；为空时使用 sleeper。</param>
        /// <param name="checkSocialProperness">原样传给原版的社交适当性检查开关。</param>
        /// <param name="ignoreOtherReservations">原样传给原版的忽略其他预留开关。</param>
        /// <param name="guestStatus">原版找床调用的身份参数。</param>
        /// <param name="bed">返回第一张可用床，否则为 null；不扫描其他无关普通床。</param>
        public static bool TryGetPreferredPartnerBed(Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool ignoreOtherReservations, GuestStatus? guestStatus, out Building_Bed bed)
        {
            bed = null;
            if (!SSCIdentityUtility.IsSexSlave(sleeper) || IsPrisoner(sleeper, guestStatus)) return false;
            Building_Bed ownBed = sleeper.ownership?.OwnedBed;
            if (ownBed != null && RestUtility.IsValidBedFor(ownBed, sleeper, traveler ?? sleeper,
                checkSocialProperness, false, ignoreOtherReservations, guestStatus))
            {
                bed = ownBed;
                return true;
            }
            foreach (Pawn partner in GetAllowedPartners(sleeper))
            {
                Building_Bed candidate = partner.ownership?.OwnedBed;
                if (candidate == ownBed || !HasPartnerBedPermission(candidate, sleeper)) continue;
                // 调用完整原版检查，SSC 不复制环境、寻路、禁用或预留规则。
                if (!RestUtility.IsValidBedFor(candidate, sleeper, traveler ?? sleeper, checkSocialProperness,
                    false, ignoreOtherReservations, guestStatus)) continue;
                bed = candidate;
                return true;
            }
            return false;
        }

        /// <summary>医疗需求、死眠状态或原版特殊床结果存在时保留原结果，确保特殊休息优先于同床偏好。</summary>
        /// <remarks>医疗需求下即使原版没找到床也不覆盖；空角色同样保留原结果。</remarks>
        public static bool PreserveSpecialRest(Pawn sleeper, Building_Bed vanillaBed)
        {
            return sleeper == null || sleeper.Deathresting || HealthAIUtility.ShouldSeekMedicalRest(sleeper)
                || (vanillaBed != null && (vanillaBed.Medical || vanillaBed.def == ThingDefOf.DeathrestCasket));
        }

        /// <summary>按特殊关系、恶堕比例、对本次对象的好感依次选择六档睡后心情阶段。</summary>
        /// <returns>0～5 分别对应 -6、-3、+2、+5、+9、+13；缺少任一角色时返回 -1。</returns>
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
