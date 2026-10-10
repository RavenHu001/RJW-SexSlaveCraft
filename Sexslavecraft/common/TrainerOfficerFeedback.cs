using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>已任职训导官的职责记忆与实际主人评价；不修改资格、绑定或培养进度。</summary>
    public static class TrainerOfficerFeedback
    {
        /// <summary>有效任职只读取个人开关和持续资格，不受工作优先级、倒地或离图影响。</summary>
        public static bool IsActiveOfficer(Pawn officer)
        {
            // 主人固定具备调教员身份，但并非本反馈奖励的训导官。
            // 复用统一资格入口，使普通 20%、普通满进度与终极跨方向保持相同规则。
            return IsLivingPawn(officer)
                && SSCIdentityUtility.IsSexSlave(officer)
                && SSCIdentityUtility.IsTrainer(officer);
        }

        /// <summary>只供已确认正常完成且认领一次结算的日常任务或整场仪式调用。</summary>
        public static void NotifyTrainingCompleted(Pawn trainer, Pawn receiver)
        {
            // 不以施教经验增加量判断：普通满进度和有效终极仍能履行职责。
            // 自我调教、无效对象和身份已变化的对象不能产生施教职责记忆。
            if (trainer == receiver || !IsActiveOfficer(trainer) || !IsLivingPawn(receiver)
                || !SSCIdentityUtility.IsSexSlave(receiver)) return;

            ThoughtDef def = SSCDefOf.SSC_TrainerOfficer_DutyFulfilled;
            MemoryThoughtHandler memories = trainer.needs?.mood?.thoughts?.memories;
            if (def == null || memories == null) return;

            // 普通 Thought_Memory 配合 stackLimit=1，由原生合并 Renew 已有记忆；
            // 不绑定受训对象，因此不同对象也只保留一份，并保留原对象的其他状态。
            memories.TryGainMemory(def);
        }

        /// <summary>
        /// 主人仅评价实际绑定给自己、当前有效任职的训导官；查询完全只读。
        /// 本方法只判断“这对角色是否成立”，不表示好感方向：
        /// 主人对训导官的评价与训导官对主人的任职信任都复用它作为共同条件。
        /// </summary>
        public static bool HasOwnerAppraisal(Pawn owner, Pawn officer)
        {
            // 不从指定调教员或缰绳目标推断关系，也不扫描其他受训者的指派。
            return owner != officer && IsLivingPawn(owner)
                && SSCIdentityUtility.IsMaster(owner)
                && IsActiveOfficer(officer)
                && SSCBondUtility.GetBoundMaster(officer) == owner;
        }

        /// <summary>
        /// 训导官当前是否拥有可信的主人：有效任职，且实际绑定主人存活并具 SSC 主人身份。
        /// 与 <see cref="HasOwnerAppraisal"/> 校验的是同一对角色，只是查询方向相反。
        /// 只读，不扫描其他受训者指派，也不以开关或工作完成发历史记忆。
        /// </summary>
        public static bool HasTrustedOwner(Pawn officer, out Pawn owner)
        {
            owner = null;
            if (!IsActiveOfficer(officer)) return false;

            Pawn boundMaster = SSCBondUtility.GetBoundMaster(officer);
            // 自身、已死亡或已销毁的主人不能提供任职信任。
            if (boundMaster == null || boundMaster == officer || !IsLivingPawn(boundMaster)) return false;
            if (!SSCIdentityUtility.IsMaster(boundMaster)) return false;

            owner = boundMaster;
            return true;
        }

        private static bool IsLivingPawn(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded;
        }
    }

    /// <summary>主人对自己有效任职训导官的单向、条件性社交评价。</summary>
    public sealed class ThoughtWorker_TrainerOfficerAppraisal : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            // 由原版社交想法刷新当前状态；关闭开关、解绑或失格后不留下永久记忆。
            return TrainerOfficerFeedback.HasOwnerAppraisal(p, otherPawn)
                ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }

    /// <summary>训导官对实际绑定主人的条件性好感；对象必须正是自己当前的主人。</summary>
    public sealed class ThoughtWorker_TrainerOfficerTrustedByOwner : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            // 原版传入的 p 是持有想法的训导官，otherPawn 是社交栏正在比较的角色。
            return TrainerOfficerFeedback.HasTrustedOwner(p, out Pawn owner) && owner == otherPawn
                ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }

    /// <summary>训导官本人因被主人委派调教职责而获得的持续心情；条件消失即失效。</summary>
    public sealed class ThoughtWorker_TrainerOfficerTrustedByOwnerMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return TrainerOfficerFeedback.HasTrustedOwner(p, out _)
                ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }
}
