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
        Run("真实Verb允许地图可达选择，开始与From预热检查均限定同格", VerbSelectionAndStart);
        Run("真实Verb在进入原版前拒绝无资格、配置损坏、冷却和撤销实例", VerbStartGuards);
        Run("意识门控独立于睡眠倒地和精神来源，缺意识容器也拒绝", ConsciousnessBoundary);
        Run("普通睡眠仅同格开始原版预热才唤醒，意识合格倒地可安抚", SleepingAndDownedWarmup);
        Run("真实驱动仅接近与一次原版CastVerb，不调用基类结束扣冷却", ToilGraphAndReservations);
        Run("远距离行走不暂停目标，到达同格才120tick预热及效果冷却", ApproachAndNativeWarmup);
        Run("移动目标保持Pawn引用，到达后移位回到接近而不提前预热", MovingTargetAndArrivalRace);
        Run("行走中资格和技能归属改变中断，未启动预热也不扣冷却", ApproachCancellation);
        Run("读条中位置意识和成果失效只中断本人预热，不清其他来源眩晕", WarmupCancellation);
        Run("真实From检查在原版Stance边界拒绝相邻格，隔墙同格不受视线影响", NativeWarmupFromBoundary);
        Run("成功冷却和无心情恢复正常收尾，开始失败及预热中断无busy空收尾", SuccessfulFinishedBusy);
        Run("真实读条结束只按最新精神实例选一个分支，不缓存开始状态", WarmupLateBranches);
        Run("接近停滞每60tick最多10次补寻路，正常移动清重试不设行程时限", ApproachRetries);
        Run("接近失败或不可达停止重试，读条阶段不重寻路或重复暂停", RetryAndCastIsolation);
        Run("ExposeData保存重试计数和旧默认值，字段装载本身不重启路径预热", RetrySerializationBoundary);
        Run("任务取消保留其他Verb或其他任务预热，本人离图仍清本人预热", CleanupOwnership);
        Run("升级读档PostLoadInit只修复新Verb归属，保留能力冷却与预热对象", UpgradeVerbOwner);
    }

    private static Verb_PetCatComfort Verb(Ability ability) => (Verb_PetCatComfort)ability.verb;
    private static void Tick(CastBoundaryRunner runner, int count)
    {
        for (int i = 0; i < count; i++) runner.Tick();
    }
    private static void NoSettlement((Pawn caster, Pawn target, Ability_PetCatComfort ability) p)
    {
        Check(p.ability.ActivationCalls == 0 && p.ability.CooldownStartCalls == 0 && BuffCount(p.target) == 0,
            "interrupted or approaching cast settled effect/cooldown");
    }

    /// <summary>调用实际Verb override；原版零射程宿主没有替猫实现同格规则。</summary>
    private static void VerbSelectionAndStart()
    {
        var p = Pair(); var verb = Verb(p.ability);
        p.target.Position = new IntVec3(120, 37); p.caster.Map.LineOfSight = false;
        Check(verb.ValidateTarget(p.target) && !verb.CanHitTarget(p.target) && !verb.CanHitTargetFrom(p.caster.Position, p.target), "selection or same-cell contract wrong");
        Check(!verb.TryStartCastOn(p.target, default) && verb.BaseStartCalls == 0 && p.target.stances.stunner.StunCalls == 0, "remote target entered native warmup");
        p.caster.Reachable = false;
        Check(!verb.ValidateTarget(p.target, true) && Messages.Requests.Last() == "SSC_PetCatComfortUnreachableTarget", "selection did not report unreachable reason");
        p.caster.Reachable = true; p.caster.Position = p.target.Position;
        Check(verb.CanHitTarget(p.target) && verb.CanHitTargetFrom(p.caster.Position, p.target) && verb.TryStartCastOn(p.target, default), "same cell refused native warmup");
        Check(!verb.CanHitTargetFrom(new IntVec3(121, 37), p.target) && !verb.CanHitTargetFrom(p.caster.Position, default), "From accepts adjacent or missing target");
        Check(verb.BaseStartCalls == 1 && verb.WarmingUp && p.target.stances.stunner.StunCalls == 1, "native warmup entered more than once");
        NoSettlement(p);
    }

    /// <summary>只观察能否进入原版开始边界，损坏定义不靠宿主特殊处理。</summary>
    private static void VerbStartGuards()
    {
        for (int scenario = 0; scenario < 8; scenario++)
        {
            Reset(); var p = Pair(); var verb = Verb(p.ability);
            if (scenario == 0) p.ability.BaseAllowed = false;
            if (scenario == 1) p.ability.StartCooldown(100);
            if (scenario == 2) p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
            if (scenario == 3) { p.caster.abilities.RemoveAbility(p.ability.def); p.caster.abilities.GainAbility(p.ability.def); }
            if (scenario == 4) p.target.Conscious = false;
            if (scenario == 5) Effect(p.ability).BaseAllowed = false;
            if (scenario == 6) Effect(p.ability).Props.durationTicks = 0;
            if (scenario == 7) p.caster.abilities = null;
            int starts = p.ability.CooldownStartCalls;
            Check(!verb.TryStartCastOn(p.target, default) && verb.BaseStartCalls == 0 && p.target.stances.stunner.StunCalls == 0 && !verb.WarmingUp,
                "late start guard entered native pause " + scenario);
            Check(!p.ability.Activate(p.target, default) && p.ability.ActivationCalls == 0 && p.ability.CooldownStartCalls == starts, "late Activate guard spent cooldown " + scenario);
        }
    }

    /// <summary>意识来自真实工具读取的 capacities；疾病名字和精神状态不能绕过此门槛。</summary>
    private static void ConsciousnessBoundary()
    {
        for (int scenario = 0; scenario < 4; scenario++)
        {
            Reset(); var p = Pair(); var state = Mental(p.target);
            p.target.needs = null; p.target.Downed = true;
            if (scenario == 0) p.target.Conscious = false;
            if (scenario == 1) p.target.health.capacities = null;
            if (scenario == 2) { p.target.Conscious = false; p.target.MentalState = null; p.target.needs = new(); }
            if (scenario == 3) { p.target.Conscious = false; p.target.health.AddHediff(new HediffDef { defName = "CatatonicBreakdown" }); }
            Check(!PetCatAbilityUtility.CanTarget(p.caster, p.target, out string reason) && reason == "SSC_PetCatComfortUnconsciousTarget", "unconscious target accepted or wrong reason");
            Check(!Verb(p.ability).ValidateTarget(p.target, false) && !Verb(p.ability).TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default), "consciousness gate missing from an entry");
            Check(state.RecoverCalls == 0 && p.target.stances.stunner.StunCalls == 0, "unconscious target paused or recovered"); NoSettlement(p);
        }
    }

    /// <summary>唤醒和有限眩晕由原版边界宿主记录；正常睡眠不被生产意识资格误判。</summary>
    private static void SleepingAndDownedWarmup()
    {
        foreach (bool mental in new[] { false, true })
        {
            Reset(); var p = Pair(); p.target.Downed = true; p.target.Sleeping = true;
            var state = mental ? Mental(p.target) : null;
            if (mental) p.target.needs = null;
            p.target.Position = new IntVec3(20, 5);
            Check(Verb(p.ability).ValidateTarget(p.target), "awake-capable sleeping/downed target rejected");
            var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); Tick(runner, 10);
            Check(p.target.Sleeping && p.target.NativeWakeCalls == 0 && p.target.stances.stunner.StunCalls == 0, "approach woke or paused target");
            runner.NotifyArrival();
            Check(!p.target.Sleeping && p.target.NativeWakeCalls == 1 && p.target.stances.stunner.Stunned, "native start failed to wake and pause eligible sleeper");
            Tick(runner, 120); runner.Tick();
            Check(p.caster.jobs.EndCondition == JobCondition.Succeeded && (mental ? state.RecoverCalls == 1 && BuffCount(p.target) == 0 : BuffCount(p.target) == 1), "conscious downed result wrong");
        }
    }

    /// <summary>实际驱动生成Toil图，宿主只标记原版API；预订继承行为不代表游戏寻路验证。</summary>
    private static void ToilGraphAndReservations()
    {
        var p = Pair(); p.target.Position = new IntVec3(90, 10);
        var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        Check(runner.Driver.TryMakePreToilReservations(true) && runner.Driver.BaseMakeToilsCalls == 0, "driver called base graph or reservations changed");
        Check(runner.Toils.Select(t => t.Kind).SequenceEqual(new[] { "Goto", "Jump", "Do", "CastVerb" }) && runner.Toils.Last().Progress != null, "wrong native toil order or missing progress");
        Check(p.caster.pather.LastMode == PathEndMode.OnCell && !p.ability.Casting && !runner.Driver.job.def.abilityCasting, "walking marked casting or touch path");
        runner.Cancel(); NoSettlement(p);
    }

    /// <summary>测试显式通知同格到达，随后逐tick执行原版边界，绝不以移动距离推导真实路径。</summary>
    private static void ApproachAndNativeWarmup()
    {
        var p = Pair(); p.target.Position = new IntVec3(180, 110); p.caster.Map.LineOfSight = false;
        var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        Tick(runner, 650);
        Check(!runner.Ended && !p.ability.Casting && p.target.stances.stunner.StunCalls == 0 && Verb(p.ability).BaseStartCalls == 0, "long moving approach timed out or started cast"); NoSettlement(p);
        runner.NotifyArrival();
        Check(runner.Current.Kind == "CastVerb" && p.caster.pather.StopDeadCalls == 1 && p.ability.Casting && p.target.stances.stunner.StunCalls == 1, "arrival did not stop and start native warmup once");
        Equal(0, runner.Current.Progress()); Tick(runner, 119); NoSettlement(p);
        Check(p.target.stances.stunner.Stunned && runner.Current.Progress() > .99f, "native warmup or progress boundary wrong");
        runner.Tick(); Check(BuffCount(p.target) == 1 && p.ability.CooldownStartCalls == 1 && p.ability.CooldownTicksRemaining == 60000 && !p.ability.Casting, "completion did not settle once");
        runner.Tick(); Check(p.caster.jobs.EndCondition == JobCondition.Succeeded && p.ability.CooldownStartCalls == 1, "FinishedBusy recounted cooldown or failed after success");
    }

    /// <summary>路径宿主保留Thing引用并读取当前位置，验证生产任务没有缓存选择时的格子。</summary>
    private static void MovingTargetAndArrivalRace()
    {
        var p = Pair(); p.target.Position = new IntVec3(8, 3);
        var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        p.target.Position = new IntVec3(12, 9);
        Check(ReferenceEquals(p.target, p.caster.pather.Destination) && p.caster.pather.DestinationCell == p.target.Position, "moving pawn replaced by initial cell");
        runner.NotifyArrival(() => p.target.Position = new IntVec3(13, 9));
        Check(runner.Current.Kind == "Goto" && p.caster.pather.StartPathCalls == 2 && p.target.stances.stunner.StunCalls == 0 && !p.ability.Casting, "arrival race did not jump back to approach");
        NoSettlement(p); runner.NotifyArrival(); Check(p.ability.Casting && p.target.stances.stunner.StunCalls == 1, "second actual arrival failed");
        runner.Cancel(); NoSettlement(p);
    }

    /// <summary>资格改变由实际approach.FailOn或归属谓词处理，不在宿主复制失败矩阵。</summary>
    private static void ApproachCancellation()
    {
        for (int scenario = 0; scenario < 11; scenario++)
        {
            Reset(); var p = Pair(); p.target.Position = new IntVec3(10, 10);
            var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
            if (scenario == 0) p.target.Conscious = false;
            if (scenario == 1) p.target.Spawned = false;
            if (scenario == 2) p.target.Faction = new Faction();
            if (scenario == 3) p.caster.Downed = true;
            if (scenario == 4) p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
            if (scenario == 5) { p.caster.abilities.RemoveAbility(p.ability.def); p.caster.abilities.GainAbility(p.ability.def); }
            if (scenario == 6) runner.Driver.job.verbToUse = new Verb_CastAbility { ability = p.ability, caster = p.caster };
            if (scenario == 7) p.caster.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            if (scenario == 8) p.ability.BaseAllowed = false;
            if (scenario == 9) p.target.Map = new Map();
            if (scenario == 10) p.target.needs = null;
            runner.Tick();
            Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Incompletable && p.target.stances.stunner.StunCalls == 0, "approach invalidation missed " + scenario); NoSettlement(p);
        }
    }

    /// <summary>取消时保留目标原版有限眩晕，包含更长外来源眩晕，不用测试替身自行取消目标动作。</summary>
    private static void WarmupCancellation()
    {
        for (int scenario = 0; scenario < 10; scenario++)
        {
            Reset(); var p = Pair(); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival();
            var stance = p.caster.stances.curStance as Stance_Warmup;
            p.target.stances.stunner.StunFor(500, new Pawn()); int stunEnd = p.target.stances.stunner.EndTick;
            if (scenario == 0) p.target.Position = new IntVec3(1, 0);
            if (scenario == 1) p.caster.Position = new IntVec3(0, 1);
            if (scenario == 2) p.target.Conscious = false;
            if (scenario == 3) p.target.Dead = true;
            if (scenario == 4) p.caster.Spawned = false;
            if (scenario == 5) p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
            if (scenario == 6) p.caster.MentalState = new MentalState { pawn = p.caster };
            if (scenario == 7) p.target.needs.mood = null;
            if (scenario == 8) p.target.Spawned = false;
            if (scenario == 9) { p.caster.abilities.RemoveAbility(p.ability.def); p.caster.abilities.GainAbility(p.ability.def); }
            runner.Tick();
            Check(runner.Ended && !Verb(p.ability).WarmingUp && stance.InterruptCalls == 1 && Verb(p.ability).ResetCalls == 1, "warmup invalidation missed cleanup " + scenario);
            Check(p.target.stances.stunner.EndTick == stunEnd && p.target.stances.stunner.ClearCalls == 0, "cleanup cleared foreign or native target stun"); NoSettlement(p);
            Find.TickManager.TicksGame = stunEnd; Verb(p.ability).TickNativeWarmup();
            Check(!p.target.stances.stunner.Stunned, "limited target stun did not expire"); NoSettlement(p);
        }
    }

    /// <summary>绕过任务tick单独执行原版Stance调用，防止只在Job层修同格而漏掉From版本。</summary>
    private static void NativeWarmupFromBoundary()
    {
        var p = Pair(); p.caster.Map.LineOfSight = false; var verb = Verb(p.ability);
        Check(verb.TryStartCastOn(p.target, default), "same-cell wall flag refused");
        p.target.Position = new IntVec3(1, 0); verb.TickNativeWarmup();
        Check(!verb.WarmingUp && verb.WarmupStance.InterruptCalls == 1, "native From check allowed adjacent target"); NoSettlement(p);
    }

    /// <summary>特意保留结束后一个FinishedBusy tick，成功后冷却或心情资格变化不得反过来判任务失败。</summary>
    private static void SuccessfulFinishedBusy()
    {
        foreach (bool mentalWithoutMood in new[] { false, true })
        {
            Reset(); var p = Pair(); MentalState state = null;
            if (mentalWithoutMood) { p.target.needs = null; state = Mental(p.target); }
            var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 120);
            Check(!runner.Ended && p.ability.CooldownTicksRemaining == 60000 && !p.ability.Casting && !runner.FailedNow, "successful activation invalidated pending FinishedBusy");
            if (mentalWithoutMood)
                Check(state.RecoverCalls == 1 && !PetCatAbilityUtility.CanTarget(p.caster, p.target, out _), "fixture did not become ineligible only after recovery");
            runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Succeeded && p.ability.CooldownStartCalls == 1, "successful job ended as failure or took second cooldown");
        }
        // 原版CastVerb不因TryStartCastOn=false主动结束；没有busy时允许自然空收尾。
        Reset(); var rejected = Pair(); Verb(rejected.ability).NativeStartAllowed = false;
        var rejectedRunner = new CastBoundaryRunner(rejected.caster, rejected.target, rejected.ability); rejectedRunner.NotifyArrival();
        Check(!rejectedRunner.Ended && Verb(rejected.ability).BaseStartCalls == 1 && !rejected.caster.FullBodyBusy &&
            rejected.target.stances.stunner.StunCalls == 0, "native rejected start created busy/stun or forced failure");
        rejectedRunner.Tick();
        Check(rejectedRunner.Ended && rejected.caster.jobs.EndCondition == JobCondition.Succeeded, "no-busy rejected start did not close FinishedBusy normally"); NoSettlement(rejected);

        // 引擎独立Interrupt或Reset之后也按busy状态收尾，不能等待实际施放标记。
        foreach (bool reset in new[] { false, true })
        {
            Reset(); var interrupted = Pair(); var runner = new CastBoundaryRunner(interrupted.caster, interrupted.target, interrupted.ability);
            runner.NotifyArrival(); Tick(runner, 20);
            if (reset) Verb(interrupted.ability).Reset();
            else Verb(interrupted.ability).WarmupStance.Interrupt();
            Check(!interrupted.caster.FullBodyBusy, "engine interrupt/reset left modeled stance busy");
            runner.Tick(); Check(runner.Ended && interrupted.caster.jobs.EndCondition == JobCondition.Succeeded, "engine interrupt/reset did not close FinishedBusy normally"); NoSettlement(interrupted);
            Verb(interrupted.ability).TickNativeWarmup(); NoSettlement(interrupted);
        }
    }

    /// <summary>逐tick结束边界只动态调用实际能力；旧精神实例及疾病标签不参与分支缓存。</summary>
    private static void WarmupLateBranches()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            Reset(); var p = Pair(); MentalState old = scenario > 0 ? Mental(p.target) : null;
            var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 119);
            MentalState current = null;
            if (scenario == 0 || scenario == 2) current = Mental(p.target);
            if (scenario == 1) p.target.MentalState = null;
            if (scenario == 2) current.OnRecover = () => p.target.MentalState = new MentalState { pawn = p.target };
            runner.Tick();
            Check(p.ability.ActivationCalls == 1 && p.ability.CooldownStartCalls == 1 && (old == null || old.RecoverCalls == 0), "warmup used old mental instance or repeated activation");
            Check(scenario == 1 ? BuffCount(p.target) == 1 : current.RecoverCalls == 1 && BuffCount(p.target) == 0, "late branch wrong");
            if (scenario == 2) Check(p.target.MentalState != current && p.target.MentalState.RecoverCalls == 0, "recovery callback new state handled twice");
            runner.Tick(); Check(p.caster.jobs.EndCondition == JobCondition.Succeeded, "latest-branch completion did not close normally");
        }
    }

    /// <summary>推进游戏tick运行实际重试委托，不复制计数规则；MovingNow来自明确引擎边界输入。</summary>
    private static void ApproachRetries()
    {
        var p = Pair(); p.target.Position = new IntVec3(100, 1); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        Find.TickManager.TicksGame = 100018; p.caster.pather.MovingNow = false; runner.Tick();
        Check(p.caster.pather.StartPathCalls == 1, "retry ignored 60tick interval");
        for (int i = 0; i < 10; i++)
        {
            p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100019 + (i + 1) * 60; runner.Tick();
            Check(!runner.Ended && p.caster.pather.StartPathCalls == i + 2, "retry count or timing wrong " + i);
        }
        p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100679; runner.Tick();
        Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Incompletable && p.caster.pather.StartPathCalls == 11, "11th idle interval did not end bounded retries"); NoSettlement(p);

        Reset(); p = Pair(); p.target.Position = new IntVec3(100, 1); runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        for (int i = 0; i < 10; i++) { p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100079 + i * 60; runner.Tick(); }
        p.caster.pather.MovingNow = true; runner.Tick();
        p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100679; runner.Tick();
        Check(!runner.Ended && p.caster.pather.StartPathCalls == 12, "normal movement did not reset retry budget");
        p.caster.pather.MovingNow = true; Tick(runner, 2000); Check(!runner.Ended, "long normal path acquired a time limit"); runner.Cancel(); NoSettlement(p);
    }

    /// <summary>选择可达性失效只在接近补寻路入口处理，读条不能重新发路或重复眩晕。</summary>
    private static void RetryAndCastIsolation()
    {
        var p = Pair(); p.target.Position = new IntVec3(9, 9); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        p.caster.Reachable = false; p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100079; runner.Tick();
        Check(runner.Ended && p.caster.pather.StartPathCalls == 1, "unreachable idle path restarted"); NoSettlement(p);
        Reset(); p = Pair(); runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 119);
        Check(p.caster.pather.StartPathCalls == 1 && p.target.stances.stunner.StunCalls == 1 && !p.caster.pather.MovingNow, "warmup retried approach or stunned again"); runner.Cancel(); NoSettlement(p);
    }

    /// <summary>只核对真实ExposeData调用的字段和默认值；不宣称字典宿主是游戏Scribe存读档。</summary>
    private static void RetrySerializationBoundary()
    {
        var p = Pair(); p.target.Position = new IntVec3(9, 9); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        for (int i = 0; i < 3; i++) { p.caster.pather.MovingNow = false; Find.TickManager.TicksGame = 100079 + i * 60; runner.Tick(); }
        Scribe.mode = LoadSaveMode.Saving; runner.Driver.ExposeData();
        Check(Scribe_Values.Values["sscCatComfortApproachIdleRetries"] == 3 && Scribe_Values.Values.Count == 1, "driver did not expose exact retry field");
        var restored = new JobDriver_PetCatComfort { pawn = p.caster, job = runner.Driver.job };
        int pathStarts = p.caster.pather.StartPathCalls;
        Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData(); Scribe.mode = LoadSaveMode.Saving; restored.ExposeData();
        Check(Scribe_Values.Values["sscCatComfortApproachIdleRetries"] == 3 && p.caster.pather.StartPathCalls == pathStarts && p.target.stances.stunner.StunCalls == 0, "loading field restarted path/warmup or lost count");
        Scribe_Values.Values.Clear(); Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData(); Scribe.mode = LoadSaveMode.Saving; restored.ExposeData();
        Check(Scribe_Values.Values["sscCatComfortApproachIdleRetries"] == 0, "old save missing retry field not default zero");
        Scribe.mode = LoadSaveMode.Inactive;
    }

    /// <summary>任务结束只对本人Verb及本人当前任务清理，角色离图仍需清本人效果。</summary>
    private static void CleanupOwnership()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            Reset(); var p = Pair(); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival();
            var ownStance = p.caster.stances.curStance as Stance_Warmup;
            var foreignVerb = new Verb { caster = p.caster, WarmingUp = true };
            var foreignStance = new Stance_Warmup { verb = foreignVerb };
            if (scenario == 0) p.caster.stances.curStance = foreignStance;
            if (scenario == 1) { p.caster.jobs.curDriver = new JobDriver_CastAbility(); p.caster.stances.curStance = foreignStance; }
            if (scenario == 2) p.caster.Spawned = false;
            runner.Driver.Finish(JobCondition.InterruptForced);
            Check(foreignStance.InterruptCalls == 0 && foreignVerb.WarmingUp && foreignVerb.ResetCalls == 0, "cleanup interrupted another verb/driver warmup");
            Check(scenario == 2 ? ownStance.InterruptCalls == 1 && Verb(p.ability).ResetCalls == 1 :
                ownStance.InterruptCalls == 0 && Verb(p.ability).ResetCalls == (scenario == 0 ? 1 : 0), "own cleanup ownership condition wrong");
            NoSettlement(p);
        }
    }

    /// <summary>替换Verb模拟原版VerbTracker升级边界；不复制生产owner补绑，也不宣称覆盖完整Scribe。</summary>
    private static void UpgradeVerbOwner()
    {
        foreach (var mode in new[] { LoadSaveMode.Inactive, LoadSaveMode.LoadingVars, LoadSaveMode.PostLoadInit })
        {
            Reset(); var p = Pair(); p.ability.StartCooldown(4321);
            var newVerb = new Verb_PetCatComfort { caster = p.caster, WarmingUp = true, WarmupRemaining = 63 };
            p.ability.verb = newVerb; var stance = new Stance_Warmup { verb = newVerb }; p.caster.stances.curStance = stance;
            Scribe.mode = mode; p.ability.ExposeData();
            Check((newVerb.ability == p.ability) == (mode == LoadSaveMode.PostLoadInit), "owner repaired at wrong load phase");
            Check(ReferenceEquals(p.ability, p.caster.abilities.GetAbility(p.ability.def)) && p.ability.CooldownTicksRemaining == 4321 &&
                ReferenceEquals(newVerb, p.ability.verb) && newVerb.WarmingUp && newVerb.WarmupRemaining == 63 &&
                ReferenceEquals(stance, p.caster.stances.curStance), "owner patch replaced skill, cooldown, verb or warmup");
        }
        Reset(); var blank = new Ability_PetCatComfort(); Scribe.mode = LoadSaveMode.PostLoadInit; blank.ExposeData();
        var originalVerb = new Verb(); blank.verb = originalVerb; blank.def = SSCDefOf.SSC_PetCatComfort; blank.ExposeData();
        Check(ReferenceEquals(originalVerb, blank.verb), "owner patch replaced nonability verb");
    }
}
