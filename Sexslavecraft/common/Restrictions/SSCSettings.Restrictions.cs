using Verse;

namespace SexSlaveCraft
{
    public partial class SSCSettings
    {
        public bool enableSpecializationRestrictionOverrides = true;
        public SSCRestrictionRules restrictionDefaults = new SSCRestrictionRules();

        /// <summary>读写特化覆盖开关及全局默认规则；旧设置加载结束后补全默认对象，不初始化任何角色。</summary>
        private void ExposeRestrictionSettings()
        {
            Scribe_Values.Look(ref enableSpecializationRestrictionOverrides, "enableSpecializationRestrictionOverrides", true);
            Scribe_Deep.Look(ref restrictionDefaults, "restrictionDefaults");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && restrictionDefaults == null)
                restrictionDefaults = new SSCRestrictionRules();
        }
    }
}
