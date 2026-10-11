using Verse;

namespace SexSlaveCraft
{
    public partial class SSCSettings
    {
        public bool allowDirectMasterSelection = true;

        /// <summary>默认允许手动选择主人；设置只控制准入，不授予永久资格。</summary>
        public void ExposeMasterQualificationSettings()
        {
            Scribe_Values.Look(ref allowDirectMasterSelection, "allowDirectMasterSelection", true);
        }
    }
}
