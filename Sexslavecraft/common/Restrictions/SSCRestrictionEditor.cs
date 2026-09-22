using Verse;

namespace SexSlaveCraft
{
    /// <summary>所有设置界面的配置写入入口，拒绝覆盖待迁移数据并在成功编辑后协调指定者。</summary>
    public static class SSCRestrictionEditor
    {
        /// <summary>检查配置版本和所有保存值，供界面决定是否允许编辑；不修复或替换异常数据。</summary>
        public static bool IsValid(SSCRestrictionConfig config)
        {
            return config?.IsValid() == true;
        }

        /// <summary>仅在玩家明确重试且适用角色没有配置时建立独立配置；已有配置绝不覆盖。</summary>
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
            if (comp == null || !SSCRestrictionResolver.IsApplicable(pawn) || comp.restrictionConfig != null ||
                comp.legacyRestrictionInput != null || comp.restrictionRestoreDepth > 0) return false;
            if (!SSCRestrictionConfigurationBuilder.TryCreateInitial(pawn, SSCMod.settings?.restrictionDefaults,
                out SSCRestrictionConfig config, out error)) return false;
            comp.restrictionConfig = config;
            comp.restrictionLifecycleSeen = true;
            SSCRestrictionLifecycle.CoordinateTrainer(pawn);
            return true;
        }

        /// <summary>修改当前适用角色的单项保存值；强制来源不写回配置，调教许可改变时协调指定者。</summary>
        public static bool TrySet(Pawn pawn, SSCRestrictionRule rule, SSCRestrictionValue value)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            SSCRestrictionConfig config = comp?.restrictionConfig;
            if (comp == null || comp.restrictionRestoreDepth > 0 || !SSCRestrictionResolver.IsApplicable(pawn) ||
                !IsValid(config) || !SSCRestrictionRules.IsValid(rule, value)) return false;
            config.rules.Set(rule, value);
            SSCRestrictionLifecycle.CoordinateTrainer(pawn);
            return true;
        }
    }
}
