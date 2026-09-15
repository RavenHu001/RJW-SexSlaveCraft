using System;
using System.Collections.Generic;
using System.Linq;

// 根据本机 RimWorld 1.6 程序集构建最小测试宿主，直接链接生产任务驱动。
// 寻路、任务调度和人格继承副作用仅作有限模拟，不能替代游戏内验证。
namespace Verse
{
    public class Thing
    {
        public bool Spawned = true, Destroyed, Forbidden;
        public int X, Z, DestroyCalls;
        public Map Map;
        public CarryTracker Carrier;
        public SexSlaveCraft.CompPersonalityStore Store;
        /// <summary>将模拟物品上的人格存储组件转换为请求类型；其他类型返回空值。</summary>
        public T TryGetComp<T>() where T : class => Store as T;
        /// <summary>记录物品销毁、撤销地图生成状态，并解除携带器对该物品的引用。</summary>
        public void Destroy()
        {
            Destroyed = true;
            Spawned = false;
            DestroyCalls++;
            if (Carrier?.CarriedThing == this) Carrier.CarriedThing = null;
        }
    }
    public class Pawn : Thing
    {
        public string LabelShort = "Pawn";
        public bool Dead, Asleep, LyingDown, ContactBlocked;
        public int ForcedWaitTicks;
        public HealthTracker health = new HealthTracker();
        public CarryTracker carryTracker = new CarryTracker();
        public RotationTracker rotationTracker = new RotationTracker();
        public PathFollower pather = new PathFollower();
        /// <summary>模拟对有效物品的预约；本宿主不模拟多个角色之间的预约竞争。</summary>
        public bool Reserve(Thing thing, Verse.AI.Job job, int maxPawns, int stackCount,
            object layer, bool errorOnFailed) => thing != null && !thing.Destroyed;
    }
    public class Hediff { public float Severity; }
    public class HediffSet
    {
        public bool Hollow = true;
        /// <summary>读取测试角色的空壳状态；本宿主只需支持这一健康状态查询。</summary>
        public bool HasHediff(object def) => Hollow;
    }
    public class HealthTracker
    {
        public HediffSet hediffSet = new HediffSet();
        public int Comas;
        /// <summary>记录植入后昏迷的添加次数，并返回可设置严重度的模拟健康状态。</summary>
        public Hediff AddHediff(object def) { Comas++; return new Hediff(); }
    }
    public class CarryTracker
    {
        public Thing CarriedThing;
        /// <summary>销毁当前携带物并清空引用，供用例检查凝胶是否只被消耗一次。</summary>
        public void DestroyCarriedThing() { CarriedThing?.Destroy(); CarriedThing = null; }
    }
    public class RotationTracker
    {
        /// <summary>接收生产代码的面向目标调用；测试不模拟角色朝向或绘制。</summary>
        public void FaceTarget(Pawn pawn) { }
    }
    public class PathFollower
    {
        public int StopCalls;
        /// <summary>记录操作者停止移动的次数，模拟等待步骤的初始化行为。</summary>
        public void StopDead() { StopCalls++; }
    }
    public class Map
    {
        public SexSlaveCraft.MapComponent_PersonalityAssignment Assignments =
            new SexSlaveCraft.MapComponent_PersonalityAssignment();
        /// <summary>返回模拟地图上的人格分配组件；请求其他类型时返回空值。</summary>
        public T GetComponent<T>() where T : class => Assignments as T;
    }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        /// <summary>将物品引用包装成任务目标，供生产代码调用接触检查。</summary>
        public static implicit operator LocalTargetInfo(Thing thing) => new LocalTargetInfo { Thing = thing };
    }
    public static class ReachabilityImmediate
    {
        /// <summary>用相邻坐标和接触阻挡标记模拟即时接触判定。</summary>
        /// <remarks>阻挡标记用于区分距离近和确实可接触；此处不实现完整地图寻路。</remarks>
        public static bool CanReachImmediate(Pawn actor, LocalTargetInfo target, Verse.AI.PathEndMode mode)
        {
            Thing thing = target.Thing;
            return thing != null && !actor.ContactBlocked &&
                Math.Abs(actor.X - thing.X) <= 1 && Math.Abs(actor.Z - thing.Z) <= 1;
        }
    }
    public static class Log
    {
        /// <summary>接收并忽略生产错误日志，避免模拟日志干扰测试结果输出。</summary>
        public static void Error(string text) { }
    }
    public static class Messages
    {
        public static int Successes;
        /// <summary>统计植入成功提示；拒绝提示无需在本宿主中显示。</summary>
        public static void Message(string text, Pawn target, object type, bool historical = true)
        {
            if (type == RimWorld.MessageTypeDefOf.PositiveEvent) Successes++;
        }
    }
    public static class Translation
    {
        /// <summary>原样返回翻译键，满足生产提示的调用接口而不加载游戏语言数据库。</summary>
        public static string Translate(this string key) => key;
    }
}
namespace RimWorld
{
    public static class MessageTypeDefOf
    {
        public static object RejectInput = new object(), PositiveEvent = new object();
    }
}
namespace Verse.AI
{
    public enum TargetIndex { None, A, B }
    public enum PathEndMode { ClosestTouch, Touch }
    public enum ToilCompleteMode { Instant, Delay }
    public enum JobCondition { Ongoing, Incompletable, InterruptForced, Succeeded }
    public class Job
    {
        public Thing A, B;
        /// <summary>按任务目标索引返回凝胶或空壳；本宿主只使用 A、B 两个目标。</summary>
        public LocalTargetInfo GetTarget(TargetIndex target) => target == TargetIndex.A ? A : B;
    }
    public interface IJobEndable
    {
        /// <summary>取得条件所属的任务驱动，用于读取操作者和任务目标。</summary>
        JobDriver Driver { get; }
        /// <summary>取得当前任务或步骤登记的失败条件集合。</summary>
        List<Func<bool>> Failures { get; }
    }
    public class Toil : IJobEndable
    {
        public JobDriver Owner;
        /// <summary>返回当前步骤所属的任务驱动。</summary>
        public JobDriver Driver => Owner;
        /// <summary>保存只在当前步骤生效的失败条件。</summary>
        public List<Func<bool>> Failures { get; } = new List<Func<bool>>();
        public Action initAction, tickAction;
        public Action<int> tickIntervalAction;
        public bool handlingFacing;
        public int defaultDuration;
        public ToilCompleteMode defaultCompleteMode;
        /// <summary>保留进度条配置调用的链式接口；无界面测试不绘制进度条。</summary>
        public Toil WithProgressBarToilDelay(TargetIndex target) => this;
    }
    public abstract class JobDriver : IJobEndable
    {
        public Pawn pawn;
        public Job job;
        /// <summary>返回当前驱动自身，统一任务与步骤的失败条件访问方式。</summary>
        public JobDriver Driver => this;
        /// <summary>保存整个任务期间持续生效的失败条件。</summary>
        public List<Func<bool>> Failures { get; } = new List<Func<bool>>();
        public JobCondition EndCondition = JobCondition.Ongoing;
        /// <summary>根据结束状态判断模拟任务是否已停止。</summary>
        public bool Ended => EndCondition != JobCondition.Ongoing;
        /// <summary>由生产驱动实现开始任务前对凝胶和空壳的预约。</summary>
        public abstract bool TryMakePreToilReservations(bool errorOnFailed);
        /// <summary>由生产驱动生成行走、携带、等待和最终植入步骤。</summary>
        protected abstract IEnumerable<Toil> MakeNewToils();
        /// <summary>枚举生产步骤并关联所属驱动，同时完成全局失败条件的登记。</summary>
        public List<Toil> BuildToils()
        {
            var toils = MakeNewToils().ToList();
            foreach (Toil toil in toils) toil.Owner = this;
            return toils;
        }
        /// <summary>记录任务结束原因；本宿主不执行完整游戏任务切换和物品放下流程。</summary>
        public void EndJobWith(JobCondition condition) { EndCondition = condition; }
        /// <summary>检查任务与当前步骤的失败条件，失败时以无法完成状态结束任务。</summary>
        public bool Check(Toil toil)
        {
            if (Ended) return false;
            // 依次执行登记的条件回调，任意一个返回真即视为失败。
            if (!Failures.Concat(toil.Failures).Any(fail => fail())) return true;
            EndJobWith(JobCondition.Incompletable);
            return false;
        }
    }
    public static class ToilFailConditions
    {
        /// <summary>为任务或步骤登记失败判定，并返回原对象以支持链式配置。</summary>
        public static T FailOn<T>(this T target, Func<bool> condition) where T : IJobEndable
        {
            target.Failures.Add(condition);
            return target;
        }
        /// <summary>登记目标为空、已销毁或被禁止时中断任务的判定。</summary>
        public static T FailOnDestroyedNullOrForbidden<T>(this T target, TargetIndex index) where T : IJobEndable
        {
            // 每次检查重新读取目标状态，保证等待期间发生的变化可被识别。
            return target.FailOn(() =>
            {
                Thing thing = target.Driver.job.GetTarget(index).Thing;
                return thing == null || thing.Destroyed || thing.Forbidden;
            });
        }
        /// <summary>在目标有效性检查之外，登记目标不再生成于地图时中断的判定。</summary>
        public static T FailOnDespawnedNullOrForbidden<T>(this T target, TargetIndex index) where T : IJobEndable
        {
            target.FailOnDestroyedNullOrForbidden(index);
            return target.FailOn(() => target.Driver.job.GetTarget(index).Thing?.Spawned != true);
        }
        /// <summary>登记操作者与目标无法即时接触时中断当前步骤的判定。</summary>
        public static T FailOnCannotTouch<T>(this T target, TargetIndex index, PathEndMode mode) where T : IJobEndable
        {
            return target.FailOn(() => !ReachabilityImmediate.CanReachImmediate(
                target.Driver.pawn, target.Driver.job.GetTarget(index), mode));
        }
    }
    public static class Toils_Goto
    {
        /// <summary>提供行走步骤占位对象；测试直接设置到达场景，不模拟寻路。</summary>
        public static Toil GotoThing(TargetIndex target, PathEndMode mode) => new Toil();
    }
    public static class Toils_Haul
    {
        /// <summary>提供拾取步骤占位对象；凝胶携带状态由测试场景显式设置。</summary>
        public static Toil StartCarryThing(TargetIndex target, bool putRemainderInQueue,
            bool subtractNumTakenFromJobCount) => new Toil();
    }
    public static class Toils_General
    {
        /// <summary>模拟仅让操作者停止移动并面向目标的限时等待步骤。</summary>
        public static Toil Wait(int ticks, TargetIndex face = TargetIndex.None)
        {
            var toil = new Toil { defaultDuration = ticks, defaultCompleteMode = ToilCompleteMode.Delay };
            // 初始化回调停止操作者移动；逐间隔回调维持面向目标。
            toil.initAction = () => toil.Driver.pawn.pather.StopDead();
            toil.tickIntervalAction = delta => toil.Driver.pawn.rotationTracker.FaceTarget(
                toil.Driver.job.GetTarget(face).Thing as Pawn);
            return toil;
        }
        /// <summary>模拟双方等待，按参数保留目标姿势与睡眠，并登记离图和接触失败条件。</summary>
        /// <remarks>依据 RimWorld 1.6 的 WaitWith 行为；不模拟完整目标任务切换或进度条绘制。</remarks>
        public static Toil WaitWith(TargetIndex targetInd, int ticks, bool useProgressBar = false,
            bool maintainPosture = false, bool maintainSleep = false,
            TargetIndex face = TargetIndex.None, PathEndMode pathEndMode = PathEndMode.Touch)
        {
            Toil toil = Wait(ticks, face);
            // 双方等待的初始化回调：停止操作者并为目标设置有限等待时间。
            toil.initAction = () =>
            {
                toil.Driver.pawn.pather.StopDead();
                var target = (Pawn)toil.Driver.job.GetTarget(targetInd).Thing;
                target.ForcedWaitTicks = ticks;
                if (!maintainSleep) target.Asleep = false;
                if (!maintainPosture) target.LyingDown = false;
            };
            toil.FailOn(() => toil.Driver.job.GetTarget(targetInd).Thing?.Spawned != true);
            toil.FailOnCannotTouch(targetInd, pathEndMode);
            return toil;
        }
    }
}
namespace SexSlaveCraft
{
    public class CompPersonalityStore { public Verse.Thing parent; }
    public class MapComponent_PersonalityAssignment
    {
        public int UnassignCalls;
        /// <summary>记录生产驱动主动清除凝胶分配的次数，不模拟周期性的分配清理。</summary>
        public void Unassign(Verse.Thing gel) { UnassignCalls++; }
    }
    public static class SSCDefOf
    {
        public static object SSC_PersonalityExcreted_Done = new object(), SSC_PostInsertionComa = new object();
    }
    public static class Strings
    {
        /// <summary>返回角色名称作为成功提示占位文本，避免引入真实翻译依赖。</summary>
        public static string Message_PersonalityInserted(string name) => name;
    }
    public static class ExcretionUtility
    {
        public static int Calls;
        /// <summary>记录人格继承调用，并模拟清除空壳和消耗凝胶的成功副作用。</summary>
        /// <remarks>真实人格数据迁移由其他回归套件覆盖，此处只检查调用时机。</remarks>
        public static bool InheritEverything(Verse.Pawn consumer, CompPersonalityStore data)
        {
            Calls++;
            consumer.health.hediffSet.Hollow = false;
            data.parent.Destroy();
            return true;
        }
    }
}
