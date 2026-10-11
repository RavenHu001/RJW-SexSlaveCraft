using Verse;

namespace SexSlaveCraft
{
    /// <summary>个人主人资格的只读准入和人格清理入口，不依据当前身份自动授予。</summary>
    public static class SSCMasterQualificationUtility
    {
        public static bool HasQualification(Pawn pawn)
        {
            return pawn?.TryGetComp<CompSexSlaveTraining>()?.masterQualificationUnlocked == true;
        }

        public static bool CanSelectMaster(Pawn pawn)
        {
            return pawn?.TryGetComp<CompSexSlaveTraining>() != null &&
                ((SSCMod.settings?.allowDirectMasterSelection ?? true) || HasQualification(pawn));
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
