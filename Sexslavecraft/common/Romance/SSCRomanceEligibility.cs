// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>执行所需的参与者与关系条件；不把自主权重、好感或接受意愿当作必要关系。</summary>
    public enum SSCRomanceEligibilityReason { None, InvalidParticipants, AlreadyPartners, LoverRequired, NoRelationship, UnknownAction }

    public static class SSCRomanceEligibility
    {
        /// <summary>只读检查原版三类互动的必要前提；距离、清醒、能力和冷却仍由原互动入口负责。</summary>
        public static SSCRomanceEligibilityReason Check(Pawn actor, Pawn target, SSCRomanceAction action)
        {
            // Dead 与 RaceProps 的原生 getter 依赖载体字段；先检查字段，坏外部引用不能在拒绝前抛异常。
            if (actor == null || target == null || actor == target || actor.health == null || target.health == null || actor.Dead || target.Dead ||
                actor.Destroyed || target.Destroyed || actor.Discarded || target.Discarded ||
                actor.def?.race?.Humanlike != true || target.def?.race?.Humanlike != true ||
                actor.relations == null || target.relations == null || actor.needs == null || target.needs == null)
                return SSCRomanceEligibilityReason.InvalidParticipants;
            // 只识别三种原版当前关系；SSC 奖励关系不是恋人关系，也不能替代求婚的 Lover 前提。
            bool lover = actor.relations.DirectRelationExists(PawnRelationDefOf.Lover, target);
            bool fiance = actor.relations.DirectRelationExists(PawnRelationDefOf.Fiance, target);
            bool spouse = actor.relations.DirectRelationExists(PawnRelationDefOf.Spouse, target);
            switch (action)
            {
                case SSCRomanceAction.Romance:
                    return lover || fiance || spouse ? SSCRomanceEligibilityReason.AlreadyPartners : SSCRomanceEligibilityReason.None;
                case SSCRomanceAction.Proposal:
                    return lover && !fiance && !spouse ? SSCRomanceEligibilityReason.None : SSCRomanceEligibilityReason.LoverRequired;
                case SSCRomanceAction.Breakup:
                    return lover || fiance || spouse ? SSCRomanceEligibilityReason.None : SSCRomanceEligibilityReason.NoRelationship;
                default: return SSCRomanceEligibilityReason.UnknownAction;
            }
        }
    }
}
