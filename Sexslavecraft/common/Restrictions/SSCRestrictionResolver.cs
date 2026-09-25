using System;
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
        /// <summary>以真实锁链中的主人引用作为当前生效门槛；仅设置性奴身份或指定调教员不会启用限制。</summary>
        /// <remarks>
        /// 旧档可能尚未补齐 SSC 身份，因此只要已有真实绑定仍须生效。主人死亡不等于解绑，不能借此释放保护。
        /// 未来若加入科技授予机制，应在这个集中入口扩展；界面、许可和生命周期不能各自推断生效条件。
        /// </remarks>
        public static bool IsApplicable(Pawn pawn)
        {
            return SSCBondUtility.GetBoundMaster(pawn) != null;
        }

        /// <summary>检查当前特化及保留的有效状态是否匹配定义，只读取角色而不修正其状态。</summary>
        public static bool HasProfile(Pawn pawn, SSCRestrictionProfileDef profile)
        {
            var context = new ProfileContext(pawn);
            return context.Matches(profile);
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
                    // 稀疏 Profile 只为少数条目提供强制值。先跳过本次未声明的
                    // 条目，再检查训导官持续条件，避免接收许可等无关查询重复读锁链。
                    if (profile?.forced != null && profile.forced.Get(rule) != SSCRestrictionValue.Unspecified
                        && context.Matches(profile))
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
        internal static SSCRestrictionResolution FromSaved(SSCRestrictionRules saved, SSCRestrictionRule rule)
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
        internal struct ProfileContext
        {
            private readonly Pawn pawn;
            private readonly SexSlaveSpecializationType specialization;
            private bool busChecked;
            private bool hasBusState;
            private bool trainerChecked;
            private bool hasTrainerEffect;

            /// <summary>读取当前所选方向；仅在需要匹配额外巴士状态时才查询健康状态。</summary>
            public ProfileContext(Pawn pawn)
            {
                this.pawn = pawn;
                specialization = pawn?.TryGetComp<CompSexSlaveTraining>()?.specializationType ?? SexSlaveSpecializationType.None;
                busChecked = false;
                hasBusState = false;
                trainerChecked = false;
                hasTrainerEffect = false;
            }

            /// <summary>训导官单独检查持续条件与禁用状态；其他方向保持原有匹配规则。</summary>
            public bool Matches(SSCRestrictionProfileDef profile)
            {
                if (pawn == null || profile == null || profile.specialization == SexSlaveSpecializationType.None) return false;

                // 训导官从选择方向时就有主动行为强制效果，但 20% 任职资格
                // 不能用于此处；终极标记还允许在切换培养方向后继续匹配。
                // 必须先于通用的“方向相同即匹配”分支，才能排除失格与禁用。
                if (profile.specialization == SexSlaveSpecializationType.TrainerOfficer)
                {
                    if (!trainerChecked)
                    {
                        hasTrainerEffect = TrainerSpecializationUtility.HasActiveRestrictionEffect(pawn);
                        trainerChecked = true;
                    }
                    return hasTrainerEffect;
                }

                // 巴士和现有方向维持原行为；同一次解析只重复使用已读取的状态。
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
        internal struct Layer
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
