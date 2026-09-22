using System;
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>文件中的稀疏覆盖；未声明项保持 Unspecified，不能被解释成禁止。</summary>
    public sealed class SSCRestrictionOverrides
    {
        public SSCRestrictionValue masturbation = SSCRestrictionValue.Unspecified;
        public SSCRestrictionValue consensualInitiation = SSCRestrictionValue.Unspecified;
        public SSCRestrictionValue forcedInitiation = SSCRestrictionValue.Unspecified;
        public SSCRestrictionValue receiveConsensual = SSCRestrictionValue.Unspecified;
        public SSCRestrictionValue receiveForced = SSCRestrictionValue.Unspecified;
        public SSCRestrictionValue receiveTraining = SSCRestrictionValue.Unspecified;

        /// <summary>读取文件声明的单项覆盖；未声明项保留 Unspecified，未知条目抛出异常。</summary>
        public SSCRestrictionValue Get(SSCRestrictionRule rule)
        {
            switch (rule)
            {
                case SSCRestrictionRule.Masturbation: return masturbation;
                case SSCRestrictionRule.ConsensualInitiation: return consensualInitiation;
                case SSCRestrictionRule.ForcedInitiation: return forcedInitiation;
                case SSCRestrictionRule.ReceiveConsensual: return receiveConsensual;
                case SSCRestrictionRule.ReceiveForced: return receiveForced;
                case SSCRestrictionRule.ReceiveTraining: return receiveTraining;
                default: throw new ArgumentOutOfRangeException(nameof(rule));
            }
        }

        /// <summary>逐项报告显式覆盖中的非法值，允许未声明项继续使用下层配置。</summary>
        public IEnumerable<string> ConfigErrors()
        {
            foreach (SSCRestrictionRule rule in Enum.GetValues(typeof(SSCRestrictionRule)))
            {
                SSCRestrictionValue value = Get(rule);
                if (value != SSCRestrictionValue.Unspecified && !SSCRestrictionRules.IsValid(rule, value))
                    yield return "Invalid restriction override: " + rule + " = " + value;
            }
        }
    }

    /// <summary>特化只声明默认及强制条目；角色不持有逐特化配置表。</summary>
    public sealed class SSCRestrictionProfileDef : Def
    {
        public SexSlaveSpecializationType specialization;
        public SSCRestrictionOverrides defaults;
        public SSCRestrictionOverrides forced;

        /// <summary>检查特化类型以及默认、强制两组条目，并保留基础 Def 的校验错误。</summary>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (specialization == SexSlaveSpecializationType.None ||
                !Enum.IsDefined(typeof(SexSlaveSpecializationType), specialization))
                yield return "A restriction profile requires a known specialization.";
            if (defaults != null) foreach (string error in defaults.ConfigErrors()) yield return "defaults: " + error;
            if (forced != null) foreach (string error in forced.ConfigErrors()) yield return "forced: " + error;
        }
    }

    /// <summary>装备仅提供强制条目，不自行返回行为许可。</summary>
    public sealed class SSCRestrictionEquipmentExtension : DefModExtension
    {
        public SSCRestrictionOverrides forced;

        /// <summary>确认装备声明了强制条目对象，并报告其中非法的规则值。</summary>
        public override IEnumerable<string> ConfigErrors()
        {
            if (forced == null) yield return "Restriction equipment must declare forced entries.";
            else foreach (string error in forced.ConfigErrors()) yield return error;
        }
    }
}
