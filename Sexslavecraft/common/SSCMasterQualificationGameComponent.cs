using Verse;

namespace SexSlaveCraft
{
    /// <summary>在地图、世界、商队及容器引用恢复后完成个人资格迁移，不增加周期扫描。</summary>
    public sealed class SSCMasterQualificationGameComponent : GameComponent
    {
        public SSCMasterQualificationGameComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            SSCMasterQualificationMigration.Migrate(SSCRestrictionGameComponent.AllPawns());
        }
    }
}
