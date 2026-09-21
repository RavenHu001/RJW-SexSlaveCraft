using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>测试界面的显式配置写入入口；不负责生命周期迁移或任务指派。</summary>
    public static class SSCRestrictionEditor
    {
        /// <summary>检查配置版本和所有保存值，供界面决定是否允许编辑；不修复或替换异常数据。</summary>
        public static bool IsValid(SSCRestrictionConfig config)
        {
            if (config == null || config.version != SSCRestrictionConfig.CurrentVersion || config.rules == null) return false;
            foreach (SSCRestrictionRule rule in Enum.GetValues(typeof(SSCRestrictionRule)))
                if (!SSCRestrictionRules.IsValid(rule, config.rules.Get(rule))) return false;
            return true;
        }

        /// <summary>仅在玩家明确点击且适用角色没有配置时建立独立测试配置；已有配置绝不覆盖。</summary>
        public static bool TryInitialize(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !SSCRestrictionResolver.IsApplicable(pawn) || comp.restrictionConfig != null) return false;
            SSCRestrictionConfig config = SSCRestrictionResolver.CreateInitialConfiguration(pawn, SSCMod.settings?.restrictionDefaults);
            if (!IsValid(config)) return false;
            comp.restrictionConfig = config;
            return true;
        }

        /// <summary>修改当前适用角色的单项保存值；强制来源不写回配置，也不改变绑定、旧许可或指定者。</summary>
        public static bool TrySet(Pawn pawn, SSCRestrictionRule rule, SSCRestrictionValue value)
        {
            SSCRestrictionConfig config = pawn?.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            if (!SSCRestrictionResolver.IsApplicable(pawn) || !IsValid(config) || !SSCRestrictionRules.IsValid(rule, value)) return false;
            config.rules.Set(rule, value);
            return true;
        }
    }
}
