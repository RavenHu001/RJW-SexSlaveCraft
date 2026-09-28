using System;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>调用真实任务驱动和交接工具，在引擎边界注入工作切换并验证旧回调不越权。</summary>
    private static void RunReceiverHandoffTests()
    {
        // 正常不同格交接必须保留发起工作和占用，并将接收任务准确指向原调教员。
        Check("position synchronization preserves the active daily job", () =>
        {
            var f = Daily();
            Pawn actor = f.driver.pawn;
            Pawn target = (Pawn)f.driver.job.targetA.Thing;
            target.Position = new IntVec3(5, 0, 0);
            f.toils[0].initAction();
            f.toils[2].initAction();
            Require(actor.jobs.EndCondition == null, "teleport ended the training job");
            Equal(f.driver, actor.jobs.curDriver, "initiator driver");
            Require(f.comp.isBeingTrained, "training occupancy lost");
            Equal(actor, target.CurJob.targetA.Thing, "receiver actor");
        });
        // 分别模拟新建加工 Job 和回池后复用原对象；两条路径都必须保留加工点预约及新占用。
        foreach (bool reuseJob in new[] { false, true })
        {
            Check("handoff preserves replacement work and reservation; pooled=" + reuseJob, () =>
            {
                var f = Daily();
                Pawn actor = f.driver.pawn;
                Pawn target = (Pawn)f.driver.job.targetA.Thing;
                Pawn workbench = TestWorld.NewPawn();
                target.Position = new IntVec3(5, 0, 0);
                Job replacement = null;
                actor.OnTeleport = () =>
                {
                    // 显式模拟引擎先清理旧调教，再安装加工工作；不在替身中复制生产防护逻辑。
                    actor.Map.reservationManager.Release(f.driver.job.targetA, actor, f.driver.job);
                    f.driver.Finish(JobCondition.InterruptForced);
                    replacement = reuseJob ? f.driver.job : new Job();
                    replacement.loadID += 10000; // 引擎从池中取回同一对象时分配新编号。
                    replacement.def = new JobDef { defName = "DoBill" };
                    replacement.targetA = new LocalTargetInfo(workbench);
                    actor.jobs.curJob = replacement;
                    actor.jobs.curDriver = new JobDriver_Training { pawn = actor, job = replacement };
                    actor.jobs.EndCondition = null;
                    actor.Map.reservationManager.Reserve(workbench, actor, replacement);
                    f.comp.isBeingTrained = true; // 后来建立的占用不属于旧回调。
                };
                f.toils[0].initAction();
                f.toils[2].initAction();
                Equal(replacement, actor.CurJob, "replacement work");
                Require(actor.jobs.EndCondition == null, "old callback ended replacement work");
                Require(actor.Map.reservationManager.ReservedBy(new LocalTargetInfo(workbench), actor, replacement), "workbench reservation released");
                Equal(0, target.jobs.StartCalls, "receiver started after actor replacement");
                Require(f.comp.isBeingTrained, "replacement occupancy cleared");
                Equal(-999999, f.comp.lastFailedTrainingValidationTick, "stale failure cooldown");
            });
        }
        // 接收器已安装但发起方随后换工作时，只回收本次孤立接收器，不能结束新加工工作。
        Check("replacement during receiver startup removes only the orphan receiver", () =>
        {
            var f = Daily();
            Pawn actor = f.driver.pawn;
            Pawn target = (Pawn)f.driver.job.targetA.Thing;
            target.jobs.AfterStartJob = () =>
            {
                f.driver.Finish(JobCondition.InterruptForced);
                var next = new Job { def = new JobDef { defName = "DoBill" } };
                actor.jobs.curJob = next;
                actor.jobs.curDriver = new JobDriver_Training { pawn = actor, job = next };
                f.comp.isBeingTrained = true;
            };
            f.toils[0].initAction();
            f.toils[2].initAction();
            Require(actor.jobs.EndCondition == null, "replacement actor job ended");
            Require(target.CurJob == null, "orphan receiver survived");
            Require(f.comp.isBeingTrained, "new occupancy cleared");
        });
        // 首次交接成功后仍可能在场景初始化的第二次同步中失效，旧回调不得再调用 RJW Start。
        Check("second scene synchronization stops after actor replacement", () =>
        {
            var f = Daily();
            Pawn actor = f.driver.pawn;
            Pawn target = (Pawn)f.driver.job.targetA.Thing;
            f.toils[0].initAction(); f.toils[2].initAction();
            target.Position = new IntVec3(5, 0, 0);
            actor.OnTeleport = () =>
            {
                actor.jobs.curJob = new Job();
                actor.jobs.curDriver = new JobDriver_Training { pawn = actor, job = actor.CurJob };
            };
            int starts = 0;
            TestWorld.OnRjwStart = _ => starts++;
            f.toils[3].initAction();
            Equal(0, starts, "stale scene started");
        });
        // 保留所有引用只改变编号，单独证明归属判断不只依赖引用相等。
        Check("job id detects reuse even when the driver and object references survive", () =>
        {
            var f = Daily();
            Pawn actor = f.driver.pawn;
            Pawn target = (Pawn)f.driver.job.targetA.Thing;
            target.Position = new IntVec3(5, 0, 0);
            actor.OnTeleport = () => f.driver.job.loadID++;
            f.toils[2].initAction();
            Equal(0, target.jobs.StartCalls, "receiver started for reused job");
            Equal(0, actor.Map.reservationManager.ReleaseCalls, "reused job reservation released");
            Require(actor.jobs.EndCondition == null, "reused job ended");
        });
        // 接收侧也可对象复用；旧启动回调不能把同一对象上的新等待任务作为孤立接收器删除。
        Check("orphan cleanup preserves a pooled replacement receiver job", () =>
        {
            var f = Daily();
            Pawn actor = f.driver.pawn;
            Pawn target = (Pawn)f.driver.job.targetA.Thing;
            target.jobs.AfterStartJob = () =>
            {
                actor.jobs.curDriver = null;
                actor.jobs.curJob = null;
                // 相同接收 Job 对象已经被后续任务复用，不能因引用相同而结束它。
                target.CurJob.loadID++;
                target.CurJob.def = new JobDef { defName = "Wait" };
            };
            f.toils[2].initAction();
            Equal("Wait", target.CurJobDef.defName, "replacement receiver preserved");
            Require(target.jobs.EndCondition == null, "replacement receiver ended");
        });
        // 接收任务名称相同并不足以复用；错误配对须在移动角色或释放预约前被拒绝。
        Check("handoff does not reuse another trainer's receiver", () =>
        {
            var f = Daily();
            Pawn target = (Pawn)f.driver.job.targetA.Thing;
            Pawn other = TestWorld.NewPawn();
            target.Position = new IntVec3(5, 0, 0);
            IntVec3 originalPosition = f.driver.pawn.Position;
            Job existing = JobMaker.MakeJob(SSCDefOf.SSC_TrainingReceiver, other);
            target.jobs.StartJob(existing, JobCondition.InterruptForced);
            Require(!TrainingJobUtility.TryStartDailyTrainingReceiver(f.driver.pawn, target, f.driver.job, SSCDefOf.SSC_TrainingReceiver), "wrong pair reused");
            Equal(existing, target.CurJob, "existing receiver preserved");
            Equal(1, target.jobs.StartCalls, "existing receiver restarted");
            Equal(originalPosition, f.driver.pawn.Position, "unrelated actor moved");
            Equal(0, f.driver.pawn.Map.reservationManager.ReleaseCalls, "reservation changed for wrong pair");
        });
        // 仪式会同时移动主持者和目标；双方任务应保持到生产代码显式启动接收器为止。
        Check("ritual position synchronization preserves both jobs until explicit handoff", () =>
        {
            var f = new RitualFixture();
            var execution = new PhaseExecution(f);
            f.Master.Position = new IntVec3(10, 0, 0);
            f.Slave.Position = new IntVec3(20, 0, 0);
            f.Slave.jobs.curJob = new Job { def = new JobDef { defName = "Wait" } };
            f.Slave.jobs.curDriver = new JobDriver_Training { pawn = f.Slave, job = f.Slave.CurJob };
            execution.StartScene();
            Require(f.Master.jobs.EndCondition == null && f.Slave.jobs.EndCondition == null, "teleport ended ritual participant job");
            Equal(SSCDefOf.SSC_TrainingReceiver, f.Slave.CurJobDef, "ritual receiver");
            Equal(f.Master.Position, f.Slave.Position, "ritual positions");
        });
        // 在移动第一端的通知内切换主持者工作，验证同步不会继续移动第二端或创建接收器。
        Check("ritual stops synchronization when receiver teleport replaces the actor job", () =>
        {
            var f = new RitualFixture();
            var execution = new PhaseExecution(f);
            IntVec3 originalPosition = new IntVec3(10, 0, 0);
            f.Master.Position = originalPosition;
            f.Slave.Position = new IntVec3(20, 0, 0);
            f.Slave.OnTeleport = () =>
            {
                f.Master.jobs.curJob = new Job();
                f.Master.jobs.curDriver = new JobDriver_Training { pawn = f.Master, job = f.Master.CurJob };
            };
            execution.Toils[1].initAction();
            Equal(originalPosition, f.Master.Position, "replacement actor was moved");
            Equal(0, f.Slave.jobs.StartCalls, "stale ritual receiver started");
            Require(f.Master.jobs.EndCondition == null, "replacement actor ended");
        });
        // 人格排泄复用相同交接工具，旧回调同样不得结束新工作或写入目标失败冷却。
        Check("PE handoff exits when teleport replaces its actor job", () =>
        {
            var f = new PEExecution();
            f.Target.Position = new IntVec3(5, 0, 0);
            f.Actor.OnTeleport = () =>
            {
                f.Actor.jobs.curJob = new Job();
                f.Actor.jobs.curDriver = new JobDriver_Training { pawn = f.Actor, job = f.Actor.CurJob };
            };
            f.Toils[1].initAction();
            Require(f.Actor.jobs.EndCondition == null, "replacement job ended by PE");
            Equal(0, f.Target.jobs.StartCalls, "stale PE receiver started");
            Equal(-999999, f.Target.TryGetComp<CompSexSlaveTraining>().lastFailedTrainingValidationTick, "stale PE cooldown");
        });
        // 对照家具正常登记与登记中途取消：两者都保留家具任务，仅失败场景撤销本次新增关系。
        foreach (bool interrupt in new[] { false, true })
        {
            Check("furniture receiver survives handoff; interrupt=" + interrupt, () =>
            {
                var f = Daily();
                Pawn actor = f.driver.pawn;
                Pawn target = (Pawn)f.driver.job.targetA.Thing;
                target.Position = new IntVec3(5, 0, 0);
                var furniture = new TestFurnitureReceiver { pawn = target, job = new Job() };
                target.jobs.curJob = furniture.job;
                target.jobs.curDriver = furniture;
                if (interrupt) furniture.OnRegister = () =>
                {
                    actor.jobs.curJob = new Job();
                    actor.jobs.curDriver = new JobDriver_Training { pawn = actor, job = actor.CurJob };
                };
                f.toils[2].initAction();
                Equal(furniture, target.jobs.curDriver, "furniture receiver replaced");
                Require(target.jobs.EndCondition == null && actor.jobs.EndCondition == null, "participant job ended");
                Equal(!interrupt, furniture.parteners.Contains(actor), "furniture registration");
                Equal(0, target.jobs.StartCalls, "standard receiver created on furniture");
            });
        }
    }
}
