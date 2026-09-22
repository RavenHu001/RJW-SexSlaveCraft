using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>构建独立配置候选；不写角色、指派或输入配置，由生命周期或显式编辑入口决定是否提交。</summary>
    public static class SSCRestrictionConfigurationBuilder
    {
        /// <summary>从模板与当前特化默认构建独立初始配置，全部条目合法后才返回可提交结果。</summary>
        /// <remarks>默认不是实时强制层：总开关及特化强制开关不影响初始化，装备强制永远不进入保存值。</remarks>
        public static bool TryCreateInitial(Pawn pawn, SSCRestrictionRules template,
            out SSCRestrictionConfig candidate, out SSCRestrictionResolution error)
        {
            candidate = null;
            error = null;
            SSCRestrictionRules defaults = template ?? new SSCRestrictionRules();
            if (defaults.TryGetInvalidRule(out SSCRestrictionRule invalidRule))
            {
                error = SSCRestrictionResolver.FromSaved(defaults, invalidRule);
                error.Source = SSCRestrictionSource.DefaultTemplate;
                return false;
            }

            List<SSCRestrictionProfileDef> profiles = MatchingProfiles(pawn, false);
            if (!TryBuildRules(defaults, profiles, out SSCRestrictionRules rules, out error)) return false;
            candidate = new SSCRestrictionConfig
            {
                rules = rules,
                busDefaultsApplied = profiles.Exists(profile => profile.specialization == SexSlaveSpecializationType.Bus)
            };
            return true;
        }

        /// <summary>将首次公交车默认合成到输入配置的独立副本，成功或失败均不改动调用方原对象。</summary>
        /// <remarks>候选保留配置版本及开关关闭前的对象偏好；生命周期只在整份候选有效时原子替换角色配置。</remarks>
        public static bool TryApplyBusDefaults(Pawn pawn, SSCRestrictionConfig existing,
            out SSCRestrictionConfig candidate, out SSCRestrictionResolution error)
        {
            candidate = null;
            error = null;
            if (existing?.IsValid() != true) return false;
            if (!TryBuildRules(existing.rules, MatchingProfiles(pawn, true), out SSCRestrictionRules rules, out error))
                return false;
            candidate = existing.Copy();
            candidate.rules = rules;
            candidate.busDefaultsApplied = true;
            return true;
        }

        /// <summary>为一次构建收集当前匹配的特化；复用解析器的只读匹配上下文，避免重复查询巴士健康状态。</summary>
        private static List<SSCRestrictionProfileDef> MatchingProfiles(Pawn pawn, bool onlyBus)
        {
            var context = new SSCRestrictionResolver.ProfileContext(pawn);
            var profiles = new List<SSCRestrictionProfileDef>();
            foreach (SSCRestrictionProfileDef profile in DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading)
                if ((!onlyBus || profile?.specialization == SexSlaveSpecializationType.Bus) && context.Matches(profile))
                    profiles.Add(profile);
            return profiles;
        }

        /// <summary>在副本上合入所有默认层并验证每项结果；失败丢弃整个候选，不留下部分改写。</summary>
        private static bool TryBuildRules(SSCRestrictionRules original, List<SSCRestrictionProfileDef> profiles,
            out SSCRestrictionRules rules, out SSCRestrictionResolution error)
        {
            rules = original.Copy();
            error = null;
            foreach (SSCRestrictionRule rule in SSCRestrictionRules.All)
            {
                SSCRestrictionResolution value = SSCRestrictionResolver.FromSaved(rules, rule);
                var layer = new SSCRestrictionResolver.Layer();
                foreach (SSCRestrictionProfileDef profile in profiles)
                    layer.Consider(rule, profile.defaults, profile.defName);
                layer.ApplyTo(value, SSCRestrictionSource.SpecializationDefault);
                if (!value.Valid)
                {
                    error = value;
                    rules = null;
                    return false;
                }
                // Copy 保留界面偏好；未声明条目仍调用合法 Set，关闭值不会抹去先前对象选择。
                rules.Set(rule, value.Value);
            }
            return true;
        }
    }
}
