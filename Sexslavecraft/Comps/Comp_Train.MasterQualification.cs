using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        public const int CurrentMasterQualificationVersion = 1;
        // 资格属于人格；当前身份与手动赋予设置都不代替这个永久记录。
        public bool masterQualificationUnlocked;
        // 新角色已初始化；只有读入缺字段的旧档时才变为版本 0。
        public int masterQualificationVersion = CurrentMasterQualificationVersion;

        /// <summary>保存个人资格和一次性迁移版本，保留新版明确未解锁的记录。</summary>
        public void ExposeMasterQualification()
        {
            Scribe_Values.Look(ref masterQualificationUnlocked, "masterQualificationUnlocked", false);
            Scribe_Values.Look(ref masterQualificationVersion, "masterQualificationVersion", 0);
        }

        /// <summary>整体替换人格资格；清空身体或创建克隆也写当前版本，避免读档误补。</summary>
        public void ReplaceMasterQualification(bool unlocked)
        {
            masterQualificationUnlocked = unlocked;
            masterQualificationVersion = CurrentMasterQualificationVersion;
        }
    }
}
