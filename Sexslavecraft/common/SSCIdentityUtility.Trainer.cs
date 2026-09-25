using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCIdentityUtility
    {
        /// <summary>主人固定具备调教员身份；性奴须同时保存开启选择并持有训导官任职资格。</summary>
        /// <remarks>只读当前状态；不因暂时失格改写个人选择、指派、工作安排或特化状态。</remarks>
        public static bool IsTrainer(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return false;

            // 主人沿用固定身份；未选择身份与关闭个人开关的性奴无需进行
            // 锁链和终极标记查询，这是候选、同床及工作扫描的常见路径。
            if (comp.pawnIdentity == PawnIdentity.Master) return true;
            if (comp.pawnIdentity != PawnIdentity.Slave || !comp.slaveTrainerEnabled) return false;

            // 普通方向须达到 20%；有效终极可以跨方向继续任职。
            // 持续条件失效或终极禁用时，资格入口会立即返回 false。
            return TrainerSpecializationUtility.HasTrainerQualification(pawn, out _);
        }

        /// <summary>性奴仅在有任职资格时可以开启；关闭保存的个人选择不要求仍然有资格。</summary>
        public static bool SetTrainerEnabled(Pawn pawn, bool enabled)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.pawnIdentity != PawnIdentity.Slave) return false;

            // 仅开启需要检查资格。失格后玩家仍能清除先前保存的开启选择；
            // 无论哪种操作都不删除其他人保存的指派或中断已开始的工作。
            if (enabled && !TrainerSpecializationUtility.HasTrainerQualification(pawn, out _)) return false;
            comp.slaveTrainerEnabled = enabled;
            comp.trainerIdentityInitialized = true;
            return true;
        }
    }
}
