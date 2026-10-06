using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void RunMovementCases()
    {
        Run("地图选择资格与同格开始入口分离，From检查同样限定同格", VerbSelectionAndStart);
        Run("开始前冷却、撤销、失格及原版限制均拒绝", VerbStartGuards);
        Run("普通睡眠及有意识倒地动物可选，同格预热才唤醒", SleepingAndDownedWarmup);
        Run("真实任务只有接近和一次原版读条，不使用结束扣冷却基类", ToilGraphAndReservations);
        Run("远距接近不暂停目标，到达后120tick预热才驯服", ApproachAndNativeWarmup);
        Run("移动目标保留Pawn引用，到达后移位重新接近", MovingTargetAndArrivalRace);
        Run("行走中失格取消，不眩晕驯服或消耗冷却", ApproachCancellation);
        Run("预热中阵营精神意识资格和位置变化取消，不清其他来源眩晕", WarmupCancellation);
        Run("预热最后tick被他人驯服或变为不驯服目标拒绝结算", LastTickTargetChange);
        Run("独立From预热检查会打断相邻格目标", NativeWarmupFromBoundary);
        Run("成功目标已变阵营仍正常FinishedBusy收尾，原版开始失败无结算", SuccessfulFinishedBusy);
        Run("停滞接近最多十次补寻路，移动清计数且无总行程时限", ApproachRetries);
        Run("接近不可达停止重试，预热不重复路径或眩晕", RetryAndCastIsolation);
        Run("ExposeData重试字段和旧默认值，加载字段不启动路径预热", RetrySerializationBoundary);
        Run("取消只清本人当前任务的预热，不动其他Verb或任务", CleanupOwnership);
    }
    private static void Tick(CastBoundaryRunner runner, int count) { for (int i = 0; i < count; i++) runner.Tick(); }

    /// <summary>运行生产Verb的各入口，而不是让原版边界stub代做选取及同格判断。</summary>
    private static void VerbSelectionAndStart()
    {
        var p = Pair(); var verb = Verb(p.ability); p.target.Position = new IntVec3(120, 37); p.caster.Map.LineOfSight = false;
        Check(verb.ValidateTarget(p.target) && !verb.CanHitTarget(p.target) && !verb.CanHitTargetFrom(p.caster.Position, p.target), "selection/execution conflated");
        Check(!verb.TryStartCastOn(p.target, default) && verb.BaseStartCalls == 0 && p.target.stances.stunner.StunCalls == 0, "remote entered native warmup");
        p.caster.Reachable = false; Check(!verb.ValidateTarget(p.target, true) && Messages.Requests.Count == 1, "unreachable report missing");
        p.caster.Reachable = true; p.caster.Position = p.target.Position;
        Check(verb.CanHitTarget(p.target) && verb.CanHitTargetFrom(p.caster.Position, p.target) && verb.TryStartCastOn(p.target, default), "same-cell native start rejected");
        Check(!verb.CanHitTargetFrom(new IntVec3(121, 37), p.target) && !verb.CanHitTargetFrom(p.caster.Position, default), "adjacent/missing From accepted");
        Check(verb.BaseStartCalls == 1 && verb.WarmingUp && p.target.stances.stunner.StunCalls == 1, "native start duplicated"); NoSettlement(p);
    }
    private static void VerbStartGuards()
    {
        for (int scenario = 0; scenario < 6; scenario++)
        {
            Reset(); var p = Pair(); var verb = Verb(p.ability);
            if (scenario == 0) p.ability.BaseAllowed = false;
            if (scenario == 1) p.ability.StartCooldown(100);
            if (scenario == 2) p.caster.health.hediffSet.hediffs.Clear();
            if (scenario == 3) { p.caster.abilities.RemoveAbility(p.ability.def); p.caster.abilities.GainAbility(p.ability.def); }
            if (scenario == 4) Effect(p.ability).BaseAllowed = false;
            if (scenario == 5) p.caster.abilities = null;
            int starts = p.ability.CooldownStartCalls;
            Check(!verb.TryStartCastOn(p.target, default) && verb.BaseStartCalls == 0 && !verb.WarmingUp && p.target.stances.stunner.StunCalls == 0, "invalid native start " + scenario);
            Check(!p.ability.Activate(p.target, default) && p.ability.ActivationCalls == 0 && p.ability.CooldownStartCalls == starts && InteractionWorker_RecruitAttempt.Requests.Count == 0, "invalid activated " + scenario);
        }
    }
    private static void SleepingAndDownedWarmup()
    {
        foreach (bool downed in new[] { false, true })
        {
            Reset(); var p = Pair(); p.target.Sleeping = true; p.target.Downed = downed; p.target.Position = new IntVec3(20, 20);
            Check(Verb(p.ability).ValidateTarget(p.target) && !Verb(p.ability).TryStartCastOn(p.target, default) && p.target.Sleeping && p.target.NativeWakeCalls == 0, "selection/approach changed sleep or excludes conscious downed");
            p.caster.Position = p.target.Position; Check(Verb(p.ability).TryStartCastOn(p.target, default), "sleeping conscious same-cell rejected");
            Check(!p.target.Sleeping && p.target.NativeWakeCalls == 1 && p.target.stances.stunner.StunCalls == 1, "native successful start did not wake/stun once"); NoSettlement(p);
        }
    }
    private static void ToilGraphAndReservations()
    {
        var p = Pair(); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        Check(runner.Driver.TryMakePreToilReservations(true) && runner.Driver.BaseMakeToilsCalls == 0, "driver reserved or used base cast");
        Check(runner.Toils.Count == 4 && runner.Toils.Select(t => t.Kind).SequenceEqual(new[] { "Goto", "Jump", "Do", "CastVerb" }), "unexpected toil graph");
        Check(runner.Toils.Last().Progress != null && runner.Toils.Last().defaultCompleteMode == ToilCompleteMode.FinishedBusy, "native progress/busy missing");
        runner.Cancel(); NoSettlement(p);
    }
    private static void ApproachAndNativeWarmup()
    {
        var p = Pair(); p.target.Position = new IntVec3(100, 30); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        Tick(runner, 40); Check(p.caster.pather.MovingNow && p.caster.pather.Destination == p.target && p.caster.pather.LastMode == PathEndMode.OnCell && p.target.stances.stunner.StunCalls == 0, "approach paused target or wrong destination"); NoSettlement(p);
        runner.NotifyArrival(); Check(Verb(p.ability).WarmingUp && Verb(p.ability).WarmupRemaining == 120 && !p.caster.pather.MovingNow && p.target.stances.stunner.StunCalls == 1, "same-cell native warmup missing");
        Tick(runner, 119); NoSettlement(p); runner.Tick();
        Check(p.target.Faction == Faction.OfPlayer && InteractionWorker_RecruitAttempt.Requests.Count == 1 && p.ability.CooldownTicksRemaining == 60000, "120tick native effect missing");
        runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Succeeded && p.ability.CooldownStartCalls == 1, "normal completion took second cooldown");
    }
    private static void MovingTargetAndArrivalRace()
    {
        var p = Pair(); p.target.Position = new IntVec3(15, 15); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        p.target.Position = new IntVec3(25, 25); Check(p.caster.pather.DestinationCell == p.target.Position, "path lost target Pawn reference");
        runner.NotifyArrival(() => p.target.Position = new IntVec3(26, 25));
        Check(runner.Index == 0 && p.caster.pather.StartPathCalls == 2 && !Verb(p.ability).WarmingUp && p.target.stances.stunner.StunCalls == 0, "arrival race started remote warmup"); NoSettlement(p);
        runner.NotifyArrival(); Tick(runner, 120); Check(p.target.Faction == Faction.OfPlayer && InteractionWorker_RecruitAttempt.Requests.Count == 1, "rearrival failed");
    }
    private static void ApproachCancellation()
    {
        for (int scenario = 0; scenario < 6; scenario++)
        {
            Reset(); var p = Pair(); p.target.Position = new IntVec3(10, 10); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
            if (scenario == 0) p.target.Faction = Faction.OfPlayer;
            if (scenario == 1) p.target.Conscious = false;
            if (scenario == 2) p.target.MentalState = new MentalState();
            if (scenario == 3) p.caster.health.hediffSet.hediffs.Clear();
            if (scenario == 4) { p.caster.abilities.RemoveAbility(p.ability.def); p.caster.abilities.GainAbility(p.ability.def); }
            if (scenario == 5) p.target.NativeTamingAllowed = false;
            runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Incompletable && p.target.stances.stunner.StunCalls == 0, "approach invalid failed to cancel " + scenario); NoSettlement(p);
        }
        Reset(); var canceled = Pair(); var canceledRunner = new CastBoundaryRunner(canceled.caster, canceled.target, canceled.ability); canceledRunner.Cancel(); NoSettlement(canceled);
    }
    private static void WarmupCancellation()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        {
            Reset(); var p = Pair(); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 20);
            p.target.stances.stunner.StunFor(1000, new Thing()); int stunEnd = p.target.stances.stunner.EndTick;
            if (scenario == 0) p.target.Faction = Faction.OfPlayer;
            if (scenario == 1) p.target.Conscious = false;
            if (scenario == 2) p.target.MentalState = new MentalState();
            if (scenario == 3) p.target.Position = new IntVec3(1, 0);
            if (scenario == 4) p.caster.health.hediffSet.hediffs.Clear();
            if (scenario == 5) p.target.NativeTamingAllowed = false;
            if (scenario == 6) p.caster.abilities.RemoveAbility(p.ability.def);
            runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Incompletable && !Verb(p.ability).WarmingUp, "warmup invalid did not interrupt " + scenario);
            Check(p.target.stances.stunner.ClearCalls == 0 && p.target.stances.stunner.EndTick == stunEnd, "cancel cleared other stun"); NoSettlement(p);
            Verb(p.ability).TickNativeWarmup(); NoSettlement(p);
        }
    }
    private static void LastTickTargetChange()
    {
        foreach (bool alreadyTamed in new[] { false, true })
        {
            Reset(); var p = Pair(); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 119);
            if (alreadyTamed) p.target.Faction = Faction.OfPlayer; else p.target.NativeTamingAllowed = false;
            runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Incompletable, "last tick target change did not cancel"); NoSettlement(p);
        }
    }
    private static void NativeWarmupFromBoundary()
    {
        var p = Pair(); var verb = Verb(p.ability); Check(verb.TryStartCastOn(p.target, default), "valid native start rejected");
        p.target.Position = new IntVec3(1, 0); verb.TickNativeWarmup(); Check(!verb.WarmingUp && verb.WarmupStance.InterruptCalls == 1, "From check accepted adjacent target"); NoSettlement(p);
    }
    private static void SuccessfulFinishedBusy()
    {
        var p = Pair(); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 120);
        Check(!runner.Ended && !p.ability.Casting && !runner.FailedNow && !PetDogAbilityUtility.CanTarget(p.caster, p.target, out _), "success target faction triggered failure before FinishedBusy");
        runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Succeeded && p.ability.CooldownStartCalls == 1, "success did not finish once");
        Reset(); p = Pair(); Verb(p.ability).NativeStartAllowed = false; runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); runner.Tick();
        Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Succeeded && p.target.stances.stunner.StunCalls == 0, "no-busy native rejection did not close naturally"); NoSettlement(p);
        foreach (bool reset in new[] { false, true })
        {
            Reset(); p = Pair(); runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 10);
            if (reset) Verb(p.ability).Reset(); else Verb(p.ability).WarmupStance.Interrupt();
            runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Succeeded, "engine interrupt did not finish naturally"); NoSettlement(p);
        }
    }
    private static void ApproachRetries()
    {
        var p = Pair(); p.target.Position = new IntVec3(100, 1); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        Find.TickManager.TicksGame = 100018; p.caster.pather.MovingNow = false; runner.Tick(); Check(p.caster.pather.StartPathCalls == 1, "retry ignored 60tick interval");
        for (int i = 0; i < 10; i++)
        {
            p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100019 + (i + 1) * 60; runner.Tick();
            Check(!runner.Ended && p.caster.pather.StartPathCalls == i + 2, "retry count/timing wrong " + i);
        }
        p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100679; runner.Tick(); Check(runner.Ended && p.caster.pather.StartPathCalls == 11, "11th idle interval not bounded"); NoSettlement(p);
        Reset(); p = Pair(); p.target.Position = new IntVec3(100, 1); runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        for (int i = 0; i < 10; i++) { p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100079 + i * 60; runner.Tick(); }
        p.caster.pather.MovingNow = true; runner.Tick(); p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100679; runner.Tick();
        Check(!runner.Ended && p.caster.pather.StartPathCalls == 12, "normal move did not reset retry budget"); p.caster.pather.MovingNow = true; Tick(runner, 2000); Check(!runner.Ended, "normal long path has time limit"); runner.Cancel(); NoSettlement(p);
    }
    private static void RetryAndCastIsolation()
    {
        var p = Pair(); p.target.Position = new IntVec3(9, 9); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); p.caster.Reachable = false; p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100079; runner.Tick();
        Check(runner.Ended && p.caster.pather.StartPathCalls == 1, "unreachable path restarted"); NoSettlement(p);
        Reset(); p = Pair(); runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 119);
        Check(p.caster.pather.StartPathCalls == 1 && p.target.stances.stunner.StunCalls == 1 && !p.caster.pather.MovingNow, "cast repeated path/stun"); runner.Cancel(); NoSettlement(p);
    }
    private static void RetrySerializationBoundary()
    {
        var p = Pair(); p.target.Position = new IntVec3(9, 9); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        for (int i = 0; i < 3; i++) { p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100079 + i * 60; runner.Tick(); }
        Scribe.mode = LoadSaveMode.Saving; runner.Driver.ExposeData();
        Check(Scribe_Values.Values.Count == 1 && Scribe_Values.Values["sscDogTameApproachIdleRetries"] == 3, "retry field/default wrong");
        var restored = new JobDriver_PetDogTame { pawn = p.caster, job = runner.Driver.job }; int pathStarts = p.caster.pather.StartPathCalls;
        Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData(); Scribe.mode = LoadSaveMode.Saving; restored.ExposeData();
        Check(Scribe_Values.Values["sscDogTameApproachIdleRetries"] == 3 && p.caster.pather.StartPathCalls == pathStarts && p.target.stances.stunner.StunCalls == 0, "loading started path/preheat");
        Scribe_Values.Values.Clear(); Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData(); Scribe.mode = LoadSaveMode.Saving; restored.ExposeData();
        Check(Scribe_Values.Values["sscDogTameApproachIdleRetries"] == 0, "missing old retry field not zero"); Scribe.mode = LoadSaveMode.Inactive;
    }
    private static void CleanupOwnership()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            Reset(); var p = Pair(); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); var ownStance = p.caster.stances.curStance as Stance_Warmup;
            var foreignVerb = new Verb { caster = p.caster, WarmingUp = true }; var foreignStance = new Stance_Warmup { verb = foreignVerb };
            if (scenario == 0) p.caster.stances.curStance = foreignStance;
            if (scenario == 1) { p.caster.jobs.curDriver = new JobDriver_CastAbility(); p.caster.stances.curStance = foreignStance; }
            if (scenario == 2) p.caster.Spawned = false;
            runner.Driver.Finish(JobCondition.InterruptForced);
            Check(foreignStance.InterruptCalls == 0 && foreignVerb.WarmingUp && foreignVerb.ResetCalls == 0, "cleanup interrupted foreign verb/job");
            Check(scenario == 2 ? ownStance.InterruptCalls == 1 && Verb(p.ability).ResetCalls == 1 : ownStance.InterruptCalls == 0 && Verb(p.ability).ResetCalls == (scenario == 0 ? 1 : 0), "own cleanup ownership wrong"); NoSettlement(p);
        }
    }
}
