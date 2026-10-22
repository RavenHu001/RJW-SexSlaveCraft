// AI 辅助生成：1.7 文化的三个独立人口口径，不查询人物栏布局或世界全量诊断。
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    public static class SSCCulturePopulationUtility
    {
        public static void CollectMembers(HashSet<Pawn> members)
        {
            // 输出集合由快照持有并复用；重建时先清空，缺少玩家阵营时返回本次空名册。
            members.Clear();
            Faction player = Faction.OfPlayer;
            if (player == null) return;

            // 对齐原版人物栏的活体来源，而非其 UI 缓存；地图和商队的资格不同。
            // 原版属性返回复用列表，必须消费完一个来源再查询下一个，不保留这些列表。
            foreach (Map map in Find.Maps)
            {
                // 地图使用原版两组来源，保留它们对容器内成员和可控制亚人的实际收集规则。
                if (map?.mapPawns == null) continue;
                AddMapSource(map.mapPawns.FreeColonists, members, player);
                AddMapSource(map.mapPawns.ColonySubhumansControllable, members, player);
            }
            // 商队单独使用原版商队资格；不把运输舱或仅挂玩家阵营的隐藏世界人物补入名册。
            if (Find.WorldObjects == null) return;
            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                if (!caravan.IsPlayerControlled) continue;
                foreach (Pawn pawn in caravan.PawnsListForReading)
                    if (pawn != null && (pawn.IsColonist || pawn.IsColonySubhumanPlayerControlled)
                        && IsMemberCandidate(pawn, player)) members.Add(pawn);
            }
        }

        private static void AddMapSource(List<Pawn> source, HashSet<Pawn> members, Faction player)
        {
            // 仅叠加公共排除条件，原版来源负责地图资格；集合负责跨来源、跨地图引用去重。
            foreach (Pawn pawn in source)
                if (IsMemberCandidate(pawn, player)) members.Add(pawn);
        }

        // 法律奴隶不在此排除；囚犯和任务借住者则不属于已确定的人物栏资格口径。
        private static bool IsMemberCandidate(Pawn pawn, Faction player)
            => SSCCultureStateUtility.IsLiving(pawn) && pawn.Faction == player
                && !pawn.IsPrisoner && !pawn.IsQuestLodger();

        public static int CountMasters(HashSet<Pawn> members)
        {
            // 从完整名册筛出 SSC 主人；不修改名册，正式领袖查询还需识别其中的非主人。
            int count = 0;
            foreach (Pawn pawn in members)
                if (SSCIdentityUtility.IsMaster(pawn)) count++;
            return count;
        }

        public static int CountManagedChained(HashSet<Pawn> scratch)
        {
            // 保留原有只计数入口；共享快照改用同一收集器保留本代成员，以按需准备性别细分。
            CollectManagedChained(scratch);
            int count = scratch.Count;
            scratch.Clear();
            return count;
        }

        public static void CollectManagedChained(HashSet<Pawn> result)
        {
            // 输出为独立管理集合，不与人物栏名册或每主工作集混用；持有者负责快照到期释放引用。
            result.Clear();
            Faction player = Faction.OfPlayer;
            if (player == null) return;

            // 环境人数允许管理中的囚犯、法律奴隶及运输来源，不能套用人物栏资格。
            // HostFaction 本身不足以表示玩家管理：访客/借住者排除，外方关押的玩家成员也不补入。
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
            {
                if (!SSCCultureStateUtility.IsLiving(pawn) || pawn.IsQuestLodger()) continue;
                // 先确认玩家实际管理关系，再以锁链判定性奴；ITab 的途径预选不影响人数。
                bool managed = pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony
                    || (pawn.Faction == player && !pawn.IsPrisoner
                        && (pawn.HostFaction == null || pawn.HostFaction == player));
                if (managed && SSCBondUtility.GetChain(pawn) != null) result.Add(pawn);
            }
        }

        public static int CountBoundSlaves(Pawn master, HashSet<Pawn> scratch)
        {
            // 每个实际请求的主人独立统计；共享 scratch 不得带入前一项目的候选。
            scratch.Clear();
            // 既有迭代器负责从缰绳候选逐项核对有向链和存活，文化层只补丢弃与引用去重。
            // 不按文化、阵营、地图或人物栏截断，不补漏/修复被外部工具破坏的反向索引。
            foreach (Pawn pawn in SSCMasterBondUtility.GetBoundSlaves(master))
                if (!pawn.Discarded) scratch.Add(pawn);
            // 快照只缓存该主人的数量，不长期保留一份重复的绑定对象集合。
            int count = scratch.Count;
            scratch.Clear();
            return count;
        }
    }
}
