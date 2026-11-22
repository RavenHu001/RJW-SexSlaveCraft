// AI 辅助生成：1.7 文化只读条件；不改变 SSC 身份、任职或绑定。
using Verse;

namespace SexSlaveCraft
{
    public static class SSCCultureStateUtility
    {
        // 查询共用的存活边界：空引用、死亡、销毁和已丢弃对象均不参与文化统计。
        public static bool IsLiving(Pawn pawn)
            => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded;

        /// <summary>查询实际锁链。</summary>
        public static bool HasChain(Pawn pawn) => SSCBondUtility.GetChain(pawn) != null;

        /// <summary>文化戒律承认原有锁链和一至四阶的正式无主性奴。</summary>
        public static bool IsSexSlave(Pawn pawn) => GetSexSlaveRecord(pawn) != null;

        internal static Hediff GetSexSlaveRecord(Pawn pawn)
        {
            // 交接重叠时锁链优先，沿用既有绑定及旧锁链低于一阶阈值时的文化资格。
            var chain = SSCBondUtility.GetChain(pawn);
            if (chain != null) return chain;

            // 身份预选和解绑保留的零阶进度都还未达到正式无主阶段。
            var ownerless = SSCIdentityUtility.IsSexSlave(pawn) ? Hediff_SSCOwnerlessSlave.Find(pawn) : null;
            return TrainingOutcomeUtility.GetSexSlaveProgressStage(ownerless).Stage > 0 ? ownerless : null;
        }

        // 主人身份与有效训导官沿用既有入口，避免文化层另存身份或复制任职资格规则。
        public static bool IsMaster(Pawn pawn) => SSCIdentityUtility.IsMaster(pawn);
        public static bool IsActiveOfficer(Pawn pawn) => TrainerOfficerFeedback.IsActiveOfficer(pawn);

        public static bool TryGetActualMaster(Pawn pawn, out Pawn master)
        {
            // 只读取一次链；指定调教员不是实际主人，正常离图也不会使实际绑定失效。
            // 此处不修链、不维护反向索引，不以个人主人资格替代当前主人身份。
            master = SSCBondUtility.GetChain(pawn)?.LinkedPawn;
            if (master != pawn && IsLiving(master) && SSCIdentityUtility.IsMaster(master)) return true;
            // 无效目标统一清空输出；调用方不能把失败查询残留的链目标当作实际主人。
            master = null;
            return false;
        }
    }
}
