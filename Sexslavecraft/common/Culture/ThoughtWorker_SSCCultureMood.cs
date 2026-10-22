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
                    return SSCCultureStateUtility.HasChain(p) && (!config.requireAcceptedSlaveGender
                        || SSCCultureGenderUtility.Accepts(SSCCultureGenderUtility.Policy(p), p.gender));
                case SSCCultureThoughtCondition.EnvySelf:
                    // 一条 thought 切换阶段：链优先，+10 与 -6 不可能同时存在；资格和途径预选不参与。
                    var policy = SSCCultureGenderUtility.Policy(p);
                    if (!SSCCultureGenderUtility.Accepts(policy, p.gender)) return ThoughtState.Inactive;
                    if (SSCCultureStateUtility.HasChain(p)) return ThoughtState.ActiveAtStage(0);
                    // 新义务使用自身普通/艳羡互斥阶段，只有实际适用且入册时才替代旧愿望 -6。
                    return SSCCultureStateUtility.IsMaster(p) || SSCCultureGenderUtility.HasUnfulfilledDuty(p, policy)
                        ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(1);
                case SSCCultureThoughtCondition.GenderViolationSelf:
                    return SSCCultureGenderUtility.IsViolation(p, SSCCultureGenderUtility.Policy(p));
                case SSCCultureThoughtCondition.UnfulfilledDutySelf:
                    // 同一个持续 thought 切换专属阶段及文案，普通义务、艳羡义务和旧愿望不会重复扣分。
                    if (!SSCCultureGenderUtility.HasUnfulfilledDuty(p, SSCCultureGenderUtility.Policy(p)))
                        return ThoughtState.Inactive;
                    return ThoughtState.ActiveAtStage(config.enviousDutyStage >= 0 && SSCCultureGenderUtility.HasEnvy(p)
                        ? config.enviousDutyStage : 0);
                case SSCCultureThoughtCondition.GenderViolationEnvironment:
                case SSCCultureThoughtCondition.UnfulfilledDutyEnvironment:
                    return SSCCultureGenderUtility.TryEnvironmentCount(p,
                        config.condition == SSCCultureThoughtCondition.UnfulfilledDutyEnvironment, out int offenders) && offenders > 0;
                case SSCCultureThoughtCondition.OfficerSelf:
                    return SSCCultureStateUtility.IsActiveOfficer(p);
                case SSCCultureThoughtCondition.MasterSelf:
                    return SSCCultureStateUtility.IsMaster(p);
                case SSCCultureThoughtCondition.ActualMaster:
                    // 只认实际锁链指向的有效主人，不以指定调教员或永久主人资格兜底。
                    return SSCCultureStateUtility.TryGetActualMaster(p, out _);
                case SSCCultureThoughtCondition.ManagedChainedEnvironment:
                    return TryEnvironmentCount(p, config, out int count) && count > 0;
                case SSCCultureThoughtCondition.MasterCountExcess:
                case SSCCultureThoughtCondition.BoundSlaveCountExcess:
                    // 仅超额才创建数量项；激活和结算共用查询，不能把不可查询状态当成达标人数。
                    return SSCCultureCountUtility.TryGetMoodExcess(p, config, out int excess) && excess > 0;
                case SSCCultureThoughtCondition.Leader:
                    // 接收者和任职者共用身份过滤前的成员名册；未就绪不能冒充有效空缺。
                    var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
                    if (cache == null || !cache.TryIsMember(p, out bool member) || !member
                        || !cache.TryGetLeader(out SSCCultureLeaderResult leader)) return ThoughtState.Inactive;
                    if (leader.Conflict || leader.State == SSCCultureLeaderState.None) return ThoughtState.Inactive;
                    if (leader.State == SSCCultureLeaderState.Master)
                    {
                        // 主人自身项已经反馈身份；只取消对当前主人的额外正向领袖奖励。
                        // 敌视/厌恶的负向领袖项仍保留，主人也不能免除普通非主人或性奴领袖惩罚。
                        if (SSCCultureStateUtility.IsMaster(p) && def.stages[0].baseMoodEffect > 0f)
                            return ThoughtState.Inactive;
                        return ThoughtState.ActiveAtStage(0);
                    }
                    // 性奴领袖用一个互斥阶段替代普通非主人项，不叠加两份领袖惩罚。
                    // 领袖本人遵循同一负面反馈规则；对自身的社交仍由原版双人入口排除。
                    if (leader.State == SSCCultureLeaderState.SexSlave && config.sexSlaveLeaderStage >= 0)
                        return ThoughtState.ActiveAtStage(config.sexSlaveLeaderStage);
                    return config.nonMasterLeaderStage < 0 ? ThoughtState.Inactive
                        : ThoughtState.ActiveAtStage(config.nonMasterLeaderStage);
                default:
                    return ThoughtState.Inactive;
            }
        }

        public override float MoodMultiplier(Pawn p)
        {
            var config = def.GetModExtension<SSCCultureThoughtExtension>();
            bool quantity = config?.condition == SSCCultureThoughtCondition.MasterCountExcess
                || config?.condition == SSCCultureThoughtCondition.BoundSlaveCountExcess;
            // 固定个人反馈不查询组件；环境读取完整人数，不应用数量戒律的三人超额封顶。
            if (!quantity && config?.condition != SSCCultureThoughtCondition.ManagedChainedEnvironment && config?.IsGenderEnvironment != true)
                return 1f;
            // 原版缓存可能尚未重算；再次核对挂载和本人链，避免改宗/取得链后继续给出旧环境效果。
            if (!ModsConfig.IdeologyActive || p?.Ideo == null || !p.Ideo.cachedPossibleSituationalThoughts.Contains(def)) return 0f;
            if (config.IsGenderEnvironment)
            {
                // 先取已经排除本人的完整人数，再封顶三人；复用原版 SimpleCurve，基础负值由阶段保存。
                if (!SSCCultureGenderUtility.TryEnvironmentCount(p,
                    config.condition == SSCCultureThoughtCondition.UnfulfilledDutyEnvironment, out int offenders)) return 0f;
                return config.environmentMoodCurve?.PointsCount > 0
                    ? config.environmentMoodCurve.Evaluate(System.Math.Min(3, offenders)) : 0f;
            }
            if (quantity)
            {
                // 再核对适用资格与当前快照，防止旧 thought 在改宗、失去主人身份、加载中继续扣分。
                // 基础值 -3：主人总数倍率为 2/5/9，名下人数为 1/3/6；封顶只在超额查询中处理。
                if (!SSCCultureCountUtility.TryGetMoodExcess(p, config, out int n)) return 0f;
                return config.condition == SSCCultureThoughtCondition.MasterCountExcess
                    ? n * (n + 3) / 2f : n * (n + 1) / 2f;
            }
            if (!TryEnvironmentCount(p, config, out int count)) return 0f;
            // 未配置曲线的敌视/厌恶仍按人数相乘；神圣复用原版 SimpleCurve 在末点自然封顶。
            // 仅改变反馈倍率，不截断共享管理人数，也不保存人物结果；空曲线安全归零并由配置检查报错。
            return config.environmentMoodCurve == null ? count
                : config.environmentMoodCurve.PointsCount > 0 ? config.environmentMoodCurve.Evaluate(count) : 0f;
        }

        private static bool TryEnvironmentCount(Pawn p, SSCCultureThoughtExtension config, out int count)
        {
            count = 0;
            // 先做个人短路：持链者只获本人项；无效/非玩家对象不触发人口收集。
            if (!SSCCultureStateUtility.IsLiving(p) || p.Faction != Faction.OfPlayer
                || SSCCultureStateUtility.HasChain(p)) return false;
            var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
            // 接收者用成员名册，计数对象用管理持链集合；后者包含囚犯，不能被前者替代。
            if (cache == null || !cache.TryIsMember(p, out bool member) || !member) return false;
            // 只有神圣正向环境使用合规性别数；无规范老档直接复用原总数，不准备性别细分。
            return config.filterAcceptedManagedGender
                ? cache.TryGetAcceptedManagedChainedCount(SSCCultureGenderUtility.Policy(p), out count)
                : cache.TryGetManagedChainedCount(out count);
        }
    }
}
