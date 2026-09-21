using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        public SSCRestrictionConfig restrictionConfig;

        /// <summary>只读写新配置；旧档缺字段保持 null，由阶段 2 的迁移/首次适用入口处理。</summary>
        public void ExposeRestrictions()
        {
            Scribe_Deep.Look(ref restrictionConfig, "sscRestrictionConfig");
        }
    }
}
