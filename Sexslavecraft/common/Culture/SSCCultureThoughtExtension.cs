// AI 辅助生成：文化 thought 的只读条件和有限上限；ThoughtStage 保存基础值，数量心情由原版倍率结算。
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
        ChainedOther, OfficerOther, MasterOther,
        MasterCountExcess, BoundSlaveCountExcess, OtherMasterBoundSlaveCountExcess,
        SexSlaveLeaderOther,
        GenderViolationSelf, UnfulfilledDutySelf, GenderViolationEnvironment, UnfulfilledDutyEnvironment,
        GenderViolationOther, UnfulfilledDutyOther
    }

    public sealed class SSCCultureThoughtExtension : DefModExtension
    {
        public SSCCultureThoughtCondition condition;
        // 仅艳羡社交使用：观察者本人未持链且非当前主人，目标条件仍是实际持链。
        public bool requireEnviousObserver;
        // 仅神圣本人/社交与艳羡本人/社交显式启用；尊重和既有负向反馈不按性别取消。
        public bool requireAcceptedSlaveGender;
        // 仅神圣的正向管理环境使用；不可把此过滤传入数量、敌视或厌恶统计。
        public bool filterAcceptedManagedGender;
        // 领袖主人固定用阶段 0；非主人可显式配置阶段 1，-1 表示该分支没有反馈。
        public int nonMasterLeaderStage = -1;
        // 尊奉/圣主的性奴领袖使用独立阶段 2，替代普通非主人阶段；缺省保持旧档位反馈。
        public int sexSlaveLeaderStage = -1;
        // 义务本人项的艳羡专属阶段；默认 -1 保持单阶段，配置 1 时替代普通义务，不额外挂一份惩罚。
        public int enviousDutyStage = -1;
        // 数量条件显式配置有限上限；0 仅表示旧条件未配置，不能用它表达“不限”。不限戒律无组件。
        public int countLimit;
        // 可选环境人数倍率曲线；缺省保留线性人数倍率，神圣由 XML 配置五人峰值。
        public SimpleCurve environmentMoodCurve;

        public bool IsCountCondition => condition == SSCCultureThoughtCondition.MasterCountExcess
            || condition == SSCCultureThoughtCondition.BoundSlaveCountExcess
            || condition == SSCCultureThoughtCondition.OtherMasterBoundSlaveCountExcess;
        public bool IsGenderEnvironment => condition == SSCCultureThoughtCondition.GenderViolationEnvironment
            || condition == SSCCultureThoughtCondition.UnfulfilledDutyEnvironment;

        public override IEnumerable<string> ConfigErrors()
        {
            // 错误配置在 Def 检查阶段报出，不在运行查询中静默转换成另一种玩法。
            if (condition == SSCCultureThoughtCondition.Unspecified || !Enum.IsDefined(typeof(SSCCultureThoughtCondition), condition))
                yield return "SSC culture thought requires a defined condition.";
            if (requireEnviousObserver && condition != SSCCultureThoughtCondition.ChainedOther)
                yield return "SSC envy observer restriction is only valid for ChainedOther.";
            // 显式约束新开关的作用域，避免 XML 拼错条件后静默过滤主人、训导官或旧负向项。
            if (requireAcceptedSlaveGender && condition != SSCCultureThoughtCondition.ChainedSelf
                && condition != SSCCultureThoughtCondition.EnvySelf && condition != SSCCultureThoughtCondition.ChainedOther)
                yield return "SSC accepted gender restriction requires a chained self/other or envy condition.";
            if (filterAcceptedManagedGender && condition != SSCCultureThoughtCondition.ManagedChainedEnvironment)
                yield return "SSC accepted managed gender filter requires ManagedChainedEnvironment.";
            if (IsGenderEnvironment && environmentMoodCurve == null)
                yield return "SSC gender environment requires a cumulative mood curve.";
            if (nonMasterLeaderStage != -1 && (condition != SSCCultureThoughtCondition.Leader || nonMasterLeaderStage != 1))
                yield return "SSC non-master leader stage must be -1, or stage 1 on Leader.";
            if (sexSlaveLeaderStage != -1 && (condition != SSCCultureThoughtCondition.Leader || sexSlaveLeaderStage != 2))
                yield return "SSC sex-slave leader stage must be -1, or stage 2 on Leader.";
            if (enviousDutyStage != -1 && (condition != SSCCultureThoughtCondition.UnfulfilledDutySelf || enviousDutyStage != 1))
                yield return "SSC envious duty stage must be -1, or stage 1 on UnfulfilledDutySelf.";
            // 有限档仅允许 1/3；旧条件带数量配置同样报错，避免拼错条件后静默套用另一套逻辑。
            if (IsCountCondition ? countLimit != 1 && countLimit != 3 : countLimit != 0)
                yield return "SSC count limit must be 1 or 3 on a count condition, and 0 otherwise.";
            // 曲线只服务环境反馈；复用原版排序检查，并补原版未报错的空曲线，避免运行时求值报错。
            if (environmentMoodCurve != null)
            {
                if (condition != SSCCultureThoughtCondition.ManagedChainedEnvironment && !IsGenderEnvironment)
                    yield return "SSC environment mood curve requires a managed or gender environment condition.";
                if (environmentMoodCurve.PointsCount == 0)
                    yield return "SSC environment mood curve must contain points.";
                foreach (string error in environmentMoodCurve.ConfigErrors("SSC environment mood curve"))
                    yield return error;
            }
        }
    }
}
