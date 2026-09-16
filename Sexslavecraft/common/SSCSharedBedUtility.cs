using RimWorld;
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

        /// <summary>未绑定时使用指定调教员；已有锁链时绝不回退到其他调教员。</summary>
        /// <param name="pawn">必须拥有 SSC 性奴身份的角色，普通原版奴隶不适用。</param>
        /// <param name="partner">解析得到的对象；返回 false 时不得使用该输出。</param>
        /// <param name="isMaster">是否存在锁链，用于区分主人和调教员记忆，而非表示对象一定有效。</param>
        /// <returns>对象非空、非自身且未死亡或销毁时返回 true。</returns>
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
        /// <remarks>此处只确定 SSC 许可范围，不代表床位可达或可使用；完整有效性仍需原版检查。</remarks>
        public static bool HasPartnerBedPermission(Building_Bed bed, Pawn pawn)
        {
            return bed != null && !bed.Medical && !bed.ForPrisoners && !bed.ForSlaves
                && bed.SleepingSlotsCount > 1 && HasMeaningfulCorruption(pawn)
                && TryGetPartner(pawn, out Pawn partner, out _)
                && bed.OwnersForReading.Contains(partner);
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
            if (sleeper == null || IsPrisoner(sleeper, guestStatusOverride)) return;
            if ((guestStatusOverride ?? sleeper.GuestStatus) != GuestStatus.Slave) return;
            if (HasPartnerBedPermission(bedThing as Building_Bed, sleeper))
                guestStatusOverride = GuestStatus.Guest;
        }

        /// <summary>分配界面仅放宽目标床的奴隶分类检查，保留原版体型等拒绝条件。</summary>
        /// <returns>角色是原版奴隶且没有这张床的额外许可时返回 true；只影响本次分类分支。</returns>
        public static bool IsSlaveForBedAssignment(Pawn pawn, CompAssignableToPawn_Bed assignable)
        {
            return pawn.IsSlave && !HasPartnerBedPermission(assignable.parent as Building_Bed, pawn);
        }

        /// <summary>尝试取得当前主人或调教员的归属床，并使用原版完整检查确认它可供本次休息使用。</summary>
        /// <param name="sleeper">准备休息的性奴。</param>
        /// <param name="traveler">实际移动或搬运的角色；为空时使用 sleeper。</param>
        /// <param name="checkSocialProperness">原样传给原版的社交适当性检查开关。</param>
        /// <param name="ignoreOtherReservations">原样传给原版的忽略其他预留开关。</param>
        /// <param name="guestStatus">原版找床调用的身份参数。</param>
        /// <param name="bed">全部检查通过时返回指定对象的床，否则为 null。</param>
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
