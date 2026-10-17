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
        /// <summary>显式构建一次独立快照；调用方负责刷新频率，不在绘制循环重复调用。</summary>
        public static SSCBondOverviewSnapshot Build(Pawn source, Map viewedMap)
        {
            SSCBondOverviewQueryState state = GetQueryState(source);
            if (state != SSCBondOverviewQueryState.Ready)
                return Empty(source, viewedMap, state);
            return BuildFromCandidates(source, viewedMap, SSCBondOverviewWorld.CollectCandidates(source));
        }

        /// <summary>以物化候选构建同一生产查询；内部入口供候选覆盖与关系规则分别验证。</summary>
        internal static SSCBondOverviewSnapshot BuildFromCandidates(Pawn source, Map viewedMap, IEnumerable<Pawn> candidates)
        {
            SSCBondOverviewQueryState state = GetQueryState(source);
            if (state != SSCBondOverviewQueryState.Ready) return Empty(source, viewedMap, state);

            Hediff_ChainOfSexSlave ownChain = SSCBondUtility.GetChain(source);
            Pawn master = ownChain?.LinkedPawn;
            SSCBondOverviewMasterState masterState = ownChain == null
                ? SSCBondOverviewMasterState.NoChain : SSCBondOverviewMasterState.InvalidTarget;
            SSCBondOverviewEntry masterEntry = null;
            if (IsValidPawn(master) && !ReferenceEquals(source, master))
            {
                masterState = SSCBondOverviewMasterState.Bound;
                masterEntry = new SSCBondOverviewEntry(source, master, SSCBondOverviewDirection.Master,
                    ownChain, SSCBondOverviewWorld.GetLocation(master));
            }

            var slaves = new List<SSCBondOverviewEntry>();
            var seen = new HashSet<Pawn>(SSCBondOverviewReferenceComparer<Pawn>.Instance);
            if (candidates != null)
                foreach (Pawn candidate in candidates)
                {
                    if (!IsValidPawn(candidate) || ReferenceEquals(source, candidate) || !seen.Add(candidate)) continue;
                    Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(candidate);
                    if (!ReferenceEquals(chain?.LinkedPawn, source)) continue;
                    slaves.Add(new SSCBondOverviewEntry(source, candidate, SSCBondOverviewDirection.Slave,
                        chain, SSCBondOverviewWorld.GetLocation(candidate)));
                }
            slaves.Sort((left, right) => Compare(left, right, viewedMap));
            return new SSCBondOverviewSnapshot(Current.Game, source, viewedMap, state, masterState, masterEntry, slaves);
        }

        /// <summary>轻量复查行所属游戏、来源与当前直接归属；同主的新链仍是同一关系。</summary>
        public static bool IsCurrent(SSCBondOverviewSnapshot snapshot, SSCBondOverviewEntry entry)
        {
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
                SSCBondOverviewMasterState.Unavailable, null, new List<SSCBondOverviewEntry>());

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
