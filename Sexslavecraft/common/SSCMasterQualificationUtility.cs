using Verse;

namespace SexSlaveCraft
{
    /// <summary>个人主人资格的只读准入和人格清理入口，不依据当前身份自动授予。</summary>
    public static partial class SSCMasterQualificationUtility
    {
        /// <summary>只读原有永久主人资格字段；当前主人身份、手动选择设置和初始印记均不代替该记录。</summary>
        public static bool HasQualification(Pawn pawn)
        {
            return pawn?.TryGetComp<CompSexSlaveTraining>()?.masterQualificationUnlocked == true;
        }

        /// <summary>汇总主人选择准入；初始印记成年资格只读派生，实际切换仍须通过身份锁校验。</summary>
        public static bool CanSelectMaster(Pawn pawn)
        {
            // 设置尚未可用时也沿用默认关闭，不能从后台绕过个人资格门槛。
            // 1.7额外允许成年初始印记者选择主人，但查询本身不授永久资格或触发分化。
            return pawn?.TryGetComp<CompSexSlaveTraining>() != null &&
                ((SSCMod.settings?.allowDirectMasterSelection ?? false) || HasQualification(pawn)
#if SSC_RW17
                || SSCImprintUtility.HasBornMasterEligibility(pawn)
#endif
                );
        }

        /// <summary>清空身体上的旧人格资格，同时封住旧档补发；不改身份、绑定或成长。</summary>
        public static void ClearPersonalityQualification(Pawn pawn)
        {
            pawn?.TryGetComp<CompSexSlaveTraining>()?.ReplaceMasterQualification(false);
        }

        /// <summary>克隆是独立人格，不继承来源资格或旧迁移版本。</summary>
        public static void InitializeNewClone(Pawn clone)
        {
            ClearPersonalityQualification(clone);
        }
    }
}
