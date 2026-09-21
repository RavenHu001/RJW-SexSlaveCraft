using Verse;

namespace SexSlaveCraft
{
    public partial class SSCSettings
    {
        // 旧设置只作为迁移输入保存，不能再由设置界面或运行判定读写。
        // 模组设置跨存档共享：即使当前存档已升级，也必须保留这些原值，
        // 以便之后打开其他旧存档时仍能捕获其升级所需的全局快照。
        internal bool allowSexSlaveRape;
        internal bool protectBusAggressorRape = true;
        internal bool protectChainedAggressorRape = true;
        internal bool protectNonRapeOwnerOnly = true;

        public bool enableSpecializationRestrictionOverrides = true;
        public SSCRestrictionRules restrictionDefaults = new SSCRestrictionRules();

        /// <summary>读写特化覆盖开关及全局默认规则；旧设置加载结束后补全默认对象，不初始化任何角色。</summary>
        private void ExposeRestrictionSettings()
        {
            // 沿用旧 XML 键，先读迁移输入，再补缺失的新默认；已有新默认绝不回读旧值覆盖。
            Scribe_Values.Look(ref allowSexSlaveRape, "allowSexSlaveRape", false);
            Scribe_Values.Look(ref protectBusAggressorRape, "protectBusAggressorRape", true);
            Scribe_Values.Look(ref protectChainedAggressorRape, "protectChainedAggressorRape", true);
            Scribe_Values.Look(ref protectNonRapeOwnerOnly, "protectNonRapeOwnerOnly", true);
            Scribe_Values.Look(ref enableSpecializationRestrictionOverrides, "enableSpecializationRestrictionOverrides", true);
            Scribe_Deep.Look(ref restrictionDefaults, "restrictionDefaults");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && restrictionDefaults == null)
                restrictionDefaults = SSCRestrictionLegacySettings.Capture(this).Convert(false, false).rules;
        }
    }
}
