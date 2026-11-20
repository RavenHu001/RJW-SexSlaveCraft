using System;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCRestrictionSource { Saved, Specialization, Equipment, SystemDisabled, DefaultTemplate, SpecializationDefault, OwnerlessStage }

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
        /// <summary>实际绑定和正式无主阶段都应用限制。指派只决定谁可以调教。</summary>
        /// <remarks>
        /// 旧档可能尚未补齐 SSC 身份，因此只要已有真实绑定仍须生效。主人死亡不等于解绑，不能借此释放保护。
        /// 未来若加入科技授予机制，应在这个集中入口扩展；界面、许可和生命周期不能各自推断生效条件。
        /// </remarks>
        public static bool IsApplicable(Pawn pawn)
        {
            return SSCBondUtility.GetBoundMaster(pawn) != null || GetOwnerlessStage(pawn) > 0;
        }

        internal static int GetOwnerlessStage(Pawn pawn)
        {
#if SSC_RW17
            return Hediff_SSCOwnerlessSlave.GetActiveStage(pawn);
#else
            return 0;
#endif
        }

        /// <summary>正式无主可以适用规则，但个人配置暂停编辑，保留原选择供之后实际绑定时使用。</summary>
        internal static bool CanEditPersonalRules(Pawn pawn)
            => GetOwnerlessStage(pawn) == 0 &&
                (IsApplicable(pawn) || SSCSelfTrainingEligibility.IsEligible(pawn));

        /// <summary>检查当前特化及保留的有效状态是否匹配定义，只读取角色而不修正其状态。</summary>
        public static bool HasProfile(Pawn pawn, SSCRestrictionProfileDef profile)
        {
            var context = new ProfileContext(pawn);
            return context.Matches(profile);
        }

        /// <summary>先解析保存值或无主阶段，再应用特化和装备，返回当前值及来源。</summary>
        /// <remarks>全局停用先于配置校验。有主配置整体校验，无主派生值独立于保留的个人配置。</remarks>
        public static SSCRestrictionResolution Resolve(Pawn pawn, SSCRestrictionRules saved, SSCRestrictionRule rule)
        {
            int stage = GetOwnerlessStage(pawn);
            SSCRestrictionResolution result = stage > 0 ? FromOwnerlessStage(stage, rule) : FromSaved(saved, rule);
            if (SSCMod.settings != null && !SSCMod.settings.enableSexSlaveProtectionRules)
            {
                result.Value = SSCRestrictionValue.Allow;
                result.Source = SSCRestrictionSource.SystemDisabled;
                result.Valid = true;
                return result;
            }
            if (stage == 0 && saved != null && saved.TryGetInvalidRule(out SSCRestrictionRule invalidRule))
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

        private static SSCRestrictionResolution FromOwnerlessStage(int stage, SSCRestrictionRule rule)
        {
            // 当前只收紧三种主动行为。启用自我调教和三项接收条目，任务准入仍检查实际参与者。
            // SavedValue 保持未指定，因为阶段值和覆盖项都不会成为个人保存值。
            bool allowed = rule switch
            {
                SSCRestrictionRule.Masturbation or SSCRestrictionRule.ConsensualInitiation => stage < 3,
                SSCRestrictionRule.ForcedInitiation => false,
                _ => SSCRestrictionRules.IsKnown(rule)
            };
            return new SSCRestrictionResolution
            {
                Rule = rule, SavedValue = SSCRestrictionValue.Unspecified,
                Value = allowed ? SSCRestrictionValue.Allow : SSCRestrictionValue.Deny,
                Source = SSCRestrictionSource.OwnerlessStage, Valid = SSCRestrictionRules.IsKnown(rule)
            };
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
