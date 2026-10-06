// 只模型化已从本机原版 DLL 核验的路径、Toil、预热及有限眩晕边界。
// 狗资格、接近重试、中断规则、同格检查和最终分支始终由直接链接的生产源码执行。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Verse
{
    public enum TargetIndex { A, B }
    public partial class Pawn
    {
        public PawnPathFollower pather = new();
        public PawnStanceTracker stances = new();
        public int NativeWakeCalls;
        // 本宿主只建模预热这一种全身busy；真实其它stance及完整调度仍需实机验证。
        public bool FullBodyBusy => stances?.curStance is Stance_Warmup warmup && warmup.verb.WarmingUp;
    }
    public class PawnPathFollower
    {
        public bool MovingNow;
        public Thing Destination;
        public IntVec3 DestinationCell => Destination?.Position ?? default;
        public PathEndMode LastMode;
        public int StartPathCalls, StopDeadCalls;
        public void StartPath(Thing target, PathEndMode mode)
        {
            Destination = target; LastMode = mode; StartPathCalls++; MovingNow = true;
        }
        public void StopDead() { StopDeadCalls++; MovingNow = false; }
    }
    public class PawnStanceTracker
    {
        public object curStance;
        public StunHandler stunner = new();
    }
    public class Stance_Warmup
    {
        public Verb verb;
        public int InterruptCalls;
        public void Interrupt() { InterruptCalls++; verb.WarmingUp = false; }
    }
    public class StunHandler
    {
        public int StunCalls, ClearCalls, EndTick;
        public bool Stunned => EndTick > Find.TickManager.TicksGame;
        public void StunFor(int ticks, Thing source, bool showMote = false, bool disableRotation = false, bool adapt = false)
        {
            StunCalls++; EndTick = Math.Max(EndTick, Find.TickManager.TicksGame + ticks);
        }
    }
    public partial class Verb
    {
        public Pawn caster;
        public bool WarmingUp;
        public bool NativeStartAllowed = true;
        public int BaseStartCalls, ResetCalls, WarmupRemaining;
        public LocalTargetInfo CurrentTarget;
        public Stance_Warmup WarmupStance;
        public float WarmupProgress => 1f - (float)WarmupRemaining / Math.Max(1, (int)(verbProps.warmupTime * 60));
        public virtual bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) => true;
        public virtual bool CanHitTarget(LocalTargetInfo target) => true;
        public virtual bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target) => true;
        public virtual bool TryStartCastOn(LocalTargetInfo target, LocalTargetInfo dest, bool surpriseAttack = false,
            bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            BaseStartCalls++;
            if (!NativeStartAllowed) return false;
            CurrentTarget = target;
            WarmupRemaining = (int)(verbProps.warmupTime * 60);
            WarmingUp = WarmupRemaining > 0;
            WarmupStance = new Stance_Warmup { verb = this };
            caster.stances.curStance = WarmupStance;
            return true;
        }
        public void Reset() { ResetCalls++; WarmingUp = false; CurrentTarget = default; }
        public void TickNativeWarmup()
        {
            if (!WarmingUp) return;
            // 原版 Stance_Warmup 使用 From 检查；此调用动态分派至真实狗 Verb。
            if (!CanHitTargetFrom(caster.Position, CurrentTarget)) { WarmupStance.Interrupt(); return; }
            if (--WarmupRemaining > 0) return;
            WarmingUp = false;
            if (this is Verb_CastAbility abilityVerb) abilityVerb.ability.Activate(CurrentTarget, default);
        }
    }
    public static class Scribe_Values
    {
        // 只测试字段名、默认值及 ExposeData 调用。字典不是实际 Verse 序列化或存档文件。
        public static readonly Dictionary<string, int> Values = new();
        public static void Look(ref int value, string key, int defaultValue = 0)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Values[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Values.TryGetValue(key, out int saved) ? saved : defaultValue;
        }
    }
}

namespace RimWorld
{
    public partial class Verb_CastAbility : Verb
    {
        public Ability ability;
        public override bool TryStartCastOn(LocalTargetInfo target, LocalTargetInfo dest, bool surpriseAttack = false,
            bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            bool started = base.TryStartCastOn(target, dest, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            // 对应本机原版 TryStartCastOn 的顺序：成功预热才限时眩晕，并唤醒普通睡眠。
            if (started && ability.def.stunTargetWhileCasting && verbProps.warmupTime > 0 && target.Pawn != null && target.Pawn != caster)
            {
                target.Pawn.stances.stunner.StunFor((int)(verbProps.warmupTime * 60), caster);
                if (!RestUtility.Awake(target.Pawn)) RestUtility.WakeUp(target.Pawn, true);
            }
            return started;
        }
    }
}

namespace Verse.AI
{
    public enum PathEndMode { OnCell }
    public enum JobCondition { Succeeded, Incompletable, InterruptForced }
    public enum ToilCompleteMode { Instant, PatherArrival, FinishedBusy }
    public partial class Job
    {
        public Ability ability;
        public Verb verbToUse;
        public JobDef def;
        public LocalTargetInfo targetA, targetB;
        public LocalTargetInfo GetTarget(TargetIndex index) => index == TargetIndex.A ? targetA : targetB;
    }
    public partial class PawnJobTracker
    {
        public JobDriver_CastAbility curDriver;
        public Job curJob;
        public JobCondition? EndCondition;
        public void EndCurrentJob(JobCondition condition) { EndCondition = condition; curDriver?.Finish(condition); }
    }
    public class Toil
    {
        public Pawn actor;
        public Action initAction, tickAction;
        public ToilCompleteMode defaultCompleteMode;
        public string Kind;
        public Toil JumpDestination;
        public Func<bool> JumpCondition;
        public readonly List<Func<bool>> Failures = new();
        public Func<float> Progress;
        public void FailOn(Func<bool> failure) => Failures.Add(failure);
        public void WithProgressBar(TargetIndex index, Func<float> progress, bool alwaysShow, float offset, bool interpolate) => Progress = progress;
    }
    public class JobDriver_CastAbility
    {
        public Pawn pawn;
        public Job job;
        public readonly List<Func<bool>> Failures = new();
        private readonly List<Action<JobCondition>> finishActions = new();
        public bool Finished;
        public int BaseMakeToilsCalls;
        public virtual void ExposeData() { }
        public virtual bool TryMakePreToilReservations(bool errorOnFailed) => true;
        protected virtual IEnumerable<Toil> MakeNewToils()
        {
            // 原版基类流程若被误用，此哨兵暴露取消扣冷却的问题；不为狗任务代做规则。
            BaseMakeToilsCalls++; AddFinishAction(_ => job.ability.StartCooldown(job.ability.def.cooldownTicks));
            yield return new Toil { Kind = "BaseCast", defaultCompleteMode = ToilCompleteMode.FinishedBusy };
        }
        public List<Toil> BuildToils()
        {
            var result = MakeNewToils().ToList();
            foreach (var toil in result) toil.actor = pawn;
            return result;
        }
        public void AddFinishAction(Action<JobCondition> action) => finishActions.Add(action);
        public void Finish(JobCondition condition)
        {
            if (Finished) return;
            Finished = true;
            foreach (var action in finishActions) action(condition);
        }
    }
    public static class JobDriverExtensions
    {
        public static void FailOn(this JobDriver_CastAbility driver, Func<bool> failure) => driver.Failures.Add(failure);
        public static void FailOnDespawnedOrNull(this JobDriver_CastAbility driver, TargetIndex index) =>
            driver.Failures.Add(() => driver.job.GetTarget(index).Pawn?.Spawned != true);
    }
    public static class Toils_Goto
    {
        public static Toil GotoThing(TargetIndex index, PathEndMode mode)
        {
            var toil = new Toil { Kind = "Goto", defaultCompleteMode = ToilCompleteMode.PatherArrival };
            toil.initAction = () => toil.actor.pather.StartPath(toil.actor.jobs.curJob.GetTarget(index).Thing, mode);
            return toil;
        }
    }
    public static class Toils_Jump
    {
        public static Toil JumpIf(Toil destination, Func<bool> condition) => new()
        { Kind = "Jump", defaultCompleteMode = ToilCompleteMode.Instant, JumpDestination = destination, JumpCondition = condition };
    }
    public static class Toils_General
    {
        public static Toil Do(Action action) => new() { Kind = "Do", defaultCompleteMode = ToilCompleteMode.Instant, initAction = action };
    }
    public static class Toils_Combat
    {
        public static Toil CastVerb(TargetIndex target, TargetIndex dest, bool canHitNonTargetPawns)
        {
            var toil = new Toil { Kind = "CastVerb", defaultCompleteMode = ToilCompleteMode.FinishedBusy };
            toil.initAction = () =>
            {
                Job job = toil.actor.jobs.curJob;
                // 原版丢弃开始返回值，失败但没有busy时由FinishedBusy自然结束。
                job.verbToUse.TryStartCastOn(job.GetTarget(target), job.GetTarget(dest), canHitNonTargetPawns: canHitNonTargetPawns);
            };
            return toil;
        }
    }
}

/// <summary>调用真实任务生成的 Toil 和失败谓词；由测试显式通知到达，不模拟游戏寻路。</summary>
internal sealed class CastBoundaryRunner
{
    public readonly SexSlaveCraft.JobDriver_PetDogTame Driver;
    public readonly List<Toil> Toils;
    public int Index;
    public Toil Current => Toils[Index];
    public bool Ended => Driver.Finished;
    public CastBoundaryRunner(Pawn pawn, Pawn target, Ability ability)
    {
        Driver = new SexSlaveCraft.JobDriver_PetDogTame
        {
            pawn = pawn, job = new Job { ability = ability, verbToUse = ability.verb, targetA = target, def = new JobDef { abilityCasting = false } }
        };
        pawn.jobs.curDriver = Driver; pawn.jobs.curJob = Driver.job;
        Toils = Driver.BuildToils();
        Current.initAction?.Invoke();
    }
    public bool FailedNow => Driver.Failures.Any(f => f()) || Current.Failures.Any(f => f());
    public void NotifyArrival(Action afterArrival = null)
    {
        Driver.pawn.Position = Driver.job.targetA.Cell;
        Driver.pawn.pather.MovingNow = false;
        afterArrival?.Invoke();
        Advance();
    }
    private void Advance()
    {
        if (Ended) return;
        Index++;
        while (Index < Toils.Count && Toils[Index].defaultCompleteMode == ToilCompleteMode.Instant)
        {
            var toil = Toils[Index];
            if (toil.JumpCondition?.Invoke() == true) { Index = Toils.IndexOf(toil.JumpDestination); Current.initAction?.Invoke(); return; }
            toil.initAction?.Invoke(); Index++;
        }
        if (Index >= Toils.Count) { Driver.pawn.jobs.EndCurrentJob(JobCondition.Succeeded); return; }
        Current.initAction?.Invoke();
    }
    public void Tick()
    {
        if (Ended) return;
        Find.TickManager.TicksGame++;
        if (FailedNow) { Driver.pawn.jobs.EndCurrentJob(JobCondition.Incompletable); return; }
        if (Current.defaultCompleteMode == ToilCompleteMode.FinishedBusy && !Driver.pawn.FullBodyBusy) { Advance(); return; }
        Current.tickAction?.Invoke();
        if (!Ended && Current.Kind == "CastVerb") Driver.job.verbToUse.TickNativeWarmup();
    }
    public void Cancel() => Driver.pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
}
