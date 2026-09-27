using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

// 仅提供引擎、人格数据和物品生成边界。任务、交接、目标检查与提取顺序均链接生产文件。
namespace Verse
{
    public partial class Thing
    {
        public ThingDef def;
        public bool Destroyed, Spawned = true;
        public bool IsForbidden(Pawn pawn) => false;
    }
    public partial class Pawn
    {
        public bool InMentalState;
        public RaceProperties RaceProps = new RaceProperties();
        public PawnHealth health = new PawnHealth();
        public RelationTracker relations = new RelationTracker();
        public StoryTracker story;
        public SkillTracker skills;
        public bool CanReach(Thing target, PathEndMode mode, Danger danger) => Reachable;
        public bool CanReserve(Thing target, int count, int stack, object layer, bool forced) => Reservable;
    }
    public partial class Map { public T GetComponent<T>() where T : new() => new T(); }
    public class RaceProperties { public bool Humanlike = true; }
    public class HediffDef : Def { }
    public class Hediff { public HediffDef def; public float Severity; }
    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
        public bool HasHediff(HediffDef def) => GetFirstHediffOfDef(def) != null;
    }
    public class PawnHealth
    {
        public HediffSet hediffSet = new HediffSet();
        public void RemoveHediff(Hediff hediff) => hediffSet.hediffs.Remove(hediff);
        public Hediff AddHediff(HediffDef def)
        {
            var hediff = new Hediff { def = def };
            hediffSet.hediffs.Add(hediff);
            return hediff;
        }
    }
    public class RelationTracker { public void ClearAllRelations() { } }
    public class StoryTracker { public TraitSet traits = new TraitSet(); }
    public class SkillTracker { public List<SkillRecord> skills = new List<SkillRecord>(); }
    public static class ThingMaker
    {
        public static Thing MakeThing(ThingDef def)
        {
            var result = new Thing { def = def };
            result.comps.Add(new SexSlaveCraft.CompPersonalityStore());
            return result;
        }
    }
    public static class GenSpawn
    {
        public static List<Thing> Spawned = new List<Thing>();
        public static void Spawn(Thing thing, IntVec3 position, Map map) => Spawned.Add(thing);
    }
    public enum ThingRequestGroup { Pawn }
    public struct ThingRequest { public static ThingRequest ForGroup(ThingRequestGroup group) => new ThingRequest(); }
}
namespace RimWorld
{
    public class TraitDef : Def { }
    public class Trait { public Trait(TraitDef def) { } }
    public class TraitSet { public bool HasTrait(TraitDef def) => false; public void GainTrait(Trait trait) { } }
    public class SkillDef : Def { }
    public class SkillRecord { public SkillDef def; public int Level; public float xpSinceLastLevel; }
    public static class SkillDefOf { public static SkillDef Social = new SkillDef(); }
    public abstract class WorkGiver_Scanner
    {
        public virtual ThingRequest PotentialWorkThingRequest => new ThingRequest();
        public virtual PathEndMode PathEndMode => PathEndMode.Touch;
        public virtual IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) { yield break; }
        public virtual Job JobOnThing(Pawn pawn, Thing thing, bool forced = false) => null;
    }
}
namespace rjw { public static partial class xxx { public enum rjwSextype { Anal } } }
namespace SexSlaveCraft
{
    public static partial class SSCDefOf
    {
        public static readonly JobDef SSC_Job_PE = new JobDef { defName = "SSC_Job_PE" };
        public static readonly HediffDef SSC_PersonalityExcreting = new HediffDef();
        public static readonly HediffDef SSC_PersonalityExcreted_Done = new HediffDef();
        public static readonly HediffDef SSC_PostExcretionComa = new HediffDef();
        public static readonly TraitDef SSC_Trait_PE = new TraitDef();
    }
    public static partial class OnaholeCompatibilityUtility
    {
        public static bool IsBeOnaholeDriver(object driver) => false;
    }
    public partial class CompSexSlaveTraining { public bool IsWaitingAfterFailedValidation => false; }
    public static class RabbitCloneUtility { public static bool IsRabbitClone(Pawn pawn) => false; }
    public static class Strings { public static string Message_PersonalityExcretionComplete(string label) => label; }
    public static partial class RJWSexPropsUtility
    {
        public static void ApplySexType(rjw.SexProps props, Pawn actor, Pawn target, rjw.xxx.rjwSextype type, string interaction) { }
    }
    public class CompPersonalityStore : ThingComp { public Pawn Stored; public void StorePawnData(Pawn pawn) => Stored = pawn; }
    public static class PersonalityGelUtility { public static ThingDef GetPersonalityGelDefForPawn(Pawn pawn) => new ThingDef(); }
    public static class CombatantSpecializationGelUtility { public static void DetachAfterExtraction(Pawn pawn) { } }
    public class MapComponent_PersonalityAssignment { public Thing GetAssignedGel(Pawn hollow) => null; }
}
