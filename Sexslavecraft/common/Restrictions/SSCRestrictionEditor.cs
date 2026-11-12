using Verse;

namespace SexSlaveCraft
{
    /// <summary>所有设置界面的配置写入入口，拒绝覆盖待迁移数据并在成功编辑后协调指定者。</summary>
    public static partial class SSCRestrictionEditor
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
            if (comp == null || (!SSCRestrictionResolver.IsApplicable(pawn) && !SSCSelfTrainingEligibility.IsEligible(pawn)) ||
                comp.restrictionConfig != null ||
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
            => TrySetCore(pawn, rule, value, null);

        /// <summary>角色行为页使用原按压时的配置和保存值，并在写入前重新解析强制来源。</summary>
        internal static bool TrySetFromView(Pawn pawn, SSCRestrictionRule rule, SSCRestrictionValue value,
            SSCRestrictionConfig expectedConfig, SSCRestrictionRules expectedRules, SSCRestrictionValue expectedValue,
            SSCRestrictionValue expectedTarget)
        {
            return TrySetCore(pawn, rule, value, (comp, config) =>
            {
                if (config != expectedConfig || config.rules != expectedRules) return false;
                var entry = SSCRestrictionResolver.Resolve(pawn, expectedRules, rule);
                // 解析可能调用资格查询，随后再次确认实际写入对象仍属于原控件。
                return entry.Valid && entry.Source != SSCRestrictionSource.Equipment && entry.Source != SSCRestrictionSource.Specialization &&
                    pawn.TryGetComp<CompSexSlaveTraining>() == comp && comp.restrictionConfig == config &&
                    config.rules == expectedRules && expectedRules.Get(rule) == expectedValue &&
                    (rule != SSCRestrictionRule.ConsensualInitiation || expectedRules.ConsensualTarget == expectedTarget);
            });
        }

        /// <summary>共用保存与指定者协调。原入口仍可编辑强制值底下的个人偏好。</summary>
        private static bool TrySetCore(Pawn pawn, SSCRestrictionRule rule, SSCRestrictionValue value,
            System.Func<CompSexSlaveTraining, SSCRestrictionConfig, bool> currentView)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            SSCRestrictionConfig config = comp?.restrictionConfig;
            bool applicable = rule == SSCRestrictionRule.SelfTraining
                ? SSCSelfTrainingEligibility.IsEligible(pawn) : SSCRestrictionResolver.IsApplicable(pawn);
            if (comp == null || comp.restrictionRestoreDepth > 0 || !applicable ||
                !IsValid(config) || !SSCRestrictionRules.IsValid(rule, value) ||
                (currentView != null && !currentView(comp, config))) return false;
            config.rules.Set(rule, value);
            SSCRestrictionLifecycle.CoordinateTrainer(pawn);
            return true;
        }
    }
}
