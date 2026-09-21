using Verse;

namespace SexSlaveCraft
{
    /// <summary>测试界面的显式配置写入入口；不负责生命周期迁移或任务指派。</summary>
    public static class SSCRestrictionEditor
    {
        /// <summary>检查配置版本和所有保存值，供界面决定是否允许编辑；不修复或替换异常数据。</summary>
        public static bool IsValid(SSCRestrictionConfig config)
        {
            return config?.IsValid() == true;
        }

        /// <summary>仅在玩家明确点击且适用角色没有配置时建立独立测试配置；已有配置绝不覆盖。</summary>
        public static bool TryInitialize(Pawn pawn)
        {
            return TryInitialize(pawn, out _);
        }

        /// <summary>显式初始化并返回非法默认的条目与来源；不适用或已有配置时返回 false，错误条目为空。</summary>
        /// <remarks>预期配置错误不会抛出异常；失败不写入半成品，也不覆盖已有配置。</remarks>
        public static bool TryInitialize(Pawn pawn, out SSCRestrictionResolution error)
        {
            error = null;
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !SSCRestrictionResolver.IsApplicable(pawn) || comp.restrictionConfig != null) return false;
            if (!SSCRestrictionResolver.TryCreateInitialConfiguration(pawn, SSCMod.settings?.restrictionDefaults,
                out SSCRestrictionConfig config, out error)) return false;
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
