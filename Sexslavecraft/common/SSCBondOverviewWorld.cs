// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原版集合与持有关系的只读边界；所有引擎临时列表在下一次查询前物化。</summary>
    internal static class SSCBondOverviewWorld
    {
        internal static List<Pawn> CollectCandidates(Pawn source)
        {
            var result = new List<Pawn>();
            Hediff_BridleOfSexSlave bridle = SSCBondUtility.GetBridle(source);
            if (bridle?.targets != null) result.AddRange(bridle.targets.ToArray());
            result.AddRange(SSCRestrictionGameComponent.AllPawns());

            // MapPawns 的容器枚举会滤掉死亡 Pawn，世界死亡集合也不等于地图尸体全集。
            var corpses = new List<Corpse>();
            foreach (Map map in Find.Maps.ToArray())
            {
                ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Corpse),
                    corpses, allowUnreal: true, alsoGetSpawnedThings: true);
                foreach (Corpse corpse in corpses) AddCorpse(corpse, result);
            }

            // 旅队行囊、运输容器及世界对象中可达的尸体不会一定出现在成员 Pawn 集合。
            var things = new List<Thing>();
            var ships = new HashSet<Gravship>(SSCBondOverviewReferenceComparer<Gravship>.Instance);
            if (Find.World != null)
                foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects.ToArray())
                {
                    if (worldObject is IThingHolder holder) AddHeld(holder, things, result);
                    if (worldObject is Gravship ship && ships.Add(ship)) AddShip(ship, things, result);
                }
            if (Find.CurrentGravship != null && ships.Add(Find.CurrentGravship))
                AddShip(Find.CurrentGravship, things, result);
            return result;
        }

        private static void AddCorpse(Corpse corpse, List<Pawn> result)
        {
            if (corpse != null && !corpse.Destroyed && corpse.InnerPawn != null) result.Add(corpse.InnerPawn);
        }

        private static void AddHeld(IThingHolder holder, List<Thing> things, List<Pawn> result)
        {
            ThingOwnerUtility.GetAllThingsRecursively(holder, things, allowUnreal: true);
            foreach (Thing thing in things)
            {
                if (thing is Corpse corpse) AddCorpse(corpse, result);
                else if (thing is Pawn pawn) result.Add(pawn);
            }
        }

        private static void AddShip(Gravship ship, List<Thing> things, List<Pawn> result)
        {
            // Gravship 用 Things 字典保存运输物，本身不是 IThingHolder。
            foreach (Thing thing in ship.Things.ToArray())
            {
                if (thing is Corpse corpse) AddCorpse(corpse, result);
                else if (thing is Pawn pawn) result.Add(pawn);
                if (thing is IThingHolder holder) AddHeld(holder, things, result);
            }
        }

        internal static SSCBondOverviewLocation GetLocation(Pawn pawn)
        {
            Thing subject = pawn.Dead && pawn.Corpse != null && !pawn.Corpse.Destroyed ? (Thing)pawn.Corpse : pawn;
            if (subject.Spawned)
                return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.Map, subject, subject.Map);

            var seen = new HashSet<IThingHolder>(SSCBondOverviewReferenceComparer<IThingHolder>.Instance);
            var heldThings = new HashSet<Thing>(SSCBondOverviewReferenceComparer<Thing>.Instance) { subject };
            for (IThingHolder holder = subject.ParentHolder; holder != null; holder = holder.ParentHolder)
            {
                // 畸形第三方持有链不递归访问 Tile/MapHeld，防止查询陷入循环。
                if (!seen.Add(holder)) return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.Unknown, subject);
                Thing container = holder as Thing ?? (holder as ThingComp)?.parent;
                if (container != null)
                {
                    heldThings.Add(container);
                    if (container.Spawned)
                        return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.MapContainer,
                            subject, container.Map, container);
                }
                if (holder is WorldObject worldObject)
                {
                    SSCBondOverviewLocationKind kind = worldObject is Caravan ? SSCBondOverviewLocationKind.Caravan
                        : worldObject is TravellingTransporters || worldObject is Gravship ? SSCBondOverviewLocationKind.Transport
                        : SSCBondOverviewLocationKind.WorldObject;
                    return new SSCBondOverviewLocation(kind, subject, worldObject: worldObject);
                }
            }

            // 飞船运输物不一定有 ParentHolder；从当前运输字典读位置，不改动其所属关系。
            Gravship currentShip = Find.CurrentGravship;
            if (ShipContains(currentShip, pawn, heldThings))
                return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.Transport, subject, worldObject: currentShip);
            if (Find.World != null)
                foreach (Gravship ship in Find.WorldObjects.AllWorldObjects.OfType<Gravship>())
                    if (!ReferenceEquals(ship, currentShip) && ShipContains(ship, pawn, heldThings))
                        return new SSCBondOverviewLocation(SSCBondOverviewLocationKind.Transport, subject, worldObject: ship);
            return new SSCBondOverviewLocation(subject.Tile.Valid ? SSCBondOverviewLocationKind.WorldTile
                : SSCBondOverviewLocationKind.Unknown, subject);
        }

        private static bool ShipContains(Gravship ship, Pawn pawn, HashSet<Thing> heldThings)
            => ship != null && (ship.Pawns.Any(candidate => ReferenceEquals(candidate, pawn)) || ship.Things.Any(heldThings.Contains));
    }
}
