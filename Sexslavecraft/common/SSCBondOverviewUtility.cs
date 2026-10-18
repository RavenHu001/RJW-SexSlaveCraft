// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只读取直接锁链归属，不整理缰绳、身份、指派、奖励或特化。</summary>
    public static class SSCBondOverviewUtility
    {
        /// <summary>显式全局诊断查询；普通会话禁止调用，只在玩家触发开发诊断后枚举完整候选。</summary>
        public static SSCBondOverviewSnapshot Build(Pawn source, Map viewedMap)
        {
            SSCBondOverviewQueryState state = GetQueryState(source);
            if (state != SSCBondOverviewQueryState.Ready)
                return Empty(source, viewedMap, state);
            return BuildReady(source, viewedMap, null, true);
        }

        /// <summary>以物化候选构建同一生产查询；内部入口供候选覆盖与关系规则分别验证。</summary>
        internal static SSCBondOverviewSnapshot BuildFromCandidates(Pawn source, Map viewedMap, IEnumerable<Pawn> candidates)
        {
            SSCBondOverviewQueryState state = GetQueryState(source);
            if (state != SSCBondOverviewQueryState.Ready) return Empty(source, viewedMap, state);
            return BuildReady(source, viewedMap, candidates, false);
        }

        /// <summary>普通概览仅枚举缰绳并校验锁链；不扫描世界，不吸收历史诊断结果。</summary>
        internal static SSCBondOverviewSnapshot BuildLocal(Pawn source, Map map)
        {
            var state = GetQueryState(source);
            return state == SSCBondOverviewQueryState.Ready
                ? BuildReady(source, map, null, false, false) : Empty(source, map, state);
        }

        private readonly struct BoundCandidate
        {
            internal readonly Pawn Pawn;
            internal readonly Hediff_ChainOfSexSlave Chain;
            internal BoundCandidate(Pawn pawn, Hediff_ChainOfSexSlave chain) { Pawn = pawn; Chain = chain; }
        }

        private static SSCBondOverviewSnapshot BuildReady(Pawn source, Map viewedMap, IEnumerable<Pawn> candidates, bool collectWorld,
            bool indexShips = true)
        {
            // 临时去重表和位置索引只服务本次构建；using 覆盖枚举、排序和构造快照的异常退出。
            // 返回的行与列表独立拥有数据，Dispose 清空缓冲不会影响已返回的快照。
            using var world = SSCBondOverviewWorld.Rent();

            // 来源自身的锁链决定主人，候选侧锁链决定从属；缰绳、身份和指派均不推导直接归属。
            // 无链与坏链分别保留状态，死亡对象仍可关联，销毁、丢弃和自指不进入结果。
            Hediff_ChainOfSexSlave ownChain = SSCBondUtility.GetChain(source);
            Pawn master = ownChain?.LinkedPawn;
            SSCBondOverviewMasterState masterState = ownChain == null
                ? SSCBondOverviewMasterState.NoChain : SSCBondOverviewMasterState.InvalidTarget;
            bool hasMaster = IsValidPawn(master) && !ReferenceEquals(source, master);
            if (hasMaster) masterState = SSCBondOverviewMasterState.Bound;
            var matched = new List<BoundCandidate>();
            void Add(Pawn candidate)
            {
                // 先按引用去重，再查健康集合；多来源重复不会反复查链或生成重复行。
                // 命中后保留同一链引用供行构造读取阶段，避免重新查找；不触发任何绑定维护。
                if (!IsValidPawn(candidate) || ReferenceEquals(source, candidate) || !world.VisitCandidate(candidate)) return;
                Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(candidate);
                if (!ReferenceEquals(chain?.LinkedPawn, source)) return;
                // 只暂存实际关系；全体候选的去重缓冲在查询结束时清空并有界复用。
                matched.Add(new BoundCandidate(candidate, chain));
            }
            // 普通概览只接受缰绳候选，逐项核链并去重；离图、容器及死亡对象的引用仍可直接读取。
            // 反向索引遭外部破坏后允许遗漏，不能借普通刷新重新扫描世界或修复绑定。
            if (!indexShips)
            {
                var bridle = SSCBondUtility.GetBridle(source);
                if (bridle?.targets != null)
                    for (int i = 0; i < bridle.targets.Count; i++) Add(bridle.targets[i]);
            }
            // 全局收集本身包含缰绳，不在完整补漏前额外再遍历一次同一列表。
            if (collectWorld) world.CollectCandidates(source, Add);
            else if (candidates != null) foreach (Pawn candidate in candidates) Add(candidate);
            // 候选收集完毕后仅为实际关联对象请求位置，再统一扫飞船建立稀疏索引。
            // 逐行构造只读取持有链与索引，避免每个离图角色都重新扫描整船货物。
            if (indexShips && hasMaster) world.AddLocationRequest(master);
            // 次级指派只增加一个位置请求，不加入绑定候选或计数；与原有关联共用一次飞船索引。
            Pawn trainer = source.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            bool hasTrainer = IsValidPawn(trainer) && !ReferenceEquals(source, trainer);
            if (indexShips)
            {
                if (hasTrainer) world.AddLocationRequest(trainer);
                foreach (BoundCandidate candidate in matched) world.AddLocationRequest(candidate.Pawn);
                world.IndexShipLocations();
            }
            SSCBondOverviewEntry masterEntry = hasMaster
                ? new SSCBondOverviewEntry(source, master, SSCBondOverviewDirection.Master, ownChain, world.GetLocation(master)) : null;
            var slaves = new List<SSCBondOverviewEntry>(matched.Count);
            foreach (BoundCandidate candidate in matched)
                slaves.Add(new SSCBondOverviewEntry(source, candidate.Pawn, SSCBondOverviewDirection.Slave,
                    candidate.Chain, world.GetLocation(candidate.Pawn)));
            slaves.Sort((left, right) => Compare(left, right, viewedMap));
            return new SSCBondOverviewSnapshot(Current.Game, source, viewedMap, SSCBondOverviewQueryState.Ready, masterState, masterEntry, slaves,
                hasTrainer ? world.GetLocation(trainer) : null);
        }

        /// <summary>轻量复查行所属游戏、来源与当前直接归属；同主的新链仍是同一关系。</summary>
        public static bool IsCurrent(SSCBondOverviewSnapshot snapshot, SSCBondOverviewEntry entry)
        {
            // 先确认行属于当前游戏中的这份快照，再核对现时锁链归属；拒绝外来行与跨存档引用。
            // 这是操作前的关系复查，不刷新姓名、位置等显示事实，也不要求锁链实例仍是原对象。
            if (snapshot == null || entry == null || snapshot.State != SSCBondOverviewQueryState.Ready ||
                !ReferenceEquals(snapshot.Game, Current.Game) || GetQueryState(snapshot.Source) != SSCBondOverviewQueryState.Ready ||
                !IsValidPawn(entry.Slave) || !IsValidPawn(entry.Master)) return false;
            return snapshot.Contains(entry) && ReferenceEquals(SSCBondUtility.GetBoundMaster(entry.Slave), entry.Master);
        }

        internal static bool IsValidPawn(Pawn pawn) => pawn != null && !pawn.Destroyed && !pawn.Discarded;

        private static SSCBondOverviewQueryState GetQueryState(Pawn source)
        {
            if (!IsValidPawn(source)) return SSCBondOverviewQueryState.InvalidSource;
            if (Current.Game == null || Current.ProgramState != ProgramState.Playing ||
                Scribe.mode != LoadSaveMode.Inactive || SSCDefOf.ChainOfSexSlave == null ||
                SSCDefOf.BridleOfSexSlave == null) return SSCBondOverviewQueryState.Unavailable;
            return SSCBondOverviewQueryState.Ready;
        }

        private static SSCBondOverviewSnapshot Empty(Pawn source, Map map, SSCBondOverviewQueryState state)
            => new SSCBondOverviewSnapshot(Current.Game, source, map, state,
                SSCBondOverviewMasterState.Unavailable, null, null);

        private static int Compare(SSCBondOverviewEntry left, SSCBondOverviewEntry right, Map map)
        {
            int group = SortGroup(left, map).CompareTo(SortGroup(right, map));
            if (group != 0) return group;
            int name = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return name != 0 ? name : left.StableId.CompareTo(right.StableId);
        }

        private static int SortGroup(SSCBondOverviewEntry entry, Map map)
            => entry.IsDead ? 2 : map != null && ReferenceEquals(entry.Location.Map, map) ? 0 : 1;
    }

    /// <summary>按引擎对象引用去重，不依赖角色姓名、ID 或第三方 Equals 实现。</summary>
    internal sealed class SSCBondOverviewReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly SSCBondOverviewReferenceComparer<T> Instance = new SSCBondOverviewReferenceComparer<T>();
        public bool Equals(T left, T right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
}
