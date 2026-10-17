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
    public static class Find
    {
        public static readonly List<Map> Maps = new List<Map>();
        public static object World;
        public static WorldObjectsHolder WorldObjects = new WorldObjectsHolder();
        public static Gravship CurrentGravship;
    }
    public readonly struct PlanetTile
    {
        public readonly bool Valid;
        public PlanetTile(bool valid) { Valid = valid; }
    }
    public interface IThingHolder
    {
        IThingHolder ParentHolder { get; }
        IEnumerable<Thing> HeldThings { get; }
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
    public class Map { public readonly List<Thing> Things = new List<Thing>(); }
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
    public enum ThingRequestGroup { Corpse }
    public readonly struct ThingRequest
    {
        public static ThingRequest ForGroup(ThingRequestGroup group) => default;
    }
    public static class ThingOwnerUtility
    {
        public static int MapQueryCount, HolderQueryCount;
        // 仅模拟引擎的对象树访问与清空输出列表；不在这里实现 SSC 绑定过滤或位置分类。
        static void AddTree(Thing thing, List<Thing> output, HashSet<IThingHolder> visited)
        {
            if (thing == null) return;
            output.Add(thing);
            if (thing is IThingHolder holder) AddHolder(holder, output, visited);
        }
        static void AddHolder(IThingHolder holder, List<Thing> output, HashSet<IThingHolder> visited)
        {
            if (!visited.Add(holder)) return;
            foreach (Thing thing in holder.HeldThings) AddTree(thing, output, visited);
        }
        public static void GetAllThingsRecursively<T>(Map map, ThingRequest request, List<T> outThings,
            bool allowUnreal = true, Predicate<IThingHolder> passCheck = null, bool alsoGetSpawnedThings = true) where T : Thing
        {
            MapQueryCount++;
            outThings.Clear();
            var things = new List<Thing>();
            var visited = new HashSet<IThingHolder>();
            foreach (Thing thing in map.Things) AddTree(thing, things, visited);
            // 本边界只承接生产代码所用的 Corpse 请求；地图内尸体不会混入 AllPawns 替身。
            outThings.AddRange(things.OfType<Corpse>().OfType<T>());
        }
        public static void GetAllThingsRecursively(IThingHolder holder, List<Thing> outThings,
            bool allowUnreal = true, Predicate<IThingHolder> passCheck = null)
        {
            HolderQueryCount++;
            outThings.Clear();
            AddHolder(holder, outThings, new HashSet<IThingHolder>());
        }
    }
}
namespace RimWorld.Planet
{
    public class WorldObject { public bool Spawned = true; }
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
        public readonly List<Thing> Things = new List<Thing>();
        public readonly List<Pawn> Pawns = new List<Pawn>();
    }
    public class HeldWorldObject : WorldObject, IThingHolder
    {
        public IThingHolder ParentHolder { get; set; }
        public readonly List<Thing> Contents = new List<Thing>();
        public IEnumerable<Thing> HeldThings => Contents;
    }
}
namespace SexSlaveCraft
{
    public enum SexSlaveSpecializationType { None, Bus, Cow, PetCat, PetDog, PetRabbit, TrainerOfficer, Combatant }
    public partial class CompSexSlaveTraining { public SexSlaveSpecializationType specializationType; public bool TrainerActive = true; }
}
