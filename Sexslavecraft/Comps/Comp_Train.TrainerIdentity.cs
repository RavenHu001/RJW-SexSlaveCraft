using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        public bool slaveTrainerEnabled;
        // 新组件已经初始化；只有旧存档缺少标记时才需要迁移。
        public bool trainerIdentityInitialized = true;
        // 与旧的指派身份迁移分开计数；新角色跳过旧档开关重置。
        public const int CurrentTrainerOfficerMigrationVersion = 1;
        public int trainerOfficerMigrationVersion = CurrentTrainerOfficerMigrationVersion;

        /// <summary>保存个人开关及两代独立迁移标记，区分新角色与缺字段的旧档。</summary>
        public void ExposeTrainerIdentity()
        {
            Scribe_Values.Look(ref slaveTrainerEnabled, "slaveTrainerEnabled", false);
            Scribe_Values.Look(ref trainerIdentityInitialized, "trainerIdentityInitialized", false);
            // 旧档没有该字段时读为零；完成一次关闭后保存当前版本，
            // 再次读档不能重置玩家在升级后重新开启的个人选择。
            Scribe_Values.Look(ref trainerOfficerMigrationVersion, "trainerOfficerMigrationVersion", 0);
        }
    }
}
