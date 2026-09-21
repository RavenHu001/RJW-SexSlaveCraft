using System;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCRestrictionRule
    {
        Masturbation, ConsensualInitiation, ForcedInitiation,
        ReceiveConsensual, ReceiveForced, ReceiveTraining
    }

    // Unspecified is only valid in file overrides, never in saved rules.
    public enum SSCRestrictionValue { Unspecified = -1, Deny = 0, OwnerOnly = 1, Allow = 2 }

    /// <summary>六项保存值；全局默认使用同一结构，既不包含全局开关，也不包含覆盖结果。</summary>
    public sealed class SSCRestrictionRules : IExposable
    {
        public bool allowMasturbation;
        public SSCRestrictionValue consensualInitiation = SSCRestrictionValue.OwnerOnly;
        public bool allowForcedInitiation;
        public bool receiveConsensual;
        public bool receiveForced;
        public bool receiveTraining;

        /// <summary>读取指定条目的保存值，将布尔条目统一转换为 Allow 或 Deny；未知条目抛出异常。</summary>
        public SSCRestrictionValue Get(SSCRestrictionRule rule)
        {
            switch (rule)
            {
                case SSCRestrictionRule.Masturbation: return Value(allowMasturbation);
                case SSCRestrictionRule.ConsensualInitiation: return consensualInitiation;
                case SSCRestrictionRule.ForcedInitiation: return Value(allowForcedInitiation);
                case SSCRestrictionRule.ReceiveConsensual: return Value(receiveConsensual);
                case SSCRestrictionRule.ReceiveForced: return Value(receiveForced);
                case SSCRestrictionRule.ReceiveTraining: return Value(receiveTraining);
                default: throw new ArgumentOutOfRangeException(nameof(rule));
            }
        }

        /// <summary>校验并写入单项保存值；只有主动普通互动允许 OwnerOnly，非法值不会修改配置。</summary>
        public void Set(SSCRestrictionRule rule, SSCRestrictionValue value)
        {
            if (!IsValid(rule, value)) throw new ArgumentOutOfRangeException(nameof(value));
            switch (rule)
            {
                case SSCRestrictionRule.Masturbation: allowMasturbation = value == SSCRestrictionValue.Allow; break;
                case SSCRestrictionRule.ConsensualInitiation: consensualInitiation = value; break;
                case SSCRestrictionRule.ForcedInitiation: allowForcedInitiation = value == SSCRestrictionValue.Allow; break;
                case SSCRestrictionRule.ReceiveConsensual: receiveConsensual = value == SSCRestrictionValue.Allow; break;
                case SSCRestrictionRule.ReceiveForced: receiveForced = value == SSCRestrictionValue.Allow; break;
                case SSCRestrictionRule.ReceiveTraining: receiveTraining = value == SSCRestrictionValue.Allow; break;
            }
        }

        /// <summary>复制全部值类型字段，避免角色配置与全局默认共用同一规则对象。</summary>
        public SSCRestrictionRules Copy() { return (SSCRestrictionRules)MemberwiseClone(); }

        /// <summary>检查条目及其保存值是否合法；Unspecified 仅供文件覆盖使用，不能成为保存值。</summary>
        public static bool IsValid(SSCRestrictionRule rule, SSCRestrictionValue value)
        {
            return Enum.IsDefined(typeof(SSCRestrictionRule), rule) &&
                (value == SSCRestrictionValue.Allow || value == SSCRestrictionValue.Deny ||
                 (rule == SSCRestrictionRule.ConsensualInitiation && value == SSCRestrictionValue.OwnerOnly));
        }

        /// <summary>将布尔许可转换为规则解析器使用的统一枚举值。</summary>
        private static SSCRestrictionValue Value(bool allowed)
        {
            return allowed ? SSCRestrictionValue.Allow : SSCRestrictionValue.Deny;
        }

        /// <summary>读写六项原始规则；缺失字段使用工厂默认，不把特化或装备覆盖写入保存值。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref allowMasturbation, "allowMasturbation", false);
            Scribe_Values.Look(ref consensualInitiation, "consensualInitiation", SSCRestrictionValue.OwnerOnly);
            Scribe_Values.Look(ref allowForcedInitiation, "allowForcedInitiation", false);
            Scribe_Values.Look(ref receiveConsensual, "receiveConsensual", false);
            Scribe_Values.Look(ref receiveForced, "receiveForced", false);
            Scribe_Values.Look(ref receiveTraining, "receiveTraining", false);
        }
    }

    /// <summary>属于当前 Pawn 的独立配置。null 表示尚未初始化，查询不能自动创建它。</summary>
    public sealed class SSCRestrictionConfig : IExposable
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public SSCRestrictionRules rules = new SSCRestrictionRules();
        public bool busDefaultsApplied;

        /// <summary>复制版本、迁移标记及独立规则对象；保留空规则，交由调用方校验。</summary>
        public SSCRestrictionConfig Copy()
        {
            return new SSCRestrictionConfig
            {
                version = version, rules = rules?.Copy(), busDefaultsApplied = busDefaultsApplied
            };
        }

        /// <summary>读写配置版本、规则和巴士默认应用标记；保留未知版本或缺失规则供校验，不自动修复。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref version, "version", CurrentVersion);
            Scribe_Deep.Look(ref rules, "rules");
            Scribe_Values.Look(ref busDefaultsApplied, "busDefaultsApplied", false);
            // Missing/unknown payloads remain visible to validation; do not reset player choices here.
        }
    }
}
