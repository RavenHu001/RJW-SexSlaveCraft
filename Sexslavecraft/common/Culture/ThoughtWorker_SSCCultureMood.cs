// AI 辅助生成：1.7 文化持续心情；保留原版资格、挂载、来源与刷新流程。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class ThoughtWorker_SSCCultureMood : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            // 基类已经核对观察者文化与 thought 挂载；个人状态读取现状，不建立人口快照。
            var config = def.GetModExtension<SSCCultureThoughtExtension>();
            if (!ModsConfig.IdeologyActive || !SSCCultureStateUtility.IsLiving(p) || config == null)
                return ThoughtState.Inactive;
            switch (config.condition)
            {
                case SSCCultureThoughtCondition.ChainedSelf:
                    return SSCCultureStateUtility.HasChain(p);
                case SSCCultureThoughtCondition.EnvySelf:
                    // 一条 thought 切换阶段：链优先，+10 与 -6 不可能同时存在；资格和途径预选不参与。
                    if (SSCCultureStateUtility.HasChain(p)) return ThoughtState.ActiveAtStage(0);
                    return SSCCultureStateUtility.IsMaster(p) ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(1);
                case SSCCultureThoughtCondition.OfficerSelf:
                    return SSCCultureStateUtility.IsActiveOfficer(p);
                case SSCCultureThoughtCondition.MasterSelf:
                    return SSCCultureStateUtility.IsMaster(p);
                case SSCCultureThoughtCondition.ActualMaster:
                    // 只认实际锁链指向的有效主人，不以指定调教员或永久主人资格兜底。
                    return SSCCultureStateUtility.TryGetActualMaster(p, out _);
                case SSCCultureThoughtCondition.ManagedChainedEnvironment:
                    return TryEnvironmentCount(p, out int count) && count > 0;
                case SSCCultureThoughtCondition.Leader:
                    // 接收者和任职者共用身份过滤前的成员名册；未就绪不能冒充有效空缺。
                    var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
                    if (cache == null || !cache.TryIsMember(p, out bool member) || !member
                        || !cache.TryGetLeader(out SSCCultureLeaderResult leader)) return ThoughtState.Inactive;
                    if (leader.Conflict || leader.State == SSCCultureLeaderState.None) return ThoughtState.Inactive;
                    if (leader.State == SSCCultureLeaderState.Master) return ThoughtState.ActiveAtStage(0);
                    return config.nonMasterLeaderStage < 0 ? ThoughtState.Inactive
                        : ThoughtState.ActiveAtStage(config.nonMasterLeaderStage);
                default:
                    return ThoughtState.Inactive;
            }
        }

        public override float MoodMultiplier(Pawn p)
        {
            // 固定个人反馈不查询组件。环境效果通过原版 MoodOffset 乘人数，不创建按人数增长的阶段。
            if (def.GetModExtension<SSCCultureThoughtExtension>()?.condition != SSCCultureThoughtCondition.ManagedChainedEnvironment)
                return 1f;
            // 原版缓存可能尚未重算；再次核对挂载和本人链，避免改宗/取得链后继续给出旧环境效果。
            if (!ModsConfig.IdeologyActive || p?.Ideo == null || !p.Ideo.cachedPossibleSituationalThoughts.Contains(def)) return 0f;
            return TryEnvironmentCount(p, out int count) ? count : 0f;
        }

        private static bool TryEnvironmentCount(Pawn p, out int count)
        {
            count = 0;
            // 先做个人短路：持链者只获本人项；无效/非玩家对象不触发人口收集。
            if (!SSCCultureStateUtility.IsLiving(p) || p.Faction != Faction.OfPlayer
                || SSCCultureStateUtility.HasChain(p)) return false;
            var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
            // 接收者用成员名册，计数对象用管理持链集合；后者包含囚犯，不能被前者替代。
            return cache != null && cache.TryIsMember(p, out bool member) && member
                && cache.TryGetManagedChainedCount(out count);
        }
    }
}
