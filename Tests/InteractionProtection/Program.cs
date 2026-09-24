using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static int passed, failed;

    /// <summary>按编译模式安装真实 Harmony 补丁或使用直接调用，运行全部保护场景并返回测试退出码。</summary>
    private static int Main()
    {
#if REAL_HARMONY
        new Harmony("ssc.tests.interaction-protection").PatchAll(typeof(Program).Assembly);
        Console.WriteLine("模式：真实 Harmony PatchAll + 最小游戏生命周期模型");
#else
        Console.WriteLine("模式：生产补丁/策略 + 最小游戏生命周期模型");
#endif
        Run("补丁类型与目标方法可被 Harmony 发现", PatchRegistration);
        Run("C 单独发起在创建接收任务前被拒绝，无预约黄字", () => BlockedFirst(false));
        Run("玩家强制指派 C 同样在步骤前被拒绝", () => BlockedFirst(true));
        Run("A+B 已开始时拒绝 C，保留原任务和参与者", () => BlockedJoin(false));
        Run("强制指派 C 加入也不能绕过保护", () => BlockedJoin(true));
        Run("行走期间主人变更，创建接收任务前重新检查", OwnerChangesWhileWalking);
        Run("接收任务已存在但 C 尚未 Start，拒绝前不进入行为步骤", RejectBeforeSceneToil);
        Run("接收任务提前登记参与者不等于 Start 已执行", ReceiverPreparationIsNotStarted);
        Run("主人 A 正常开始、收尾和结算", OwnerCanComplete);
        Run("关闭总保护开关后允许 C", () => AllowedSetting(s => s.enableSexSlaveProtectionRules = false));
        Run("接受非主人强制许可开启后允许 C", () => { var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true; var d = Driver(p.c, p.b); Begin(d); Assert(d.StartCalls == 1, "个体许可应生效"); });
        Run("允许非主人时防护装备仍有效", ProtectionGear);
        Run("自愿行为主人限制关闭不放行强制行为", DerivedRapeDriver);
        Run("泛型发起者加入强制接收任务仍识别为强制行为", RapedReceiverClassification);
        Run("普通训练的主人仍可开始", () => Training(false, false, true));
        Run("获准由他人训练的目标仍可开始", () => Training(true, true, true));
        Run("未授权他人训练仍被拒绝", () => Training(true, false, false));
        Run("公交车被动条目强制生效", BusReceiver);
        Run("被绑定发起者的强制行为保护保持有效", ChainedInitiator);
        Run("LifeForce任务名不再绕过统一限制", Whitelist);
        Run("缺少参与者时保留安全放行", MissingTarget);
        Run("直接 Start 兜底阻止 RJW 原方法及原生 End", DirectStartFallback);
        Run("迟到 Start 回调不能结束该角色的新任务", StaleStartDoesNotEndNewJob);
        Run("已开始场景的收尾不因途中修改设置被拦截", FinishStartedScene);
        Run("读档恢复的已开始场景仍可收尾", ResumeStartedScene);
        Run("独立接收任务仍保留预约保护", ReceiverReservation);
        Run("无关 JobDriver 不受步骤补丁影响", UnrelatedJob);
        RunStage3ATests();
        RunStage3BTests();
        RunStage3CTests();
        RunStage3DTests();
        RunJobRefactorTests();
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>重置本用例的设置与提示计数，执行测试并记录结果；单例失败不会阻止后续用例运行。</summary>
    private static void Run(string name, Action test)
    {
        SSCMod.settings = new Settings();
        Messages.Count = 0;
        Scribe.mode = LoadSaveMode.Inactive;
        Scribe.node.Clear();
        DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Clear();
        DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Add(new SSCRestrictionProfileDef
        {
            defName = "Bus", specialization = SexSlaveSpecializationType.Bus,
            forced = new SSCRestrictionOverrides { receiveConsensual = SSCRestrictionValue.Allow, receiveForced = SSCRestrictionValue.Allow }
        });
        try { test(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception error) { failed++; Console.WriteLine("失败：" + name + "\n" + error); }
    }
    /// <summary>条件不满足时抛出带原因的异常，交由用例执行器汇总为失败。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    /// <summary>创建独立的 A（主人）、B（绑定目标）、C（非主人）测试数据，避免跨用例共享角色状态。</summary>
    private static (Pawn a, Pawn b, Pawn c) People()
    {
        var a = new Pawn { LabelShort = "A", thingIDNumber = 1 };
        var b = new Pawn { LabelShort = "B", thingIDNumber = 2, Chain = new Hediff_ChainOfSexSlave { LinkedPawn = a } };
        var c = new Pawn { LabelShort = "C", thingIDNumber = 3 };
        return (a, b, c);
    }
    /// <summary>创建并挂接指定类型的发起驱动，准备模拟步骤，并确认其覆盖的预约方法不会调用基类保护。</summary>
    private static JobDriver_SexBaseInitiator Driver(Pawn actor, Pawn target, bool rape = true, bool forced = false)
    {
        JobDriver_SexBaseInitiator driver = rape ? new JobDriver_Rape() : new JobDriver_SexBaseInitiator();
        driver.pawn = actor;
        driver.job = new Job
        {
            def = rape ? SSCDefOf.RapeComfortPawn : new JobDef { defName = "OrdinaryPair" },
            targetA = new LocalTargetInfo { Thing = target }, playerForced = forced
        };
        actor.jobs.curDriver = driver;
        driver.job.def.driverClass = driver.GetType();
        driver.MakeScenarioToils();

        return driver;
    }
    /// <summary>依次推进模拟的行走、接收准备和场景开始步骤；一旦任务结束就停止推进。</summary>
    private static void Begin(JobDriver_SexBaseInitiator driver)
    {
        for (int i = 0; i < 3 && !driver.Ended; i++) driver.TryActuallyStartNextToil();
    }
    /// <summary>检查任务被拒绝且没有行为初始化、原生收尾、收益或预约黄字，并且没有立即重选 AI 任务。</summary>
    /// <param name="beforeToil">为 true 时额外要求未触发外部动画收尾；直接调用 Start 的探针不作此保证。</param>
    private static void Rejected(JobDriver_SexBaseInitiator driver, bool beforeToil = true)
    {
        Assert(driver.Ended && driver.EndCondition == JobCondition.Incompletable, "应只使被拒绝的发起任务失败");
        Assert(driver.StartCalls == 0 && driver.TargetEffects == 0 && driver.AfterStartCalls == 0,
            "不得进入行为初始化、Start 或其后续副作用");
        Assert(driver.SceneEndCalls == 0 && driver.CompletedEffects == 0,
            "未开始场景不得调用 RJW 原生 End 或成功结算");
        if (beforeToil) Assert(driver.ExternalEndCalls == 0, "正常拒绝路径不得触发外部动画收尾前缀");
        Assert(driver.ReservationWarnings == 0, "不能靠新建接收任务预约失败来阻止行为");
        Assert(driver.pawn.jobs.ImmediateJobSearches == 0, "拒绝不能在同一调用栈立即重新选择 AI 任务");
        Assert(Messages.Count == (driver.job.playerForced ? 1 : 0), "玩家命令提示一次，AI 不刷屏");
    }
    /// <summary>核对前后缀特性与目标方法；真实 Harmony 模式进一步检查 PatchAll 实际安装的补丁信息。</summary>
    private static void PatchRegistration()
    {
        Type patch = typeof(Patch_JobDriver_Sex_ProtectChainOfSexSlave);
        foreach (string method in new[] { "NextToil_Prefix", "Start_Prefix", "End_Prefix" })
            Assert(patch.GetMethod(method).IsDefined(typeof(HarmonyPrefix)), method + " 必须显式声明 HarmonyPrefix");
        Assert(patch.GetMethod("Start_Postfix").IsDefined(typeof(HarmonyPostfix)), "成功标记必须声明 HarmonyPostfix");
#if REAL_HARMONY
        foreach (var entry in new[]
        {
            (typeof(JobDriver), "TryActuallyStartNextToil", "NextToil_Prefix"),
            (typeof(JobDriver_SexBaseInitiator), "Start", "Start_Prefix"),
            (typeof(JobDriver_SexBaseInitiator), "End", "End_Prefix"),
            (typeof(JobDriver_Sex), "TryMakePreToilReservations", "Prefix")
        })
        {
            var info = Harmony.GetPatchInfo(entry.Item1.GetMethod(entry.Item2));
            Assert(info != null && info.Prefixes.Any(p => p.PatchMethod == patch.GetMethod(entry.Item3)),
                entry.Item2 + " 必须经真实 PatchAll 注册正确前缀");
        }
        Assert(Harmony.GetPatchInfo(typeof(JobDriver_SexBaseInitiator).GetMethod("Start"))
            .Postfixes.Any(p => p.PatchMethod == patch.GetMethod("Start_Postfix")), "Start 后缀必须注册");
#else
        foreach (var entry in new[]
        {
            ("NextToil_Prefix", typeof(JobDriver), "TryActuallyStartNextToil"),
            ("Start_Prefix", typeof(JobDriver_SexBaseInitiator), "Start"),
            ("Start_Postfix", typeof(JobDriver_SexBaseInitiator), "Start"),
            ("End_Prefix", typeof(JobDriver_SexBaseInitiator), "End")
        })
        {
            var target = patch.GetMethod(entry.Item1).GetCustomAttribute<HarmonyPatch>();
            Assert(target?.Type == entry.Item2 && target.Method == entry.Item3, entry.Item1 + " 目标方法错误");
        }
#endif
    }
    /// <summary>验证 C 首次发起时在创建接收任务之前被拒绝，且 B 的普通任务保持不变。</summary>
    private static void BlockedFirst(bool forced)
    {
        var p = People();
        var previous = new JobDriver { pawn = p.b, job = new Job() };
        p.b.jobs.curDriver = previous;
        var driver = Driver(p.c, p.b, forced: forced);
        Begin(driver);
        Rejected(driver);
        Assert(driver.ReceiverCreations == 0 && p.b.jobs.curDriver == previous && !previous.Ended,
            "B 的原任务不得被替换或终止");
    }
    /// <summary>先让 A、B 开始，再验证 C 加入会被拒绝，原双方任务和仅含 A 的参与者名单不受影响。</summary>
    private static void BlockedJoin(bool forced)
    {
        var p = People();
        var owner = Driver(p.a, p.b);
        Begin(owner);
        var receiver = (JobDriver_SexBaseReciever)p.b.jobs.curDriver;
        var driver = Driver(p.c, p.b, forced: forced);
        Begin(driver);
        Rejected(driver);
        Assert(p.a.jobs.curDriver == owner && !owner.Ended && p.b.jobs.curDriver == receiver && !receiver.Ended,
            "A 与 B 的原任务必须继续");
        Assert(receiver.parteners.SequenceEqual(new[] { p.a }), "B 的参与者仍只能包含 A");
    }
    /// <summary>在行走期间更换 B 的主人，验证旧主人到达后会被重新检查并拒绝创建接收任务。</summary>
    private static void OwnerChangesWhileWalking()
    {
        var p = People();
        var driver = Driver(p.a, p.b);
        driver.TryActuallyStartNextToil();
        p.b.Chain.LinkedPawn = p.c;
        driver.TryActuallyStartNextToil();
        Rejected(driver);
        Assert(driver.ReceiverCreations == 0, "行走后应在接收任务创建之前重查");
    }
    /// <summary>让 C 完成加入准备后关闭非主人许可，验证进入场景步骤前仍会拒绝，保留 A、B 的行为。</summary>
    private static void RejectBeforeSceneToil()
    {
        var p = People();
        var owner = Driver(p.a, p.b);
        Begin(owner);
        p.b.Training.restrictionConfig.rules.receiveForced = true;
        var driver = Driver(p.c, p.b);
        driver.TryActuallyStartNextToil();
        driver.TryActuallyStartNextToil();
        p.b.Training.restrictionConfig.rules.receiveForced = false;
        driver.TryActuallyStartNextToil();
        Rejected(driver);
        Assert(((JobDriver_SexBaseReciever)p.b.jobs.curDriver).parteners.SequenceEqual(new[] { p.a }),
            "拒绝前不得进入带有 End 收尾的行为步骤");
    }
    /// <summary>验证主人能够正常开始、执行一次原生收尾并结算收益，最终以成功条件结束任务。</summary>
    private static void OwnerCanComplete()
    {
        var p = People();
        var driver = Driver(p.a, p.b);
        Begin(driver);
        Assert(driver.StartCalls == 1 && driver.AfterStartCalls == 1, "主人必须正常开始");
        driver.TryActuallyStartNextToil();
        driver.TryActuallyStartNextToil();
        Assert(driver.SceneEndCalls == 1 && driver.CompletedEffects == 1 && driver.EndCondition == JobCondition.Succeeded,
            "正常场景必须按原流程收尾与结算");
    }
    /// <summary>验证接收方准备时提前登记 C 不会被误认为 Start 已执行，许可收紧后仍在场景前拒绝。</summary>
    private static void ReceiverPreparationIsNotStarted()
    {
        var p = People();
        p.b.Training.restrictionConfig.rules.receiveForced = true;
        var driver = Driver(p.c, p.b);
        driver.TryActuallyStartNextToil();
        driver.TryActuallyStartNextToil();
        Assert(((JobDriver_SexBaseReciever)p.b.jobs.curDriver).parteners.Contains(p.c),
            "接收方准备步骤应已预先登记 C");
        p.b.Training.restrictionConfig.rules.receiveForced = false;
        driver.TryActuallyStartNextToil();
        Rejected(driver);
    }
    /// <summary>应用指定放行设置并验证 C 可正常开始，且不产生保护提示或接收方预约黄字。</summary>
    private static void AllowedSetting(Action<Settings> configure)
    {
        var p = People();
        configure(SSCMod.settings);
        var driver = Driver(p.c, p.b);
        Begin(driver);
        Assert(driver.StartCalls == 1 && driver.ReservationWarnings == 0 && Messages.Count == 0, "设置应允许开始");
    }
    /// <summary>验证允许非主人设置开启时，目标身上的防护组件仍能阻止该行为。</summary>
    private static void ProtectionGear()
    {
        var p = People();
        p.b.Training.restrictionConfig.rules.receiveForced = true;
        Equip(p.b);
        var driver = Driver(p.c, p.b);
        Begin(driver);
        Rejected(driver);
    }
    /// <summary>使用非白名单任务名的强制行为驱动，验证不会被当作已放宽限制的自愿行为。</summary>
    private static void DerivedRapeDriver()
    {
        var p = People();
        SSCMod.settings.protectNonRapeOwnerOnly = false;
        var driver = Driver(p.c, p.b);
        driver.job.def = new JobDef { defName = "ExternalDerivedRapeJob" };
        Begin(driver);
        Rejected(driver);
    }
    /// <summary>验证普通发起驱动加入强制接收任务时，仍按强制行为执行主从保护。</summary>
    private static void RapedReceiverClassification()
    {
        var p = People();
        Begin(Driver(p.a, p.b));
        SSCMod.settings.protectNonRapeOwnerOnly = false;
        var driver = Driver(p.c, p.b, rape: false);
        Begin(driver);
        Rejected(driver);
    }
    /// <summary>组合发起者身份与他人训练许可，验证自愿训练按预期放行或拒绝。</summary>
    private static void Training(bool nonOwner, bool allowOthers, bool expected)
    {
        var p = People();
        p.b.Training.restrictionConfig.rules.receiveTraining = allowOthers;
        Pawn actor = nonOwner ? p.c : p.a;
        actor.Training.pawnIdentity = PawnIdentity.Master;
        p.b.Training.selectedTrainer = actor;
        var driver = new JobDriver_Training { pawn = actor, job = new Job { def = new JobDef { defName = "SSC_Training_SexSlave" }, targetA = new LocalTargetInfo { Thing = p.b } } };
        actor.jobs.curDriver = driver;
        driver.MakeScenarioToils();
        Begin(driver);
        if (!expected) Rejected(driver);
        else Assert(driver.StartCalls == 1 && driver.ReservationWarnings == 0, "合法自愿训练必须保持放行");
    }
    /// <summary>验证目标具有公交车状态时，既有策略例外仍允许非主人发起。</summary>
    private static void BusReceiver()
    {
        var p = People();
        p.b.IsBus = true;
        var driver = Driver(p.c, p.b);
        Begin(driver);
        Assert(driver.StartCalls == 1, "公交车强制条目应允许接收");
    }
    /// <summary>验证带锁链的角色不能通过发起驱动绕过其主动强制行为限制。</summary>
    private static void ChainedInitiator()
    {
        var p = People();
        p.c.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = p.a };
        var driver = Driver(p.c, p.a);
        Begin(driver);
        Rejected(driver);
    }
    /// <summary>直接调用白名单任务的 Start，验证原有兼容放行规则仍然生效。</summary>
    private static void Whitelist()
    {
        var p = People();
        var driver = Driver(p.c, p.b);
        driver.job.def.defName = "rjw_genes_lifeforce_randomrape";
        Begin(driver);
        Rejected(driver);
    }
    /// <summary>验证目标缺失时保护适配层不会自行拒绝，继续交由原任务的有效性检查处理。</summary>
    private static void MissingTarget()
    {
        var p = People();
        var driver = Driver(p.c, null);
        driver.TryActuallyStartNextToil();
        Assert(!driver.Ended && driver.Index == 0, "空目标交由原 Job 的有效性检查处理");
    }
    /// <summary>模拟绕过步骤检查直接到达 Start，验证原生初始化和 End 被阻止，B 的原参与者保持不变。</summary>
    /// <remarks>本探针只验证 SSC/RJW 原生兜底，不断言外部补丁回调或调用方后续语句均被停止。</remarks>
    private static void DirectStartFallback()
    {
        var p = People();
        Begin(Driver(p.a, p.b));
        var receiver = (JobDriver_SexBaseReciever)p.b.jobs.curDriver;
        var driver = Driver(p.c, p.b);
        driver.Index = 2; // simulate a caller that reached Start without the earlier transition check
        driver.Start();
        // Harmony still invokes other mods' callbacks when an original is skipped.
        // This direct-call probe tests the core fallback; the actual game scenarios
        // above must reject before entering Start/End, including their external hooks.
        Rejected(driver, beforeToil: false);
        Assert(receiver.parteners.SequenceEqual(new[] { p.a }) && !receiver.Ended,
            "Start 兜底的清理不能操作 A+B 场景");
    }
    /// <summary>让旧驱动在角色已换任务后调用 Start，验证拒绝旧场景不会结束该角色的新任务。</summary>
    private static void StaleStartDoesNotEndNewJob()
    {
        var p = People();
        var stale = Driver(p.c, p.b);
        var newer = new JobDriver { pawn = p.c, job = new Job() };
        p.c.jobs.curDriver = newer;
        stale.Start();
        Assert(stale.StartCalls == 0 && p.c.jobs.curDriver == newer && !newer.Ended, "只能终止原驱动的任务");
    }
    /// <summary>验证合法开始后收紧设置不会阻止现有场景执行正常收尾和结算。</summary>
    private static void FinishStartedScene()
    {
        var p = People();
        p.b.Training.restrictionConfig.rules.receiveForced = true;
        var driver = Driver(p.c, p.b);
        Begin(driver);
        p.b.Training.restrictionConfig.rules.receiveForced = false;
        driver.TryActuallyStartNextToil();
        Assert(driver.SceneEndCalls == 1 && driver.CompletedEffects == 1 && !driver.Ended,
            "已合法开始的场景应按原流程完成收尾");
    }
    /// <summary>模拟升级旧存档，验证已发生实际计时的场景恢复后仍可正常收尾。</summary>
    private static void ResumeStartedScene()
    {
        var p = People();
        var driver = Driver(p.c, p.b);
        driver.Sexprops = new SexProps { pawn = p.c, partner = p.b, isRape = true };
        driver.Index = 2;
        driver.ticks_left = 500;
        Scribe.mode = LoadSaveMode.LoadingVars;
        driver.ExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit;
        driver.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        var receiver = new JobDriver_SexBaseRecieverRaped { pawn = p.b, job = new Job() };
        receiver.parteners.Add(p.c);
        p.b.jobs.curDriver = receiver;
        driver.TryActuallyStartNextToil();
        Assert(driver.SceneEndCalls == 1 && driver.CompletedEffects == 1 && receiver.parteners.Count == 0,
            "没有内存开始标记的读档场景应依据现有参与者恢复并完成清理");
    }
    /// <summary>独立检查接收方预约入口，验证其反转角色后仍拒绝非主人的强制行为。</summary>
    private static void ReceiverReservation()
    {
        var p = People();
        var receiver = new JobDriver_SexBaseRecieverRaped
        {
            pawn = p.b,
            job = new Job { def = new JobDef { defName = "GettinRaped" }, targetA = new LocalTargetInfo { Thing = p.c } }
        };
        Assert(!receiver.TryMakePreToilReservations(false), "接收方应反转角色并保留原有保护");
    }
    /// <summary>验证普通 JobDriver 可以正常切换步骤，不会触发仅面向 RJW 发起者的保护。</summary>
    private static void UnrelatedJob()
    {
        var p = People();
        var driver = new JobDriver { pawn = p.c, job = new Job { targetA = new LocalTargetInfo { Thing = p.b } } };
        p.c.jobs.curDriver = driver;
        driver.Toils.Add(new Toil());
        driver.TryActuallyStartNextToil();
        Assert(driver.Index == 0 && !driver.Ended && Messages.Count == 0, "普通任务不得接受 SSC 行为限制");
    }
}
