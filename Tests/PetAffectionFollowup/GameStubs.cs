// 只提供游戏/RJW/统一守卫的可观察公共边界；后续资格、掷骰、调度、交接与驱动链接生产。
// 不复刻原版ForceWait/Quickie生命周期、成年/Appraisal或统一许可算法。
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Verse
{
    public class Thing { public bool Destroyed; }
    public class Def { public string defName; }
    public class JobDef : Def { public Type driverClass; }
    public class EffecterDef : Def { }
    public static class DefDatabase<T> where T : Def
    {
        public static readonly Dictionary<string, T> Definitions = new();
        public static T GetNamedSilentFail(string name) => Definitions.TryGetValue(name, out T result) ? result : null;
    }
    public class Pawn : Thing
    {
        public Pawn() { jobs.Owner = this; }
        public bool Dead, Downed, Drafted, Spawned = true, InMentalState, Fighting, AwakeNow = true;
        public bool Human = true, Adult = true, Ready = true, CanFuck = true, CanBeFucked = true, Reachable = true;
        public bool AffectionQualified = true, Idle = true, Contact = true, Cat = true, Dog;
        public bool HasTraining = true, AdultCategory = true, BeerGoggles, Nympho, Frustrated, Horny = true;
        public bool CanHaveSex = true, ReadyForLovin = true, ReadyForHookup = true, CanTargetHookup = true, FoolAround = true;
        public SexSlaveCraft.CompSexSlaveTraining Training = new();
        public DevelopmentalStage DevelopmentalStage = DevelopmentalStage.Adult;
        public string LabelShort = "pawn";
        public object Map = new();
        public Pawn BoundMaster;
        public PawnHealth health = new();
        public RaceProperties RaceProps = new();
        public PawnAgeTracker ageTracker = new();
        public PawnJobTracker jobs = new();
        public RotationTracker rotationTracker = new();
        public IntVec3 Position;
        public Job CurJob => jobs?.curJob;
        public JobDef CurJobDef => CurJob?.def;
        public bool Awake() => AwakeNow;
        public bool IsFighting() => Fighting;
        public bool CanReserveAndReach(Thing target, PathEndMode mode, Danger danger) => Reachable;
        public bool CanReach(Thing target, PathEndMode mode, Danger danger) => Reachable;
        public bool HostileTo(Pawn target) => false;
        public T TryGetComp<T>() where T : class => HasTraining ? Training as T : null;
    }
    public class PawnHealth { public PawnCapacityTracker capacities = new(); }
    public class PawnCapacityTracker { public bool CanBeAwake = true; }
    public class RaceProperties { public bool Animal, Humanlike = true; }
    public class PawnAgeTracker { public int AgeBiologicalYears = 24; }
    public enum DevelopmentalStage { Baby, Child, Adult }
    public class RotationTracker { public void FaceTarget(Pawn target) { } }
    public struct IntVec3
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public float DistanceToSquared(IntVec3 other) => (x - other.x) * (x - other.x) + (z - other.z) * (z - other.z);
        public bool InHorDistOf(IntVec3 other, float range) => DistanceToSquared(other) <= range * range;
    }
    public enum Danger { Some, Deadly }
    public static class Find { public static readonly TickManager TickManager = new(); }
    public class TickManager { public int TicksGame = 200000; }
    public static class GenDate { public const int TicksPerDay = 60000, TicksPerHour = 2500; }
    public static class Rand
    {
        public static bool ChanceResult;
        public static readonly List<float> Chances = new();
        public static Action OnChance;
        public static bool Chance(float chance) { Chances.Add(chance); OnChance?.Invoke(); return ChanceResult; }
    }
    public static class Log { public static readonly List<string> Warnings = new(); public static void Warning(string value) => Warnings.Add(value); public static void Error(string value) => Warnings.Add(value); }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; public static readonly Dictionary<string, object> Values = new(); }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T defaultValue = default)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Values[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Scribe.Values.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
        }
    }
    public static class Scribe_References { public static void Look<T>(ref T value, string key) where T : class => Scribe_Values.Look(ref value, key); }
    public enum LookMode { Value }
    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> values, string key, LookMode mode)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Values[key] = values?.ToList();
            if (Scribe.mode == LoadSaveMode.LoadingVars) values = Scribe.Values.TryGetValue(key, out object saved) ? (saved as List<T>)?.ToList() : null;
        }
    }
}
namespace RimWorld
{
    public static class JobDefOf { public static readonly JobDef Wait = new() { defName = "Wait" }, Wait_Wander = new() { defName = "Wait_Wander" }; }
    public static class LovePartnerRelationUtility { public static bool Lovers; public static bool LovePartnerRelationExists(Pawn a, Pawn b) => Lovers; }
}
namespace Verse.AI
{
    public enum TargetIndex { A, B }
    public enum PathEndMode { Touch, OnCell }
    public enum JobCondition { InterruptForced, Succeeded, Incompletable }
    public enum JobTag { Misc }
    public enum ToilCompleteMode { Instant, Delay, PatherArrival }
    public struct LocalTargetInfo { public Thing Thing; public Pawn Pawn => Thing as Pawn; }
    public class Job
    {
        private static int nextId;
        public int loadID = ++nextId, expiryInterval, startTick;
        public JobDef def;
        public bool playerForced;
        public Thing target, targetB;
        public LocalTargetInfo GetTarget(TargetIndex index) => new() { Thing = index == TargetIndex.B ? targetB : target };
    }
    public static class JobMaker
    {
        public static int Made;
        public static readonly List<Job> Returned = new();
        public static Job MakeJob(JobDef def, Thing target) { Made++; return new() { def = def, target = target }; }
        public static void ReturnToPool(Job job) => Returned.Add(job);
    }
    public class QueuedJob { public Job job; public JobTag? tag; }
    public class JobQueue : List<QueuedJob>
    {
        public void RemoveAll(Pawn pawn, Predicate<Job> predicate) => RemoveAll(queued => predicate(queued.job));
        public QueuedJob Dequeue() { QueuedJob result = this[0]; RemoveAt(0); return result; }
        public bool Contains(Job job) => this.Any(queued => queued.job == job);
        public void EnqueueLast(Job job, JobTag? tag = null) => Add(new() { job = job, tag = tag });
    }
    public class PawnJobTracker
    {
        public Pawn Owner;
        public Job curJob;
        public JobDriver curDriver;
        public JobQueue jobQueue = new();
        public int EndCalls, StartCalls;
        public bool AcceptStart = true;
        public Job LastStarted;
        public Action<Job> OnStart;
        public void StartJob(Job job, JobCondition condition, JobTag tag, bool preToilReservationsCanFail)
        {
            StartCalls++; LastStarted = job; OnStart?.Invoke(job);
            if (AcceptStart)
            {
                curJob = job;
                curDriver = (JobDriver)Activator.CreateInstance(job.def.driverClass);
                curDriver.job = job;
                curDriver.pawn = Owner;
            }
        }
        public void EndCurrentJob(JobCondition condition, bool startNewJob = true, bool canReturnToPool = true) { EndCalls++; curJob = null; curDriver = null; }
    }
    public class Toil
    {
        public Pawn actor;
        public Action initAction, tickAction;
        public Action<int> tickIntervalAction;
        public ToilCompleteMode defaultCompleteMode;
        public int defaultDuration;
        public readonly List<Func<bool>> FailureConditions = new();
        public void FailOn(Func<bool> condition) => FailureConditions.Add(condition);
    }
    public static class Toils_Goto { public static Toil GotoThing(TargetIndex index, PathEndMode mode) => new() { defaultCompleteMode = ToilCompleteMode.PatherArrival }; }
    public static class Toils_General
    {
        public static Action NativeWaitWithInit;
        public static Toil WaitWith(TargetIndex index, int ticks, bool useProgressBar = false, bool maintainPosture = false, bool maintainSleep = false,
            TargetIndex face = TargetIndex.A, PathEndMode pathEndMode = PathEndMode.Touch) =>
            new() { defaultDuration = ticks, defaultCompleteMode = ToilCompleteMode.Delay, initAction = () => NativeWaitWithInit?.Invoke() };
    }
    public abstract class JobDriver
    {
        public Pawn pawn;
        public Job job;
        public bool ended;
        public JobCondition? EndCondition;
        public readonly List<Func<bool>> FailureConditions = new();
        public readonly List<Action<JobCondition>> FinishActions = new();
        public virtual void ExposeData() { }
        public virtual bool TryMakePreToilReservations(bool errorOnFailed) => true;
        protected abstract IEnumerable<Toil> MakeNewToils();
        public Toil[] BuildToils() { Toil[] result = MakeNewToils().ToArray(); foreach (Toil toil in result) toil.actor = pawn; return result; }
        public void AddFinishAction(Action<JobCondition> action) => FinishActions.Add(action);
        public void EndJobWith(JobCondition condition) { ended = true; EndCondition = condition; }
        public void NotifyFinish(JobCondition condition) { foreach (var action in FinishActions) action(condition); }
    }
    public static class JobDriverExtensions
    {
        public static void FailOn(this JobDriver driver, Func<bool> condition) => driver.FailureConditions.Add(condition);
        public static void FailOnDespawnedNullOrForbidden(this JobDriver driver, TargetIndex index) => driver.FailOn(() => driver.job.GetTarget(index).Pawn is not Pawn target || !target.Spawned || target.Destroyed);
    }
    public static class ToilEffects { public static Toil WithEffect(this Toil toil, object effect, TargetIndex target) => toil; }
}
