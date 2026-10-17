// AI 辅助生成
using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一次查询的收集与位置上下文；不跨查询缓存游戏对象，不写入存档。</summary>
    internal sealed class SSCBondOverviewWorld : IDisposable
    {
        // 仅复用已清空的容器，每线程最多一个；绝不把游戏对象留在静态缓冲中。
        [ThreadStatic] private static SSCBondOverviewWorld available;
        private const int MaxRetainedItems = 65536;
        private bool leased;
        private HashSet<Pawn> candidatePawns;
        private List<IThingHolder> pendingHolders;
        private List<IThingHolder> childHolders;
        private HashSet<IThingHolder> visitedHolders;
        private HashSet<IThingHolder> locationPath;
        private List<Gravship> ships;
        private HashSet<Gravship> seenShips;
        // 仅索引关联角色的位置主体及祖先，普通货物不进入字典；-1 表示未在运输字典找到。
        private Dictionary<Thing, int> shipRequests;
        private bool shipsCollected;

        internal static SSCBondOverviewWorld Rent()
        {
            // 借出时摘除线程槽，防止同线程嵌套查询共享仍在使用的集合。
            // ThreadStatic 仅隔离临时容器，不意味着可以在后台线程读取游戏世界。
            SSCBondOverviewWorld result = available ?? new SSCBondOverviewWorld();
            available = null;
            result.leased = true;
            return result;
        }

        public void Dispose()
        {
            if (!leased) return;
            leased = false;
            // using 保证成功与异常路径都清除引用；超大缓冲直接释放，限制保留容量。
            if (candidatePawns?.Count > MaxRetainedItems) candidatePawns = null; else candidatePawns?.Clear();
            if (visitedHolders?.Count > MaxRetainedItems) visitedHolders = null; else visitedHolders?.Clear();
            if (locationPath?.Count > MaxRetainedItems) locationPath = null; else locationPath?.Clear();
            if (shipRequests?.Count > MaxRetainedItems) shipRequests = null; else shipRequests?.Clear();
            if (pendingHolders?.Capacity > MaxRetainedItems) pendingHolders = null; else pendingHolders?.Clear();
            if (childHolders?.Capacity > MaxRetainedItems) childHolders = null; else childHolders?.Clear();
            if (ships?.Capacity > MaxRetainedItems) ships = null; else ships?.Clear();
            if (seenShips?.Count > MaxRetainedItems) seenShips = null; else seenShips?.Clear();
            shipsCollected = false;
            // 嵌套查询可能已经归还一个空上下文；只保留一个，其余交给 GC。
            // 上面的 Clear 清除对象引用，保留的是集合容量，不是上一份查询结果。
            if (available == null) available = this;
        }

        internal bool VisitCandidate(Pawn pawn)
        {
            if (candidatePawns == null)
                candidatePawns = new HashSet<Pawn>(SSCBondOverviewReferenceComparer<Pawn>.Instance);
            return candidatePawns.Add(pawn);
        }

        internal void CollectCandidates(Pawn source, Action<Pawn> add)
        {
            // 缰绳只提供额外候选，不作为归属事实；漏项由其他来源补齐，陈旧项由 add 核对锁链。
            // 直接读取原列表，回调不得修复绑定或改写集合，避免枚举期间产生副作用。
            Hediff_BridleOfSexSlave bridle = SSCBondUtility.GetBridle(source);
            if (bridle?.targets != null)
                for (int i = 0; i < bridle.targets.Count; i++) add(bridle.targets[i]);

            // 不调用 AllPawns：它在各地图内已经递归一遍容器，再补尸体会重复整棵持有树。
            // 原版临时集合立即消费；add 只查锁链，不嵌套调用 PawnsFinder 或维护关系。
            if (Find.World != null)
            {
                foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive) add(pawn);
                foreach (Pawn pawn in Find.WorldPawns.AllPawnsDead) add(pawn);
            }
            foreach (Pawn pawn in PawnsFinder.Temporary) add(pawn);
            foreach (Pawn pawn in PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead) add(pawn);

            foreach (Map map in Find.Maps)
            {
                // 已生成角色、地图尸体和容器内容各有独立入口，不能仅依赖 mapPawns 覆盖死亡角色。
                // 活体与尸体共用同一次持有树遍历；候选的最终去重在查询器的 Add 中完成。
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned) add(pawn);
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)) AddThing(thing, add);
                EnsureTraversal();
                // 只缓存待访问的持有者；边遍历边筛选，不复制整批普通货物。
                map.GetChildHolders(pendingHolders);
                DrainHolders(add);
            }

            // 当前飞船优先，与旧版位置解析顺序一致；重复注册不会再次遍历其内容。
            Gravship currentShip = Find.CurrentGravship;
            if (RegisterShip(currentShip)) AddShip(currentShip, add);
            if (Find.World != null)
                foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
                {
                    if (worldObject is IThingHolder holder) AddHeld(holder, add);
                    if (worldObject is Gravship ship && RegisterShip(ship)) AddShip(ship, add);
                }
            shipsCollected = true;
        }

        private static void AddThing(Thing thing, Action<Pawn> add)
        {
            if (thing is Corpse corpse)
            {
                if (!corpse.Destroyed && corpse.InnerPawn != null) add(corpse.InnerPawn);
            }
            else if (thing is Pawn pawn) add(pawn);
        }

        private void EnsureTraversal()
        {
            if (pendingHolders == null) pendingHolders = new List<IThingHolder>();
            if (childHolders == null) childHolders = new List<IThingHolder>();
            if (visitedHolders == null)
                visitedHolders = new HashSet<IThingHolder>(SSCBondOverviewReferenceComparer<IThingHolder>.Instance);
        }

        private void AddHeld(IThingHolder holder, Action<Pawn> add)
        {
            EnsureTraversal();
            pendingHolders.Add(holder);
            DrainHolders(add);
        }

        private void DrainHolders(Action<Pawn> add)
        {
            // visitedHolders 在整次查询中共享，地图、世界对象、飞船重复可达的持有者只展开一次。
            // 子节点必须通过 GetChildHolders 获取，组件持有者未必是直接内容里的 Thing。
            // 显式栈避免深容器递归；所有内容即时筛选，仅持有者进入待访问列表。
            while (pendingHolders.Count > 0)
            {
                int last = pendingHolders.Count - 1;
                IThingHolder holder = pendingHolders[last];
                pendingHolders.RemoveAt(last);
                if (holder == null || !visitedHolders.Add(holder)) continue;
                // 与原查询 allowUnreal:true 一致，直接读取所有持有内容。
                var held = holder.GetDirectlyHeldThings();
                if (held != null) foreach (Thing thing in held) AddThing(thing, add);
                childHolders.Clear();
                holder.GetChildHolders(childHolders);
                pendingHolders.AddRange(childHolders);
            }
        }

        private void AddShip(Gravship ship, Action<Pawn> add)
        {
            // 成员表和货物表不保证相互包含，两者都读以保留只有成员引用的角色。
            // 此处负责发现候选；后续位置索引是另一轮按需扫描，不随结果行数增加轮数。
            foreach (Pawn pawn in ship.Pawns) add(pawn);
            foreach (Thing thing in ship.Things)
            {
                AddThing(thing, add);
                if (thing is IThingHolder holder) AddHeld(holder, add);
            }
        }

        private bool RegisterShip(Gravship ship)
        {
            if (ship == null) return false;
            if (ships == null) ships = new List<Gravship>();
            if (seenShips == null) seenShips = new HashSet<Gravship>(SSCBondOverviewReferenceComparer<Gravship>.Instance);
            if (!seenShips.Add(ship)) return false;
            ships.Add(ship);
            return true;
        }

        private static Thing Subject(Pawn pawn)
        {
            Corpse corpse = pawn.Dead ? pawn.Corpse : null;
            return corpse != null && !corpse.Destroyed ? (Thing)corpse : pawn;
        }

        private void BeginPath()
        {
            if (locationPath == null)
                locationPath = new HashSet<IThingHolder>(SSCBondOverviewReferenceComparer<IThingHolder>.Instance);
            else locationPath.Clear();
        }

        private void Request(Thing thing)
        {
            if (shipRequests == null)
                shipRequests = new Dictionary<Thing, int>(SSCBondOverviewReferenceComparer<Thing>.Instance);
            shipRequests.TryAdd(thing, -1);
        }

        internal void AddLocationRequest(Pawn pawn)
        {
            // 先沿持有链排除可直接定位的行，再为剩余行登记主体、Pawn 和各级 Thing 祖先。
            // ThingComp.parent 即便不是 IThingHolder 也可能登记在飞船货物表，不能省略。
            // 持有环保持 Unknown；不为环上的对象猜测运输位置。
            if (pawn == null) return;
            Thing subject = Subject(pawn);
            if (subject.Spawned) return;
            BeginPath();
            // 已有地图或世界持有者可直接定位，不为此类行建立飞船请求。
            for (IThingHolder holder = subject.ParentHolder; holder != null; holder = holder.ParentHolder)
            {
                if (!locationPath.Add(holder)) return;
                Thing container = holder as Thing ?? (holder as ThingComp)?.parent;
                if (container?.Spawned == true || holder is WorldObject) return;
            }
            Request(pawn);
            Request(subject);
            foreach (IThingHolder holder in locationPath)
            {
                Thing container = holder as Thing ?? (holder as ThingComp)?.parent;
                if (container != null) Request(container);
            }
        }

        internal void IndexShipLocations()
        {
            // 清空复用后字典可能非 null 但 Count 为零；此时也必须跳过整船扫描。
            // 测试注入候选绕过世界收集时，在这里补船清单；正常入口沿用已收集的顺序。
            if (shipRequests == null || shipRequests.Count == 0) return;
            if (!shipsCollected)
            {
                RegisterShip(Find.CurrentGravship);
                if (Find.World != null)
                    foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
                        if (worldObject is Gravship ship) RegisterShip(ship);
                shipsCollected = true;
            }
            if (ships == null) return;
            for (int i = 0; i < ships.Count; i++)
            {
                // 每船至多一次成员及一次货物索引扫描，不再对每个离图角色分别查找。
                foreach (Pawn pawn in ships[i].Pawns) IndexThing(pawn, i);
                foreach (Thing thing in ships[i].Things) IndexThing(thing, i);
            }
        }

        private void IndexThing(Thing thing, int shipIndex)
        {
            // 只保存请求中的对象，不为全部货物分配索引项；首次命中保留当前飞船优先的顺序。
            if (thing != null && shipRequests.TryGetValue(thing, out int found) && found < 0)
                shipRequests[thing] = shipIndex;
        }

        private int EarlierShip(Thing thing, int best)
        {
            if (shipRequests != null && shipRequests.TryGetValue(thing, out int index) && index >= 0 && (best < 0 || index < best))
                return index;
            return best;
        }

        internal SSCBondOverviewLocation GetLocation(Pawn pawn)
        {
            // 已生成主体、地图容器、世界持有者均优先于飞船字典；不能因为字典命中提前返回。
            // 所有祖先的飞船命中取清单中最早者，避免祖先遍历顺序改变当前飞船优先规则。
            // 死亡角色以有效尸体为位置主体，无法定位时才回退到世界格或 Unknown。
            Thing subject = Subject(pawn);
            if (subject.Spawned)
                return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.Map, subject, subject.Map);
            BeginPath();
            int shipIndex = EarlierShip(subject, EarlierShip(pawn, -1));
            for (IThingHolder holder = subject.ParentHolder; holder != null; holder = holder.ParentHolder)
            {
                if (!locationPath.Add(holder)) return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.Unknown, subject);
                Thing container = holder as Thing ?? (holder as ThingComp)?.parent;
                if (container != null)
                {
                    if (container.Spawned)
                        return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.MapContainer, subject, container.Map, container);
                    shipIndex = EarlierShip(container, shipIndex);
                }
                if (holder is WorldObject worldObject)
                {
                    SSCBondOverviewLocationKind kind = worldObject is Caravan ? SSCBondOverviewLocationKind.Caravan
                        : worldObject is TravellingTransporters || worldObject is Gravship ? SSCBondOverviewLocationKind.Transport
                        : SSCBondOverviewLocationKind.WorldObject;
                    return new SSCBondOverviewLocation(kind, subject, worldObject: worldObject);
                }
            }
            if (shipIndex >= 0)
                return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.Transport, subject, worldObject: ships[shipIndex]);
            return new SSCBondOverviewLocation(subject.Tile.Valid ? SSCBondOverviewLocationKind.WorldTile
                : SSCBondOverviewLocationKind.Unknown, subject);
        }
    }
}
