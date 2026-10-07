using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static int waitingId = 4000;
    /// <summary>测试给出原版WaitWith的目标任务输出，再执行真实包装；不调度或重写ForceWait。</summary>
    private static Job BeginAffection(JobDriver_PetAffection driver, Toil[] toils, Pawn master)
    {
        driver.pawn.jobs.curDriver = driver; driver.pawn.jobs.curJob = driver.job;
        Job nativeWait = new Job { def = JobDefOf.Wait, loadID = ++waitingId, expiryInterval = 121,
            startTick = Find.TickManager.TicksGame };
        Assert(!toils[1].FailureConditions.Any(condition => condition()), "pre-init predicate refused eligible idle master");
        Toils_General.NativeWaitWithInit = _ => master.jobs.curJob = nativeWait;
        toils[1].initAction();
        Toils_General.NativeWaitWithInit = null;
        Assert(driver.EndCondition == null && ReferenceEquals(master.CurJob, nativeWait), "interaction init refused native wait output");
        return nativeWait;
    }
    private static void RunAffectionVisibleCases()
    {
        Run("亲昵发现范围与实际接触分离，接近调度不提前发奖", AffectionApproachAndContact);
        Run("亲昵自动调度拒绝双方睡眠、精神状态、意识缺失及战斗", AffectionAwarenessBoundaries);
        Run("亲昵自动调度拒绝主人工作、玩家命令及双方任务队列", AffectionWorkProtection);
        Run("亲昵空闲白名单不把其他互动的姿势等待当作空闲", AffectionIdleWhitelist);
        Run("亲昵发现与完成继续要求当前资格、真实主人及不同对象", AffectionApproachQualification);
        Run("亲昵接近需要可达主人，结算需要即时Touch资格", AffectionReachability);
        Run("亲昵驱动使用GotoTouch、120tick进度与121tick主人等待及效果", AffectionVisibleToilWiring);
        Run("亲昵接近时主人变忙及到达前复查不会强行创建等待", AffectionMasterBecomesBusy);
        Run("亲昵接触丢失在初始化与等待中拒绝，不结算奖励", AffectionContactLoss);
        Run("亲昵取消只释放自己的等待，重复清理无副作用", AffectionCancellationCleanup);
        Run("亲昵清理保留主人后来接手的普通工作及外来等待", AffectionReplacementWaitSurvives);
        Run("亲昵等待认领拒绝非本次新建的原版等待输出", AffectionNativeWaitOutputGuards);
        Run("亲昵原版等待回调重入取消或替换时释放自己等待", AffectionWaitStartReentry);
        Run("亲昵完成只结算一次并立即清理主人等待", AffectionVisibleCompletion);
        Run("亲昵等待存档字段恢复归属，不再初始化或补发奖励", AffectionWaitSerialization);
        Run("旧亲昵存档缺少新增字段按未开始互动恢复", AffectionOldSaveDefaults);
        Run("亲昵旧driver回调不能结束新任务、发奖或改变主人朝向", AffectionStaleDriverCallbacks);
        Run("亲昵无效方向和身体在等待期间取消仍不消耗冷却", AffectionWaitingQualificationChanges);
        Run("亲昵缺失可见效果定义仍保持完整交互与结算", AffectionMissingEffectIsSafe);
        Run("亲昵读档后主人等待已被替换时拒绝结算且保留新任务", AffectionLoadedReplacementWait);
        Run("亲昵目标引用改变后仍清理原主人等待，不能清理新对象", AffectionTargetReferenceChange);
        Run("亲昵原版等待安装后异常不吞异常，清理仍释放精确等待", AffectionWaitStartException);
        Run("亲昵效果使用原版Heart与30tick喷射，四语任务报告及镜像一致", AffectionResourceContracts);
    }

    /// <summary>接触资格由测试提供引擎的即时可达返回；不在宿主复制寻路算法。</summary>
    private static void AffectionApproachAndContact()
    {
        var p = AffectionPair(Pets[0], .3f);
        p.pet.Position = new IntVec3(2, 0); p.pet.ImmediatelyReachable = false;
        Assert(PetSpecializationUtility.CanApproachPetAffectionNow(p.pet, p.master), "nearby master not discovered");
        Assert(!PetSpecializationUtility.CanDoPetAffectionNow(p.pet, p.master), "approach mistaken for contact");
        Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "approach job not submitted");
        Job request = p.pet.jobs.Requests.Single();
        Assert(request.GetTarget(TargetIndex.A).Pawn == p.master && request.expiryInterval >= 120, "invalid approach job target/expiry");
        Assert(!PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "out-of-touch completion accepted");
        Equal(.3f, p.pet.Training.specializationProgress);
        Assert(p.pet.Training.lastPetAffectionTick == -999999 && p.master.needs.mood.thoughts.memories.Requests.Count == 0,
            "approach consumed cooldown or emitted memory");
        p.pet.ImmediatelyReachable = true;
        Assert(PetSpecializationUtility.CanDoPetAffectionNow(p.pet, p.master), "native Touch return ignored");
        p.pet.Position = new IntVec3(3, 0);
        Assert(!PetSpecializationUtility.CanApproachPetAffectionNow(p.pet, p.master), "discovery range expanded");
    }

    /// <summary>身体条件双方对称，所有拒绝都必须保持奖励和冷却不变。</summary>
    private static void AffectionAwarenessBoundaries()
    {
        for (int side = 0; side < 2; side++)
        for (int condition = 0; condition < 6; condition++)
        {
            var p = AffectionPair(Pets[0]); Pawn subject = side == 0 ? p.pet : p.master;
            if (condition == 0) subject.AwakeNow = false;
            if (condition == 1) subject.InMentalState = true;
            if (condition == 2) subject.health.CanAwake = false;
            if (condition == 3) subject.Fighting = true;
            if (condition == 4) subject.health = null;
            if (condition == 5) subject.health.capacities = null;
            Assert(!PetSpecializationUtility.CanApproachPetAffectionNow(p.pet, p.master), "awareness accepted " + side + "/" + condition);
            Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "invalid body scheduled");
            Assert(!PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "invalid body rewarded");
            Equal(.3f, p.pet.Training.specializationProgress);
            Assert(p.pet.Training.lastPetAffectionTick == -999999 && p.master.needs.mood.thoughts.memories.Requests.Count == 0,
                "awareness failure emitted side effects");
        }
    }

    private static void AffectionWorkProtection()
    {
        for (int side = 0; side < 2; side++)
        for (int condition = 0; condition < 5; condition++)
        {
            var p = AffectionPair(); Pawn subject = side == 0 ? p.pet : p.master;
            Job existing = JobMaker.MakeJob(condition == 0 ? new JobDef { defName = "Build" } : JobDefOf.Wait, null);
            subject.jobs.curJob = existing;
            if (condition == 1) existing.playerForced = true;
            if (condition == 2) subject.jobs.jobQueue.Add(new QueuedJob { job = JobMaker.MakeJob(new JobDef { defName = "Carry" }, null) });
            if (condition == 3) subject.jobs.jobQueue.Add(new QueuedJob { job = JobMaker.MakeJob(JobDefOf.Wait, null) });
            if (condition == 4) existing.expiryInterval = 121;
            Assert(!PetSpecializationUtility.CanStartPetAffectionJobWithoutDisruptingWork(subject), "work boundary accepted");
            Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "work or command interrupted " + side + "/" + condition);
            Assert(ReferenceEquals(existing, subject.CurJob) && p.pet.jobs.Requests.Count == 0, "refusal modified existing job");
        }
    }

    private static void AffectionIdleWhitelist()
    {
        foreach (JobDef def in new[] { JobDefOf.Wait, JobDefOf.Wait_Wander, new JobDef { defName = "GotoWander" } })
        {
            var p = AffectionPair(); p.pet.CurJobDef = def; p.master.CurJobDef = def;
            Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "allowed idle rejected " + def.defName);
        }
        foreach (string name in new[] { "Wait_MaintainPosture", "Goto", "LayDown", "WaitCombat" })
        {
            var p = AffectionPair(); p.master.CurJobDef = new JobDef { defName = name };
            Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "nonidle accepted " + name);
        }
        var emptyQueue = AffectionPair(); emptyQueue.master.jobs.jobQueue.Add(new QueuedJob());
        Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(emptyQueue.pet), "empty pooled queue entry rejected");
        Assert(!PetSpecializationUtility.CanStartPetAffectionJobWithoutDisruptingWork(null), "null tracker accepted");
    }

    private static void AffectionApproachQualification()
    {
        for (int condition = 0; condition < 5; condition++)
        {
            var p = AffectionPair(Pets[0]);
            if (condition == 0) p.pet.BoundMaster = null;
            if (condition == 1) p.pet.BoundMaster = Pawn();
            if (condition == 2) { p.pet.BoundMaster = null; p.pet.AssignedTrainer = p.master; }
            if (condition == 3) p.pet.Training.SetSpecialization(SexSlaveSpecializationType.None);
            if (condition == 4) { p.pet.BoundMaster = p.pet; p.master = p.pet; }
            Assert(!PetSpecializationUtility.CanApproachPetAffectionNow(p.pet, p.master), "invalid bind/direction accepted " + condition);
        }
        var final = AffectionPair(Pets[0]); Final(final.pet, Pets[0]); final.pet.Training.SetSpecialization(SexSlaveSpecializationType.None);
        Assert(PetSpecializationUtility.CanApproachPetAffectionNow(final.pet, final.master), "independent final lost affection");
        Assert(!PetSpecializationUtility.CanApproachPetAffectionNow(null, final.master), "null pet accepted");
    }

    private static void AffectionReachability()
    {
        var p = AffectionPair(Pets[0]); p.pet.Reachable = false;
        Assert(PetSpecializationUtility.CanApproachPetAffectionNow(p.pet, p.master), "physical discovery depends on pathfinding");
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "unreachable master scheduled");
        p.pet.Reachable = true; p.pet.ImmediatelyReachable = false;
        Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "pathable approach rejected");
        Assert(!PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "wall/contact rejection bypassed");
    }

    private static (Pawn pet, Pawn master, JobDriver_PetAffection driver, Toil[] toils) VisiblePair(bool effect = true)
    {
        var p = AffectionPair(Pets[0]);
        DefDatabase<EffecterDef>.Definitions.Clear();
        if (effect) DefDatabase<EffecterDef>.Add(new EffecterDef { defName = "SSC_PetAffectionInteraction" });
        var driver = new JobDriver_PetAffection { pawn = p.pet, job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, p.master) };
        p.pet.jobs.curJob = driver.job; p.pet.jobs.curDriver = driver;
        return (p.pet, p.master, driver, driver.BuildToils().ToArray());
    }

    private static void AssertNoAffectionReward(Pawn pet, Pawn master, float expectedProgress = .3f)
    {
        Equal(expectedProgress, pet.Training.specializationProgress);
        Assert(pet.Training.lastPetAffectionTick == -999999 && master.needs.mood.thoughts.memories.Requests.Count == 0,
            "incomplete affection emitted reward/cooldown");
    }

    private static void AffectionVisibleToilWiring()
    {
        var p = VisiblePair();
        Assert(p.driver.TryMakePreToilReservations(false), "eligible master rejected before approach");
        Assert(p.toils.Length == 3 && p.toils[0].Kind == "Goto" && p.toils[0].Target == TargetIndex.A && p.toils[0].EndMode == PathEndMode.Touch,
            "approach not original GotoThing/Touch");
        Assert(p.toils[1].Kind == "WaitWith" && p.toils[1].duration == 120 && p.toils[1].NativeWaitTicks == 121 &&
            p.toils[1].EndMode == PathEndMode.Touch && p.toils[1].Progress && p.toils[1].Effect &&
            ((EffecterDef)p.toils[1].EffectDef).defName == "SSC_PetAffectionInteraction", "visible wait wiring wrong");
        Assert(p.toils[2].defaultCompleteMode == ToilCompleteMode.Instant, "completion not instantaneous");
        AssertNoAffectionReward(p.pet, p.master);
        int nativeTicks = 0; Toils_General.NativeWaitWithTick = (_, interval) => nativeTicks += interval;
        Job wait = BeginAffection(p.driver, p.toils, p.master);
        Assert(wait.expiryInterval == 121 && p.master.rotationTracker.LastTarget == p.pet, "master wait/facing wrong");
        for (int tick = 0; tick < 120; tick++)
        {
            p.toils[1].tickAction(); p.toils[1].tickIntervalAction(1);
            AssertNoAffectionReward(p.pet, p.master);
        }
        Assert(nativeTicks == 120, "native WaitWith tickInterval action lost");
        p.driver.NotifyFinish(JobCondition.InterruptForced);
    }

    private static void AffectionMasterBecomesBusy()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            var p = VisiblePair();
            if (scenario == 0) p.master.CurJobDef = new JobDef { defName = "Build" };
            if (scenario == 1) p.master.jobs.curJob = new Job { def = JobDefOf.Wait, playerForced = true };
            if (scenario == 2) p.master.jobs.jobQueue.Add(new QueuedJob { job = new Job { def = new JobDef { defName = "Carry" } } });
            Job existing = p.master.CurJob; int nativeCalls = 0;
            Toils_General.NativeWaitWithInit = _ => nativeCalls++;
            Assert(p.toils[0].FailureConditions.Any(f => f()), "approach ignored busy master");
            Assert(p.toils[1].FailureConditions.Any(f => f()), "pre-init ignored busy master");
            p.toils[1].initAction();
            Assert(p.driver.EndCondition == JobCondition.Incompletable && nativeCalls == 0 && ReferenceEquals(existing, p.master.CurJob),
                "arrival forcibly waited busy master");
            AssertNoAffectionReward(p.pet, p.master);
        }
    }

    private static void AffectionContactLoss()
    {
        var before = VisiblePair(); before.pet.ImmediatelyReachable = false;
        int calls = 0; Toils_General.NativeWaitWithInit = _ => calls++;
        before.toils[1].initAction();
        Assert(calls == 0 && before.driver.EndCondition == JobCondition.Incompletable, "no-contact init created wait");
        AssertNoAffectionReward(before.pet, before.master);
        var during = VisiblePair(); BeginAffection(during.driver, during.toils, during.master);
        during.pet.ImmediatelyReachable = false;
        Assert(during.toils[1].FailureConditions.Any(f => f()), "wait did not check contact");
        during.toils[2].initAction(); during.driver.NotifyFinish(JobCondition.Incompletable);
        AssertNoAffectionReward(during.pet, during.master);
        Assert(during.master.CurJob == null && during.master.jobs.EndCalls == 1, "lost-contact wait leaked");
    }

    private static void AffectionCancellationCleanup()
    {
        var p = VisiblePair(); BeginAffection(p.driver, p.toils, p.master);
        p.driver.NotifyFinish(JobCondition.InterruptForced);
        Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1 && p.master.jobs.EndCondition == JobCondition.InterruptForced,
            "cancel did not end owned wait");
        p.driver.NotifyFinish(JobCondition.InterruptForced); p.toils[2].initAction();
        Assert(p.master.jobs.EndCalls == 1, "repeated cleanup ended another job");
        AssertNoAffectionReward(p.pet, p.master);
    }

    private static void AffectionReplacementWaitSurvives()
    {
        foreach (JobDef def in new[] { JobDefOf.Wait, new JobDef { defName = "Build" } })
        {
            var p = VisiblePair(); Job original = BeginAffection(p.driver, p.toils, p.master);
            Job replacement = new Job { def = def, loadID = original.loadID + 1, expiryInterval = 121 };
            p.master.jobs.curJob = replacement;
            Assert(p.toils[1].FailureConditions.Any(f => f()), "replacement wait mistaken for own");
            p.toils[1].tickAction();
            p.driver.NotifyFinish(JobCondition.InterruptForced);
            Assert(ReferenceEquals(p.master.CurJob, replacement) && p.master.jobs.EndCalls == 0, "cleanup canceled replacement job");
            p.toils[2].initAction(); AssertNoAffectionReward(p.pet, p.master);
        }
    }

    private static void AffectionNativeWaitOutputGuards()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        {
            var p = VisiblePair(); Job previous = new Job { def = JobDefOf.Wait }; p.master.jobs.curJob = previous;
            Job output = new Job { def = JobDefOf.Wait, loadID = ++waitingId, expiryInterval = 121, startTick = Find.TickManager.TicksGame };
            if (scenario == 0) output = previous;
            if (scenario == 1) output = null;
            if (scenario == 2) output.def = new JobDef { defName = "Other" };
            if (scenario == 3) output.expiryInterval = 120;
            if (scenario == 4) output.playerForced = true;
            if (scenario == 5) output.startTick--;
            if (scenario == 6) output.def = null;
            Toils_General.NativeWaitWithInit = _ => p.master.jobs.curJob = output;
            p.toils[1].initAction();
            Assert(p.driver.EndCondition == JobCondition.Incompletable, "invalid native wait output accepted " + scenario);
            p.driver.NotifyFinish(JobCondition.Incompletable);
            Assert(ReferenceEquals(p.master.CurJob, output) && p.master.jobs.EndCalls == 0, "invalid foreign output cleared " + scenario);
            AssertNoAffectionReward(p.pet, p.master);
        }
    }

    private static void AffectionWaitStartReentry()
    {
        for (int scenario = 0; scenario < 2; scenario++)
        {
            var p = VisiblePair(); Job newActorJob = new Job { def = new JobDef { defName = "Build" } };
            Job wait = new Job { def = JobDefOf.Wait, loadID = ++waitingId, expiryInterval = 121, startTick = Find.TickManager.TicksGame };
            Toils_General.NativeWaitWithInit = _ =>
            {
                p.master.jobs.curJob = wait;
                p.driver.NotifyFinish(JobCondition.InterruptForced);
                if (scenario == 0) p.driver.ended = true;
                else { p.pet.jobs.curJob = newActorJob; p.pet.jobs.curDriver = null; }
            };
            p.toils[1].initAction();
            Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "reentrant init leaked own wait");
            if (scenario == 1) Assert(ReferenceEquals(p.pet.CurJob, newActorJob), "old init ended replacement actor job");
            AssertNoAffectionReward(p.pet, p.master);
        }
    }

    private static void AffectionVisibleCompletion()
    {
        var p = VisiblePair(); p.toils[2].initAction(); AssertNoAffectionReward(p.pet, p.master);
        // 用新的当前driver开始正式互动；之前的强行finish应当只拒绝，不能提前领奖。
        p = VisiblePair(); BeginAffection(p.driver, p.toils, p.master);
        bool reentered = true;
        p.master.needs.mood.thoughts.memories.OnNextGainMemory = () => reentered = PetSpecializationUtility.CompletePetAffection(p.pet, p.master);
        p.toils[2].initAction();
        Equal(.31f, p.pet.Training.specializationProgress);
        Assert(!reentered && p.master.needs.mood.thoughts.memories.Requests.Count == 1 && p.pet.Training.lastPetAffectionTick == Find.TickManager.TicksGame,
            "completion/reentry duplicate or missing reward");
        Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "success left master waiting");
        p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Succeeded);
        Equal(.31f, p.pet.Training.specializationProgress);
        Assert(p.master.jobs.EndCalls == 1 && p.master.needs.mood.thoughts.memories.Requests.Count == 1, "completion repeated side effects");
    }

    private static void AffectionWaitSerialization()
    {
        var p = VisiblePair(); Job wait = BeginAffection(p.driver, p.toils, p.master);
        Scribe.mode = LoadSaveMode.Saving; p.driver.ExposeData();
        Assert(ReferenceEquals(Scribe.Values["sscAffectionWaitingMaster"], p.master) &&
            (int)Scribe.Values["sscAffectionWaitingJobId"] == wait.loadID && (bool)Scribe.Values["sscAffectionInteractionStarted"], "waiting save contract missing");
        var loaded = new JobDriver_PetAffection { pawn = p.pet, job = p.driver.job };
        Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        p.pet.jobs.curDriver = loaded; var toils = loaded.BuildToils().ToArray();
        Assert(!toils[1].FailureConditions.Any(f => f()), "loaded owned wait lost ownership");
        int nativeCalls = 0; Toils_General.NativeWaitWithInit = _ => nativeCalls++;
        // 原版负责恢复当前toil而不重跑init；这里显式只执行恢复后的tick/finish。
        toils[1].tickAction(); Assert(nativeCalls == 0 && ReferenceEquals(wait, p.master.CurJob), "restoration regenerated wait");
        AssertNoAffectionReward(p.pet, p.master);
        toils[2].initAction(); Equal(.31f, p.pet.Training.specializationProgress);
        Assert(nativeCalls == 0 && p.master.jobs.EndCalls == 1, "restored finish leaked/recreated wait");
    }

    private static void AffectionOldSaveDefaults()
    {
        var p = VisiblePair(); Scribe.Values.Clear(); Scribe.mode = LoadSaveMode.LoadingVars; p.driver.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        Assert(!p.toils[1].FailureConditions.Any(f => f()), "missing old fields made idle pre-init invalid");
        BeginAffection(p.driver, p.toils, p.master); p.driver.NotifyFinish(JobCondition.InterruptForced);
        AssertNoAffectionReward(p.pet, p.master);
    }

    private static void AffectionStaleDriverCallbacks()
    {
        var p = VisiblePair(); BeginAffection(p.driver, p.toils, p.master);
        Job replacement = new Job { def = new JobDef { defName = "Carry" } };
        p.pet.jobs.curJob = replacement; p.pet.jobs.curDriver = null;
        p.master.rotationTracker.LastTarget = null;
        p.toils[1].tickAction(); p.toils[2].initAction(); p.toils[1].initAction();
        AssertNoAffectionReward(p.pet, p.master);
        Assert(p.master.rotationTracker.LastTarget == null && ReferenceEquals(p.pet.CurJob, replacement) && p.driver.EndCondition == null,
            "stale callback interfered with replacement");
        p.driver.NotifyFinish(JobCondition.InterruptForced);
        Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "stale driver cleanup did not release its still-owned wait");
    }

    private static void AffectionWaitingQualificationChanges()
    {
        for (int scenario = 0; scenario < 8; scenario++)
        {
            var p = VisiblePair(); BeginAffection(p.driver, p.toils, p.master);
            if (scenario == 0) p.pet.BoundMaster = Pawn();
            if (scenario == 1) p.pet.Training.SetSpecialization(SexSlaveSpecializationType.None);
            if (scenario == 2) p.master.AwakeNow = false;
            if (scenario == 3) p.pet.InMentalState = true;
            if (scenario == 4) p.master.health.CanAwake = false;
            if (scenario == 5) p.master.Fighting = true;
            if (scenario == 6) p.master.Drafted = true;
            if (scenario == 7) p.pet.Position = new IntVec3(3, 0);
            Assert(p.driver.FailureConditions.Any(f => f()) || p.toils[1].FailureConditions.Any(f => f()), "waiting qualifications not rechecked " + scenario);
            p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Incompletable);
            AssertNoAffectionReward(p.pet, p.master, scenario == 1 ? 0 : .3f);
            Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "qualified failure leaked waiting master");
        }
    }

    private static void AffectionMissingEffectIsSafe()
    {
        var p = VisiblePair(false); Assert(!p.toils[1].Effect, "missing effect manufactured");
        BeginAffection(p.driver, p.toils, p.master); p.toils[2].initAction();
        Equal(.31f, p.pet.Training.specializationProgress);
        Assert(p.master.CurJob == null && p.master.needs.mood.thoughts.memories.Requests.Count == 1, "missing effect prevented ordinary completion");
    }

    private static void AffectionLoadedReplacementWait()
    {
        var p = VisiblePair(); Job oldWait = BeginAffection(p.driver, p.toils, p.master);
        Scribe.mode = LoadSaveMode.Saving; p.driver.ExposeData();
        var loaded = new JobDriver_PetAffection { pawn = p.pet, job = p.driver.job };
        Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        p.pet.jobs.curDriver = loaded;
        Job replacement = new Job { def = JobDefOf.Wait, loadID = oldWait.loadID + 1, expiryInterval = 121 };
        p.master.jobs.curJob = replacement;
        var toils = loaded.BuildToils().ToArray();
        Assert(toils[1].FailureConditions.Any(f => f()), "loaded driver accepted replacement owner");
        toils[2].initAction(); loaded.NotifyFinish(JobCondition.Incompletable);
        AssertNoAffectionReward(p.pet, p.master);
        Assert(ReferenceEquals(p.master.CurJob, replacement) && p.master.jobs.EndCalls == 0, "loaded cleanup canceled replacement");
    }

    private static void AffectionTargetReferenceChange()
    {
        var p = VisiblePair(); BeginAffection(p.driver, p.toils, p.master);
        Pawn another = Pawn(); Job unrelatedWait = new Job { def = JobDefOf.Wait, loadID = ++waitingId, expiryInterval = 121 };
        another.jobs.curJob = unrelatedWait; p.driver.job.target = another;
        p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Incompletable);
        AssertNoAffectionReward(p.pet, p.master);
        Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "original target wait leaked");
        Assert(ReferenceEquals(another.CurJob, unrelatedWait) && another.jobs.EndCalls == 0, "changed target wait canceled");
    }

    private static void AffectionWaitStartException()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            var p = VisiblePair();
            Job wait = new Job { def = JobDefOf.Wait, loadID = ++waitingId, expiryInterval = 121, startTick = Find.TickManager.TicksGame };
            Job replacement = new Job { def = new JobDef { defName = "Build" } };
            var original = new InvalidOperationException("native waiting callback failure");
            Toils_General.NativeWaitWithInit = _ =>
            {
                if (scenario != 0) p.master.jobs.curJob = wait;
                if (scenario == 2) { p.pet.jobs.curJob = replacement; p.pet.jobs.curDriver = null; }
                throw original;
            };
            Exception observed = null;
            try { p.toils[1].initAction(); } catch (Exception error) { observed = error; }
            Assert(ReferenceEquals(observed, original), "native exception swallowed/replaced");
            p.driver.NotifyFinish(JobCondition.Incompletable);
            Assert(p.master.CurJob == null && p.master.jobs.EndCalls == (scenario == 0 ? 0 : 1), "exception cleanup leaked or duplicated wait");
            if (scenario == 2) Assert(ReferenceEquals(p.pet.CurJob, replacement), "exception cleanup affected replacement actor job");
            AssertNoAffectionReward(p.pet, p.master);
        }
    }

    private static void AffectionResourceContracts()
    {
        DirectoryInfo location = new DirectoryInfo(AppContext.BaseDirectory);
        while (location != null && !File.Exists(Path.Combine(location.FullName, "Sexslavecraft/SexSlaveCraft_Alpha.csproj"))) location = location.Parent;
        Assert(location != null, "repository root unavailable");
        string root = location.FullName;
        XElement effect = XDocument.Load(Path.Combine(root, "Defs/EffecterDefs/SSC_PetAffectionEffects.xml")).Root.Elements()
            .Single(e => (string)e.Element("defName") == "SSC_PetAffectionInteraction");
        XElement child = effect.Element("children").Elements("li").Single();
        Assert((string)child.Element("subEffecterClass") == "SubEffecter_SprayerContinuous" &&
            (string)child.Element("fleckDef") == "Heart" && (string)child.Element("spawnLocType") == "OnTarget" &&
            (int)child.Element("ticksBetweenMotes") == 30 && (int)child.Element("initialDelayTicks") == 0 &&
            (int)child.Element("maxMoteCount") == 5 && (bool)child.Element("makeMoteOnSubtrigger") == false,
            "continuous Core Heart timing/spawn contract incorrect");
        XElement job = XDocument.Load(Path.Combine(root, "Defs/JobDefs/SSC_PetAffectionJobDefs.xml")).Root.Elements().Single();
        Assert((string)job.Element("defName") == "SSC_Job_PetAffection" &&
            (string)job.Element("driverClass") == typeof(JobDriver_PetAffection).FullName &&
            (bool)job.Element("casualInterruptible") && !string.IsNullOrWhiteSpace((string)job.Element("reportString")),
            "affection job driver/report/interruptibility contract incorrect");
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            string relative = "Languages/" + language + "/DefInjected/JobDef/SSC_PetAffectionJobDefs.xml";
            byte[] main = File.ReadAllBytes(Path.Combine(root, relative));
            Assert(main.SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Sexslavecraft", relative))), "job mirror differs " + language);
            XElement text = XDocument.Load(Path.Combine(root, relative)).Root.Element("SSC_Job_PetAffection.reportString");
            Assert(text != null && !string.IsNullOrWhiteSpace(text.Value) && text.Value.Contains("TargetA"), "localized job report missing target " + language);
        }
    }
}
