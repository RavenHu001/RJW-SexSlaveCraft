using System;
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCRestrictionSource { Saved, Specialization, Equipment, SystemDisabled, DefaultTemplate, SpecializationDefault }

    public sealed class SSCRestrictionResolution
    {
        public SSCRestrictionRule Rule { get; internal set; }
        public SSCRestrictionValue SavedValue { get; internal set; }
        public SSCRestrictionValue Value { get; internal set; }
        public SSCRestrictionSource Source { get; internal set; }
        public string SourceDef { get; internal set; }
        public bool Valid { get; internal set; }
    }

    /// <summary>只读解析条目来源，不协调关系或创建角色配置；每次调用重新读取当前状态，不跨次缓存。</summary>
    public static class SSCRestrictionResolver
    {
        /// <summary>以性奴身份或已有绑定关系判断是否受系统管理；仅持有训练组件不代表适用。</summary>
        public static bool IsApplicable(Pawn pawn)
        {
            return pawn != null && (pawn.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity == PawnIdentity.Slave ||
                SSCBondUtility.GetBoundMaster(pawn) != null);
        }

        /// <summary>检查当前特化或已有巴士状态是否匹配定义，只读取角色而不修正其状态。</summary>
        public static bool HasProfile(Pawn pawn, SSCRestrictionProfileDef profile)
        {
            var context = new ProfileContext(pawn);
            return context.Matches(profile);
        }

        /// <summary>从模板与当前特化默认创建独立配置；非法默认返回具体条目，失败不产生可保存配置。</summary>
        /// <remarks>不写回角色；总开关及特化强制开关不影响初始化默认，装备强制也不写入保存值。</remarks>
        public static bool TryCreateInitialConfiguration(Pawn pawn, SSCRestrictionRules template,
            out SSCRestrictionConfig config, out SSCRestrictionResolution error)
        {
            config = null;
            error = null;
            SSCRestrictionRules defaults = template ?? new SSCRestrictionRules();
            if (defaults.TryGetInvalidRule(out SSCRestrictionRule invalidRule))
            {
                error = FromSaved(defaults, invalidRule);
                error.Source = SSCRestrictionSource.DefaultTemplate;
                return false;
            }

            var context = new ProfileContext(pawn);
            var profiles = new List<SSCRestrictionProfileDef>();
            bool busDefaultsApplied = false;
            foreach (SSCRestrictionProfileDef profile in DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading)
            {
                if (!context.Matches(profile)) continue;
                profiles.Add(profile);
                if (profile.specialization == SexSlaveSpecializationType.Bus) busDefaultsApplied = true;
            }

            SSCRestrictionRules rules = defaults.Copy();
            for (int i = 0; i < SSCRestrictionRules.All.Count; i++)
            {
                SSCRestrictionRule rule = SSCRestrictionRules.All[i];
                SSCRestrictionResolution value = FromSaved(rules, rule);
                var layer = new Layer();
                foreach (SSCRestrictionProfileDef profile in profiles)
                    layer.Consider(rule, profile.defaults, profile.defName);
                layer.ApplyTo(value, SSCRestrictionSource.SpecializationDefault);
                if (!value.Valid) { error = value; return false; }
                rules.Set(rule, value.Value);
            }
            config = new SSCRestrictionConfig { rules = rules, busDefaultsApplied = busDefaultsApplied };
            return true;
        }

        /// <summary>按装备、特化、保存值的优先级解析；任何层无效时保留该层错误来源并立即返回。</summary>
        /// <remarks>全局停用先于配置校验；启用时整份保存规则必须有效。强制值不写回角色。</remarks>
        public static SSCRestrictionResolution Resolve(Pawn pawn, SSCRestrictionRules saved, SSCRestrictionRule rule)
        {
            SSCRestrictionResolution result = FromSaved(saved, rule);
            if (SSCMod.settings != null && !SSCMod.settings.enableSexSlaveProtectionRules)
            {
                result.Value = SSCRestrictionValue.Allow;
                result.Source = SSCRestrictionSource.SystemDisabled;
                result.Valid = true;
                return result;
            }
            if (saved != null && saved.TryGetInvalidRule(out SSCRestrictionRule invalidRule))
                return FromSaved(saved, invalidRule);
            if (!result.Valid) return result;

            if (SSCMod.settings?.enableSpecializationRestrictionOverrides ?? true)
            {
                var context = new ProfileContext(pawn);
                var layer = new Layer();
                foreach (SSCRestrictionProfileDef profile in DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading)
                {
                    if (profile?.forced != null && context.Matches(profile))
                        layer.Consider(rule, profile.forced, profile.defName);
                }
                layer.ApplyTo(result, SSCRestrictionSource.Specialization);
                if (!result.Valid) return result;
            }

            if (pawn?.apparel != null)
            {
                var layer = new Layer();
                foreach (var apparel in pawn.apparel.WornApparel)
                {
                    SSCRestrictionOverrides values = apparel.def.GetModExtension<SSCRestrictionEquipmentExtension>()?.forced;
                    if (values != null) layer.Consider(rule, values, apparel.def.defName);
                }
                layer.ApplyTo(result, SSCRestrictionSource.Equipment);
            }
            return result;
        }

        /// <summary>生成单项保存值与合法性结果；未知条目或缺少规则返回无效，不在查询中抛出参数异常。</summary>
        private static SSCRestrictionResolution FromSaved(SSCRestrictionRules saved, SSCRestrictionRule rule)
        {
            SSCRestrictionValue value = saved != null && SSCRestrictionRules.IsKnown(rule)
                ? saved.Get(rule) : SSCRestrictionValue.Unspecified;
            return new SSCRestrictionResolution
            {
                Rule = rule, SavedValue = value, Value = value, Source = SSCRestrictionSource.Saved,
                Valid = SSCRestrictionRules.IsValid(rule, value)
            };
        }

        /// <summary>仅在单次解析内复用组件及巴士状态查询，不持有跨查询的角色状态缓存。</summary>
        private struct ProfileContext
        {
            private readonly Pawn pawn;
            private readonly SexSlaveSpecializationType specialization;
            private bool busChecked;
            private bool hasBusState;

            /// <summary>读取当前所选方向；仅在需要匹配额外巴士状态时才查询健康状态。</summary>
            public ProfileContext(Pawn pawn)
            {
                this.pawn = pawn;
                specialization = pawn?.TryGetComp<CompSexSlaveTraining>()?.specializationType ?? SexSlaveSpecializationType.None;
                busChecked = false;
                hasBusState = false;
            }

            /// <summary>匹配有效方向或保留的巴士状态；同一次解析最多查询一次巴士健康状态。</summary>
            public bool Matches(SSCRestrictionProfileDef profile)
            {
                if (pawn == null || profile == null || profile.specialization == SexSlaveSpecializationType.None) return false;
                if (specialization == profile.specialization) return true;
                if (profile.specialization != SexSlaveSpecializationType.Bus) return false;
                if (!busChecked)
                {
                    hasBusState = BusSpecializationUtility.HasAnyBusState(pawn);
                    busChecked = true;
                }
                return hasBusState;
            }
        }

        /// <summary>用单次遍历累计同层结果，无需条目对象或排序；非法项优先保留其具体值及来源。</summary>
        private struct Layer
        {
            private bool hasValue;
            private SSCRestrictionValue value;
            private string defName;
            private bool hasError;
            private SSCRestrictionValue invalidValue;
            private string invalidDefName;

            /// <summary>忽略未声明项，合法项取最严格值并按定义名打破平局；非法项也按定义名稳定选取。</summary>
            public void Consider(SSCRestrictionRule rule, SSCRestrictionOverrides values, string sourceDef)
            {
                SSCRestrictionValue candidate = values?.Get(rule) ?? SSCRestrictionValue.Unspecified;
                if (candidate == SSCRestrictionValue.Unspecified) return;
                if (!SSCRestrictionRules.IsValid(rule, candidate))
                {
                    if (!hasError || StringComparer.Ordinal.Compare(sourceDef, invalidDefName) < 0)
                    {
                        hasError = true; invalidValue = candidate; invalidDefName = sourceDef;
                    }
                    return;
                }
                if (!hasValue || candidate < value ||
                    (candidate == value && StringComparer.Ordinal.Compare(sourceDef, defName) < 0))
                {
                    hasValue = true; value = candidate; defName = sourceDef;
                }
            }

            /// <summary>将本层结果一次性合入解析对象，保持保存值；失败时准确返回坏项而非上层或旧值。</summary>
            public void ApplyTo(SSCRestrictionResolution result, SSCRestrictionSource source)
            {
                if (hasError)
                {
                    result.Valid = false;
                    result.Value = invalidValue;
                    result.Source = source;
                    result.SourceDef = invalidDefName;
                }
                else if (hasValue)
                {
                    result.Value = value;
                    result.Source = source;
                    result.SourceDef = defName;
                }
            }
        }
    }
}
