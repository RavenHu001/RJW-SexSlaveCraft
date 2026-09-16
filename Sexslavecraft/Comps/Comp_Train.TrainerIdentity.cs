using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        public bool slaveTrainerEnabled;
        // 新组件已经初始化；只有旧存档缺少标记时才需要迁移。
        public bool trainerIdentityInitialized = true;

        /// <summary>保存性奴调教员开关和迁移标记，区分旧档缺字段与玩家明确关闭。</summary>
        public void ExposeTrainerIdentity()
        {
            Scribe_Values.Look(ref slaveTrainerEnabled, "slaveTrainerEnabled", false);
            Scribe_Values.Look(ref trainerIdentityInitialized, "trainerIdentityInitialized", false);
        }
    }
}
