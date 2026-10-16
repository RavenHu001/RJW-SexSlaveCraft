using Verse;

namespace SexSlaveCraft
{
    public partial class SSCSettings
    {
        public bool allowDirectMasterSelection = false;

        /// <summary>默认要求个人主人资格；保留明确保存的选择，旧配置缺字段时也默认关闭。</summary>
        public void ExposeMasterQualificationSettings()
        {
            Scribe_Values.Look(ref allowDirectMasterSelection, "allowDirectMasterSelection", false);
        }
    }
}
