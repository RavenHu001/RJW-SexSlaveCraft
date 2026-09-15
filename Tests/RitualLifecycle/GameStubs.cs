using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;
using Verse.AI.Group;

// 本文件仅提供运行生产代码需要的游戏对象和可观察字段，不实现仪式状态算法。
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        /// <summary>提供生产补丁属性所需的构造签名；测试直接调用补丁方法，不安装 Harmony detour。</summary>
        public HarmonyPatch(Type targetType, string methodName) { }
    }
}

namespace Verse
{
    public class Def { public string defName; }
    public class ThingComp { public Thing parent; }
    public class Thing
    {
        public readonly List<ThingComp> comps = new List<ThingComp>();
        /// <summary>从测试对象的组件列表返回首个匹配类型的组件；找不到时返回 null。</summary>
        public T TryGetComp<T>() where T : ThingComp => comps.Find(comp => comp is T) as T;
    }
    public class Pawn : Thing
    {
        public bool Dead;
        public bool Spawned = true;
        public Map Map;
        public Lord lord;
        public Pawn_JobTracker jobs = new Pawn_JobTracker();
        public Job CurJob => jobs.curJob;
        public JobDef CurJobDef => CurJob?.def;
        public string LabelShort = "test pawn";
        public string Name => LabelShort;
        public IntVec3 Position;
        public Pawn_DrawTracker Drawer = new Pawn_DrawTracker();
        public bool Reachable = true;
        public bool Reservable = true;
        /// <summary>返回测试直接设置的 Lord 引用，不执行游戏中的归属查询。</summary>
        public Lord GetLord() => lord;
        /// <summary>返回用例配置的可达性结果，供生产 JobGiver 的可达性分支使用。</summary>
        public bool CanReach(Pawn target, PathEndMode pathEndMode, Danger danger) => Reachable;
        /// <summary>返回用例配置的预定检查结果；不会创建真实预定。</summary>
        public bool CanReserve(Pawn target, int maxPawns, int stackCount, object layer, bool forced) => Reservable;
    }
    public class Map { public LordManager lordManager = new LordManager(); }
    public enum Danger { Deadly }
    public readonly record struct IntVec3(int x, int y, int z);
    public struct LocalTargetInfo
    {
        public Thing Thing;
        public IntVec3 Cell;
        public bool IsValid;
        /// <summary>构造指向测试对象的目标信息；对象为空时将目标标记为无效。</summary>
        public LocalTargetInfo(Thing thing) { Thing = thing; Cell = default; IsValid = thing != null; }
        /// <summary>构造指向地图格子的有效目标信息，不绑定 Thing。</summary>
        public LocalTargetInfo(IntVec3 cell) { Cell = cell; Thing = null; IsValid = true; }
    }
    public class Pawn_DrawTracker { public PawnRenderer renderer = new PawnRenderer(); }
    public class PawnRenderer
    {
        /// <summary>提供画面刷新占位入口；无界面宿主不维护渲染状态。</summary>
        public void SetAllGraphicsDirty() { }
    }
    public static class Log
    {
        /// <summary>将生产代码的错误日志写入测试输出，便于定位执行失败。</summary>
        public static void Error(string message) => Console.WriteLine(message);
    }
    public static class Scribe_Values
    {
        /// <summary>提供布尔字段序列化占位入口；保留测试设置的值，不模拟实际存档读写。</summary>
        public static void Look(ref bool value, string label, bool defaultValue) { }
    }
    public static class Scribe_References
    {
        /// <summary>提供引用序列化占位入口；引用由测试直接恢复，不执行 Scribe 解析。</summary>
        public static void Look<T>(ref T value, string label) { }
    }
}

namespace Verse.AI
{
    public class JobDef : Def { }
    public class Job
    {
        public JobDef def;
        public LocalTargetInfo targetA;
        public LocalTargetInfo targetB;
        public bool doUntilGatheringEnded;
        public int expiryInterval;
        public bool playerForced;
    }
    public static class JobMaker
    {
        /// <summary>按给定定义和目标创建测试 Job，供生产 JobGiver 填充其余字段。</summary>
        public static Job MakeJob(JobDef def, Pawn target)
            => new Job { def = def, targetA = new LocalTargetInfo(target) };
    }
    public class Pawn_JobTracker
    {
        public Job curJob;
        public JobDriver curDriver;
        public JobCondition? EndCondition;
        /// <summary>记录请求的 Job 结束原因；具体收尾回调由测试执行器显式驱动。</summary>
        public void EndCurrentJob(JobCondition condition) { EndCondition = condition; }
    }
    public enum JobCondition { Incompletable, InterruptForced, Succeeded }
    public enum PathEndMode { Touch, OnCell }
    public enum TargetIndex { A }
    public enum ToilCompleteMode { Instant, Never }
    public class Toil
    {
        public Action initAction;
        public Action tickAction;
        public bool handlingFacing;
        public ToilCompleteMode defaultCompleteMode;
        public readonly List<Action> FinishActions = new List<Action>();
        /// <summary>保存当前 Toil 的结束回调，供测试模拟当前阶段收尾。</summary>
        public void AddFinishAction(Action action) => FinishActions.Add(action);
        /// <summary>依注册顺序执行本 Toil 的全部结束回调。</summary>
        public void Finish() { foreach (Action action in FinishActions) action(); }
    }
    public static class Toils_Goto
    {
        /// <summary>返回没有场景收尾回调的移动 Toil 占位对象；宿主不模拟寻路。</summary>
        public static Toil GotoCell(IntVec3 cell, PathEndMode endMode) => new Toil();
    }
    public abstract class ThinkNode_JobGiver
    {
        /// <summary>保留生产 JobGiver 的覆写契约，由实际生产实现决定是否派发 Job。</summary>
        protected abstract Job TryGiveJob(Pawn pawn);
        /// <summary>向测试公开受保护的生产 JobGiver 入口，返回实际派发结果。</summary>
        public Job GiveJobForTest(Pawn pawn) => TryGiveJob(pawn);
    }
    public abstract class JobDriver
    {
        public Pawn pawn;
        public Job job;
        public bool ReadyForNext;
        /// <summary>保留生产 JobDriver 的 Toil 构造契约，由实际生产实现生成流程。</summary>
        protected abstract IEnumerable<Toil> MakeNewToils();
        /// <summary>向测试公开生产 Toil 枚举入口，便于显式执行各阶段回调。</summary>
        public IEnumerable<Toil> CreateToilsForTest() => MakeNewToils();
        /// <summary>为生产覆写提供默认成功的宿主入口；不创建真实游戏预定。</summary>
        public virtual bool TryMakePreToilReservations(bool errorOnFailed) => true;
        /// <summary>提供基类存档入口占位；不模拟游戏基类的序列化及引用恢复。</summary>
        public virtual void ExposeData() { }
        /// <summary>记录生产代码请求进入下一 Toil，供阶段结束断言检查。</summary>
        public void ReadyForNextToil() => ReadyForNext = true;
        public readonly List<Action<JobCondition>> FinishActions = new List<Action<JobCondition>>();
        public readonly List<Func<bool>> FailConditions = new List<Func<bool>>();
        /// <summary>注册整份 Job 的结束回调，使移动阶段中断也能触发全局清理。</summary>
        public void AddFinishAction(Action<JobCondition> action) => FinishActions.Add(action);
        /// <summary>按给定结束原因执行 Job 全局回调；当前 Toil 的回调由测试执行器另外调用。</summary>
        public void Finish(JobCondition condition) { foreach (Action<JobCondition> action in FinishActions) action(condition); }
    }
    public static class JobDriverExtensions
    {
        /// <summary>保留目标失效检查的扩展签名；本宿主不模拟原版目标失效调度。</summary>
        public static void FailOnDespawnedOrNull(this JobDriver driver, TargetIndex target) { }
        /// <summary>保存生产有效性检查，使测试可以在指定运行时机主动求值。</summary>
        public static void FailOn(this JobDriver driver, Func<bool> condition) => driver.FailConditions.Add(condition);
    }
}

namespace Verse.AI.Group
{
    public class LordJob { public Lord lord; }
    public class Lord
    {
        public LordJob LordJob;
        public Map Map;
        public readonly List<Pawn> ownedPawns = new List<Pawn>();
        public readonly List<string> ReceivedMemos = new List<string>();
        public Action<string> MemoReceived;
        /// <summary>记录仪式消息并同步调用测试挂钩，用于模拟末阶段结算中的清理重入。</summary>
        public void ReceiveMemo(string memo) { ReceivedMemos.Add(memo); MemoReceived?.Invoke(memo); }
    }
    public class LordManager { public readonly List<Lord> lords = new List<Lord>(); }
}

namespace RimWorld
{
    public class RitualBehaviorDef : Def { }
    public class RitualBehaviorWorker { public RitualBehaviorDef def; }
    public class Precept_Ritual { public RitualBehaviorWorker behavior; }
    public class LordJob_Ritual : LordJob
    {
        public Precept_Ritual Ritual;
        public LocalTargetInfo selectedTarget;
        public readonly Dictionary<string, Pawn> Roles = new Dictionary<string, Pawn>();
        /// <summary>从测试角色表查找参与者；角色不存在时返回 null。</summary>
        public Pawn PawnWithRole(string role) => Roles.TryGetValue(role, out Pawn pawn) ? pawn : null;
        /// <summary>提供 Harmony 目标方法签名；测试由夹具显式调用生产清理补丁。</summary>
        public void Cleanup() { }
        /// <summary>提供后置清理目标签名；不模拟原版 LordJob 的内部行为。</summary>
        public void PostCleanup() { }
        /// <summary>提供角色离场目标签名；参与者和角色表的变化由测试夹具控制。</summary>
        public void Notify_PawnLost(Pawn pawn) { }
    }
}

namespace SexSlaveCraft
{
    public class CompSexSlaveTraining : ThingComp
    {
        public Lord bindingRitualLord;
        public bool bindingRitualOutcomeClaimed;
        public bool isRitualTraining;
        public bool isBeingTrained;
        public int ritualPhase;
        public Pawn selectedTrainer;
        public float specializationProgress;
        public int lastTrainingTick = -999999;
        /// <summary>若仪式路径误用日常训练完成入口则立即报错，防止测试遗漏冷却污染。</summary>
        public void Notify_TrainingCompleted() => throw new InvalidOperationException("Ritual must not add daily cooldown.");
    }
    public static class SSCDefOf
    {
        public static JobDef Training_Ritual;
        public static JobDef SSC_TrainingReceiver;
    }
    public static class SSCLog
    {
        /// <summary>忽略详细日志，避免正常执行的测试输出被游戏诊断信息淹没。</summary>
        public static void Verbose(string message) { }
        /// <summary>提供重要日志的无操作适配；用例结果由测试执行器单独输出。</summary>
        public static void Important(string message) { }
        /// <summary>提供警告日志的无操作适配，使拒绝派发分支可在宿主中执行。</summary>
        public static void WarningImportant(string message) { }
        /// <summary>提供 SSC 错误日志占位；预期失败分支通过返回值和状态断言验证。</summary>
        public static void Error(string message) { }
    }
    public static class Trainjudge
    {
        /// <summary>按测试对象是否存在、已生成且存活返回资格结果；不模拟完整游戏资格规则。</summary>
        public static bool TryCanBeFuckedWithReason(Pawn pawn, out string reason, out string report)
        {
            reason = null;
            report = null;
            return pawn != null && pawn.Spawned && !pawn.Dead;
        }
    }
    public static class TrainingJobUtility
    {
        /// <summary>复用宿主目标资格判断；有效用例中返回 true，不模拟生产失败后的中止副作用。</summary>
        public static bool TryValidateStartOrAbort(Pawn master, Pawn slave, string prefix)
            => Trainjudge.TryCanBeFuckedWithReason(slave, out _, out _);
        /// <summary>直接同步双方的测试坐标；不执行寻路停止或传送通知。</summary>
        public static void SyncPartnerPosition(Pawn master, Pawn slave, IntVec3 cell)
        { master.Position = cell; slave.Position = cell; }
        /// <summary>提供唤醒占位入口；宿主参与者没有睡眠状态。</summary>
        public static void EnsureAwake(Pawn pawn) { }
        /// <summary>给目标分配测试接收 Job 并返回成功；不运行真实接收端调度。</summary>
        public static bool TryStartBindingRitualReceiver(Pawn master, Pawn slave, Job job, JobDef receiver, IntVec3 cell)
        { TestWorld.AssignReceiverJob(slave); return true; }
        /// <summary>提供启动失败路径的简化标记复位；当前用例不通过此适配器验证生产失败恢复逻辑。</summary>
        public static void NotifyTrainingAborted(Pawn pawn)
        { var comp = pawn.TryGetComp<CompSexSlaveTraining>(); comp.isBeingTrained = false; comp.isRitualTraining = false; }
        /// <summary>设置宿主训练标记，并在指定仪式模式时标记仪式训练。</summary>
        public static void MarkTrainingStarted(Pawn pawn, bool ritual)
        { var comp = pawn.TryGetComp<CompSexSlaveTraining>(); comp.isBeingTrained = true; comp.isRitualTraining |= ritual; }
    }
    public static class OnaholeCompatibilityUtility
    {
        /// <summary>固定返回同步成功，使测试进入正常场景初始化路径。</summary>
        public static bool TrySynchronizeOnaholeSexProps(Pawn pawn, rjw.SexProps props) => true;
        /// <summary>固定返回 false；本套件不模拟飞机杯兼容状态。</summary>
        public static bool IsPawnOnOnahole(Pawn pawn) => false;
        /// <summary>返回未使用的诊断占位文本，不读取真实兼容组件。</summary>
        public static string GetOnaholeReceiverStateReport(Pawn pawn, JobDef jobDef) => "unused";
        /// <summary>提供兼容关系注销占位；宿主不保存外部模组的伙伴关系。</summary>
        public static void TryUnregisterOnaholePartner(Pawn slave, Pawn master) { }
    }
    public static class RimTalkCompatibilityUtility
    {
        /// <summary>提供对话通知占位；测试不发送外部模组事件。</summary>
        public static void NotifySexStarted(Pawn master, Pawn slave, string context, string sexType) { }
    }
    public static class RitualTrainingUtility
    {
        /// <summary>提供动作切换占位；阶段计数由生产状态管理器验证，宿主不选择真实动作。</summary>
        public static void ApplyPhaseToSexProps(rjw.SexProps props, Pawn master, Pawn slave, int phase) { }
        /// <summary>固定返回已有动画，避免生命周期用例启动动画回退分支。</summary>
        public static bool IsAnimating(Pawn pawn) => true;
        /// <summary>提供回退动画占位；不播放动画，也不调用时长设置回调。</summary>
        public static void TryStartFallbackAnimation(Pawn master, Pawn slave, Thing bed, Action<int> setTicks) { }
    }
}

namespace rjw
{
    public class SexProps { public Pawn pawn; public Pawn partner; public int sexType; }
    public class CompRJW { public bool drawNude; }
    public static class PawnExtensions
    {
        /// <summary>返回空 RJW 组件，使无界面测试跳过脱衣及画面刷新分支。</summary>
        public static CompRJW GetCompRJW(this Pawn pawn) => null;
    }
    public abstract class JobDriver_Sex : JobDriver
    {
        public int ticks_left = 100;
        public int sex_ticks;
        public int orgasmStartTick;
        public int duration;
    }
    public abstract class JobDriver_SexBaseInitiator : JobDriver_Sex
    {
        public SexProps Sexprops;
        public Thing Bed;
        /// <summary>保留初始化计时的基类签名；测试使用直接配置的计时字段。</summary>
        public void setup_ticks() { }
        /// <summary>执行测试启动钩子，供用例检查兼容解锁与外部动画启动的先后顺序。</summary>
        public void Start() => TestWorld.OnRjwStart?.Invoke(this);
        /// <summary>累计 RJW 收尾调用次数，用于检查中断清理和同步重入是否重复处理。</summary>
        public void End() => TestWorld.RjwEndCalls++;
        /// <summary>将剩余 tick 减一，让测试可以明确触发生产阶段结束条件。</summary>
        public void SexTick(Pawn master, Pawn slave) => ticks_left--;
    }
    public static class SexUtility
    {
        /// <summary>构造包含双方引用的最小场景参数，不模拟动作选择算法。</summary>
        public static SexProps SelectSextype(Pawn master, Pawn slave, bool forced, bool random)
            => new SexProps { pawn = master, partner = slave };
        /// <summary>提供体力消耗占位；宿主不维护需求数值。</summary>
        public static void reduce_rest(Pawn pawn, float multiplier = 1f) { }
        /// <summary>累计场景结算次数，不派发实际奖励或产生游戏副作用。</summary>
        public static void ProcessSex(SexProps props) => TestWorld.ProcessSexCalls++;
    }
}

internal static class TestWorld
{
    public static Map Map;
    public static int ProcessSexCalls;
    public static int RjwEndCalls;
    public static Action<rjw.JobDriver_SexBaseInitiator> OnRjwStart;

    /// <summary>重建隔离的测试地图，清零调用计数并恢复 Job 定义，避免用例相互污染。</summary>
    public static void Reset()
    {
        Map = new Map();
        ProcessSexCalls = 0;
        RjwEndCalls = 0;
        OnRjwStart = null;
#if SSC_TEST_WITH_UAP
        UAP_Animations.UAP_AnimationPositionLockManager.Reset();
#endif
        SSCDefOf.Training_Ritual = new JobDef { defName = "Training_Ritual" };
        SSCDefOf.SSC_TrainingReceiver = new JobDef { defName = "SSC_TrainingReceiver" };
    }

    /// <summary>创建属于当前测试地图且附带训练组件的参与者。</summary>
    public static Pawn NewPawn()
    {
        var pawn = new Pawn { Map = Map };
        pawn.comps.Add(new CompSexSlaveTraining { parent = pawn });
        return pawn;
    }

    /// <summary>给参与者设置共享接收 Job，用于验证接收 Job 不能单独证明仪式有效。</summary>
    public static void AssignReceiverJob(Pawn pawn)
        => pawn.jobs.curJob = new Job { def = new JobDef { defName = "SSC_TrainingReceiver" } };

    /// <summary>移除测试当前 Job，以模拟仪式阶段之间的等待状态。</summary>
    public static void ClearCurrentJob(Pawn pawn) => pawn.jobs.curJob = null;
}

internal sealed class RitualFixture
{
    public readonly Pawn Master;
    public readonly Pawn Slave;
    public readonly Lord Lord;
    public readonly LordJob_Ritual Job;
    public CompSexSlaveTraining Comp => Slave.TryGetComp<CompSexSlaveTraining>();

    /// <summary>构造包含主从角色和地图注册的仪式夹具；可复用参与者或创建其他类型的仪式。</summary>
    public RitualFixture(Pawn master = null, Pawn slave = null, bool bindingRitual = true)
    {
        Master = master ?? TestWorld.NewPawn();
        Slave = slave ?? TestWorld.NewPawn();
        Job = new LordJob_Ritual
        {
            Ritual = new Precept_Ritual
            {
                behavior = new RitualBehaviorWorker
                {
                    def = new RitualBehaviorDef
                    {
                        defName = bindingRitual ? "SSC_BindingRitualBehavior" : "UnrelatedRitualBehavior"
                    }
                }
            }
        };
        Lord = new Lord { LordJob = Job, Map = TestWorld.Map };
        Job.lord = Lord;
        Job.Roles["master"] = Master;
        Job.Roles["slave"] = Slave;
        Lord.ownedPawns.Add(Master);
        Lord.ownedPawns.Add(Slave);
        Master.lord = Lord;
        Slave.lord = Lord;
        TestWorld.Map.lordManager.lords.Add(Lord);
    }

    /// <summary>仅从地图管理器移除 Lord，模拟清理前的状态或漏掉清理回调的情况。</summary>
    public void RemoveLordWithoutCleanup() => Lord.Map.lordManager.lords.Remove(Lord);

    /// <summary>按地图先移除 Lord 的顺序调用生产前后清理补丁；保留角色引用用于检查重复清理。</summary>
    public void End()
    {
        // 原版在 Cleanup 前将 Lord 从 manager 移除；保留角色引用，模拟清理中间态。
        RemoveLordWithoutCleanup();
        Harmony_BindingRitualCleanup.Prefix(Job);
        Harmony_BindingRitualPostCleanup.Postfix(Job);
    }

    /// <summary>调用生产离场补丁，再移除参与者及角色引用，用于验证关键角色与观众的区别。</summary>
    public void RemoveParticipant(Pawn pawn)
    {
        // 补丁必须在角色表改变前执行，否则被移出的目标无法在后续 Cleanup 中找到。
        Harmony_BindingRitualPawnLost.Prefix(Job, pawn);
        Lord.ownedPawns.Remove(pawn);
        foreach (string role in new[] { "master", "slave" })
            if (Job.PawnWithRole(role) == pawn) Job.Roles.Remove(role);
        if (pawn.lord == Lord) pawn.lord = null;
    }
}

internal sealed class PhaseExecution
{
    public readonly JobDriver_RitualTraining Driver;
    public readonly List<Toil> Toils;
    public int CurrentToilIndex;
    public Toil CurrentToil => Toils[CurrentToilIndex];

    /// <summary>通过真实 JobGiver 创建阶段 Job，调用生产预定检查并枚举 Toil，建立可手动驱动的执行器。</summary>
    public PhaseExecution(RitualFixture ritual)
    {
        Job job = new JobGiver_RitualBinding().GiveJobForTest(ritual.Master);
        if (job == null) throw new Exception("Production job giver did not issue a phase job.");
        Driver = new JobDriver_RitualTraining { pawn = ritual.Master, job = job };
        ritual.Master.jobs.curJob = job;
        ritual.Master.jobs.curDriver = Driver;
        if (!Driver.TryMakePreToilReservations(false)) throw new Exception("Phase reservation was rejected.");
        Toils = Driver.CreateToilsForTest().ToList();
    }

    /// <summary>依次执行准备和场景初始化回调，将执行器推进到当前场景 Toil。</summary>
    public void StartScene()
    {
        CurrentToilIndex = 1;
        CurrentToil.initAction();
        CurrentToilIndex = 2;
        CurrentToil.initAction();
    }

    /// <summary>把剩余时间设置为一个 tick，执行生产 tick 回调并确认其请求进入下一阶段。</summary>
    public void ReachPhaseEnd()
    {
        Driver.ticks_left = 1;
        CurrentToil.tickAction();
        if (!Driver.ReadyForNext) throw new Exception("Phase did not finish after its final tick.");
    }

    /// <summary>执行整份 Job 和当前 Toil 的结束回调；不提前执行尚未进入的 Toil。</summary>
    public void FinishCurrent(JobCondition condition)
    {
        // 宿主只执行全局回调与当前 Toil；不能替尚未进入的性交 Toil 调用收尾。
        Driver.Finish(condition);
        CurrentToil.Finish();
    }
}
