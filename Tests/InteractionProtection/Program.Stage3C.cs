#if NO_LIFEFORCE
using RJW_Genes = SSCMissingGenes;
#endif
using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>创建并安装原版Lovin，准备原生步骤但尚不运行，使测试可在走床与初始化之间修改配置。</summary>
    private static JobDriver_Lovin Lovin(Pawn actor, Pawn target)
    {
        var job = new Job { def = new JobDef { defName = "Lovin", driverClass = typeof(JobDriver_Lovin) }, targetA = new LocalTargetInfo { Thing = target } };
        var driver = (JobDriver_Lovin)job.GetCachedDriver(actor);
        actor.jobs.curDriver = driver;
        driver.Toils = driver.MakeNewToils().ToList();
        return driver;
    }

    /// <summary>推进到原版配对完成后的躺卧步骤；每一步都经过实际Harmony步骤前缀。</summary>
    private static void BeginLovin(JobDriver_Lovin driver)
    {
        for (int i = 0; i < 4 && !driver.Ended; i++) driver.TryActuallyStartNextToil();
    }

    /// <summary>创建有明确施法方向的非RJW准备驱动，目标A是施法者而不是接收者。</summary>
    private static RJW_Genes.JobDriver_Seduced Seduced(Pawn caster, Pawn target)
    {
        var job = new Job { def = new JobDef { defName = "rjw_genes_lifeforce_seduced", driverClass = typeof(RJW_Genes.JobDriver_Seduced) },
            targetA = new LocalTargetInfo { Thing = caster } };
        var driver = (RJW_Genes.JobDriver_Seduced)job.GetCachedDriver(target);
        target.jobs.curDriver = driver;
        driver.Toils.Add(new Toil());
        driver.Toils.Add(new Toil { initAction = () => throw new InvalidOperationException("拒绝后不得进入交接副作用") });
        return driver;
    }

    /// <summary>运行3C方向、真实开始、旧档迁移、后期拒绝与可选补丁场景；事件选择及收益另由BusTrade套件验证。</summary>
    private static void RunStage3CTests()
    {
        Run("3C可选兼容目标按模组是否存在动态发现", () =>
        {
#if NO_LIFEFORCE
            Assert(!SSCRestrictionSeducedReservationHook.TargetMethods().Any() &&
                !SSCRestrictionSeduceValidHook.TargetMethods().Any() && !SSCRestrictionSeduceApplyHook.TargetMethods().Any(), "缺席时无硬依赖或补丁目标");
#else
            Assert(SSCRestrictionSeducedReservationHook.TargetMethods().Count() == 1 &&
                SSCRestrictionSeduceValidHook.TargetMethods().Count() == 1 && SSCRestrictionSeduceApplyHook.TargetMethods().Count() == 1, "已安装时三处都接入");
#endif
        });
        Run("3C原版Lovin第三方默认拒绝且无原生初始化或成功Cleanup", () =>
        {
            var p = People(); var d = Lovin(p.c, p.b);
            Assert(!d.TryMakePreToilReservations(false), "预约必须检查新规则");
            BeginLovin(d);
            Assert(d.Ended && d.Initializations == 0 && d.SuccessfulCleanup == 0, "尚未初始化，不应有成功结算");
        });
        Run("3C主人Lovin的接收端沿用发起方向，损坏配置不否决", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.version = 999; Equip(p.b);
            var d = Lovin(p.a, p.b); BeginLovin(d);
            Assert(!d.Ended && d.Initializations == 1 && p.b.jobs.curDriver is JobDriver_Lovin, "原版双端都开始");
            Assert(SSCRestrictionLovinGuard.Check((JobDriver_Lovin)p.b.jobs.curDriver, true), "接收端不得当成反向发起");
            d.TryActuallyStartNextToil(); Assert(d.SuccessfulCleanup == 1, "原版正常成功结束");
        });
        Run("3CLovin反向发起仍检查性奴主动条目", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            var d = Lovin(p.b, p.a); BeginLovin(d);
            Assert(d.Ended && d.Initializations == 0, "反向不继承主人特权");
        });
        Run("3CLovin走床期间收紧条目，不创建另一端", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.rules.receiveConsensual = true;
            var d = Lovin(p.c, p.b); d.TryActuallyStartNextToil(); d.TryActuallyStartNextToil();
            p.b.Training.restrictionConfig.rules.receiveConsensual = false;
            d.TryActuallyStartNextToil(); Assert(d.Ended && d.Initializations == 0 && p.b.CurJob == null, "拒绝在配对之前");
        });
        Run("3CLovin已开始收紧许可可结束，新任务不得继承", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.rules.receiveConsensual = true;
            var d = Lovin(p.c, p.b); BeginLovin(d); p.b.Training.restrictionConfig.rules.receiveConsensual = false;
            d.TryActuallyStartNextToil(); Assert(d.SuccessfulCleanup == 1, "当前场景正常结束");
            var next = Lovin(p.c, p.b); Assert(!next.TryMakePreToilReservations(false), "新任务必须重查");
        });
        Run("3CLovin接收创建失败不能发布开始凭据", () =>
        {
            var p = People(); var d = Lovin(p.a, p.b); d.RejectReceiver = true; BeginLovin(d);
            Assert(d.Ended && d.SuccessfulCleanup == 0, "双方未配对，不得伪造成功");
            d.Cleanup(JobCondition.Succeeded); Assert(d.SuccessfulCleanup == 0 && d.SuccessfulCleanupPrefix == 0, "在RJW结算前缀之前纠正Succeeded");
        });
        Run("3CLovin迟到回调不结束新任务", () =>
        {
            var p = People(); var d = Lovin(p.c, p.b); var other = new JobDriver { pawn = p.c, job = new Job() };
            p.c.jobs.curDriver = other; d.TryActuallyStartNextToil();
            Assert(p.c.jobs.curDriver == other && !other.Ended, "当前新工作保持");
        });
        Run("3CLovin准备与已开始存读档边界", () =>
        {
            foreach (bool started in new[] { false, true })
            {
                var p = People(); p.b.Training.restrictionConfig.rules.receiveConsensual = true;
                var d = Lovin(p.c, p.b); if (started) BeginLovin(d);
                Scribe.node.Clear(); Scribe.mode = LoadSaveMode.Saving; d.ExposeData();
                var restored = new JobDriver_Lovin { pawn = p.c, job = d.job, Index = d.Index };
                p.c.jobs.curDriver = restored;
                Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData();
                Scribe.mode = LoadSaveMode.PostLoadInit; restored.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
                p.b.Training.restrictionConfig.rules.receiveConsensual = false;
                Assert(restored.TryMakePreToilReservations(false) == started, "持久化区分准备和实际开始");
            }
        });
        Run("3C旧Lovin原版计时恢复双方方向，走床不恢复", () =>
        {
            foreach (bool started in new[] { false, true })
            {
                var p = People(); var a = Lovin(p.a, p.b); var b = Lovin(p.b, p.a);
                a.Index = b.Index = started ? 3 : 1;
                Scribe.node.Clear(); Scribe.node["ticksLeft"] = 9999876;
                Scribe.mode = LoadSaveMode.LoadingVars; b.ExposeData();
                Scribe.mode = LoadSaveMode.PostLoadInit; b.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
                p.b.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
                Assert(b.TryMakePreToilReservations(false) == started, "旧接收者只在实际场景恢复方向和开始");
            }
        });
        Run("3C旧Lovin计时恰好结束也恢复当前场景收尾", () =>
        {
            var p = People(); var d = Lovin(p.c, p.b); var peer = Lovin(p.b, p.c); d.Index = peer.Index = 3;
            Scribe.node.Clear(); Scribe.node["ticksLeft"] = 0;
            Scribe.mode = LoadSaveMode.LoadingVars; d.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; d.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            Assert(d.TryMakePreToilReservations(false), "到达真实躺卧步骤的旧场景即使计时耗尽仍可收尾");
        });
        Run("3CLovin玩家命令和AI预检在队列修改之前", () =>
        {
            var p = People(); var d = Lovin(p.c, p.b); p.c.jobs.pawn = p.c;
            Assert(!p.c.jobs.TryTakeOrderedJob(d.job) && p.c.jobs.OrderedMutations == 0, "手动拒绝不改队列");
            Assert(new ThinkNode_JobGiver { Candidate = d.job }.TryIssueJobPackage(p.c).Job == null, "AI候选被过滤");
        });
        Run("3CRJW替换后的新驱动继续使用普通统一守卫", () =>
        {
            var p = People(); var native = Lovin(p.a, p.b);
            var rjw = Driver(p.a, p.b, false); native.TryActuallyStartNextToil(); Begin(rjw);
            Assert(native.Initializations == 0 && rjw.StartCalls == 1, "迟到原版不运行，RJW场景自己开始");
            native.Cleanup(JobCondition.Succeeded); Assert(native.SuccessfulCleanup == 0, "原版替换不得重复结算");
        });
#if !NO_LIFEFORCE
        Run("3CSeduce能力预检和Apply均拒绝且不产生副作用", () =>
        {
            var p = People(); var effect = new RJW_Genes.CompAbilityEffect_Seduce { parent = new Ability { pawn = p.c } };
            var target = new LocalTargetInfo { Thing = p.b };
            Assert(!effect.Valid(target, true) && Messages.Count == 1, "手动显示拒绝原因");
            effect.Apply(target, default); Assert(effect.ApplyCalls == 0, "Apply不进入StopAll等副作用");
            p.b.Training.restrictionConfig.rules.receiveConsensual = true;
            Assert(effect.Valid(target, false), "许可更改即时生效"); effect.Apply(target, default);
            Assert(effect.ApplyCalls == 1, "允许后保留原能力");
        });
        Run("3CSeduce实际主人放行，但能力自身条件保留", () =>
        {
            var p = People(); p.b.Training.restrictionConfig = null;
            var effect = new RJW_Genes.CompAbilityEffect_Seduce { parent = new Ability { pawn = p.a } };
            var target = new LocalTargetInfo { Thing = p.b };
            Assert(effect.Valid(target, false), "主人无配置直接通过许可");
            effect.NativeValid = false; Assert(!effect.Valid(target, false), "正常能力条件仍保留");
        });
        Run("3CSeduced准备按施法者发起，走近期间改规则不交接", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.rules.receiveConsensual = true;
            var d = Seduced(p.c, p.b); Assert(d.TryMakePreToilReservations(false), "初始允许");
            d.TryActuallyStartNextToil(); p.b.Training.restrictionConfig.rules.receiveConsensual = false;
            d.TryActuallyStartNextToil(); Assert(d.Ended, "交接副作用之前拒绝");
        });
        Run("3CSeduced接收方向不会将主人请求误判成反向", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.version = 999;
            var d = Seduced(p.a, p.b); Assert(d.TryMakePreToilReservations(false), "targetA为实际发起者");
            d.TryActuallyStartNextToil(); Assert(!d.Ended, "准备步骤通过");
        });
#endif
        Run("3C生命力强制场景遵守装备与主人最高许可", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true; Equip(p.b);
            var d = Driver(p.c, p.b); d.job.def.defName = "rjw_genes_lifeforce_randomrape";
            Assert(!d.TryMakePreToilReservations(false), "名称不能绕过装备条目");
            d = Driver(p.a, p.b); d.job.def.defName = "rjw_genes_lifeforce_randomrape";
            Begin(d); Assert(d.StartCalls == 1, "主人不被装备否决");
        });
        Run("3C生命力旧白名单场景的真实进度可跨升级收尾", () =>
        {
            var p = People(); var d = Driver(p.c, p.b); d.job.def.defName = "rjw_genes_lifeforce_randomrape";
            d.Sexprops = new SexProps { pawn = p.c, partner = p.b, isRape = true }; d.ticks_left--;
            Scribe.node["sscRestrictionSceneRecord"] = true;
            Scribe.mode = LoadSaveMode.LoadingVars; d.ExposeData(); d.ticks_left = d.duration;
            Scribe.mode = LoadSaveMode.PostLoadInit; d.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            Assert(d.TryMakePreToilReservations(false), "旧通用记录不掩盖新接管类型的实际进度");
        });
        Run("3C事件通知只在Start后一次，保存恢复不重复", () =>
        {
            foreach (SSCRestrictionEvent source in new[] { SSCRestrictionEvent.TradeConsensual, SSCRestrictionEvent.TradeForced, SSCRestrictionEvent.Dog })
            {
                Messages.Count = 0; var p = People(); var d = Driver(p.a, p.b, source == SSCRestrictionEvent.TradeForced);
                d.job.CachedDriver = d;
                Assert(SSCRestrictionJobGuard.PrepareEvent(p.a, d.job, source) && Messages.Count == 0, "预检不提示发生");
                Begin(d); Assert(Messages.Count == 1, "实际开始提示一次");
                var restored = Driver(p.a, p.b, source == SSCRestrictionEvent.TradeForced); Restore(d, restored);
                SSCRestrictionJobGuard.TryMarkStarted(restored, true); Assert(Messages.Count == 1, "恢复不重复提示");
            }
        });
        Run("3C事件等待途中被拒绝只清关联任务且不发发生消息", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.rules.receiveForced = true;
            var d = Driver(p.c, p.b, true, true); d.job.CachedDriver = d;
            Assert(SSCRestrictionJobGuard.PrepareEvent(p.c, d.job, SSCRestrictionEvent.TradeForced), "预检允许");
            var wait = new Job { def = JobDefOf.Wait }; var unrelated = new Job { def = JobDefOf.Wait };
            p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            p.b.jobs.jobQueue.Add(new QueuedJob { job = unrelated });
            SSCRestrictionJobGuard.RegisterEventWait(p.c, d.job, p.b, wait);
            p.b.Training.restrictionConfig.rules.receiveForced = false; Begin(d);
            Assert(p.b.CurJob == null && p.b.jobs.jobQueue.Single().job == unrelated && Messages.Count == 0, "只清本次等待且AI事件不刷屏");
        });
        Run("3C事件从预检缓存驱动交接给原生新建运行驱动", () =>
        {
            var p = People(); var job = new Job { def = new JobDef { defName = "Event", driverClass = typeof(JobDriver_SexBaseInitiator) },
                targetA = new LocalTargetInfo { Thing = p.b } };
            Assert(SSCRestrictionJobGuard.PrepareEvent(p.a, job, SSCRestrictionEvent.TradeConsensual), "预检通过");
            var cached = job.GetCachedDriver(p.a);
            var wait = new Job { def = JobDefOf.Wait }; p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            SSCRestrictionJobGuard.RegisterEventWait(p.a, job, p.b, wait);
            var actual = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a); p.a.jobs.curDriver = actual; actual.MakeScenarioToils();
            Assert(actual != cached, "原版实际创建另一实例，不能用错误模型掩盖登记丢失");
            Begin(actual); Assert(Messages.Count == 1, "通知来源已交给实际运行驱动");
        });
        Run("3C尚未开始的事件更换运行驱动后仍可清等待", () =>
        {
            var p = People(); var job = new Job { def = new JobDef { defName = "Event", driverClass = typeof(JobDriver_Rape) },
                targetA = new LocalTargetInfo { Thing = p.b } };
            p.b.Training.restrictionConfig.rules.receiveForced = true;
            SSCRestrictionJobGuard.PrepareEvent(p.c, job, SSCRestrictionEvent.TradeForced);
            var wait = new Job { def = JobDefOf.Wait }; p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            SSCRestrictionJobGuard.RegisterEventWait(p.c, job, p.b, wait);
            var actual = (JobDriver_SexBaseInitiator)job.MakeDriver(p.c); p.c.jobs.curDriver = actual; actual.MakeScenarioToils();
            p.b.Training.restrictionConfig.rules.receiveForced = false;
            Begin(actual); Assert(actual.Ended && p.b.CurJob == null && Messages.Count == 0, "新驱动也清理原登记等待");
        });
        Run("3C事件外部取消及等待读档恢复均清理准备任务", () =>
        {
            var p = People(); var d = Driver(p.a, p.b); d.job.CachedDriver = d;
            SSCRestrictionJobGuard.PrepareEvent(p.a, d.job, SSCRestrictionEvent.TradeForced);
            var wait = new Job { def = JobDefOf.Wait }; p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            SSCRestrictionJobGuard.RegisterEventWait(p.a, d.job, p.b, wait);
            d.TryMakePreToilReservations(false); // 模拟原生安装时由运行驱动领取事件。
            var restored = Driver(p.a, p.b); Restore(d, restored); restored.Cleanup(JobCondition.Incompletable);
            Assert(p.b.CurJob == null && Messages.Count == 0, "保存的等待归属仍有效");
        });
        Run("3C宠物床上移动只回收本次GotoMindControlled", () =>
        {
            var p = People(); var d = new JobDriver_BestialityForFemale { pawn = p.c,
                job = new Job { targetA = new LocalTargetInfo { Thing = p.b } } };
            p.c.jobs.curDriver = d;
            var move = new Job { def = JobDefOf.GotoMindControlled };
            d.Preparation = new[] { new Toil { initAction = () => p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = move } } };
            d.MakeNewToils().Single().initAction(); d.Cleanup(JobCondition.Incompletable);
            Assert(p.b.CurJob == null, "取消准备不能留下被控制移动");
        });
        Run("3C家具待机没有虚构行为请求，真实使用仍按统一许可", () =>
        {
            var p = People();
            var receiver = new RJW_Onahole.Jobs.JobDriver_BeOnahole { pawn = p.b, job = new Job() };
            p.b.jobs.curDriver = receiver;
            Assert(!SSCRestrictionJobContext.TryCreate(receiver, out _), "单纯占用家具不虚构双人场景");
            var d = Driver(p.c, p.b); receiver.PartnerPawn = p.c; receiver.parteners.Add(p.c);
            Assert(!d.TryMakePreToilReservations(false), "使用家具不能放行被拒绝的真实请求");
            SSCRestrictionJobGuard.TryCheck(receiver, "Receiver", out bool keepFurniture);
            Assert(keepFurniture && p.b.jobs.curDriver == receiver && !receiver.Ended && d.Ended, "只拒绝本次使用者，保留家具占用");
            Assert(receiver.PartnerPawn == null && receiver.parteners.Count == 0, "解除本次参与者引用");
        });
        Run("3C家具主人场景开始后可收尾，非主人新加入仍拒绝", () =>
        {
            var p = People(); var d = Driver(p.a, p.b);
            var receiver = new RJW_Onahole.Jobs.JobDriver_BeOnahole { pawn = p.b, job = new Job(), PartnerPawn = p.a };
            p.b.jobs.curDriver = receiver; receiver.parteners.Add(p.a); d.Start();
            Assert(d.StartCalls == 1, "实际主人通过");
            var late = Driver(p.c, p.b); Begin(late);
            Assert(late.Ended && p.b.jobs.curDriver == receiver && receiver.parteners.Contains(p.a), "原场景与常驻家具保留");
        });
        Run("3C所有非RJW适配也服从总开关", () =>
        {
            var p = People(); SSCMod.settings.enableSexSlaveProtectionRules = false;
            Assert(Lovin(p.c, p.b).TryMakePreToilReservations(false), "Lovin停用限制");
#if !NO_LIFEFORCE
            Assert(Seduced(p.c, p.b).TryMakePreToilReservations(false), "Seduced停用限制");
#endif
        });
    }
}
