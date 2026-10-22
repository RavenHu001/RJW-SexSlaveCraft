// AI 辅助生成：数量反馈的只读资格与超额查询；完整人数仍由每 Game 的既有快照保存。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class SSCCultureCountUtility
    {
        public static bool TryGetMoodExcess(Pawn pawn, SSCCultureThoughtExtension config, out int excess)
        {
            excess = 0;
            // 先排除不适用配置和个人资格，名下项的非主人不触发任何人口准备。
            // 这里只读 Pawn 自身的玩家阵营标记，不在加载/切档间隙访问全局 Faction.OfPlayer。
            // 精确的当前玩家阵营及人物栏资格仍由下方有效名册核对。
            if (!ModsConfig.IdeologyActive || !ValidLimit(config) || !SSCCultureStateUtility.IsLiving(pawn)
                || pawn.Faction?.def?.isPlayer != true) return false;
            bool masterTotal = config.condition == SSCCultureThoughtCondition.MasterCountExcess;
            if (!masterTotal && (config.condition != SSCCultureThoughtCondition.BoundSlaveCountExcess
                || !SSCCultureStateUtility.IsMaster(pawn))) return false;

            // 名册资格与人数共享同一代；查询失败不冒充有效零人，也不在文化层补收隐藏阵营角色。
            var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
            if (cache == null || !cache.TryIsMember(pawn, out bool member) || !member) return false;
            int count;
            if (!(masterTotal ? cache.TryGetMasterCount(out count) : cache.TryGetBoundSlaveCount(pawn, out count)))
                return false;
            excess = CappedExcess(count, config.countLimit);
            return true;
        }

        public static bool TryGetSocialExcess(Pawn observer, Pawn target, SSCCultureThoughtExtension config, out int excess)
        {
            excess = 0;
            // 上限属于观察者挂载的 thought；不检查目标文化/数量戒律，也不要求观察者是主人。
            if (!ModsConfig.IdeologyActive || !ValidLimit(config)
                || config.condition != SSCCultureThoughtCondition.OtherMasterBoundSlaveCountExcess
                || observer == target || !SSCCultureStateUtility.IsLiving(observer) || !SSCCultureStateUtility.IsLiving(target)
                || observer.Faction?.def?.isPlayer != true || target.Faction?.def?.isPlayer != true
                || !SSCCultureStateUtility.IsMaster(target)) return false;

            // 仅数量社交要求双方在名册内；目标的绑定对象沿用独立口径，不按名册、阵营或文化截断。
            var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
            if (cache == null || !cache.TryIsMember(observer, out bool observerMember) || !observerMember
                || !cache.TryIsMember(target, out bool targetMember) || !targetMember
                || !cache.TryGetBoundSlaveCount(target, out int count)) return false;
            excess = CappedExcess(count, config.countLimit);
            return true;
        }

        // 只封顶计罚超额，不截断实际人口统计；先比较避免不必要的负数参与后续曲线。
        private static int CappedExcess(int count, int limit) => count <= limit ? 0 : System.Math.Min(3, count - limit);
        private static bool ValidLimit(SSCCultureThoughtExtension config) => config != null
            && (config.countLimit == 1 || config.countLimit == 3);
    }
}
