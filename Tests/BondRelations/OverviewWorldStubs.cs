// AI 辅助生成
// 1.7.4981 rev136 的集合、持有和位置接口边界；候选编排、分类、筛选与快照均链接生产文件。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace Verse
{
    public enum ProgramState { Entry, Playing }
    public static class Current { public static Game Game; public static ProgramState ProgramState; }
    public static partial class Find
    {
        public static readonly List<Map> Maps = new List<Map>();
        public static object World;
        public static WorldObjectsHolder WorldObjects = new WorldObjectsHolder();
        public static WorldPawns WorldPawns = new WorldPawns();
        public static Gravship CurrentGravship;
    }
    public readonly struct PlanetTile
    {
        public readonly bool Valid;
        public readonly int Value;
        public PlanetTile(bool valid, int value = 0) { Valid = valid; Value = value; }
    }
    public interface IThingHolder
    {
        // 两个入口分别模拟直接内容和子持有者，测试可显式提供非 Thing 的组件子节点。
        // 只在直接内容读取处计次，避免将框架枚举与业务上的角色筛选混为同一个指标。
        IThingHolder ParentHolder { get; }
        IEnumerable<Thing> HeldThings { get; }
        IEnumerable<Thing> GetDirectlyHeldThings()
        {
            ThingOwnerUtility.NoteVisit(this);
            return HeldThings;
        }
        void GetChildHolders(List<IThingHolder> children)
        {
            foreach (Thing thing in HeldThings) if (thing is IThingHolder holder) children.Add(holder);
        }
    }
    public partial class Thing
    {
        static int nextId;
        public int thingIDNumber = ++nextId;
        public bool Spawned;
        public Map Map;
        public IThingHolder ParentHolder { get; set; }
        public PlanetTile Tile;
    }
    public partial class Map
    {
        public WorldObject Parent = new WorldObject();
        public readonly List<Thing> Things = new List<Thing>();
        public readonly MapPawns mapPawns;
        public readonly ListerThings listerThings;
        public Map() { mapPawns = new MapPawns(this); listerThings = new ListerThings(this); }
        public void GetChildHolders(List<IThingHolder> children)
        {
            ThingOwnerUtility.MapQueryCount++;
            foreach (Thing thing in Things) if (thing is IThingHolder holder) children.Add(holder);
        }
    }
    public class MapPawns
    {
        readonly Map map;
        public MapPawns(Map map) { this.map = map; }
        public IEnumerable<Pawn> AllPawnsSpawned => map.Things.OfType<Pawn>().Where(p => p.Spawned);
    }
    public class ListerThings
    {
        readonly Map map;
        readonly List<Thing> result = new List<Thing>();
        public ListerThings(Map map) { this.map = map; }
        public List<Thing> ThingsInGroup(ThingRequestGroup group)
        {
            result.Clear();
            result.AddRange(map.Things.Where(t => t.Spawned && (group == ThingRequestGroup.Everything ||
                group == ThingRequestGroup.Corpse && t is Corpse || group == ThingRequestGroup.Pawn && t is Pawn)));
            return result;
        }
    }
    public partial class Pawn : IThingHolder
    {
        public Corpse Corpse => ParentHolder as Corpse;
        public readonly List<Thing> Inventory = new List<Thing>();
        public IEnumerable<Thing> HeldThings => Inventory;
    }
    public class Corpse : Thing, IThingHolder
    {
        public Pawn InnerPawn;
        public IEnumerable<Thing> HeldThings => new[] { InnerPawn }.Where(p => p != null);
    }
    public class Container : Thing, IThingHolder
    {
        public readonly List<Thing> Contents = new List<Thing>();
        public IEnumerable<Thing> HeldThings => Contents;
    }
    public class ThingComp : IThingHolder
    {
        public Thing parent;
        public IThingHolder ParentHolder => parent.ParentHolder;
        public readonly List<Thing> Contents = new List<Thing>();
        public IEnumerable<Thing> HeldThings => Contents;
    }
    public enum ThingRequestGroup { Corpse, Pawn, Everything }
    public readonly struct ThingRequest
    {
        public readonly ThingRequestGroup Group;
        ThingRequest(ThingRequestGroup group) { Group = group; }
        public static ThingRequest ForGroup(ThingRequestGroup group) => new ThingRequest(group);
        public bool Accepts(Thing thing) => Group == ThingRequestGroup.Everything ||
            Group == ThingRequestGroup.Corpse && thing is Corpse || Group == ThingRequestGroup.Pawn && thing is Pawn;
    }
    public static class ThingOwnerUtility
    {
        // 两版基准共用计数边界与临时容器；旧查询的重复递归应来自生产调用编排。
        // 替身不负责 SSC 候选归属、位置或排序，也不为旧版本额外制造每次遍历的新缓冲。
        public static int MapQueryCount, HolderQueryCount;
        public static int HolderVisitCount;
        public static readonly Dictionary<IThingHolder, int> Visits = new Dictionary<IThingHolder, int>();
        static readonly Stack<IThingHolder> pending = new Stack<IThingHolder>();
        static readonly List<IThingHolder> children = new List<IThingHolder>();
        static readonly List<IThingHolder> mapRoots = new List<IThingHolder>();
        static readonly List<Thing> scratchThings = new List<Thing>();
        static readonly HashSet<IThingHolder> seen = new HashSet<IThingHolder>();
        public static void NoteVisit(IThingHolder holder)
        {
            HolderVisitCount++;
            Visits[holder] = Visits.TryGetValue(holder, out int n) ? n + 1 : 1;
        }
        // 模拟原版复用的临时缓冲；只承接引擎遍历，不实现 SSC 筛选或位置规则。
        public static void GetAllThingsRecursively<T>(Map map, ThingRequest request, List<T> outThings,
            bool allowUnreal = true, Predicate<IThingHolder> passCheck = null, bool alsoGetSpawnedThings = true) where T : Thing
        {
            outThings.Clear();
            if (alsoGetSpawnedThings)
                foreach (Thing thing in map.Things)
                    if (thing.Spawned && request.Accepts(thing) && thing is T typed) outThings.Add(typed);
            mapRoots.Clear();
            map.GetChildHolders(mapRoots);
            foreach (IThingHolder holder in mapRoots)
            {
                GetAllThingsRecursively(holder, scratchThings, allowUnreal, passCheck);
                foreach (Thing thing in scratchThings)
                    if (request.Accepts(thing) && thing is T typed) outThings.Add(typed);
            }
            mapRoots.Clear(); scratchThings.Clear();
        }
        public static void GetAllThingsRecursively(IThingHolder holder, List<Thing> outThings,
            bool allowUnreal = true, Predicate<IThingHolder> passCheck = null)
        {
            HolderQueryCount++;
            outThings.Clear();
            pending.Clear(); seen.Clear(); pending.Push(holder);
            while (pending.Count > 0)
            {
                IThingHolder next = pending.Pop();
                if (!seen.Add(next) || passCheck != null && !passCheck(next)) continue;
                var held = next.GetDirectlyHeldThings();
                if (held != null) outThings.AddRange(held);
                children.Clear(); next.GetChildHolders(children);
                foreach (IThingHolder child in children) pending.Push(child);
            }
            seen.Clear(); children.Clear();
        }
    }
}
namespace RimWorld.Planet
{
    public class WorldPawns
    {
        public List<Pawn> AllPawnsAlive => SexSlaveCraft.SSCRestrictionGameComponent.Pawns;
        public readonly HashSet<Pawn> AllPawnsDead = new HashSet<Pawn>();
    }
    public partial class WorldObject { public bool Spawned = true; public string LabelCap = "World object"; }
    public class WorldObjectsHolder { public readonly List<WorldObject> AllWorldObjects = new List<WorldObject>(); }
    public class Caravan : WorldObject, IThingHolder
    {
        public IThingHolder ParentHolder { get; set; }
        public readonly List<Thing> Contents = new List<Thing>();
        public IEnumerable<Thing> HeldThings => Contents;
    }
    public class TravellingTransporters : WorldObject, IThingHolder
    {
        public IThingHolder ParentHolder { get; set; }
        public readonly List<Thing> Contents = new List<Thing>();
        public IEnumerable<Thing> HeldThings => Contents;
    }
    public class Gravship : WorldObject
    {
        public readonly CountedList<Thing> Things = new CountedList<Thing>();
        public readonly CountedList<Pawn> Pawns = new CountedList<Pawn>();
    }
    public class HeldWorldObject : WorldObject, IThingHolder
    {
        public IThingHolder ParentHolder { get; set; }
        public readonly List<Thing> Contents = new List<Thing>();
        public IEnumerable<Thing> HeldThings => Contents;
    }
}
namespace RimWorld
{
    public static class PawnsFinder
    {
        public static readonly List<Pawn> TemporaryPawns = new List<Pawn>();
        public static int TemporaryQueries, TransportQueries;
        public static List<Pawn> Temporary { get { TemporaryQueries++; return TemporaryPawns; } }
        // 成员集合与容器内容有意分开，验证只有成员引用、没有持有树引用时不会遗漏。
        public static readonly List<Pawn> TransportMembers = new List<Pawn>();
        static readonly List<Pawn> transportResult = new List<Pawn>();
        public static List<Pawn> AllCaravansAndTravellingTransporters_AliveOrDead
        {
            get
            {
                TransportQueries++;
                transportResult.Clear();
                transportResult.AddRange(TransportMembers);
                if (Find.World != null && Find.CurrentGravship != null)
                    foreach (Pawn pawn in Find.CurrentGravship.Pawns)
                        if (!pawn.Spawned) transportResult.Add(pawn);
                return transportResult;
            }
        }
    }
}
// 记录生产枚举次数与访问条目数；与实际 Gravship 的 IEnumerable 接口一致。
public sealed class CountedList<T> : IEnumerable<T>
{
    readonly List<T> items = new List<T>();
    public int Enumerations, ItemVisits;
    public void Add(T value) => items.Add(value);
    public void Remove(T value) => items.Remove(value);
    public IEnumerator<T> GetEnumerator()
    {
        Enumerations++;
        foreach (T item in items) { ItemVisits++; yield return item; }
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
// 模拟旧 AllPawns 的覆盖与地图持有树访问，供原关系夹具及优化前后共用的性能边界。
internal static class OverviewWorldBoundary
{
    public static List<Pawn> AllPawns()
    {
        var result = new List<Pawn>(SexSlaveCraft.SSCRestrictionGameComponent.Pawns);
        result.AddRange(Find.WorldPawns.AllPawnsDead);
        foreach (Map map in Find.Maps)
        {
            result.AddRange(map.mapPawns.AllPawnsSpawned);
            var held = new List<Pawn>();
            ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), held,
                allowUnreal: true, alsoGetSpawnedThings: false);
            result.AddRange(held.Where(p => !p.Dead));
        }
        result.AddRange(RimWorld.PawnsFinder.Temporary.Where(p => !p.Dead));
        result.AddRange(RimWorld.PawnsFinder.Temporary.Where(p => p.Dead));
        if (Find.CurrentGravship != null)
        {
            result.AddRange(Find.CurrentGravship.Pawns);
            result.AddRange(Find.CurrentGravship.Pawns);
        }
        result.AddRange(RimWorld.PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead);
        return result.Where(p => p != null).Distinct().ToList();
    }
}
namespace SexSlaveCraft
{
    public enum SexSlaveSpecializationType { None, Bus, Cow, PetCat, PetDog, PetRabbit, TrainerOfficer, Combatant }
    public partial class CompSexSlaveTraining { public SexSlaveSpecializationType specializationType; public bool TrainerActive = true; }
}
