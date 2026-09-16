using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCIdentityUtility
    {
        /// <summary>主人固定具备调教员身份，未选择固定不具备；性奴读取独立保存的个人开关。</summary>
        /// <remarks>只判断身份，不把工作开关、临时身体状态或指定关系当作身份来源。</remarks>
        public static bool IsTrainer(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && (comp.pawnIdentity == PawnIdentity.Master
                || (comp.pawnIdentity == PawnIdentity.Slave && comp.slaveTrainerEnabled));
        }

        /// <summary>只允许修改性奴的个人调教员开关；保留指派、绑定、工作安排和正在执行的任务。</summary>
        public static bool SetTrainerEnabled(Pawn pawn, bool enabled)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.pawnIdentity != PawnIdentity.Slave) return false;
            comp.slaveTrainerEnabled = enabled;
            comp.trainerIdentityInitialized = true;
            return true;
        }
    }
}
