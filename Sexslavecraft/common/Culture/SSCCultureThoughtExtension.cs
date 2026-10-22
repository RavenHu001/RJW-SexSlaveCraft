// AI 辅助生成：文化 thought 的只读配置；数值仍由原版 ThoughtStage 保存。
using System;
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    // 按反馈语义分支，不按戒律档位复制 worker，也不从 defName 或 impact 猜条件。
    public enum SSCCultureThoughtCondition
    {
        Unspecified, ChainedSelf, EnvySelf, ManagedChainedEnvironment,
        OfficerSelf, MasterSelf, ActualMaster, Leader,
        ChainedOther, OfficerOther, MasterOther
    }

    public sealed class SSCCultureThoughtExtension : DefModExtension
    {
        public SSCCultureThoughtCondition condition;
        // 仅艳羡社交使用：观察者本人未持链且非当前主人，目标条件仍是实际持链。
        public bool requireEnviousObserver;
        // 领袖主人固定用阶段 0；非主人可显式配置阶段 1，-1 表示该分支没有反馈。
        public int nonMasterLeaderStage = -1;

        public override IEnumerable<string> ConfigErrors()
        {
            // 错误配置在 Def 检查阶段报出，不在运行查询中静默转换成另一种玩法。
            if (condition == SSCCultureThoughtCondition.Unspecified || !Enum.IsDefined(typeof(SSCCultureThoughtCondition), condition))
                yield return "SSC culture thought requires a defined condition.";
            if (requireEnviousObserver && condition != SSCCultureThoughtCondition.ChainedOther)
                yield return "SSC envy observer restriction is only valid for ChainedOther.";
            if (nonMasterLeaderStage != -1 && (condition != SSCCultureThoughtCondition.Leader || nonMasterLeaderStage != 1))
                yield return "SSC non-master leader stage must be -1, or stage 1 on Leader.";
        }
    }
}
