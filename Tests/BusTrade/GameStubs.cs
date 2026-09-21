using System;
using System.Collections.Generic;

// Minimal game boundary: the actual trade patch decides eligibility, probability,
// job definitions, participant order and progression. These stubs only supply inputs
// and record job requests, acceptance, messages and progression effects.
namespace Verse
{
    public class Thing { }
    public class Map { }
    public class JobDef : Def { public Type driverClass; public bool forceCompleteBeforeNextJob; }
    public enum Gender { None, Male, Female }
    public enum Danger { None, Some, Deadly }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        /// <summary>将角色转换为任务目标，保留原引用以核对有向参与者。</summary>
        public static implicit operator LocalTargetInfo(Thing thing) => new LocalTargetInfo { Thing = thing };
    }
    public partial struct IntVec3
    {
        public int x, z;
        /// <summary>用平面欧氏距离实现测试所需的 15 格边界。</summary>
        public bool InHorDistOf(IntVec3 other, float distance)
            => (x - other.x) * (x - other.x) + (z - other.z) * (z - other.z) <= distance * distance;
    }
    public partial class Pawn : Thing
    {
        public string LabelShort;
        public Map Map;
        public IntVec3 Position;
        public Gender gender;
        public bool Dead, Downed, Drafted, InMentalState, Fighting;
        public bool Spawned = true;
        public bool IsBus, IsFinalBus;
        public bool CanFuck, CanBeFucked, CanRape = true, CanGetRaped = true;
        public bool Reachable = true;
        public int ReachChecks;
        public LocalTargetInfo LastReachTarget;
        public Verse.AI.PathEndMode LastPathEndMode;
        public Danger LastDanger;
        public NeedsTracker needs = new NeedsTracker();
        public Verse.AI.Pawn_JobTracker jobs;
        public Verse.AI.Job CurJob => jobs?.curJob;
        /// <summary>为每个角色创建独立的任务接收器。</summary>
        public Pawn() { jobs = new Verse.AI.Pawn_JobTracker(this); }
        /// <summary>返回用例指定的战斗状态。</summary>
        public bool IsFighting() => Fighting;
        /// <summary>记录预约目标和寻路参数，返回用例指定的可达性。</summary>
        public bool CanReserveAndReach(LocalTargetInfo target, Verse.AI.PathEndMode mode, Danger danger,
            int maxPawns = 1, int stackCount = -1, object layer = null, bool ignoreOtherReservations = false)
        {
            ReachChecks++;
            LastReachTarget = target;
            LastPathEndMode = mode;
            LastDanger = danger;
            return Reachable;
        }
    }
    public class NeedsTracker
    {
        public rjw.Need_Corruption Corruption = new rjw.Need_Corruption();
        /// <summary>返回测试预设的恶堕需求。</summary>
        public T TryGetNeed<T>() where T : class => Corruption as T;
    }
    public class LookTargets
    {
        public Pawn[] Pawns;
        /// <summary>保存提示目标，供测试核对角色。</summary>
        public LookTargets(params Pawn[] pawns) { Pawns = pawns; }
        /// <summary>将单个角色转换为提示目标，避免丢失原生调用签名。</summary>
        public static implicit operator LookTargets(Pawn pawn) => new LookTargets(pawn);
    }
    public static class Messages
    {
        public static List<(string Text, LookTargets Targets, object Type)> Entries = new();
        /// <summary>记录提示内容和类型，不模拟实际界面。</summary>
        public static void Message(string message, LookTargets targets, object type, bool historical = true) => Entries.Add((message, targets, type));
    }
    public static partial class Rand
    {
        public static int Roll = 1;
        /// <summary>返回固定骰点，使概率分界可重复检查。</summary>
        public static int RangeInclusive(int minimum, int maximum)
        {
            Rolls++;
            if (Roll < minimum || Roll > maximum) throw new InvalidOperationException("Test roll is outside requested range");
            return Roll;
        }
    }
    public static class DefDatabase<T> where T : class
    {
        public static Dictionary<string, T> Named = new();
        public static List<T> AllDefsListForReading = new();
        /// <summary>保留旧代码的定义查询接口，未注册的 Sex/Rape 名称按真实缺失情形返回 null。</summary>
        public static T GetNamedSilentFail(string name) => Named.TryGetValue(name, out var value) ? value : null;
    }
    public static class TranslationExtensions
    {
        /// <summary>保留翻译键与角色参数，避免依赖游戏语言库。</summary>
        public static string Translate(this string key, params object[] args) => key + "|" + string.Join("|", args);
        /// <summary>提供消息格式化所需的代词。</summary>
        public static string GetPronoun(this Gender gender) => gender == Gender.Female ? "she" : "he";
    }
}
namespace Verse.AI
{
    public enum JobTag { Misc }
    public enum JobCondition { InterruptForced, Incompletable, Succeeded }
    public enum PathEndMode { OnCell, Touch }
    public class JobDriver { public Verse.Pawn pawn; public Job job; }
    public class JobQueue
    {
        public List<Job> Jobs = new();
        /// <summary>仅移除谓词匹配的排队任务；保留无关任务及其顺序。</summary>
        public void RemoveAll(Verse.Pawn pawn, Predicate<Job> predicate) => Jobs.RemoveAll(predicate);
    }
    public partial class Job
    {
        public Verse.JobDef def;
        public Verse.LocalTargetInfo targetA;
        public int expiryInterval = -1;
        public bool checkOverrideOnExpire;
        public bool playerForced;
    }
    public static partial class JobMaker
    {
        /// <summary>构造不带目标的任务。</summary>
        public static Job MakeJob(Verse.JobDef def) => new Job { def = def };
        /// <summary>构造指定角色目标的任务。</summary>
        public static Job MakeJob(Verse.JobDef def, Verse.LocalTargetInfo target) => new Job { def = def, targetA = target };
        /// <summary>保留修复前代码使用的时长构造重载。</summary>
        public static Job MakeJob(Verse.JobDef def, int expiryInterval) => new Job { def = def, expiryInterval = expiryInterval };
    }
    public class Pawn_JobTracker
    {
        public static List<(Verse.Pawn Pawn, Job Job)> Requests = new();
        public Verse.Pawn Pawn;
        public Job curJob;
        public JobDriver curDriver;
        public JobQueue jobQueue = new JobQueue();
        public bool AcceptJobs = true;
        public bool Interruptible = true;
        public bool QueueOrderedJobs;
        public Func<Job, bool> OnRequest;
        public int EndCalls, StopCalls;
        public bool LastStartNewJob;
        /// <summary>绑定任务跟踪器所属角色。</summary>
        public Pawn_JobTracker(Verse.Pawn pawn) { Pawn = pawn; }
        /// <summary>模拟原生直接启动边界；拒绝启动时保持现有任务，供生产补丁核对 CurJob。</summary>
        public void StartJob(Job job, JobCondition condition = JobCondition.InterruptForced,
            JobTag? tag = null, bool preToilReservationsCanFail = false)
        {
            Requests.Add((Pawn, job));
            bool accepted = OnRequest != null ? OnRequest(job) : AcceptJobs;
            if (accepted) curJob = job;
        }
        /// <summary>返回当前工作的玩家中断许可，保留生产入口所需的边界条件。</summary>
        public bool IsCurrentJobPlayerInterruptible() => Interruptible;
        /// <summary>记录命令并按用例指定的接收结果更新当前任务。</summary>
        public bool TryTakeOrderedJob(Job job, JobTag tag = JobTag.Misc, bool requestQueueing = false)
        {
            Requests.Add((Pawn, job));
            bool accepted = OnRequest != null ? OnRequest(job) : AcceptJobs;
            if (accepted && (QueueOrderedJobs || curJob?.def == job.def)) return true;
            if (accepted) curJob = job;
            return accepted;
        }
        /// <summary>记录显式终止，清空当前任务。</summary>
        public void EndCurrentJob(JobCondition condition, bool startNewJob = true, bool canReturnToPool = true)
        {
            EndCalls++;
            LastStartNewJob = startNewJob;
            curJob = null;
        }
        /// <summary>支持修复前补丁的停工调用，以便同一套行为测试能够加载旧源码。</summary>
        public void StopAll(bool ifLayingKeepLaying = true, bool canReturnToPool = true)
        {
            StopCalls++;
            curJob = null;
        }
    }
}
namespace RimWorld
{
    public class TradeDeal
    {
        /// <summary>提供交易后缀的目标签名；用例直接给后缀传入成交结果。</summary>
        public bool TryExecute(out bool actuallyTraded) { actuallyTraded = true; return true; }
    }
    public class TradeShip { }
    public class Faction { }
    public static class TradeSession
    {
        public static Verse.Pawn playerNegotiator;
        public static object trader;
    }
    public static class MessageTypeDefOf
    {
        public static readonly object PositiveEvent = new(), NegativeEvent = new(), NeutralEvent = new();
    }
    public static class JobDefOf
    {
        public static readonly Verse.JobDef Wait = new() { defName = "Wait" };
        public static readonly Verse.JobDef Wait_Wander = new() { defName = "Wait_Wander" };
    }
}
namespace rjw
{
    public class JobDriver_Sex : Verse.AI.JobDriver {
        public SexProps Sexprops;
        public Verse.Pawn Partner => job?.targetA.Thing as Verse.Pawn;
    }
    public class Need_Corruption { public float CurLevel; }
    public static partial class xxx
    {
        public static Verse.JobDef quick_sex = new() { defName = "Quickie" };
        public static Verse.JobDef RapeRandom = new() { defName = "RandomRape" };
        /// <summary>返回用例指定的主动身体能力；女性基准用例将其设为 false。</summary>
        public static bool can_fuck(Verse.Pawn pawn) => pawn.CanFuck;
        /// <summary>返回用例指定的接收身体能力。</summary>
        public static bool can_be_fucked(Verse.Pawn pawn) => pawn.CanBeFucked;
        /// <summary>返回用例指定的强制行为发起能力。</summary>
        public static bool can_rape(Verse.Pawn pawn) => pawn.CanRape;
        /// <summary>返回用例指定的强制行为接收能力。</summary>
        public static bool can_get_raped(Verse.Pawn pawn) => pawn.CanGetRaped;
    }
}
namespace UnityEngine
{
    public static partial class Mathf
    {
        /// <summary>按游戏概率公式所需方式取整，边界骰点由用例显式给出。</summary>
        public static int RoundToInt(float value) => (int)MathF.Round(value);
        /// <summary>返回较小值以保留生产成长上限算法。</summary>
        public static float Min(float a, float b) => MathF.Min(a, b);
    }
}
namespace SexSlaveCraft
{
    public static class BusSpecializationUtility
    {
        /// <summary>读取模拟公交车资格，不因读取规则而改变特化。</summary>
        public static bool HasAnyBusState(Verse.Pawn pawn) => pawn?.IsBus == true;
        /// <summary>读取模拟终极状态，决定原有交易成长是否停止。</summary>
        public static bool HasFinalBusState(Verse.Pawn pawn) => pawn?.IsFinalBus == true;
    }
    public static class ConditioningUtility
    {
        public static List<(Verse.Pawn Pawn, float Gain)> Gains = new();
        /// <summary>记录真实事件入口发放的交易成长，供拒绝路径核对。</summary>
        public static void IncreaseBusHediffSeverity(Verse.Pawn pawn, float gain) => Gains.Add((pawn, gain));
    }
    public static class TrainingOutcomeUtility
    {
        public static float Gain = 0.12f;
        public static Verse.Pawn ScoredActor, ScoredTarget;
        /// <summary>保存计分时的商人与公交车角色顺序。</summary>
        public static float GetScore(Verse.Pawn actor, Verse.Pawn target)
        {
            ScoredActor = actor;
            ScoredTarget = target;
            return 1f;
        }
        /// <summary>提供可控的计分输出，生产补丁仍负责上限及折半。</summary>
        public static float CalculateCorruptionGain(float score) => Gain;
    }
    public static class SSCLog
    {
        public static List<string> Entries = new();
        /// <summary>收集诊断日志，不向游戏消息列表插入事件发生提示。</summary>
        public static void Verbose(string message) => Entries.Add(message);
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyPatch : Attribute
    {
        public Type Type;
        public string Method;
        /// <summary>保存补丁声明的类型与方法名，供元数据断言使用。</summary>
        public HarmonyPatch(Type type, string method) { Type = type; Method = method; }
    }
    public class HarmonyPostfix : Attribute { }
}
