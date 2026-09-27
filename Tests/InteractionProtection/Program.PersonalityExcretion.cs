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
    private sealed class PEFixture
    {
        public Pawn Actor, Victim;
        public JobDriver_PE Initiator;
        public JobDriver_SexBaseReciever Receiver;
    }

    private static PEFixture ExcretionPair(bool forced = false, bool start = true)
    {
        var p = People();
        p.a.Map = p.b.Map = new object();
        p.a.jobs.pawn = p.a;
        p.b.jobs.pawn = p.b;
        var driver = Personality(p.a, p.b);
        driver.job.playerForced = forced;
        driver.TryActuallyStartNextToil();
        driver.TryActuallyStartNextToil();
        var receiver = (JobDriver_SexBaseReciever)p.b.jobs.curDriver;
        receiver.job.def = SSCDefOf.SSC_TrainingReceiver;
        if (start) driver.TryActuallyStartNextToil();
        return new PEFixture { Actor = p.a, Victim = p.b, Initiator = driver, Receiver = receiver };
    }

    private static Job OrdinaryCandidate(string name = "HaulToCell")
        => new Job { def = new JobDef { defName = name, driverClass = typeof(JobDriver) } };

    private static void AssertPEProtected(PEFixture pair, string work, bool constantTree)
    {
        Job candidate = OrdinaryCandidate(work);
        JobMaker.LastReturned = null;
        bool replaced = constantTree ? pair.Victim.jobs.ConstantThinkTreeTick(candidate)
            : pair.Victim.jobs.CheckForJobOverride(candidate);
        Assert(!replaced && pair.Victim.jobs.curDriver == pair.Receiver && !pair.Receiver.Ended,
            "自动候选不能结束或替换人格排泄接收任务");
        Assert(JobMaker.LastReturned == candidate, "未使用的新候选应由原入口归还对象池");
    }

    private static void RunPersonalityExcretionProtectionTests()
    {
        Run("人格排泄保护注册到自动任务替换判断", () =>
        {
            var target = typeof(Pawn_JobTracker).GetMethod("ShouldStartJobFromThinkTree", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(target != null, "必须匹配原版私有入口");
#if REAL_HARMONY
            Assert(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(PersonalityExcretionJobProtection)) == true,
                "实际 PatchAll 必须安装人格排泄后缀");
#else
            var patch = typeof(PersonalityExcretionJobProtection).GetCustomAttribute<HarmonyPatch>();
            Assert(patch.Type == typeof(Pawn_JobTracker) && patch.Method == target.Name, "补丁目标错误");
#endif
        });

        foreach (bool forced in new[] { false, true })
        foreach (string work in new[] { "HaulToCell", "Ingest", "LayDown", "WatchTelevision" })
            Run($"人格排泄 {(forced ? "手动" : "自动")}发起期间阻止 {work} 抢占", () =>
            {
                var pair = ExcretionPair(forced);
                AssertPEProtected(pair, work, false);
                AssertPEProtected(pair, work, true);
                Assert(pair.Initiator.job.playerForced == forced && !pair.Receiver.job.playerForced,
                    "不能通过改写玩家指派标记实现保护");
            });

        Run("人格排泄接收准备完成后即保护，不等待 Start", () =>
        {
            var pair = ExcretionPair(start: false);
            Assert(pair.Initiator.StartCalls == 0, "应处于接收准备和 Start 之间");
            AssertPEProtected(pair, "Ingest", true);
        });
        Run("人格排泄连续自动指派不重启接收任务或倒计时", () =>
        {
            var pair = ExcretionPair();
            pair.Receiver.ticks_left = 123;
            for (int i = 0; i < 40; i++) AssertPEProtected(pair, "Ingest", i % 2 == 0);
            Assert(pair.Receiver.ticks_left == 123 && pair.Initiator.StartCalls == 1
                && pair.Initiator.ReceiverCreations == 1 && pair.Victim.jobs.AutomaticReplacements == 0,
                "不能靠结束后重启恢复场景或重置进度");
        });
        Run("人格排泄阻止队列候选抢占时不回收队列拥有的 Job", () =>
        {
            var pair = ExcretionPair();
            var candidate = OrdinaryCandidate();
            pair.Victim.jobs.jobQueue.Add(new QueuedJob { job = candidate });
            JobMaker.LastReturned = null;
            Assert(!pair.Victim.jobs.CheckForJobOverride(candidate), "队列候选也不能自动抢占");
            Assert(JobMaker.LastReturned == null && pair.Victim.jobs.jobQueue.Single().job == candidate,
                "拒绝候选应保留队列及其 Job 所有权");
        });
        Run("原版拒绝当前 Job 时不回收也不放行", () =>
        {
            var pair = ExcretionPair();
            JobMaker.LastReturned = null;
            Assert(!pair.Victim.jobs.CheckForJobOverride(pair.Receiver.job) && JobMaker.LastReturned == null,
                "不得反转原版 false 或回收当前任务");
        });
        Run("人格排泄正常收尾注销参与者后立即解除保护", () =>
        {
            var pair = ExcretionPair();
            pair.Initiator.TryActuallyStartNextToil();
            Assert(pair.Initiator.SceneEndCalls == 1 && !pair.Receiver.parteners.Contains(pair.Actor), "正常 End 应注销参与者");
            Assert(pair.Victim.jobs.CheckForJobOverride(OrdinaryCandidate()), "正常收尾后应恢复自动任务");
        });
        Run("人格排泄发起任务取消后不残留保护", () =>
        {
            var pair = ExcretionPair();
            pair.Actor.jobs.EndCurrentJob(JobCondition.Incompletable, false);
            Assert(pair.Victim.jobs.ConstantThinkTreeTick(OrdinaryCandidate()), "取消应释放保护");
        });
        Run("玩家直接取消接收任务仍可结束人格排泄接收", () =>
        {
            var pair = ExcretionPair();
            var order = OrdinaryCandidate("Goto");
            Assert(pair.Victim.jobs.TryTakeOrderedJob(order), "普通玩家命令入口应可接受指令");
            pair.Victim.jobs.EndCurrentJob(JobCondition.Incompletable, false);
            Assert(pair.Receiver.Ended && pair.Victim.CurJob == null, "显式结束不能被自动任务保护阻止");
        });
        Run("人格排泄双方状态或配对失效时解除保护", () =>
        {
            Action<PEFixture>[] invalidate =
            {
                p => p.Actor.Dead = true, p => p.Victim.Dead = true,
                p => p.Actor.Destroyed = true, p => p.Victim.Spawned = false,
                p => p.Actor.Drafted = true, p => p.Victim.Drafted = true,
                p => p.Actor.InMentalState = true, p => p.Victim.InMentalState = true,
                p => p.Actor.Map = new object(), p => p.Victim.Map = null,
                p => p.Initiator.job.targetA = new LocalTargetInfo { Thing = new Pawn() },
                p => p.Receiver.parteners.Clear(),
                p => p.Actor.jobs.curDriver = new JobDriver { pawn = p.Actor, job = OrdinaryCandidate() }
            };
            for (int i = 0; i < invalidate.Length; i++)
            {
                var pair = ExcretionPair();
                invalidate[i](pair);
                Assert(pair.Victim.jobs.CheckForJobOverride(OrdinaryCandidate()), "失效配对不得保留保护：" + i);
            }
        });
        Run("仅登记参与者而没有人格排泄任务不保护普通训练", () =>
        {
            var pair = ExcretionPair();
            pair.Actor.jobs.curDriver = new JobDriver_Training { pawn = pair.Actor, job = pair.Initiator.job };
            Assert(pair.Victim.jobs.CheckForJobOverride(OrdinaryCandidate()), "共享接收 JobDef 的普通调教不能被锁定");
        });
        Run("人格排泄不保护发起方或已换掉的接收任务", () =>
        {
            var pair = ExcretionPair();
            Assert(pair.Actor.jobs.CheckForJobOverride(OrdinaryCandidate()), "此保护仅限被执行者");
            pair = ExcretionPair();
            pair.Victim.jobs.curDriver = new JobDriver { pawn = pair.Victim, job = OrdinaryCandidate() };
            Assert(pair.Victim.jobs.ConstantThinkTreeTick(OrdinaryCandidate()), "旧接收驱动不能锁住新任务");
        });
        Run("未支持的 RJW 接收任务不因登记人格排泄参与者而被锁定", () =>
        {
            var pair = ExcretionPair();
            pair.Receiver.job.def = new JobDef { defName = "GettinLoved" };
            Assert(pair.Victim.jobs.CheckForJobOverride(OrdinaryCandidate()), "只保护 SSC 接收任务或已支持的兼容设备任务");
        });
        Run("兼容设备按参与者识别人格排泄并在注销后解除保护", () =>
        {
            var pair = ExcretionPair();
            var receiver = new RJW_Onahole.Jobs.JobDriver_BeOnahole
            {
                pawn = pair.Victim, job = OrdinaryCandidate("BeOnahole")
            };
            receiver.parteners.Add(new Pawn());
            receiver.parteners.Add(pair.Actor);
            pair.Victim.jobs.curDriver = pair.Receiver = receiver;
            AssertPEProtected(pair, "Ingest", true);
            receiver.parteners.Remove(pair.Actor);
            Assert(pair.Victim.jobs.CheckForJobOverride(OrdinaryCandidate()), "不能因设备中其他参与者残留而锁住目标");
        });
        Run("人格排泄保护不依赖身份、限制开关或新增存档标记", () =>
        {
            var pair = ExcretionPair();
            pair.Victim.Chain = null;
            pair.Victim.Training = null;
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            Scribe.mode = LoadSaveMode.PostLoadInit;
            AssertPEProtected(pair, "LayDown", false);
        });
    }
}
