// 游戏任务、随机数与外部插件的最小宿主。效果查询、成长公式、亲昵驱动和对账均链接生产源码。
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Verse
{
    public partial class Pawn
    {
        public bool InMentalState, Fighting, Hostile, Reachable = true, Forbidden;
        public bool AwakeNow = true, ImmediatelyReachable = true;
        public RaceProperties RaceProps = new();
        public Ownership ownership = new();
        public RotationTracker rotationTracker = new();
        public Job CurJob => jobs?.curJob;
        public bool IsFighting() => Fighting;
        public bool HostileTo(Pawn other) => Hostile;
        public bool CanReach(Thing other, PathEndMode mode, Danger danger) => Reachable;
        public bool CanReserveAndReach(Thing other, PathEndMode mode, Danger danger) => Reachable;
        public bool Awake() => AwakeNow;
    }
    public class RaceProperties { public bool Animal; }
    public class Ownership { public Building_Bed OwnedBed; }
    public class Building_Bed : Thing { public IntVec3 Position; }
    public class RotationTracker
    {
        public Pawn LastTarget;
        public void FaceTarget(Pawn pawn) => LastTarget = pawn;
    }
    public enum Danger { Some, Deadly }
    public static class GenDate { public const int TicksPerDay = 60000, TicksPerHour = 2500; }
    public static class Rand
    {
        public static bool ChanceResult;
        public static readonly List<float> Chances = new();
        public static bool Chance(float chance) { Chances.Add(chance); return ChanceResult; }
    }
}

namespace Verse.AI
{
    public enum TargetIndex { A, B }
    public enum PathEndMode { Touch, OnCell }
    public enum JobCondition { InterruptForced, Succeeded, Incompletable }
    public enum ToilCompleteMode { Instant, Delay, PatherArrival }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        public Pawn Pawn => Thing as Pawn;
    }
    public partial class Job
    {
        public JobDef def;
        public Thing target, targetB;
        public bool playerForced;
        public int loadID;
        public int startTick;
        public LocalTargetInfo GetTarget(TargetIndex index) => new() { Thing = index == TargetIndex.B ? targetB : target };
    }
    public partial class PawnJobTracker
    {
        public Job curJob;
        public JobDriver curDriver;
        public JobQueue jobQueue = new();
        public int EndCalls;
        public JobCondition? EndCondition;
        public void StartJob(Job job, JobCondition condition, JobTag tag, bool preToilReservationsCanFail) => curJob = job;
        public void EndCurrentJob(JobCondition condition, bool startNewJob = true, bool canReturnToPool = true)
        { EndCalls++; EndCondition = condition; curJob = null; }
    }
    public class QueuedJob { public Job job; }
    public class JobQueue : List<QueuedJob>
    {
        public void RemoveAll(Pawn pawn, Predicate<Job> predicate) { }
    }
    public class Toil
    {
        public Pawn actor;
        public ToilCompleteMode defaultCompleteMode;
        public Action initAction, tickAction;
        public Action<int> tickIntervalAction;
        public bool handlingFacing;
        public int duration { get => defaultDuration; set => defaultDuration = value; }
        public int defaultDuration;
        public int NativeWaitTicks;
        public string Kind;
        public TargetIndex? Target;
        public PathEndMode? EndMode;
        public bool Progress, Effect;
        public object EffectDef;
        public readonly List<Func<bool>> FailureConditions = new();
        public void FailOn(Func<bool> condition) => FailureConditions.Add(condition);
    }
    public static class Toils_General
    {
        public static Toil Wait(int ticks, TargetIndex index) => new() { duration = ticks, defaultCompleteMode = ToilCompleteMode.Delay };
        // 原版WaitWith的实现不在宿主重写；测试显式提供其init的目标等待输出。
        public static Action<Toil> NativeWaitWithInit;
        public static Action<Toil, int> NativeWaitWithTick;
        public static Toil WaitWith(TargetIndex index, int ticks, bool useProgressBar = false, bool maintainPosture = false, bool maintainSleep = false,
            TargetIndex face = TargetIndex.A, PathEndMode pathEndMode = PathEndMode.Touch)
        {
            var toil = new Toil { Kind = "WaitWith", Target = index, duration = ticks,
                NativeWaitTicks = ticks, EndMode = pathEndMode, Progress = useProgressBar, defaultCompleteMode = ToilCompleteMode.Delay };
            toil.initAction = () => NativeWaitWithInit?.Invoke(toil);
            toil.tickIntervalAction = interval => NativeWaitWithTick?.Invoke(toil, interval);
            return toil;
        }
    }
    public static class Toils_Goto
    {
        public static Toil GotoThing(TargetIndex index, PathEndMode mode) => new()
        { Kind = "Goto", Target = index, EndMode = mode, defaultCompleteMode = ToilCompleteMode.PatherArrival };
    }
    public static class ReachabilityImmediate
    {
        public static bool CanReachImmediate(Pawn actor, Thing target, PathEndMode mode) => actor.ImmediatelyReachable;
    }
    public abstract class JobDriver
    {
        public Pawn pawn;
        public Job job;
        public readonly List<Func<bool>> FailureConditions = new();
        public readonly List<Action<JobCondition>> FinishActions = new();
        public JobCondition? EndCondition;
        public bool ended;
        public virtual void ExposeData() { }
        public virtual bool TryMakePreToilReservations(bool errorOnFailed) => true;
        protected abstract IEnumerable<Toil> MakeNewToils();
        // 只暴露生产驱动注册的条件和动作，不模拟引擎执行顺序、寻路或存档。
        public IEnumerable<Toil> BuildToils()
        {
            var toils = new List<Toil>(MakeNewToils());
            foreach (Toil toil in toils) toil.actor = pawn;
            return toils;
        }
        public void AddFinishAction(Action<JobCondition> action) => FinishActions.Add(action);
        public void EndJobWith(JobCondition condition) { EndCondition = condition; ended = true; }
        public void NotifyFinish(JobCondition condition)
        { foreach (Action<JobCondition> action in FinishActions) action(condition); }
    }
    public static class JobDriverExtensions
    {
        public static void FailOn(this JobDriver driver, Func<bool> condition) => driver.FailureConditions.Add(condition);
        public static void FailOnDespawnedNullOrForbidden(this JobDriver driver, TargetIndex index) =>
            driver.FailOn(() => driver.job.GetTarget(index).Pawn is not Pawn pawn || !pawn.Spawned || pawn.Destroyed || pawn.Forbidden);
    }
    public static class ToilEffects
    {
        public static Toil WithEffect(this Toil toil, object def, TargetIndex index)
        { toil.Effect = true; toil.EffectDef = def; return toil; }
    }
}

namespace rjw
{
    public class SexProps { public Pawn pawn, partner; }
    public class JobDriver_Sex : JobDriver
    {
        protected override IEnumerable<Toil> MakeNewToils() { yield break; }
    }
    public static class xxx
    {
        public static readonly JobDef bestiality = new(), bestialityForFemale = new();
        public static bool can_rape(Pawn pawn) => true;
        public static bool can_fuck(Pawn pawn) => true;
        public static bool can_be_fucked(Pawn pawn) => true;
        public static bool can_do_animalsex(Pawn a, Pawn b) => true;
        public static bool is_human(Pawn pawn) => !pawn.RaceProps.Animal;
        public static bool is_animal(Pawn pawn) => pawn.RaceProps.Animal;
    }
}

namespace SexSlaveCraft
{
    public enum SSCRestrictionEvent { Dog }
    public static class SSCRestrictionJobGuard
    {
        // 此套件不启动外部行为；仅观察真实狗工作入口的资格、收益、冷却和概率。
        public static int PrepareCalls;
        public static bool PrepareEvent(Pawn pawn, Job job, SSCRestrictionEvent source) { PrepareCalls++; return false; }
        public static void CancelPendingEvent(Job job) { }
    }
}
