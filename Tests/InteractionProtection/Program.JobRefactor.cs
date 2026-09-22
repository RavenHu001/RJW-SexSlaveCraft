using System;
using System.Linq;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    // 这些类型只模拟第三方版本变化；签名是否支持和诊断是否去重均由生产发现器判断。
    private sealed class ChangedCompatibilityDriver : JobDriver
    {
        /// <summary>模拟参数形状不变但返回类型已经改变的第三方入口。</summary>
        public void ChangedReturn(bool value) { }
        /// <summary>模拟方法由实例入口改成静态入口的第三方版本。</summary>
        public static bool ChangedStatic(bool value) => true;
        /// <summary>模拟仍存在同名方法，但参数已经改变的第三方入口。</summary>
        public bool ChangedParameters(int value) => true;
        /// <summary>提供完整匹配的入口，确保诊断器不会误拒绝可用版本。</summary>
        public bool Matching(bool value) => true;
    }

    private sealed class ChangedCompatibilityBase
    {
        /// <summary>模拟不再继承已核对游戏基类的第三方同名方法。</summary>
        public bool Matching(bool value) => true;
    }

    /// <summary>执行职责抽离后的存档兼容、事件归属及兼容诊断边界，不模拟或替代生产守卫判定。</summary>
    private static void RunJobRefactorTests()
    {
        Run("兼容类型缺席安静跳过，重复发现不产生警告", () =>
        {
            int before = SSCLog.Warnings.Count;
            for (int i = 0; i < 2; i++)
                Assert(SSCRestrictionCompatibilityDiagnostics.OptionalMethod("SSC.Tests.AbsentOptionalDriver", typeof(JobDriver),
                    "TryMakePreToilReservations", typeof(bool), typeof(bool)) == null, "缺席不能产生补丁目标");
            Assert(SSCLog.Warnings.Count == before, "没有安装可选模组不是故障");
        });
        Run("兼容类型存在但参数返回类型或继承改变时仅诊断一次", () =>
        {
            int before = SSCLog.Warnings.Count;
            foreach (string name in new[] { "ChangedReturn", "ChangedStatic", "ChangedParameters" })
                for (int i = 0; i < 2; i++)
                    Assert(SSCRestrictionCompatibilityDiagnostics.OptionalMethod(typeof(ChangedCompatibilityDriver).FullName,
                        typeof(JobDriver), name, typeof(bool), typeof(bool)) == null, "不匹配不得尝试安装补丁");
            for (int i = 0; i < 2; i++)
                Assert(SSCRestrictionCompatibilityDiagnostics.OptionalMethod(typeof(ChangedCompatibilityBase).FullName,
                    typeof(JobDriver), "Matching", typeof(bool), typeof(bool)) == null, "错误基类不得用兼容前缀");
            Assert(SSCLog.Warnings.Count == before + 4, "每个改变入口仅一条诊断");
        });
        Run("完整兼容签名仍返回声明方法且没有告警", () =>
        {
            int before = SSCLog.Warnings.Count;
            var method = SSCRestrictionCompatibilityDiagnostics.OptionalMethod(typeof(ChangedCompatibilityDriver).FullName,
                typeof(JobDriver), "Matching", typeof(bool), typeof(bool));
            Assert(method == typeof(ChangedCompatibilityDriver).GetMethod("Matching") && SSCLog.Warnings.Count == before,
                "支持的版本应照常发现入口");
        });
        Run("Lovin替换回调布局只警告一次且保持原生步骤和回调", () =>
        {
            int before = SSCLog.Warnings.Count, calls = 0;
            Action original = () => calls++;
            var input = new[] { new Toil(), new Toil { initAction = original } };
            var driver = new JobDriver_Lovin();
            for (int i = 0; i < 2; i++)
            {
                var output = SSCRestrictionLovinGuard.Wrap(driver, input).ToArray();
                Assert(output.SequenceEqual(input) && output[1].initAction == original, "不得变更未知步骤或强行包裹陌生回调");
                output[1].initAction();
            }
            Assert(calls == 2 && SSCLog.Warnings.Count == before + 1, "保留原生执行且只诊断一次");
        });
        Run("事件待领取信息不跨越对象池任务编号", () =>
        {
            var p = People();
            var job = EventJob(p.b);
            Assert(SSCRestrictionJobGuard.PrepareEvent(p.a, job, SSCRestrictionEvent.TradeConsensual), "登记原候选");
            job.loadID++;
            var actual = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a);
            p.a.jobs.curDriver = actual; actual.MakeScenarioToils();
            Begin(actual);
            Assert(actual.StartCalls == 1 && Messages.Count == 0, "新任务不能领取旧事件通知");
        });
        Run("取消待调度事件后同一Job重新启动不会宣告旧事件", () =>
        {
            var p = People(); var job = EventJob(p.b);
            Assert(SSCRestrictionJobGuard.PrepareEvent(p.a, job, SSCRestrictionEvent.TradeConsensual), "登记候选");
            SSCRestrictionJobGuard.CancelPendingEvent(job);
            var actual = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a);
            p.a.jobs.curDriver = actual; actual.MakeScenarioToils();
            Begin(actual);
            Assert(actual.StartCalls == 1 && Messages.Count == 0, "取消只撤销事件交接，不应制造已发生通知");
        });
        Run("抽离准备与事件职责后仍写入既有存档键", () =>
        {
            var p = People(); var job = EventJob(p.b);
            SSCRestrictionJobGuard.PrepareEvent(p.a, job, SSCRestrictionEvent.TradeConsensual);
            var wait = new Job { def = JobDefOf.Wait }; p.b.jobs.curDriver = new JobDriver { pawn = p.b, job = wait };
            SSCRestrictionJobGuard.RegisterEventWait(p.a, job, p.b, wait);
            var actual = (JobDriver_SexBaseInitiator)job.MakeDriver(p.a); p.a.jobs.curDriver = actual;
            Assert(actual.TryMakePreToilReservations(false), "运行驱动领取待调度状态");
            Scribe.mode = LoadSaveMode.Saving; actual.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            Assert(Scribe.node.ContainsKey("sscRestrictionPreparedTarget") && Scribe.node.ContainsKey("sscRestrictionPreparedJobs") &&
                Scribe.node.ContainsKey("sscRestrictionEvent") && Scribe.node.ContainsKey("sscRestrictionEventNotified"), "存档字段名保持兼容");
            Assert((Pawn)Scribe.node["sscRestrictionPreparedTarget"] == p.b &&
                (SSCRestrictionEvent)Scribe.node["sscRestrictionEvent"] == SSCRestrictionEvent.TradeConsensual, "归属与通知来源未丢失");
        });
        Run("外部更换准备对象只撤销旧请求登记任务，后续取消清理新对象", () =>
        {
            var p = People(); var driver = PrepareQuickie(p.c, p.b, out Job unrelated);
            var oldMovement = p.b.jobs.curDriver;
            driver.PartnerPawn = p.a;
            var newMovement = new JobDriver { pawn = p.a, job = new Job { def = JobDefOf.Goto } };
            var toils = SSCRestrictionJobGuard.TrackPreparation(driver,
                new[] { new Toil { initAction = () => p.a.jobs.curDriver = newMovement } });
            toils.Single().initAction();
            Assert(oldMovement.Ended && p.b.CurJob == null && p.b.jobs.jobQueue.Single().job == unrelated,
                "准备对象变化不能留下旧等待，也不能清掉旧对象自己的排队命令");
            Assert(!newMovement.Ended, "新对象刚创建的准备工作保留");
            driver.Cleanup(JobCondition.Incompletable);
            Assert(newMovement.Ended && p.a.CurJob == null && p.b.jobs.jobQueue.Single().job == unrelated,
                "后续取消只清新登记对象和编号");
        });
    }

    /// <summary>创建尚未安装的候选事件任务，让缓存驱动与真实启动驱动保持不同实例。</summary>
    private static Job EventJob(Pawn target) => new Job
    {
        def = new JobDef { defName = "RefactorEvent", driverClass = typeof(JobDriver_SexBaseInitiator) },
        targetA = new LocalTargetInfo { Thing = target }
    };
}
