using System;
using System.Linq;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    // 运行真实统一许可、事件交接及准备归属生产层，不借事件身份赋予许可。
    private static void RunPetAffectionEventTests()
    {
        Run("亲昵事件枚举仅尾部追加，旧存档数字保持稳定", () =>
        {
            Assert((int)SSCRestrictionEvent.None == 0 && (int)SSCRestrictionEvent.TradeConsensual == 1 &&
                (int)SSCRestrictionEvent.TradeForced == 2 && (int)SSCRestrictionEvent.Dog == 3 &&
                (int)SSCRestrictionEvent.PetAffection == 4, "事件来源数字发生兼容性变化");
        });
        Run("亲昵后续以宠物发起方向检查许可，事件来源不绕过主动拒绝", () =>
        {
            var p = People(); p.b.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            var d = Driver(p.b, p.a, false); d.job.CachedDriver = d;
            Assert(!SSCRestrictionJobGuard.PrepareEvent(p.b, d.job, SSCRestrictionEvent.PetAffection), "反向拒绝应生效");
            Assert(Messages.Count == 0 && p.a.CurJob == null, "拒绝预检不能提示发生或创建目标任务");
        });
        Run("亲昵事件运行驱动领取精确等待，缓存和错误对象不能认领", () =>
        {
            var p = People(); var job = EventJob(p.b);
            Assert(SSCRestrictionJobGuard.PrepareEvent(p.a, job, SSCRestrictionEvent.PetAffection), "预检失败");
            var cached = (JobDriver_Sex)job.GetCachedDriver(p.a);
            var wait = new Job { def = JobDefOf.Wait }; p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            SSCRestrictionJobGuard.RegisterEventWait(p.a, job, p.b, wait);
            Assert(!SSCRestrictionJobGuard.OwnsPreparedJob(cached, p.b, wait), "缓存驱动不应提前领取");
            var actual = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a); p.a.jobs.curDriver = actual;
            Assert(SSCRestrictionJobGuard.OwnsPreparedJob(actual, p.b, wait), "实际驱动未领取等待");
            Assert(!SSCRestrictionJobGuard.OwnsPreparedJob(actual, p.c, wait) &&
                !SSCRestrictionJobGuard.OwnsPreparedJob(actual, p.b, new Job { def = JobDefOf.Wait }), "对象和编号必须精确");
            actual.Cleanup(JobCondition.Incompletable);
            Assert(p.b.CurJob == null && Messages.Count == 0, "尚未开始的取消应清等待且不提示发生");
        });
        Run("亲昵事件读档后保留精确等待归属，取消不删除后来同类型任务", () =>
        {
            var p = People(); var d = Driver(p.a, p.b, false); d.job.CachedDriver = d;
            SSCRestrictionJobGuard.PrepareEvent(p.a, d.job, SSCRestrictionEvent.PetAffection);
            var wait = new Job { def = JobDefOf.Wait }; p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            SSCRestrictionJobGuard.RegisterEventWait(p.a, d.job, p.b, wait);
            Assert(SSCRestrictionJobGuard.OwnsPreparedJob(d, p.b, wait), "未接管等待");
            var restored = Driver(p.a, p.b, false); Restore(d, restored);
            Assert(SSCRestrictionJobGuard.OwnsPreparedJob(restored, p.b, wait), "归属存档丢失");
            var unrelated = new Job { def = JobDefOf.Wait };
            p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = unrelated };
            restored.Cleanup(JobCondition.Incompletable);
            Assert(p.b.CurJob == unrelated && Messages.Count == 0, "清理误删了后来任务或发布假开始");
        });
        Run("亲昵事件取消Pending后同一Job启动不继承通知和等待", () =>
        {
            var p = People(); var job = EventJob(p.b);
            SSCRestrictionJobGuard.PrepareEvent(p.a, job, SSCRestrictionEvent.PetAffection);
            var wait = new Job { def = JobDefOf.Wait }; p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            SSCRestrictionJobGuard.RegisterEventWait(p.a, job, p.b, wait);
            SSCRestrictionJobGuard.CancelPendingEvent(job);
            var actual = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a); p.a.jobs.curDriver = actual;
            Assert(!SSCRestrictionJobGuard.OwnsPreparedJob(actual, p.b, wait), "取消事件仍被领取");
            actual.MakeScenarioToils(); Begin(actual);
            Assert(actual.StartCalls == 1 && Messages.Count == 0, "取消事件身份不能产生旧通知");
        });
        Run("原准备init先取消发起者后晚建移动等待仍清理精确新任务", () =>
        {
            var p = People(); var job = EventJob(p.b); var driver = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a);
            p.a.jobs.curDriver = driver; Job unrelated = new() { def = JobDefOf.Wait }; p.b.jobs.jobQueue.Add(new() { job = unrelated });
            Job lateMovement = new() { def = JobDefOf.Goto }, lateWait = new() { def = JobDefOf.Wait };
            var toils = SSCRestrictionJobGuard.TrackPreparation(driver, new[] { new Toil { initAction = () =>
            {
                driver.EndJobWith(JobCondition.Incompletable);
                p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = lateMovement };
                p.b.jobs.jobQueue.Add(new() { job = lateWait });
            } } });
            toils.Single().initAction();
            Assert(p.b.CurJob == null && p.b.jobs.jobQueue.Single().job == unrelated && Messages.Count == 0,
                "同步取消后晚建的准备泄漏或误删原队列");
        });
        Run("原准备init新增玩家强制移动等待不得认领或清理", () =>
        {
            var p = People(); var job = EventJob(p.b); var driver = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a); p.a.jobs.curDriver = driver;
            Job movement = new() { def = JobDefOf.Goto, playerForced = true }, wait = new() { def = JobDefOf.Wait, playerForced = true };
            var toils = SSCRestrictionJobGuard.TrackPreparation(driver, new[] { new Toil { initAction = () =>
            {
                p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = movement };
                p.b.jobs.jobQueue.Add(new() { job = wait });
            } } });
            toils.Single().initAction();
            Assert(!SSCRestrictionJobGuard.OwnsPreparedJob(driver, p.b, movement) && !SSCRestrictionJobGuard.OwnsPreparedJob(driver, p.b, wait),
                "玩家命令被错误认领");
            driver.Cleanup(JobCondition.Incompletable);
            Assert(p.b.CurJob == movement && p.b.jobs.jobQueue.Single().job == wait, "准备清理删除了新玩家命令");
        });
        Run("原准备init安装后异常仍传播并在cleanup精确释放新增准备", () =>
        {
            var p = People(); var job = EventJob(p.b); var driver = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a); p.a.jobs.curDriver = driver;
            Job originalIdle = new() { def = JobDefOf.Wait }, unrelated = new() { def = JobDefOf.Goto };
            p.b.jobs.jobQueue.Add(new() { job = originalIdle }); p.b.jobs.jobQueue.Add(new() { job = unrelated });
            Job movement = new() { def = JobDefOf.Goto }, wait = new() { def = JobDefOf.Wait }; var original = new InvalidOperationException("native preparation");
            var toils = SSCRestrictionJobGuard.TrackPreparation(driver, new[] { new Toil { initAction = () =>
            {
                p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = movement }; p.b.jobs.jobQueue.Add(new() { job = wait }); throw original;
            } } });
            Exception observed = null; try { toils.Single().initAction(); } catch (Exception error) { observed = error; }
            Assert(ReferenceEquals(observed, original) && SSCRestrictionJobGuard.OwnsPreparedJob(driver, p.b, movement), "异常被吞或新增准备未登记");
            driver.Cleanup(JobCondition.Incompletable);
            Assert(p.b.CurJob == null && p.b.jobs.jobQueue.Select(queued => queued.job).SequenceEqual(new[] { originalIdle, unrelated }),
                "异常清理泄漏新增等待或删除原队列");
        });
    }
}
