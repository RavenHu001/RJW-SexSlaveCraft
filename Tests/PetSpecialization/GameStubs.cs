// 只隔离游戏容器、未修改的其他特化和任务宿主；宠物规则、培养、狗工作和亲昵入口直接编译生产源码。
using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
    }
}

namespace Verse
{
    public class Thing { public bool Destroyed; }
    public class Def { public string defName; }
    public class HediffDef : Def { }
    public class ResearchProjectDef : Def { public bool IsFinished = true; }
    public class ThoughtDef : Def { }
    public class JobDef : Def { }
    public static class DefDatabase<T> where T : Def
    {
        public static readonly Dictionary<string, T> Definitions = new Dictionary<string, T>();
        public static T GetNamedSilentFail(string name) => name != null && Definitions.TryGetValue(name, out T def) ? def : null;
        public static void Add(T def) => Definitions[def.defName] = def;
    }
    public class Hediff
    {
        public HediffDef def;
        public float Severity = .01f;
        public T TryGetComp<T>() where T : class => null;
    }
    public class HediffSet
    {
        public readonly List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
        public bool HasHediff(HediffDef def) => GetFirstHediffOfDef(def) != null;
    }
    public class PawnHealth
    {
        public readonly HediffSet hediffSet = new HediffSet();
        public int AddCalls, RemoveCalls;
        public Hediff AddHediff(HediffDef def)
        {
            AddCalls++;
            var hediff = new Hediff { def = def };
            hediffSet.hediffs.Add(hediff);
            return hediff;
        }
        public void RemoveHediff(Hediff hediff) { RemoveCalls++; hediffSet.hediffs.Remove(hediff); }
    }
    public partial class Pawn : Thing
    {
        public CompSexSlaveTraining Training;
        public PawnHealth health = new PawnHealth();
        public bool Dead, Downed, Drafted, Spawned = true;
        public bool TrainerEligible = true, TrainerFinal;
        public string LabelShort => "pet";
        public object Map = new object();
        public IntVec3 Position;
        public Pawn BoundMaster, AssignedTrainer;
        public PawnNeeds needs = new PawnNeeds();
        public Verse.AI.PawnJobTracker jobs = new Verse.AI.PawnJobTracker();
        public JobDef CurJobDef;
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public struct IntVec3
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public float DistanceToSquared(IntVec3 other) => (x - other.x) * (x - other.x) + (z - other.z) * (z - other.z);
        public bool InHorDistOf(IntVec3 other, float distance) => DistanceToSquared(other) <= distance * distance;
    }
    public static class ThingExtensions { public static bool DestroyedOrNull(this Thing thing) => thing == null || thing.Destroyed; }
    public class PawnNeeds { public Mood mood = new Mood(); }
    public class Mood { public Thoughts thoughts = new Thoughts(); }
    public class Thoughts { public Memories memories = new Memories(); }
    public class Memories
    {
        // 仅观察完成入口是否发出记忆请求，不模拟原版记忆合并与心情属性。
        public readonly List<(ThoughtDef def, Pawn pawn)> Requests = new();
        // 一次性回调用于验证结算重入边界；宿主本身不实现冷却或发奖规则。
        public Action OnNextGainMemory;
        public void TryGainMemory(ThoughtDef def, Pawn pawn)
        {
            Requests.Add((def, pawn));
            Action callback = OnNextGainMemory; OnNextGainMemory = null;
            callback?.Invoke();
        }
    }
    public static class Find { public static readonly TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame; }
    public static class GenTicks { public const int TickRareInterval = 250; }
    public enum LoadSaveMode { Inactive, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Messages
    {
        public static int Calls;
        public static void Message(string text, Pawn pawn, object type, bool historical) { Calls++; }
    }
}

namespace RimWorld
{
    public static class MessageTypeDefOf { public static readonly object PositiveEvent = new object(); }
    public static class JobDefOf
    {
        public static readonly Verse.JobDef Wait = new Verse.JobDef { defName = "Wait" };
        public static readonly Verse.JobDef Wait_Wander = new Verse.JobDef { defName = "Wait_Wander" };
    }
}

namespace Verse.AI
{
    public partial class Job { public int expiryInterval; }
    public enum JobTag { Misc }
    public static class JobMaker
    {
        public static Job MakeJob(Verse.JobDef def, Verse.Thing target, Verse.Thing second = null) => new Job { def = def, target = target };
        public static void ReturnToPool(Job job) { }
    }
    public partial class PawnJobTracker
    {
        public bool AcceptJobs = true;
        public readonly List<Job> Requests = new();
        public bool TryTakeOrderedJob(Job job, JobTag tag) { Requests.Add(job); return AcceptJobs; }
    }
}

namespace SexSlaveCraft
{
    using Verse;
    public enum PawnIdentity { Unset, Slave, Master }
    public enum RabbitReproductionMode { Offspring, Clone }
    public partial class CompSexSlaveTraining
    {
        public Thing parent;
        public PawnIdentity pawnIdentity;
        public bool specializationExplicitlyUnset, trainerInvalidExitBlocksAdoption;
        public RabbitReproductionMode rabbitReproductionMode;
        public float savedCowReservoirCharge;
        public int lastPetAffectionTick;
        public int lastDogAnimalInteractionTick = -999999;
        public const int PetAffectionCooldownTicks = 60000;
    }
    public static class SSCIdentityUtility { public static bool IsSexSlave(Pawn pawn) => pawn?.Training?.pawnIdentity == PawnIdentity.Slave; }
    public static class ResearchUtils { public static bool IsResearchFinished(ResearchProjectDef research) => research?.IsFinished == true; }
    public static class SSCBondUtility
    {
        public static Pawn GetBoundMaster(Pawn pawn) => pawn?.BoundMaster;
        public static Pawn GetResolvedMaster(Pawn pawn) => pawn?.BoundMaster ?? pawn?.AssignedTrainer;
    }
    public static class SSCDefOf
    {
        public static readonly ResearchProjectDef SSC_BasicTraining = new ResearchProjectDef { defName = "SSC_BasicTraining" };
        public static readonly HediffDef SSC_Hediff_Bus = new HediffDef { defName = "SSC_Hediff_Bus" };
        public static readonly HediffDef SSC_Hediff_Bus_Final = new HediffDef { defName = "SSC_Hediff_Bus_Final" };
        public static readonly HediffDef SSC_Hediff_Cow = new HediffDef { defName = "SSC_Hediff_Cow" };
        public static readonly HediffDef SSC_Hediff_Cow_Final = new HediffDef { defName = "SSC_Hediff_Cow_Final" };
        public static readonly HediffDef SSC_Hediff_TrainerOfficer = new HediffDef { defName = "SSC_Hediff_TrainerOfficer" };
        public static readonly HediffDef SSC_Hediff_Combatant = new HediffDef { defName = "SSC_Hediff_Combatant" };
        public static readonly HediffDef SSC_Hediff_Combatant_Final = new HediffDef { defName = "SSC_Hediff_Combatant_Final" };
        public static readonly JobDef SSC_Job_PetAffection = new JobDef { defName = "SSC_Job_PetAffection" };
    }
    public class HediffComp_CowMilkReservoir { public float CurrentCharge; }
    public static class TrainerSpecializationLifecycle
    {
        public static int NotifyCalls;
        public static void Notify(Pawn pawn) { NotifyCalls++; }
        public static void Maintain(Pawn pawn) { }
    }
    public static class TrainerSpecializationUtility
    {
        public static bool HasFinalRecord(Pawn pawn) => pawn?.TrainerFinal == true;
        public static bool MeetsContinuousConditions(Pawn pawn, out object failure) { failure = null; return pawn?.TrainerEligible == true; }
    }
    public static class TrainerSpecializationProgressUtility
    {
        public static float TryGainProgress(Pawn pawn, float amount)
        {
            float before = pawn.Training.specializationProgress;
            return pawn.Training.AddSpecializationProgress(amount) - before;
        }
    }
    public static class CombatantSpecializationUtility
    {
        public static int SyncCalls;
        public static bool HasFinalState(Pawn pawn) => pawn?.health?.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Combatant_Final) == true;
        public static void Sync(Pawn pawn) { SyncCalls++; }
        public static void RemoveOrdinaryState(Pawn pawn)
        {
            Hediff hediff = pawn?.health?.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant);
            if (hediff != null) pawn.health.RemoveHediff(hediff);
        }
    }
    public static class BusSpecializationUtility
    {
        public static bool HasFinalBusState(Pawn pawn) => pawn?.health?.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Bus_Final) == true;
        public static bool HasFinalCowState(Pawn pawn) => pawn?.health?.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Cow_Final) == true;
        public static void EnsureBusHediffFromSpecialization(Pawn pawn) { }
        public static void EnsureCowHediffFromSpecialization(Pawn pawn) { }
    }
    public class CompPersonalityStore
    {
        public readonly Dictionary<HediffDef, float> hediffTags = new Dictionary<HediffDef, float>();
        public void RemoveTag(HediffDef def) { if (def != null) hediffTags.Remove(def); }
        public void SetTag(HediffDef def, float severity) { if (def != null) hediffTags[def] = severity; }
        public bool HasTag(HediffDef def) => def != null && hediffTags.ContainsKey(def);
        public float GetTagSeverity(HediffDef def) => hediffTags.TryGetValue(def, out float value) ? value : 0;
    }
    public static class Strings
    {
        public const string ITab_SpecializationPetCat = "cat", ITab_SpecializationPetDog = "dog", ITab_SpecializationPetRabbit = "rabbit";
        public const string ITab_SelectSpecializationPetCat = "select cat", ITab_SelectSpecializationPetDog = "select dog", ITab_SelectSpecializationPetRabbit = "select rabbit";
        public const string ITab_SpecializationNone = "none", ITab_SelectSpecializationNone = "select none";
        public const string ITab_SpecializationPetDisabledMissingRequirements = "missing requirements";
        public const string ITab_SpecializationPetDisabledResearch = "research required";
        public const string ITab_SpecializationUnfinishedSuffix = "unfinished";
        public const string ITab_SpecializationFinalizedSuffix = "finalized";
        public const string ITab_SpecializationPetDisabledConflictingFinal = "conflicting final";
        public static string Message_PetSpecializationUnlocked(string name, string type) => name + type;
        public static string Message_BusSpecializationUnlocked(string name) => name;
        public static string Message_CowSpecializationUnlocked(string name) => name;
    }
}
