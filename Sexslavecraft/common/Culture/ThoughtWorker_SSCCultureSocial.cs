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
                    return SSCCultureStateUtility.HasChain(otherPawn);
                case SSCCultureThoughtCondition.OfficerOther:
                    // 复用有效任职，不把主人自带的调教能力或单独特化资格误认作训导官。
                    return SSCCultureStateUtility.IsActiveOfficer(otherPawn);
                case SSCCultureThoughtCondition.MasterOther:
                    // 评价任何另一名当前主人，不另叠“自己的主人”或“领袖”的文化社交奖励。
                    return SSCCultureStateUtility.IsMaster(otherPawn);
                default:
                    return ThoughtState.Inactive;
            }
        }
    }
}
