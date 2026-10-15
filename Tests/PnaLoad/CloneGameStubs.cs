using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

// SSC 克隆生产入口的游戏边界。地图、复制器、专精和身份系统隔离；
// PNA 分类、清理、实际锁链保护及需求收尾运行真实生产代码，不在这里重写。
namespace Verse
{
    public partial class Thing
    {
        public void Destroy(DestroyMode mode = DestroyMode.Vanish) { Destroyed=true; }
        public void Discard(bool silently) { Discarded=true; }
    }
    public enum DestroyMode { Vanish }
    public partial class Pawn
    {
        public RimWorld.Faction Faction;
        public bool Spawned;
        public IntVec3 Position;
        public AgeTracker ageTracker = new AgeTracker();
        public PawnDrawer Drawer = new PawnDrawer();
        public StoryTracker story = new StoryTracker();
        public MindState mindState = new MindState();
        public void SetFaction(RimWorld.Faction faction) { Faction=faction; }
    }
    public class AgeTracker { public long AgeBiologicalTicks=3600000; public void AgeTickMothballed(int ticks) { AgeBiologicalTicks+=ticks; } }
    public class PawnDrawer { public PawnRenderer renderer = new PawnRenderer(); }
    public class PawnRenderer { public void SetAllGraphicsDirty() { } }
    public class StoryTracker { public TraitTracker traits = new TraitTracker(); }
    public class TraitTracker { public List<RimWorld.Trait> allTraits = new List<RimWorld.Trait>(); public void GainTrait(RimWorld.Trait trait) { allTraits.Add(trait); } }
    public class MindState { public Verse.AI.MentalStateHandler mentalStateHandler = new Verse.AI.MentalStateHandler(); }
    public class Map { }
    public struct IntVec3 { public bool Standable(Map map)=>true; public bool Fogged(Map map)=>false; }
    public static class CellFinder { public static bool TryFindRandomCellNear(IntVec3 origin, Map map, int radius, Func<IntVec3,bool> validator, out IntVec3 cell) { cell=origin;return true; } }
    public static class GenSpawn { public static void Spawn(Pawn pawn, IntVec3 cell, Map map) { pawn.Map=map;pawn.Spawned=true; } }
    public static class PawnExtensions { public static bool DestroyedOrNull(this Pawn pawn)=>pawn==null || pawn.Destroyed; }
    public class ModContentPack { public string PackageId, PackageIdPlayerFacing, Name; }
    public partial class HediffDef { public ModContentPack modContentPack; public List<HediffCompProperties> comps = new List<HediffCompProperties>(); }
    public static class DefDatabase<T>
    {
        public static List<T> AllDefs = new List<T>();
        public static T GetNamedSilentFail(string name)=>AllDefs.FirstOrDefault(d=>d is HediffDef h && h.defName==name);
    }
    public static class Log { public static void Error(string text)=>Console.WriteLine(text); }
    public static partial class Find
    {
        public static TickManager TickManager = new TickManager();
        public static PawnDuplicator PawnDuplicator = new PawnDuplicator();
    }
    public class TickManager { public int TicksGame; }
    public class PawnDuplicator
    {
        // 用例提供原版复制完成后的身体；不在替身中重复实现 SSC 新生过滤。
        public Func<Pawn,Pawn> OnDuplicate;
        public Pawn Duplicate(Pawn source)=>OnDuplicate(source);
    }
}
namespace Verse.AI
{
    public class MentalStateHandler
    {
        public bool InMentalState;
        public void TryStartMentalState(object def,string reason,bool forced,bool forceWake,bool causedByMood,Pawn otherPawn,bool transitionSilently) { InMentalState=true; }
    }
}
namespace RimWorld
{
    public static class GenDate { public const int TicksPerDay=60000, TicksPerYear=3600000; }
    public class Faction { }
    public class TraitDef { }
    public class Trait { public TraitDef def; public int Degree; public bool ScenForced; public Trait(TraitDef def,int degree,bool forced) { this.def=def;Degree=degree;ScenForced=forced; } }
    public static class MentalStateDefOf { public static object Wander_Psychotic = new object(); }
    public class Need { public float CurLevel; public float MaxLevel=>1f; public float CurLevelPercentage { get=>CurLevel;set=>CurLevel=value; } }
    public partial class Pawn_NeedsTracker { public Need rest = new Need(), mood = new Need(); }
}
namespace rjw
{
    public class Hediff_BasePregnancy : Hediff { public List<Pawn> babies = new List<Pawn>(); }
}
namespace SexSlaveCraft
{
    public enum RabbitReproductionMode { Normal, Clone }
    public enum SexSlaveSpecializationType { PetRabbit }
    public class CompSexSlaveTraining
    {
        public bool IsPetRabbitSpecialized=true, slaveTrainerEnabled, trainerIdentityInitialized;
        public RabbitReproductionMode rabbitReproductionMode;
        public SexSlaveSpecializationType specializationType;
        public float specializationProgress;
        public Pawn selectedTrainer;
        public int pawnIdentity, trainerOfficerMigrationVersion;
        public const int CurrentTrainerOfficerMigrationVersion=1;
    }
    public class HediffComp_RabbitCloneSource : HediffComp
    {
        public List<Pawn> clones = new List<Pawn>();
        public int LivingCloneCount=>clones.Count;
        public bool CanRegisterMoreClones=>LivingCloneCount<2;
        public void RegisterClone(Pawn clone) { clones.Add(clone); }
        public void CleanupClones() { }
        public void ClearClones() { clones.Clear(); }
    }
    public class HediffComp_RabbitCloneLink : HediffComp
    {
        public Pawn source;
        public bool mature;
        public long startAgeTicks;
        public int bornTick;
        public void Initialize(Pawn source,long age) { this.source=source;startAgeTicks=age;bornTick=Find.TickManager.TicksGame; }
    }
    public class HediffCompProperties_SeverityProduct : HediffCompProperties { public ThingDef thingToSpawn; }
    public static class PetSpecializationUtility
    {
        public static HediffDef RabbitDef = new HediffDef{defName="SSC_TestRabbit"};
        public static HediffDef GetBaseHediffDef(SexSlaveSpecializationType type)=>RabbitDef;
        public static HediffDef GetFinalHediffDef(SexSlaveSpecializationType type)=>RabbitDef;
        public static bool HasAnyPetState(Pawn pawn,SexSlaveSpecializationType type)=>pawn.Comp is CompSexSlaveTraining;
    }
    public static class SSCMasterQualificationUtility { public static void InitializeNewClone(Pawn pawn) { } }
    public static class SSCMasterProgressionUtility
    {
        public static void InitializeNewClone(Pawn pawn) { }
        public static bool IsProgressionDef(HediffDef def)=>def?.defName=="SSC_Hediff_MasterProgression";
    }
    public static class SSCIdentityUtility
    {
        // 本宿主只提供当前身份；实际身份提交的协调调用另由 TrainerIdentity 覆盖。
        public static bool IsMaster(Pawn pawn)=>pawn?.IsMaster==true;
        public static void SyncSexSlaveTraitFromHighestCorruption(Pawn pawn) { }
    }
    public static class Strings { public static string Message_RabbitCloneBirth(string source,string clone)=>source+clone; }
}
