using System;
using SexSlaveCraft;
using Verse.AI;
#if SSC_TEST_WITH_UAP
using Uap = UAP_Animations.UAP_AnimationPositionLockManager;
#endif

internal static partial class Program
{
    /// <summary>通过生产驱动和反射兼容入口验证旧锁误停的修复；无 UAP 构建验证静默跳过路径。</summary>
    private static void RunUapCompatibilityTests()
    {
#if SSC_TEST_WITH_UAP
        // 对照用例证明替身确实会在旧锁残留时停止新动画，避免无效宿主导致假通过。
        Check("control: stale UAP locks stop a replacement animation without ending its job", () =>
        {
            var ritual = new RitualFixture();
            Uap.LockParticipants(ritual.Master, ritual.Slave);
            var execution = new PhaseExecution(ritual);
            Uap.Animating.UnionWith(new[] { ritual.Master, ritual.Slave });
            Uap.CheckStaleGroup(ritual.Master, ritual.Slave);
            Require(!Uap.Animating.Contains(ritual.Master), "control did not reproduce stale animation stop");
            Equal(execution.Driver, ritual.Master.jobs.curDriver, "replacement job after stale cleanup");
        });

        // 模拟旧阶段未清理就恢复或重试；必须在 RJW 启动新动画之前释放双方旧锁。
        Check("ritual start removes stale locks before RJW starts a non-UAP animation", () =>
        {
            var ritual = new RitualFixture();
            Uap.LockParticipants(ritual.Master, ritual.Slave);
            TestWorld.OnRjwStart = driver =>
            {
                Require(!Uap.IsLocked(ritual.Master) && !Uap.IsLocked(ritual.Slave), "stale locks reached RJW Start");
                Uap.Animating.UnionWith(new[] { ritual.Master, ritual.Slave });
            };
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            Uap.CheckStaleGroup(ritual.Master, ritual.Slave);
            Require(Uap.Animating.Contains(ritual.Master) && Uap.Animating.Contains(ritual.Slave), "new animation stopped");
        });

        // 正常完成和中断都必须释放已开始阶段的锁，且不能清理其他场次参与者。
        foreach (JobCondition condition in new[] { JobCondition.Succeeded, JobCondition.InterruptForced })
        {
            Check($"ritual finish releases its locks on {condition} and preserves unrelated locks", () =>
            {
                var ritual = new RitualFixture();
                var outsider = TestWorld.NewPawn();
                Uap.LockParticipants(outsider);
                var execution = new PhaseExecution(ritual);
                execution.StartScene();
                Uap.LockParticipants(ritual.Master, ritual.Slave);
                if (condition == JobCondition.Succeeded) execution.ReachPhaseEnd();
                execution.FinishCurrent(condition);
                Require(!Uap.IsLocked(ritual.Master) && !Uap.IsLocked(ritual.Slave), "finished phase retains locks");
                Require(Uap.IsLocked(outsider), "another scene was unlocked");
            });
        }

        // 新阶段建立的新锁必须保留到阶段结束，不能被兼容清理在启动后立即删除。
        Check("new UAP animation retains its own locks while the ritual phase runs", () =>
        {
            var ritual = new RitualFixture();
            TestWorld.OnRjwStart = driver => Uap.LockParticipants(ritual.Master, ritual.Slave);
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            execution.CurrentToil.tickAction();
            Require(Uap.IsLocked(ritual.Master) && Uap.IsLocked(ritual.Slave), "current phase locks were removed");
        });

        // 同一个仪式的旧阶段回调迟到时，也不能释放新驱动已建立的位置锁。
        Check("late old phase finish preserves locks belonging to the replacement phase", () =>
        {
            var ritual = new RitualFixture();
            var previous = new PhaseExecution(ritual);
            previous.StartScene();
            var current = new PhaseExecution(ritual);
            current.StartScene();
            Uap.LockParticipants(ritual.Master, ritual.Slave);
            previous.FinishCurrent(JobCondition.InterruptForced);
            Require(Uap.IsLocked(ritual.Master) && Uap.IsLocked(ritual.Slave), "late old finish unlocked the new phase");
        });

        // 读档重建 Toil 时不能执行运行期解锁，防止恢复过程改变当前场景状态。
        Check("load-time ritual toil enumeration does not release position locks", () =>
        {
            var ritual = new RitualFixture();
            Uap.LockParticipants(ritual.Master, ritual.Slave);
            var execution = new PhaseExecution(ritual);
            Require(Uap.IsLocked(ritual.Master) && Uap.IsLocked(ritual.Slave), "enumeration changed external state");
        });

        // 解锁必须只改变位置锁，不能直接播放、停止动画，或结束角色任务。
        Check("compatibility unlock preserves current animations and jobs", () =>
        {
            var ritual = new RitualFixture();
            var execution = new PhaseExecution(ritual);
            Uap.LockParticipants(ritual.Master, ritual.Slave);
            Uap.Animating.UnionWith(new[] { ritual.Master, ritual.Slave });
            UapRitualCompatibilityUtility.ReleasePositionLocks(ritual.Master, ritual.Slave);
            Require(Uap.Animating.Contains(ritual.Master) && Uap.Animating.Contains(ritual.Slave), "unlock changed animation state");
            Equal(execution.Driver, ritual.Master.jobs.curDriver, "driver after unlock");
            Equal<JobCondition?>(null, ritual.Master.jobs.EndCondition, "job end request after unlock");
        });

        // 外部 API 异常必须被兼容层隔离，不得使仪式启动或阶段推进报错。
        Check("UAP API exceptions do not break ritual startup or completion", () =>
        {
            var ritual = new RitualFixture();
            Uap.ThrowOnUnlock = true;
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            execution.ReachPhaseEnd();
            execution.FinishCurrent(JobCondition.Succeeded);
            Equal(1, ritual.Comp.ritualPhase, "phase after unavailable UAP cleanup");
        });
#else
        // 此构建完全不包含 UAP 类型，验证真实反射查找失败后仍可完成仪式阶段。
        Check("ritual remains usable without any UAP type loaded", () =>
        {
            var ritual = new RitualFixture();
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            execution.ReachPhaseEnd();
            execution.FinishCurrent(JobCondition.Succeeded);
            Equal(1, ritual.Comp.ritualPhase, "phase without UAP");
        });
#endif
    }
}
