using System.Collections.Generic;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>建立已绑定的调教请求，复用真实守卫和 Harmony 路由，原生游戏任务仅保留最小生命周期模型。</summary>
    private static JobDriver_SexBaseInitiator TrainingDriver(Pawn actor, Pawn target, bool ritual = false, bool forced = false)
    {
        JobDriver_SexBaseInitiator driver = ritual ? new JobDriver_RitualTraining() : new JobDriver_Training();
        driver.pawn = actor;
        driver.job = new Job { def = new JobDef { defName = ritual ? "SSC_Training_Ritual" : "SSC_Training_SexSlave", driverClass = driver.GetType() },
            targetA = new LocalTargetInfo { Thing = target }, playerForced = forced };
        actor.jobs.curDriver = driver;
        driver.MakeScenarioToils();
        return driver;
    }

    /// <summary>验证3B开始、接收、升级存档、阶段边界与拒绝清理，避免只在界面预检中生效。</summary>
    private static void RunStage3BTests()
    {
        Run("3B主人玩家命令在原版设置强制位之前也能通过", () =>
        {
            var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = null;
            var d = TrainingDriver(p.a, p.b); d.job.CachedDriver = d; p.a.jobs.pawn = p.a;
            Assert(!d.job.playerForced, "尚未进入原版命令方法");
            Assert(p.a.jobs.TryTakeOrderedJob(d.job), "明确命令来源不得误判为自动补位");
            Assert(d.job.playerForced && d.TryMakePreToilReservations(false), "实际预约沿用原版强制标记");
            Begin(d); Assert(d.StartCalls == 1, "主人命令确实开始");
        });
        Run("3B初次准备开始后建立绑定不改变当前场景用途", () =>
        {
            foreach (bool ritual in new[] { false, true })
            {
                var p = People(); p.b.Chain = null; p.b.Training.pawnIdentity = PawnIdentity.Slave;
                p.c.Training.pawnIdentity = PawnIdentity.Master; p.b.Training.selectedTrainer = p.c;
                var d = TrainingDriver(p.c, p.b, ritual); Begin(d);
                Assert(d.StartCalls == 1, "初次准备已开始");
                p.b.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = p.a };
                Assert(SSCRestrictionJobGuard.TryCheck(d, "Finish", out bool allowed) && allowed, "原场景仍应收尾");
                var next = TrainingDriver(p.c, p.b, ritual);
                Assert(!next.TryMakePreToilReservations(false), "新请求使用绑定后的规则");
            }
        });
        Run("3B日常和仪式只检查调教条目", () =>
        {
            foreach (bool ritual in new[] { false, true })
            {
                var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
                p.b.Training.selectedTrainer = p.c; p.b.Training.restrictionConfig.rules.receiveTraining = true;
                Equip(p.b); var d = TrainingDriver(p.c, p.b, ritual); Begin(d);
                Assert(d.StartCalls == 1, "普通接收和装备强制不能覆盖调教许可");
            }
        });
        Run("3B旧开放和公交车不能解除调教限制", () =>
        {
            foreach (bool ritual in new[] { false, true })
            {
                var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master; p.b.Training.selectedTrainer = p.c;
                p.b.IsBus = p.b.Training.AllowsOthersForTrainingOrSex = true;
                var d = TrainingDriver(p.c, p.b, ritual, true);
                Assert(!d.TryMakePreToilReservations(false), "玩家强制不放行旧例外");
                Messages.Count = 0;
                Begin(d);
                Assert(d.Ended && d.StartCalls == 0 && d.SceneEndCalls == 0 && d.CompletedEffects == 0, "拒绝没有初始化或结算副作用");
                Assert(Messages.Count == (ritual ? 0 : 1), "仪式取消提示由真实仪式组件负责，不重复发任务提示");
            }
        });
        Run("3B主人手动发起或主持不受空指派和损坏配置拒绝", () =>
        {
            foreach (bool ritual in new[] { false, true })
            {
                var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
                p.b.Training.restrictionConfig.version = 999; Equip(p.b);
                var d = TrainingDriver(p.a, p.b, ritual, true); Begin(d);
                Assert(d.StartCalls == 1, "实际主人整次通过");
            }
        });
        Run("3B自动主人任务也不能替代指定者，接收端不改变调度方式", () =>
        {
            var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
            p.c.Training.pawnIdentity = PawnIdentity.Master; p.b.Training.selectedTrainer = p.c;
            var d = TrainingDriver(p.a, p.b); Assert(!d.TryMakePreToilReservations(false), "自动只交给指定者");
            var r = new JobDriver_SexBaseReciever { pawn = p.b, job = new Job { playerForced = true, targetA = new LocalTargetInfo { Thing = p.a } } };
            Assert(!r.TryMakePreToilReservations(false), "接收端强制位不能放行自动任务");
        });
        Run("3B行走期间修改许可，接收准备之前拒绝", () =>
        {
            var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.c; p.b.Training.restrictionConfig.rules.receiveTraining = true;
            var d = TrainingDriver(p.c, p.b); d.TryActuallyStartNextToil();
            p.b.Training.restrictionConfig.rules.receiveTraining = false;
            d.TryActuallyStartNextToil(); Rejected(d);
            Assert(d.ReceiverCreations == 0, "没有创建接收任务");
        });
        Run("3B准备期间换指定者，旧调教任务不开始", () =>
        {
            var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.c; p.b.Training.restrictionConfig.rules.receiveTraining = true;
            var d = TrainingDriver(p.c, p.b); d.TryActuallyStartNextToil(); d.TryActuallyStartNextToil();
            p.b.Training.selectedTrainer = p.a;
            d.TryActuallyStartNextToil(); Rejected(d);
            Assert(p.b.jobs.curDriver == null, "释放本请求接收任务");
        });
        Run("3B已开始调教收紧规则和停用资格后仍可收尾", () =>
        {
            var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.c; p.b.Training.restrictionConfig.rules.receiveTraining = true;
            var d = TrainingDriver(p.c, p.b); Begin(d);
            p.b.Training.restrictionConfig.rules.receiveTraining = false; p.c.Training.pawnIdentity = PawnIdentity.Unset;
            Assert(SSCRestrictionJobGuard.TryCheck(d, "Finish", out bool allowed) && allowed, "原场景保留收尾");
            d.End(); Assert(d.SceneEndCalls == 1, "完成原生收尾");
        });
        Run("3B新仪式阶段不能继承上一阶段开始凭据", () =>
        {
            var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.c; p.b.Training.restrictionConfig.rules.receiveTraining = true;
            var d = TrainingDriver(p.c, p.b, true); Begin(d);
            p.b.Training.restrictionConfig.rules.receiveTraining = false;
            Assert(SSCRestrictionJobGuard.TryCheck(d, "FinishPhase", out bool allowed) && allowed, "当前阶段正常结束");
            var next = TrainingDriver(p.c, p.b, true);
            Assert(!next.TryMakePreToilReservations(false), "新阶段重新检查");
            next.TryActuallyStartNextToil();
            Assert(((JobDriver_RitualTraining)next).CancelCalls == 1, "拒绝请求取消整场");
        });
        Run("3B初次准备只授予指定合法主人用途", () =>
        {
            var p = People(); p.b.Chain = null; p.b.Training.pawnIdentity = PawnIdentity.Slave;
            p.c.Training.pawnIdentity = PawnIdentity.Master; p.b.Training.selectedTrainer = p.c;
            var d = TrainingDriver(p.c, p.b, true);
            Assert(SSCRestrictionJobContext.TryCreate(d, out var req) && req.Kind == SSCInteractionKind.BindingPreparation, "识别准备而非主人");
            Begin(d); Assert(d.StartCalls == 1, "默认禁止不锁死初次准备");
        });
        Run("3B准备仪式已有SexProps但未Start，存档恢复仍检查", () =>
        {
            var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.c; p.b.Training.restrictionConfig.rules.receiveTraining = true;
            var d = TrainingDriver(p.c, p.b, true); d.Sexprops = new SexProps { pawn = p.c, partner = p.b };
            Scribe.mode = LoadSaveMode.Saving; d.ExposeData();
            var restored = TrainingDriver(p.c, p.b, true); restored.Sexprops = d.Sexprops;
            Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; restored.ExposeData();
            SSCRestrictionJobGuard.RestoreRitualScene((JobDriver_RitualTraining)restored, false);
            Scribe.mode = LoadSaveMode.Inactive; p.b.Training.restrictionConfig.rules.receiveTraining = false;
            Assert(!restored.TryMakePreToilReservations(false), "准备不能伪装开始");
        });
        Run("3B旧仪式精确Start标记可恢复尚未计时的阶段", () =>
        {
            var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.c;
            var d = (JobDriver_RitualTraining)TrainingDriver(p.c, p.b, true);
            d.Sexprops = new SexProps { pawn = p.c, partner = p.b };
            d.duration = d.ticks_left = 100;
            Scribe.mode = LoadSaveMode.PostLoadInit; SSCRestrictionJobGuard.RestoreRitualScene(d, true);
            Scribe.mode = LoadSaveMode.Inactive;
            Assert(SSCRestrictionJobGuard.TryCheck(d, "Resume", out bool allowed) && allowed, "精确开始证据保留阶段");
        });
        Run("3B升级3A的日常计时场景，即使旧通用记录存在也保留收尾", () =>
        {
            var p = People(); p.c.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.c;
            var d = TrainingDriver(p.c, p.b); d.Sexprops = new SexProps { pawn = p.c, partner = p.b };
            d.duration = 100; d.ticks_left = 80;
            Scribe.node = new Dictionary<string, object> { ["sscRestrictionSceneRecord"] = true };
            Scribe.mode = LoadSaveMode.LoadingVars; d.ExposeData();
            d.ticks_left = d.duration;
            Scribe.mode = LoadSaveMode.PostLoadInit; d.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            Assert(SSCRestrictionJobGuard.TryCheck(d, "Resume", out bool allowed) && allowed, "旧批次未接管不代表场景未开始");
        });
    }
}
