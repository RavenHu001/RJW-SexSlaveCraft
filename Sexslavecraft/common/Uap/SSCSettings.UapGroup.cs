// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public partial class SSCSettings
    {
        /// <summary>只影响新 UAP 窗口请求的全局豁免偏好，默认关闭；不写入任何个人能力或人格数据。</summary>
        public bool allowUapManualGroupRestrictionOverride;
        /// <summary>使用独立保存键读写偏好，旧配置缺少此键时保持 false，不扩大既有请求权限。</summary>
        private void ExposeUapGroupSettings()
            => Scribe_Values.Look(ref allowUapManualGroupRestrictionOverride, "allowUapManualGroupRestrictionOverride", false);
    }

    /// <summary>与接口就绪状态一致的设置入口；没有可用 UAP 适配时不显示无法兑现的豁免选项。</summary>
    internal static class SSCUapGroupSettingsUI
    {
        /// <summary>复用 SSC 模组设置页的复选框布局，只改变后续请求的偏好，不重置个人规则。</summary>
        internal static void Draw(Listing_Standard listing)
        {
            if (!SSCUapGroupApi.Ready || SSCMod.settings == null) return;
            listing.CheckboxLabeled("SSC_Uap_Override".Translate(),
                ref SSCMod.settings.allowUapManualGroupRestrictionOverride, "SSC_Uap_OverrideTip".Translate());
        }
    }
}
