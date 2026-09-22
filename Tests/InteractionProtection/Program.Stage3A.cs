using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>运行阶段 3A 的真实入口、方向、单人、共享接收、持久化和清理回归。</summary>
    private static void RunStage3ATests()
    {
        Run("重写预约方法也拒绝玩家强制请求", ReservationOverrides);
        Run("普通双人不再读取旧例外或旧全局条目", OldSettingsDoNotDecide);
        Run("只有身份但未绑定时不应用限制，建立绑定后恢复个人配置", IdentityOnly);
        Run("实际主人不被自身条目或目标装备拒绝", OwnerWinsWholeRequest);
        Run("反向及仅主人身份不获得整次放行", DirectionIsNotReversed);
        Run("主动允许不能覆盖对方被动禁止", BothParticipants);
        Run("公交车不能整次放行被禁止的发起方", BusDoesNotOverrideActor);
        Run("装备覆盖公交车强制且穿脱不改保存值", GearAndBus);
        Run("装备不限制普通非强制请求", GearDoesNotBlockConsensual);
        Run("总开关关闭同时暂停条目及装备强制", DisabledGear);
        Run("单人预约禁止及允许均生效", SoloReservations);
        Run("单人开始和收尾持久化", SoloSaveAndFinish);
        Run("缺少目标不能被推断为自慰", MissingTargetIsNotSolo);
        Run("PartnerPawn 布局优先于目标 A", PartnerPawnLayout);
        Run("姿势反转不反转实际发起方向", ReversePose);
        Run("接收方按发起任务判断方向", ReceiverDirection);
        Run("冲突 SexProps 不能伪造主人放行", ConflictingProps);
        Run("人格排泄只查普通条目", PersonalityUsesOrdinaryRules);
        Run("共享接收任务保留人格排泄用途", PersonalityReceiverPurpose);
        Run("准备时已分配 SexProps，读档仍重查", PreparedSaveDoesNotStart);
        Run("开始后收紧许可并读档仍正常结算", StartedSaveFinishes);
        Run("开始凭据不允许换参与者", StartedParticipantChange);
        Run("新的 Job 不继承同一驱动的开始凭据", ReusedDriver);
        Run("拒绝准备好的独占接收任务会释放其任务", PreparedReceiverCleanup);
        Run("迟到回调不终止双方的新任务", StaleBothSides);
        Run("日常和仪式共享接收用途按3B正确识别", DeferredReceiverPurpose);
        Run("动态预约补丁确实安装到重写方法", ReservationRegistration);
        Run("快速任务拒绝只清本次移动等待", QuickieCleanup);
        Run("快速任务读档后仍能清理本次等待", QuickieSaveCleanup);
        Run("快速任务不结束后来替换的新任务", QuickiePreservesNewJob);
        Run("旧存档恢复早于 RJW 重建计时", LegacyProgressBeforeSetup);
        Run("玩家命令拒绝不清空现有任务和队列", OrderedCommandBeforeMutation);
        Run("AI 候选拒绝返回思考树并回收预约", AutomaticCandidateRejected);
        Run("对象池复用 Job 也不继承开始凭据", PooledJobReuse);
        Run("实际加载顺序后接回 Pawn/Job 不丢开始凭据", ReattachAfterLoadingVars);
    }

    /// <summary>创建只有强制接收条目覆盖的装备，直接使用生产扩展与解析器。</summary>
    private static void Equip(Pawn pawn)
    {
        var apparel = new Apparel();
        apparel.def.defName = "Protection";
        apparel.def.modExtensions.Add(new SSCRestrictionEquipmentExtension
        { forced = new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny } });
        pawn.apparel.WornApparel.Add(apparel);
    }

    /// <summary>核对主动重写预约在产生任何接收或开始副作用前拒绝，不保留 playerForced 例外。</summary>
    private static void ReservationOverrides()
    {
        var p = People();
        var driver = Driver(p.c, p.b, forced: true);
        Assert(!driver.TryMakePreToilReservations(false), "覆盖预约应拒绝");
        Assert(driver.ReceiverCreations == 0 && driver.StartCalls == 0 && !driver.Ended, "预约检查不进入任务终止重入");
    }

    /// <summary>打开全部旧例外仍拒绝新配置，再仅改新保存值确认能够开始。</summary>
    private static void OldSettingsDoNotDecide()
    {
        var p = People();
        SSCMod.settings.allowSexSlaveRape = true;
        SSCMod.settings.protectNonRapeOwnerOnly = false;
        p.b.Training.AllowsOthersForTrainingOrSex = true;
        Assert(!Driver(p.c, p.b).TryMakePreToilReservations(false), "旧设置不应放行");
        p.b.Training.restrictionConfig.rules.receiveForced = true;
        var driver = Driver(p.c, p.b);
        Assert(driver.TryMakePreToilReservations(false), "新保存值应生效");
        Begin(driver);
        Assert(driver.StartCalls == 1, "新许可开始成功");
    }

    /// <summary>身份分配不等于获得限制能力；未绑定期间保留配置，绑定后同一配置立即重新参与许可。</summary>
    private static void IdentityOnly()
    {
        var p = People();
        p.b.Chain = null;
        p.b.Training.pawnIdentity = PawnIdentity.Slave;
        Equip(p.b);
        Assert(Driver(p.c, p.b, false).TryMakePreToilReservations(false), "未绑定不受被动自愿限制");
        Assert(Driver(p.c, p.b).TryMakePreToilReservations(false), "未绑定不受条目或装备强制影响");
        Assert(Solo(p.b).TryMakePreToilReservations(false), "未绑定单人也不应用限制");
        Assert(!p.b.Training.restrictionConfig.rules.allowMasturbation, "停用不能改写已保存的禁止");
        p.b.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = p.a };
        Assert(!Driver(p.c, p.b, false).TryMakePreToilReservations(false), "建立绑定后恢复原保存限制");
        Assert(!Solo(p.b).TryMakePreToilReservations(false), "绑定后的单人使用原保存条目");
    }

    /// <summary>将主人本身设置成受限角色，并让接收方配置损坏且穿戴装备，仍应整次放行。</summary>
    private static void OwnerWinsWholeRequest()
    {
        var p = People();
        p.a.Training.pawnIdentity = PawnIdentity.Slave;
        p.a.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = p.c };
        p.b.Training.restrictionConfig.version = 999;
        Equip(p.b);
        var driver = Driver(p.a, p.b);
        Assert(driver.TryMakePreToilReservations(false), "主人应在所有配置前通过");
        Begin(driver);
        Assert(driver.StartCalls == 1, "接收端不得推翻主人结果");
    }

    /// <summary>分别检查反向发起和第三方主人身份，保证最高许可只属于实际有向绑定。</summary>
    private static void DirectionIsNotReversed()
    {
        var p = People();
        Assert(!Driver(p.b, p.a).TryMakePreToilReservations(false), "反向强制不能继承主人许可");
        p.c.Training.pawnIdentity = PawnIdentity.Master;
        Assert(!Driver(p.c, p.b, false).TryMakePreToilReservations(false), "身份不是实际关系");
    }

    /// <summary>发起者主动开放后仍需接收者同意，全部开放后才允许。</summary>
    private static void BothParticipants()
    {
        var p = People();
        p.c.Training.pawnIdentity = PawnIdentity.Slave;
        p.c.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = p.a };
        p.c.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Allow;
        Assert(!Driver(p.c, p.b, false).TryMakePreToilReservations(false), "被动限制不可丢失");
        p.b.Training.restrictionConfig.rules.receiveConsensual = true;
        Assert(Driver(p.c, p.b, false).TryMakePreToilReservations(false), "两方都允许");
    }

    /// <summary>目标的公交车覆盖只能影响目标条目，不能替发起者打开主动许可。</summary>
    private static void BusDoesNotOverrideActor()
    {
        var p = People(); p.b.IsBus = true; p.c.Training.pawnIdentity = PawnIdentity.Slave;
        p.c.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = p.a };
        Assert(!Driver(p.c, p.b).TryMakePreToilReservations(false), "禁止主动强制应优先拒绝");
    }

    /// <summary>核对公交车强制与装备冲突，并确保脱下装备恢复有效值时没有污染保存配置。</summary>
    private static void GearAndBus()
    {
        var p = People(); p.b.IsBus = true; Equip(p.b);
        Assert(!Driver(p.c, p.b).TryMakePreToilReservations(false), "装备禁止覆盖公交车允许");
        p.b.apparel.WornApparel.Clear();
        Assert(Driver(p.c, p.b).TryMakePreToilReservations(false), "脱下装备恢复公交车强制");
        Assert(!p.b.Training.restrictionConfig.rules.receiveForced, "保存值不应改写");
    }

    /// <summary>装备仅影响其声明条目，普通非强制请求仍按保存值允许。</summary>
    private static void GearDoesNotBlockConsensual()
    {
        var p = People(); Equip(p.b); p.b.Training.restrictionConfig.rules.receiveConsensual = true;
        Assert(Driver(p.c, p.b, false).TryMakePreToilReservations(false), "不能追加旧装备拦截");
    }

    /// <summary>全局停用应由统一入口暂停全部覆盖来源。</summary>
    private static void DisabledGear()
    {
        var p = People(); Equip(p.b); SSCMod.settings.enableSexSlaveProtectionRules = false;
        Assert(Driver(p.c, p.b).TryMakePreToilReservations(false), "总开关停用包括装备");
    }

    /// <summary>建立单人驱动的行走、行为和结算步骤，不引入不存在的接收任务。</summary>
    private static JobDriver_Masturbate Solo(Pawn pawn)
    {
        var driver = new JobDriver_Masturbate { pawn = pawn, job = new Job { def = new JobDef { defName = "Masturbate" } } };
        pawn.jobs.curDriver = driver;
        driver.Toils.Add(new Toil());
        driver.Toils.Add(new Toil { initAction = driver.Start, finishAction = driver.End });
        driver.Toils.Add(new Toil { initAction = () => driver.CompletedEffects++ });
        return driver;
    }

    /// <summary>单人也检查明确的自慰条目，空 Partner 不能跳过入口。</summary>
    private static void SoloReservations()
    {
        var p = People();
        Assert(!Solo(p.b).TryMakePreToilReservations(false), "默认禁止自慰");
        p.b.Training.restrictionConfig.rules.allowMasturbation = true;
        Assert(Solo(p.b).TryMakePreToilReservations(false), "显式允许自慰");
    }

    /// <summary>模拟保存再建立新驱动并加载生产开始凭据，保留同一场景的实际执行位置和数据。</summary>
    private static void Restore(JobDriver_Sex oldDriver, JobDriver_Sex restored)
    {
        Scribe.mode = LoadSaveMode.Saving; oldDriver.ExposeData();
        Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit; restored.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
    }

    /// <summary>单人已开始后保存且收紧许可，恢复仍执行一次正常收尾和结算。</summary>
    private static void SoloSaveAndFinish()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.allowMasturbation = true;
        var oldDriver = Solo(p.b); oldDriver.TryActuallyStartNextToil(); oldDriver.TryActuallyStartNextToil();
        var restored = Solo(p.b); restored.Index = 1; restored.Sexprops = oldDriver.Sexprops;
        Restore(oldDriver, restored);
        p.b.Training.restrictionConfig.rules.allowMasturbation = false;
        restored.TryActuallyStartNextToil();
        Assert(restored.SceneEndCalls == 1 && restored.CompletedEffects == 1, "单人应恢复已开始状态");
    }

    /// <summary>有管理身份且缺少目标的双人请求应报上下文不足，不冒充自慰放行。</summary>
    private static void MissingTargetIsNotSolo()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.allowMasturbation = true;
        Assert(!Driver(p.b, null, false).TryMakePreToilReservations(false), "空目标不是单人证据");
    }

    /// <summary>当 A 是家具或其他目标而 PartnerPawn 保存真正角色时仍用真实参与者检查。</summary>
    private static void PartnerPawnLayout()
    {
        var p = People(); var driver = Driver(p.a, p.c); driver.PartnerPawn = p.b;
        Assert(driver.TryMakePreToilReservations(false), "PartnerPawn 应识别出绑定目标");
        driver.pawn = p.c; driver.job.targetA = new LocalTargetInfo { Thing = p.a };
        Assert(!driver.TryMakePreToilReservations(false), "不得用 A 目标猜方向");
    }

    /// <summary>姿势标记只影响表现，即使使用接收方形式的数据，解析出的有向参与者仍须相同。</summary>
    private static void ReversePose()
    {
        var p = People(); var driver = Driver(p.a, p.b);
        driver.Sexprops = new SexProps { pawn = p.b, partner = p.a, isReceiver = true, isRevese = true, isRape = true };
        Assert(driver.TryMakePreToilReservations(false), "实际发起者仍为主人");
    }

    /// <summary>接收驱动在尚无 SexProps 时沿发起任务关联验证主人方向。</summary>
    private static void ReceiverDirection()
    {
        var p = People(); Driver(p.a, p.b);
        var receiver = new JobDriver_SexBaseRecieverRaped { pawn = p.b, job = new Job { targetA = new LocalTargetInfo { Thing = p.a } } };
        Assert(receiver.TryMakePreToilReservations(false), "主人发起在接收端也允许");
    }

    /// <summary>拒绝把第三人的陈旧行为数据当作当前主人发起依据。</summary>
    private static void ConflictingProps()
    {
        var p = People(); var driver = Driver(p.c, p.b);
        driver.Sexprops = new SexProps { pawn = p.a, partner = p.b, isRape = true };
        Assert(!driver.TryMakePreToilReservations(false), "冲突方向必须拒绝");
    }

    /// <summary>建立人格排泄发起驱动，使用与生产相同的基类流程。</summary>
    private static JobDriver_PE Personality(Pawn actor, Pawn target)
    {
        var driver = new JobDriver_PE { pawn = actor, job = new Job { def = new JobDef { defName = "Personality" }, targetA = new LocalTargetInfo { Thing = target } } };
        actor.jobs.curDriver = driver; driver.MakeScenarioToils(); return driver;
    }

    /// <summary>人格排泄不附加调教身份或许可门槛，但调教许可也不能代替普通被动许可。</summary>
    private static void PersonalityUsesOrdinaryRules()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.receiveConsensual = true;
        var driver = Personality(p.c, p.b); Begin(driver);
        Assert(driver.StartCalls == 1, "无调教许可也可按普通规则通过");
        p.b.jobs.curDriver = null; p.b.Training.restrictionConfig.rules.receiveConsensual = false;
        p.b.Training.restrictionConfig.rules.receiveTraining = true;
        Assert(!Personality(p.c, p.b).TryMakePreToilReservations(false), "仅调教允许不足以放行");
    }

    /// <summary>共用 SSC 接收任务时以关联发起任务还原用途，不凭接收定义归类为调教。</summary>
    private static void PersonalityReceiverPurpose()
    {
        var p = People(); Personality(p.c, p.b);
        var receiver = new JobDriver_SexBaseReciever { pawn = p.b, job = new Job { def = SSCDefOf.SSC_TrainingReceiver, targetA = new LocalTargetInfo { Thing = p.c } } };
        Assert(SSCRestrictionJobContext.TryCreate(receiver, out var request) && request.Kind == SSCInteractionKind.PersonalityExcretion, "用途应还原为人格排泄");
    }

    /// <summary>准备阶段的双人数据和提前参与者登记经过保存后仍不能获得已开始豁免。</summary>
    private static void PreparedSaveDoesNotStart()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true;
        var oldDriver = Driver(p.c, p.b); oldDriver.TryActuallyStartNextToil(); oldDriver.TryActuallyStartNextToil();
        oldDriver.Sexprops = new SexProps { pawn = p.c, partner = p.b, isRape = true };
        var restored = Driver(p.c, p.b); restored.Index = 1; restored.Sexprops = oldDriver.Sexprops;
        Restore(oldDriver, restored); p.b.Training.restrictionConfig.rules.receiveForced = false;
        restored.TryActuallyStartNextToil(); Rejected(restored);
    }

    /// <summary>新凭据即使在 Start 后尚未计时就保存，也能准确恢复并完成收尾。</summary>
    private static void StartedSaveFinishes()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true;
        var oldDriver = Driver(p.c, p.b); Begin(oldDriver);
        var restored = Driver(p.c, p.b); restored.Index = 2; restored.Sexprops = oldDriver.Sexprops;
        Restore(oldDriver, restored); p.b.Training.restrictionConfig.rules.receiveForced = false;
        restored.TryActuallyStartNextToil();
        Assert(restored.SceneEndCalls == 1 && restored.CompletedEffects == 1, "已开始凭据必须保存");
    }

    /// <summary>同一驱动改为另一个受限接收者，必须重新检查该新请求。</summary>
    private static void StartedParticipantChange()
    {
        var p = People(); var driver = Driver(p.a, p.b); Begin(driver);
        p.c.Training.pawnIdentity = PawnIdentity.Slave; driver.PartnerPawn = p.c; driver.Sexprops = null;
        p.c.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = p.b };
        Assert(SSCRestrictionJobGuard.TryCheck(driver, "Changed", out bool allowed) && !allowed, "新参与者应复查");
    }

    /// <summary>外部复用驱动时换 Job 必须失去之前的开始凭据。</summary>
    private static void ReusedDriver()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true;
        var driver = Driver(p.c, p.b); Begin(driver); p.b.Training.restrictionConfig.rules.receiveForced = false;
        driver.job = new Job { def = SSCDefOf.RapeComfortPawn, targetA = new LocalTargetInfo { Thing = p.b } };
        Assert(SSCRestrictionJobGuard.TryCheck(driver, "Reused", out bool allowed) && !allowed, "新 Job 不继承旧凭据");
    }

    /// <summary>拒绝发生在接收准备后时清掉独占任务，不留下等待者；终止使用失败条件。</summary>
    private static void PreparedReceiverCleanup()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true;
        var driver = Driver(p.c, p.b); driver.TryActuallyStartNextToil(); driver.TryActuallyStartNextToil();
        var receiver = (JobDriver_SexBaseReciever)p.b.jobs.curDriver;
        p.b.Training.restrictionConfig.rules.receiveForced = false; driver.TryActuallyStartNextToil();
        Assert(receiver.Ended && receiver.EndCondition == JobCondition.Incompletable && receiver.parteners.Count == 0, "准备任务应失败清理");
    }

    /// <summary>旧 Start 回调既不结束发起者的新任务，也不删除目标后来建立的接收关联。</summary>
    private static void StaleBothSides()
    {
        var p = People(); var stale = Driver(p.c, p.b);
        var current = new JobDriver { pawn = p.c, job = new Job() }; p.c.jobs.curDriver = current;
        Begin(Driver(p.a, p.b)); var receiver = (JobDriver_SexBaseReciever)p.b.jobs.curDriver;
        stale.Start();
        Assert(p.c.jobs.curDriver == current && receiver.parteners.SequenceEqual(new[] { p.a }), "晚回调不能清新任务");
    }

    /// <summary>共享接收方关联日常或仪式任务时，不误用普通双人许可。</summary>
    private static void DeferredReceiverPurpose()
    {
        var p = People();
        foreach (var actor in new JobDriver_SexBaseInitiator[] { new JobDriver_Training(), new JobDriver_RitualTraining() })
        {
            actor.pawn = p.c; actor.job = new Job { targetA = new LocalTargetInfo { Thing = p.b } }; p.c.jobs.curDriver = actor;
            var receiver = new JobDriver_SexBaseReciever { pawn = p.b, job = new Job { def = SSCDefOf.SSC_TrainingReceiver, targetA = new LocalTargetInfo { Thing = p.c } } };
            Assert(SSCRestrictionJobContext.TryCreate(receiver, out var request) && request.Kind ==
                (actor is JobDriver_Training ? SSCInteractionKind.DailyTraining : SSCInteractionKind.RitualTraining), "调教用途不混入普通规则");
        }
    }

    /// <summary>同时验证动态目标发现和真实 Harmony 注册，防止只在手工调用测试中覆盖预约漏洞。</summary>
    private static void ReservationRegistration()
    {
        var method = typeof(JobDriver_SexBaseInitiator).GetMethod("TryMakePreToilReservations");
        Assert(SSCRestrictionReservationHook.TargetMethods().Contains(method), "必须发现重写预约");
#if REAL_HARMONY
        Assert(Harmony.GetPatchInfo(method)?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(SSCRestrictionReservationHook)) == true,
            "真实 Harmony 必须安装覆盖预约补丁");
#endif
    }

    /// <summary>模拟快速任务创建目标移动与排队等待，保留已有排队命令用于检查精确归属。</summary>
    private static JobDriver_SexQuick PrepareQuickie(Pawn actor, Pawn target, out Job existing)
    {
        var driver = new JobDriver_SexQuick { pawn = actor, job = new Job { targetA = new LocalTargetInfo { Thing = target } } };
        actor.jobs.curDriver = driver;
        existing = new Job { def = RimWorld.JobDefOf.Wait };
        target.jobs.jobQueue.Add(new QueuedJob { job = existing });
        IEnumerable<Toil> toils = new[] { new Toil { initAction = () =>
        {
            target.jobs.curDriver = new JobDriver { pawn = target, job = new Job { def = RimWorld.JobDefOf.Goto } };
            target.jobs.jobQueue.Add(new QueuedJob { job = new Job { def = RimWorld.JobDefOf.Wait } });
        } } };
        SSCRestrictionQuickiePreparationHook.Postfix(driver, ref toils);
        toils.Single().initAction();
        return driver;
    }

    /// <summary>拒绝快速任务时仅删除记录为本请求创建的任务，同时保留原有玩家排队命令。</summary>
    private static void QuickieCleanup()
    {
        var p = People(); var driver = PrepareQuickie(p.c, p.b, out Job existing);
        var movement = p.b.jobs.curDriver;
        SSCRestrictionJobGuard.TryCheck(driver, "BeforeScene", out bool allowed);
        Assert(!allowed && movement.Ended && p.b.jobs.jobQueue.Single().job == existing, "仅清本次移动和等待");
    }

    /// <summary>任务归属编号和目标引用存读档后仍可用于拒绝清理，不依赖内存前后差异。</summary>
    private static void QuickieSaveCleanup()
    {
        var p = People(); var oldDriver = PrepareQuickie(p.c, p.b, out Job existing);
        var restored = new JobDriver_SexQuick { pawn = p.c, job = oldDriver.job };
        p.c.jobs.curDriver = restored; Restore(oldDriver, restored);
        SSCRestrictionJobGuard.TryCheck(restored, "AfterLoad", out bool allowed);
        Assert(!allowed && p.b.jobs.curDriver == null && p.b.jobs.jobQueue.Single().job == existing, "读档后应识别本次准备");
    }

    /// <summary>目标已换到另一个同类型移动任务时，旧请求只能清自己的排队等待，不能终止新任务。</summary>
    private static void QuickiePreservesNewJob()
    {
        var p = People(); var driver = PrepareQuickie(p.c, p.b, out Job existing);
        var newer = new JobDriver { pawn = p.b, job = new Job { def = RimWorld.JobDefOf.Goto } }; p.b.jobs.curDriver = newer;
        SSCRestrictionJobGuard.TryCheck(driver, "BeforeScene", out _);
        Assert(p.b.jobs.curDriver == newer && !newer.Ended && p.b.jobs.jobQueue.Single().job == existing, "新移动任务必须保留");
    }

    /// <summary>模拟 RJW 在 PostLoadInit 重建步骤时重置计时，验证此前载入的实际进度已被捕获。</summary>
    private static void LegacyProgressBeforeSetup()
    {
        var p = People(); var driver = Driver(p.c, p.b);
        driver.Sexprops = new SexProps { pawn = p.c, partner = p.b, isRape = true }; driver.ticks_left = 500;
        Scribe.mode = LoadSaveMode.LoadingVars; driver.ExposeData();
        driver.ticks_left = driver.duration = 1000;
        Scribe.mode = LoadSaveMode.PostLoadInit; driver.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        Assert(SSCRestrictionJobGuard.TryCheck(driver, "AfterLoad", out bool allowed) && allowed, "旧进度不能被步骤重建抹掉");
    }

    /// <summary>经真实或直接命令补丁拒绝请求，确保原版队列修改和预约警告路径均未进入。</summary>
    private static void OrderedCommandBeforeMutation()
    {
        var p = People(); var driver = Driver(p.c, p.b, forced: true);
        driver.job.CachedDriver = driver; p.c.jobs.pawn = p.c;
        var existing = new JobDriver { pawn = p.c, job = new Job() }; p.c.jobs.curDriver = existing;
        p.c.jobs.jobQueue.Add(new QueuedJob { job = new Job() });
        Assert(!p.c.jobs.TryTakeOrderedJob(driver.job), "命令应拒绝");
        Assert(p.c.jobs.OrderedMutations == 0 && p.c.jobs.curDriver == existing && p.c.jobs.jobQueue.Count == 1 && Messages.Count == 1,
            "拒绝命令只提示，不进入原队列变更");
    }

    /// <summary>验证 AI 候选在派发前变成无任务，清理预约并回收，不触发提示或当前驱动终止。</summary>
    private static void AutomaticCandidateRejected()
    {
        var p = People(); var driver = Driver(p.c, p.b); driver.job.CachedDriver = driver;
        var giver = new ThinkNode_JobGiver { Candidate = driver.job };
        ThinkResult result = giver.TryIssueJobPackage(p.c);
        Assert(result.Job == null && p.c.ClearedReservations == 1 && JobMaker.LastReturned == driver.job &&
            !driver.Ended && Messages.Count == 0, "拒绝候选应让思考树继续其他工作");
        p.b.Training.restrictionConfig.rules.receiveForced = true;
        Assert(giver.TryIssueJobPackage(p.c).Job == driver.job, "许可开放后候选正常返回");
    }

    /// <summary>即使 Job 引用不变，其对象池编号更新也必须清除上一场开始凭据。</summary>
    private static void PooledJobReuse()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true;
        var driver = Driver(p.c, p.b); Begin(driver);
        driver.job.loadID++; p.b.Training.restrictionConfig.rules.receiveForced = false;
        Assert(SSCRestrictionJobGuard.TryCheck(driver, "Pooled", out bool allowed) && !allowed, "池化新任务必须重查");
    }

    /// <summary>模拟游戏先读驱动字段、随后才赋回 Pawn/Job 的顺序，验证已保存状态不会被当作驱动复用清空。</summary>
    private static void ReattachAfterLoadingVars()
    {
        var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true;
        var oldDriver = Driver(p.c, p.b); Begin(oldDriver);
        Scribe.mode = LoadSaveMode.Saving; oldDriver.ExposeData();
        var restored = new JobDriver_Rape { Sexprops = oldDriver.Sexprops };
        Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData();
        restored.pawn = p.c; restored.job = oldDriver.job; p.c.jobs.curDriver = restored;
        Scribe.mode = LoadSaveMode.PostLoadInit; restored.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        p.b.Training.restrictionConfig.rules.receiveForced = false;
        Assert(SSCRestrictionJobGuard.TryCheck(restored, "Loaded", out bool allowed) && allowed, "回接引用不能重置已开始状态");
    }
}
