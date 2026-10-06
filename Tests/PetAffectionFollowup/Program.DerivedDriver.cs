using System;
using System.Linq;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static (Pawn pet, Pawn master, Job waiting, JobDriver_PetAffectionFollowup driver, Toil[] toils) Derived()
    {
        var p = Pair();
        var job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffectionFollowup, p.master);
        var driver = new JobDriver_PetAffectionFollowup { pawn = p.pet, job = job };
        p.pet.jobs.curJob = job; p.pet.jobs.curDriver = driver;
        SSCRestrictionJobGuard.OwnedPreparation.Add(p.waiting);
        return (p.pet, p.master, p.waiting, driver, driver.BuildToils());
    }
    private static void InstallReceiver(Pawn pet, Pawn master)
    {
        var receiving = new Job { def = xxx.getting_quickie };
        var receiver = new JobDriver_SexBaseReciever { pawn = master, job = receiving, Partner = pet };
        receiver.parteners.Add(pet); master.jobs.curJob = receiving; master.jobs.curDriver = receiver;
    }
    private static void RunDerivedCases()
    {
        Run("后续驱动完整继承Quickie且预约检查资格", () =>
        {
            var p = Derived();
            Assert(p.driver is JobDriver_SexQuick && p.toils.Length == 2 && p.driver.TryMakePreToilReservations(false), "未复用基类或合法预约失败");
            p.master.Adult = false;
            Assert(!p.driver.TryMakePreToilReservations(false), "预约未拒绝未成年");
        });
        Run("后续准备前换主人使每个原版初始化停止", () =>
        {
            var p = Derived(); int calls = 0; JobDriver_SexQuick.NativeQuickieInit = () => calls++;
            p.pet.BoundMaster = new Pawn();
            Assert(p.driver.FailureConditions.Any(check => check()), "全局条件未复查绑定");
            p.toils[0].initAction();
            Assert(p.driver.EndCondition == JobCondition.Incompletable && calls == 0, "无效准备仍进入原版初始化");
        });
        Run("后续准备不接受主人同类型外来等待或新工作", () =>
        {
            foreach (JobDef def in new[] { JobDefOf.Wait, new JobDef { defName = "HaulToCell" } })
            {
                Reset(); var p = Derived(); int calls = 0; JobDriver_SexQuick.NativeQuickieInit = () => calls++;
                p.master.jobs.curJob = new Job { def = def };
                p.toils[0].initAction();
                Assert(calls == 0 && p.driver.EndCondition == JobCondition.Incompletable && p.master.CurJob != null,
                    "外来主人任务被当作事件等待");
            }
        });
        Run("后续准备拒绝玩家新命令与重要队列并保留原队列", () =>
        {
            foreach (bool forced in new[] { false, true })
            {
                Reset(); var p = Derived();
                Job pending = new() { def = forced ? JobDefOf.Wait_Wander : new JobDef { defName = "Cook" }, playerForced = forced };
                p.master.jobs.jobQueue.EnqueueLast(pending);
                p.toils[0].initAction();
                Assert(p.driver.EndCondition == JobCondition.Incompletable && p.master.jobs.jobQueue.Single().job == pending,
                    "新增重要队列未取消或被清空");
            }
        });
        Run("原版地点初始化清队列时保留ForceWait挂起的空闲Job及tag", () =>
        {
            var p = Pair(); Job idle = new() { def = JobDefOf.Wait_Wander }; p.master.jobs.jobQueue.EnqueueLast(idle, JobTag.Misc);
            var job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffectionFollowup, p.master);
            var driver = new JobDriver_PetAffectionFollowup { pawn = p.pet, job = job };
            p.pet.jobs.curJob = job; p.pet.jobs.curDriver = driver; SSCRestrictionJobGuard.OwnedPreparation.Add(p.waiting);
            Toil[] toils = driver.BuildToils();
            JobDriver_SexQuick.NativeQuickieInit = () =>
            {
                Assert(p.master.jobs.jobQueue.Count == 0, "原闲置队列未隔离");
                p.master.jobs.jobQueue.Clear(); InstallReceiver(p.pet, p.master);
            };
            toils[0].initAction();
            Assert(p.master.jobs.jobQueue.Single().job == idle && p.master.jobs.jobQueue.Single().tag == JobTag.Misc,
                "原队列引用/tag未恢复");
            Assert(!driver.FailureConditions.Any(check => check()), "接收已创建后有效准备被拒绝");
        });
        Run("原版初始化抛异常仍恢复原空闲队列并精确回收新接收任务", () =>
        {
            var p = Pair(); Job idle = new() { def = JobDefOf.Wait_Wander }; p.master.jobs.jobQueue.EnqueueLast(idle);
            var job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffectionFollowup, p.master);
            var driver = new JobDriver_PetAffectionFollowup { pawn = p.pet, job = job };
            p.pet.jobs.curJob = job; p.pet.jobs.curDriver = driver; SSCRestrictionJobGuard.OwnedPreparation.Add(p.waiting);
            var toils = driver.BuildToils(); var original = new InvalidOperationException("native callback");
            JobDriver_SexQuick.NativeQuickieInit = () => { InstallReceiver(p.pet, p.master); throw original; };
            Exception observed = null; try { toils[0].initAction(); } catch (Exception error) { observed = error; }
            Assert(ReferenceEquals(observed, original) && p.master.jobs.jobQueue.Single().job == idle, "异常吞没或原队列丢失");
            driver.NotifyFinish(JobCondition.Incompletable);
            Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "未开始的接收任务残留");
        });
        Run("后续取消精确回收本次新接收，重复清理不重复结束", () =>
        {
            var p = Derived(); JobDriver_SexQuick.NativeQuickieInit = () => InstallReceiver(p.pet, p.master);
            p.toils[0].initAction(); p.driver.NotifyFinish(JobCondition.Incompletable); p.driver.NotifyFinish(JobCondition.Incompletable);
            Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "取消接收泄漏或重复清理");
        });
        Run("后续取消保留后来替换的同类型接收及其他参与者", () =>
        {
            foreach (bool replaced in new[] { false, true })
            {
                Reset(); var p = Derived(); JobDriver_SexQuick.NativeQuickieInit = () => InstallReceiver(p.pet, p.master);
                p.toils[0].initAction();
                if (replaced) InstallReceiver(p.pet, p.master);
                else ((JobDriver_SexBaseReciever)p.master.jobs.curDriver).parteners.Add(new Pawn());
                Job preserved = p.master.CurJob; p.driver.NotifyFinish(JobCondition.Incompletable);
                Assert(p.master.CurJob == preserved && p.master.jobs.EndCalls == 0, "清理影响后来场景/其他参与者");
            }
        });
        Run("后续已真实开始后绑定与许可外资格变化不拦收尾", () =>
        {
            var p = Derived(); int calls = 0; JobDriver_SexQuick.NativeQuickieInit = () => calls++;
            SSCRestrictionJobGuard.Started = true; p.pet.BoundMaster = null; p.master.Dead = true;
            Assert(!p.driver.FailureConditions.Any(check => check()), "已开始场景仍被资格条件中断");
            p.toils[0].initAction(); p.driver.NotifyFinish(JobCondition.Incompletable);
            Assert(calls == 1 && p.master.CurJob == p.waiting, "已开始场景原初始化/收尾未保留");
        });
        Run("后续迟到原版回调不能改变新任务", () =>
        {
            var p = Derived(); int calls = 0; JobDriver_SexQuick.NativeQuickieInit = () => calls++;
            var other = new Job(); p.pet.jobs.curJob = other; p.pet.jobs.curDriver = null;
            p.toils[0].initAction();
            Assert(calls == 0 && p.pet.CurJob == other && p.master.CurJob == p.waiting, "迟到驱动调用污染新任务");
        });
        Run("后续保存恢复空闲队列编号与本次接收编号不再次初始化", () =>
        {
            var p = Derived(); JobDriver_SexQuick.NativeQuickieInit = () => InstallReceiver(p.pet, p.master); p.toils[0].initAction();
            Scribe.mode = LoadSaveMode.Saving; p.driver.ExposeData();
            var loaded = new JobDriver_PetAffectionFollowup { pawn = p.pet, job = p.driver.job };
            p.pet.jobs.curDriver = loaded;
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData(); Scribe.mode = LoadSaveMode.PostLoadInit; loaded.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            loaded.BuildToils(); Assert(!loaded.FailureConditions.Any(check => check()), "读档后精确准备凭据丢失");
            loaded.NotifyFinish(JobCondition.Incompletable);
            Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1, "读档后的接收清理失败");
        });
        Run("实际Start拒绝准备Wait直入且仅作用于新派生驱动", () =>
        {
            var p = Derived(); p.driver.Start();
            Assert(p.driver.NativeStartCalls == 0 && p.driver.EndCondition == JobCondition.Incompletable, "尚未创建接收仍实际Start");
            var ordinary = new JobDriver_SexBaseInitiator(); ordinary.Start(); ordinary.End();
            Assert(ordinary.NativeStartCalls == 1 && ordinary.NativeEndCalls == 1, "专属Hook影响了其他RJW驱动");
        });
        Run("实际Start拒绝新失格与错误接收归属，阻止原init后续语句", () =>
        {
            foreach (int kind in Enumerable.Range(0, 4))
            {
                Reset(); var p = Derived(); int afterStart = 0;
                JobDriver_SexQuick.NativeQuickieInit = () => InstallReceiver(p.pet, p.master); p.toils[0].initAction();
                JobDriver_SexQuick.NativeQuickieSecondInit = () =>
                {
                    if (kind == 0) p.pet.BoundMaster = null;
                    if (kind == 1) p.master.AdultCategory = false;
                    if (kind == 2) p.master.jobs.jobQueue.EnqueueLast(new Job { def = JobDefOf.Wait_Wander, playerForced = true });
                    if (kind == 3) InstallReceiver(p.pet, p.master);
                    p.driver.Start(); afterStart++;
                };
                p.toils[1].initAction();
                Assert(p.driver.NativeStartCalls == 0 && afterStart == 0 && p.driver.EndCondition == JobCondition.Incompletable,
                    "Start拒绝后原init还继续运行 " + kind);
            }
        });
        Run("更早共享前缀拒绝Start同样中止原init，未开始End不读空props", () =>
        {
            var p = Derived(); int afterStart = 0;
            JobDriver_SexQuick.NativeQuickieInit = () => InstallReceiver(p.pet, p.master); p.toils[0].initAction();
            JobDriver_SexBaseInitiator.SharedStartAllowed = false;
            JobDriver_SexQuick.NativeQuickieSecondInit = () => { p.driver.Start(); afterStart++; };
            p.toils[1].initAction(); p.driver.End(); p.driver.NotifyFinish(JobCondition.Incompletable);
            Assert(p.driver.NativeStartCalls == 0 && afterStart == 0 && p.driver.NativeEndCalls == 0 && p.master.CurJob == null,
                "其他前缀拒绝后仍执行原init或End");
        });
        Run("原idle队列临时保管时合法Start放行，开始后的End照常执行", () =>
        {
            var p = Pair(); Job idle = new() { def = JobDefOf.Wait_Wander }; p.master.jobs.jobQueue.EnqueueLast(idle);
            var job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffectionFollowup, p.master);
            var driver = new JobDriver_PetAffectionFollowup { pawn = p.pet, job = job }; p.pet.jobs.curJob = job; p.pet.jobs.curDriver = driver;
            SSCRestrictionJobGuard.OwnedPreparation.Add(p.waiting); var toils = driver.BuildToils();
            JobDriver_SexQuick.NativeQuickieInit = () => InstallReceiver(p.pet, p.master); toils[0].initAction();
            int afterStart = 0; JobDriver_SexQuick.NativeQuickieSecondInit = () => { driver.Start(); afterStart++; }; toils[1].initAction(); driver.End();
            Assert(driver.NativeStartCalls == 1 && afterStart == 1 && driver.NativeEndCalls == 1 && p.master.jobs.jobQueue.Single().job == idle,
                "临时idle队列误拒绝合法Start或正常收尾");
        });
        Run("实际Start迟到旧驱动回调只拒绝自身而不结束新工作", () =>
        {
            var p = Derived(); var replacement = new Job(); p.pet.jobs.curJob = replacement; p.pet.jobs.curDriver = null;
            p.driver.Start();
            Assert(p.driver.NativeStartCalls == 0 && p.driver.EndCondition == null && p.pet.CurJob == replacement,
                "迟到Start影响新任务");
        });
        Run("后续资格查询重入替换宠物工作时不进入原版初始化", () =>
        {
            var p = Derived(); var replacement = new Job(); int calls = 0;
            JobDriver_SexQuick.NativeQuickieInit = () => calls++;
            CasualSex_Helper.OnCanHaveSex = () => { p.pet.jobs.curJob = replacement; p.pet.jobs.curDriver = null; };
            p.toils[0].initAction();
            Assert(calls == 0 && p.pet.CurJob == replacement, "资格回调换工作后仍执行旧init");
        });
        Run("后续准备期间宠物新增玩家队列立即取消并保留命令", () =>
        {
            var p = Derived(); Job command = new() { playerForced = true }; p.pet.jobs.jobQueue.EnqueueLast(command);
            int calls = 0; JobDriver_SexQuick.NativeQuickieInit = () => calls++;
            p.toils[0].initAction();
            Assert(calls == 0 && p.driver.EndCondition == JobCondition.Incompletable && p.pet.jobs.jobQueue.Single().job == command,
                "宠物新增队列被忽略或清空");
        });
    }
}
