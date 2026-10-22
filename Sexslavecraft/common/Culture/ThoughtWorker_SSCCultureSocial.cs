// AI 辅助生成：1.7 文化条件社交；观察者文化决定评价，不写永久关系或记忆。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class ThoughtWorker_SSCCultureSocial : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn p, Pawn otherPawn)
        {
            // 原版基类负责观察者挂载；无需目标同文化，不对自身、死亡或丢弃对象产生评价。
            var config = def.GetModExtension<SSCCultureThoughtExtension>();
            if (!ModsConfig.IdeologyActive || config == null || p == otherPawn
                || !SSCCultureStateUtility.IsLiving(p) || !SSCCultureStateUtility.IsLiving(otherPawn))
                return ThoughtState.Inactive;
            switch (config.condition)
            {
                case SSCCultureThoughtCondition.ChainedOther:
                    // 艳羡额外限制作用于观察者；持链目标即使没有 Slave 途径预选也仍可被评价。
                    if (config.requireEnviousObserver && (SSCCultureStateUtility.HasChain(p) || SSCCultureStateUtility.IsMaster(p)))
                        return ThoughtState.Inactive;
                    // 神圣过滤目标；艳羡另外过滤观察者。尊重 +5 及既有敌视/厌恶未启用此开关。
                    if (config.requireAcceptedSlaveGender)
                    {
                        var policy = SSCCultureGenderUtility.Policy(p);
                        if (!SSCCultureGenderUtility.Accepts(policy, otherPawn.gender)
                            || config.requireEnviousObserver && !SSCCultureGenderUtility.Accepts(policy, p.gender))
                            return ThoughtState.Inactive;
                    }
                    return SSCCultureStateUtility.HasChain(otherPawn);
                case SSCCultureThoughtCondition.GenderViolationOther:
                    // 不要求目标入册，双方均需成年；主人或有链观察者同样可以产生新的规范评价。
                    return SSCCultureGenderUtility.IsAdult(p)
                        && SSCCultureGenderUtility.IsViolation(otherPawn, SSCCultureGenderUtility.Policy(p));
                case SSCCultureThoughtCondition.UnfulfilledDutyOther:
                    return SSCCultureGenderUtility.DutySocial(p, otherPawn, SSCCultureGenderUtility.Policy(p));
                case SSCCultureThoughtCondition.OfficerOther:
                    // 复用有效任职，不把主人自带的调教能力或单独特化资格误认作训导官。
                    return SSCCultureStateUtility.IsActiveOfficer(otherPawn);
                case SSCCultureThoughtCondition.MasterOther:
                    // 评价任何另一名当前主人，不另叠“自己的主人”或“领袖”的文化社交奖励。
                    return SSCCultureStateUtility.IsMaster(otherPawn);
                case SSCCultureThoughtCondition.SexSlaveLeaderOther:
                    // 先排除无链目标，不为普通人物收集人口；实际职位及资格复用同一代正式领袖快照。
                    // 评价由观察者戒律决定，沿用一般态度社交的观察者范围，不要求同文化或主人身份。
                    if (!SSCCultureStateUtility.HasChain(otherPawn)) return ThoughtState.Inactive;
                    var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
                    return cache != null && cache.TryGetLeader(out SSCCultureLeaderResult leader)
                        && !leader.Conflict && leader.State == SSCCultureLeaderState.SexSlave
                        && ReferenceEquals(leader.Pawn, otherPawn);
                case SSCCultureThoughtCondition.OtherMasterBoundSlaveCountExcess:
                    // 原版 OpinionOffset 不调用 MoodMultiplier；用三个固定阶段表达 -5/-15/-30。
                    // 双方名册限制只属于本分支，保留步骤 2 态度社交原有的接收范围。
                    return SSCCultureCountUtility.TryGetSocialExcess(p, otherPawn, config, out int excess) && excess > 0
                        ? ThoughtState.ActiveAtStage(excess - 1) : ThoughtState.Inactive;
                default:
                    return ThoughtState.Inactive;
            }
        }
    }
}
