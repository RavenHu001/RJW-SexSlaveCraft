using System;
using System.Collections.Generic;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static class Program
{
    private sealed class Scenario
    {
        public readonly Map Map = new Map();
        public readonly Pawn Actor = new Pawn();
        public readonly Pawn Hollow = new Pawn { X = 1 };
        public readonly Thing Gel = new Thing();
        public readonly JobDriver_InsertPersonality Driver = new JobDriver_InsertPersonality();
        public readonly List<Toil> Toils;
        private int remaining;

        /// <summary>创建相邻操作者与空壳、有效凝胶及生产驱动，并重置副作用计数。</summary>
        public Scenario()
        {
            ExcretionUtility.Calls = 0;
            Messages.Successes = 0;
            Actor.Map = Hollow.Map = Gel.Map = Map;
            Gel.Store = new CompPersonalityStore { parent = Gel };
            Driver.pawn = Actor;
            Driver.job = new Job { A = Gel, B = Hollow };
            Require(Driver.TryMakePreToilReservations(false), "Valid targets must be reservable.");
            Toils = Driver.BuildToils();
        }

        /// <summary>设置到达后的凝胶携带状态，检查并初始化生产等待步骤。</summary>
        /// <remarks>跳过完整寻路模拟，之后的失败判定和步骤回调均来自生产驱动。</remarks>
        public void StartWait()
        {
            Actor.carryTracker.CarriedThing = Gel;
            Gel.Carrier = Actor.carryTracker;
            Gel.Spawned = false;
            Toil wait = Toils[3];
            Require(Driver.Check(wait), "The adjacent fixture must enter the wait.");
            wait.initAction?.Invoke();
            remaining = wait.defaultDuration;
        }

        /// <summary>推进指定数量的等待 tick，每次先执行失败检查，再调用步骤回调并更新剩余时间。</summary>
        public void TickWait(int count)
        {
            Toil wait = Toils[3];
            for (int i = 0; i < count && remaining > 0; i++)
            {
                if (!Driver.Check(wait)) return;
                wait.tickAction?.Invoke();
                wait.tickIntervalAction?.Invoke(1);
                remaining--;
                Hollow.ForcedWaitTicks = Math.Max(0, Hollow.ForcedWaitTicks - 1);
            }
        }

        /// <summary>推进剩余等待并尝试执行最终植入；被中断的任务不会继续结算。</summary>
        public void Finish()
        {
            TickWait(remaining);
            if (!Driver.Check(Toils[4])) return;
            Toils[4].initAction?.Invoke();
            if (!Driver.Ended) Driver.EndJobWith(JobCondition.Succeeded);
        }

        /// <summary>断言尚未发生人格继承、凝胶消耗、昏迷、主动清除分配或成功提示。</summary>
        public void RequireNoEffects()
        {
            Require(ExcretionUtility.Calls == 0, "Rejected insertion must not invoke personality inheritance.");
            Require(!Gel.Destroyed && Gel.DestroyCalls == 0, "Rejected insertion must preserve the gel.");
            Require(Hollow.health.Comas == 0, "Rejected insertion must not apply an insertion coma.");
            Require(Map.Assignments.UnassignCalls == 0, "Rejected insertion must not unassign the gel.");
            Require(Messages.Successes == 0, "Rejected insertion must not report success.");
        }
    }

    private static int passed, failed;

    /// <summary>执行全部植入接触与中断用例，输出统计并以退出码报告是否全部通过。</summary>
    private static int Main()
    {
        // 正常完成用例：等待未结束时无副作用，结束后所有成功效果仅发生一次。
        Run("正常相邻植入完整等待后只结算一次", () =>
        {
            var s = new Scenario();
            s.StartWait();
            s.TickWait(599);
            s.RequireNoEffects();
            s.Finish();
            Require(s.Driver.EndCondition == JobCondition.Succeeded, "Normal insertion must succeed.");
            Require(ExcretionUtility.Calls == 1 && s.Gel.DestroyCalls == 1, "Inheritance and consumption must happen once.");
            Require(!s.Hollow.health.hediffSet.Hollow && s.Hollow.health.Comas == 1, "The receiver must be restored and get its coma.");
            Require(s.Map.Assignments.UnassignCalls == 1 && Messages.Successes == 1, "Normal completion must clear assignment and report success.");
        });
        // 睡眠用例：目标等待期间保持原有睡眠与姿势，并能够正常完成植入。
        Run("操作阶段让目标等待并保留睡眠和姿势", () =>
        {
            var s = new Scenario();
            s.Hollow.Asleep = s.Hollow.LyingDown = true;
            s.StartWait();
            Require(s.Hollow.ForcedWaitTicks == 600, "The target must wait for the insertion duration.");
            Require(s.Hollow.Asleep && s.Hollow.LyingDown, "Waiting must preserve sleep and posture.");
            s.Finish();
            Require(s.Driver.EndCondition == JobCondition.Succeeded, "A sleeping receiver must remain supported.");
        });
        // 行走用例：接触约束只在操作阶段生效，不能阻止操作者先走向远处目标。
        Run("接近目标的行走阶段不受接触检查提前中断", () =>
        {
            var s = new Scenario();
            s.Hollow.X = 30;
            Require(s.Driver.Check(s.Toils[2]), "A distant target must remain valid during travel.");
            s.Hollow.X = 1;
            s.StartWait();
            s.Finish();
            Require(s.Driver.EndCondition == JobCondition.Succeeded, "Arrival should permit insertion.");
        });
        // 目标位移回调：等待中将空壳移出接触范围，验证任务中断且凝胶保留。
        Run("等待期间目标走远会中断并保留凝胶", () => RejectDuringWait(s => s.Hollow.X = 30));
        // 操作者位移回调：等待中移动操作者，验证同样不能继续隔空植入。
        Run("等待期间操作者被移动会中断并保留凝胶", () => RejectDuringWait(s => s.Actor.X = 30));
        // 接触阻挡回调：坐标仍相邻时设置不可接触，验证实际接触约束。
        Run("相邻但不能实际接触时中断", () => RejectDuringWait(s => s.Actor.ContactBlocked = true));
        // 目标离图回调：撤销地图生成状态，验证既有目标有效性保护。
        Run("目标消失时中断", () => RejectDuringWait(s => s.Hollow.Spawned = false));
        // 空壳状态变化回调：等待中移除空壳状态，验证任务不会覆盖已恢复的人格。
        Run("目标失去空壳状态时中断", () => RejectDuringWait(s => s.Hollow.health.hediffSet.Hollow = false));
        // 禁止状态回调：等待中将目标设为禁止，验证现有中断条件继续生效。
        Run("目标被禁止时中断", () => RejectDuringWait(s => s.Hollow.Forbidden = true));
        // 主动中断用例：取消操作者任务后再次尝试推进，也不得产生结算副作用。
        Run("操作者主动中断后没有结算副作用", () =>
        {
            var s = new Scenario();
            s.StartWait();
            s.TickWait(100);
            s.Driver.EndJobWith(JobCondition.InterruptForced);
            s.Finish();
            s.RequireNoEffects();
        });
        // 结算前位移回调：绕过调度器的再次检查，独立验证最终写入前的距离保护。
        Run("最终结算前目标走远也会被独立拦截", () => RejectAtCommit(s => s.Hollow.X = 30));
        // 结算前阻挡回调：验证最终写入同样要求可实际接触。
        Run("最终结算前接触被阻挡也会被独立拦截", () => RejectAtCommit(s => s.Actor.ContactBlocked = true));
        // 跨地图回调：保持坐标相邻但更换目标地图，验证不会跨地图植入。
        Run("最终结算拒绝不同地图的相同坐标", () => RejectAtCommit(s => s.Hollow.Map = new Map()));
        // 操作者离图回调：撤销操作者的地图生成状态，验证最终结算被拒绝。
        Run("最终结算拒绝已离图的操作者", () => RejectAtCommit(s => s.Actor.Spawned = false));
        // 死亡回调：在最终写入前标记目标死亡，验证不会恢复死者人格或消耗凝胶。
        Run("最终结算拒绝死亡目标", () => RejectAtCommit(s => s.Hollow.Dead = true));
        // 结算前状态变化回调：移除空壳状态，独立验证最终写入边界的状态检查。
        Run("最终结算拒绝不再是空壳的目标", () => RejectAtCommit(s => s.Hollow.health.hediffSet.Hollow = false));
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>在等待中途应用场景变化，验证下一次检查立即中断且不产生植入副作用。</summary>
    private static void RejectDuringWait(Action<Scenario> change)
    {
        var s = new Scenario();
        s.StartWait();
        s.TickWait(100);
        change(s);
        s.TickWait(1);
        Require(s.Driver.EndCondition == JobCondition.Incompletable, "The next job check must interrupt insertion.");
        s.Finish();
        s.RequireNoEffects();
    }

    /// <summary>在最终回调前改变场景，单独验证生产结算方法能拒绝失效目标并保留凝胶。</summary>
    private static void RejectAtCommit(Action<Scenario> change)
    {
        var s = new Scenario();
        s.StartWait();
        s.TickWait(600);
        Require(s.Driver.Check(s.Toils[4]), "The fixture must reach the final callback.");
        // 直接调用最终回调，跳过调度器重检，以确认真正写入状态的边界也有保护。
        change(s);
        s.Toils[4].initAction();
        Require(s.Driver.EndCondition == JobCondition.Incompletable, "The final callback must independently reject invalid contact/state.");
        s.RequireNoEffects();
    }

    /// <summary>执行单个用例并捕获断言异常，累计通过与失败数量后继续后续用例。</summary>
    private static void Run(string name, Action body)
    {
        try { body(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception ex) { failed++; Console.WriteLine("失败：" + name + " — " + ex.Message); }
    }

    /// <summary>条件不成立时抛出带说明的断言异常，由用例执行器记录失败。</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
