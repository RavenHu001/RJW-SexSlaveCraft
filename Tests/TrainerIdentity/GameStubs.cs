// 仅模拟外部游戏接口；身份、指派、迁移、工作入口及仪式角色直接编译生产源码。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse.AI;

namespace Verse
{
    public class Thing { public bool Destroyed; public Map Map; }
    public class Pawn : Thing
    {
        public bool Dead, Downed, Spawned = true, IsColonist = true, IsPrisonerOfColony, IsSlave;
        public string LabelShort = "Pawn";
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public WorkSettings workSettings = new WorkSettings();
        public Health health = new Health();
        public Story story = new Story();
        public Needs needs = new Needs();
        public RaceProperties RaceProps = new RaceProperties();
        public JobDef CurJobDef;
        public bool Reservable = true;
        /// <summary>仅提供训练组件。</summary>
        public T TryGetComp<T>() where T : class => Training as T;
        /// <summary>仪式角色使用同一组件。</summary>
        public T GetComp<T>() where T : class => TryGetComp<T>();
        /// <summary>由用例控制目标可预留状态。</summary>
        public bool CanReserve(Pawn target, int count, int stackCount, object layer, bool forced) => Reservable;
        /// <summary>仪式角色模型不模拟寻路。</summary>
        public bool CanReach(LocalTargetInfo target, PathEndMode mode, Danger danger) => true;
    }
    public class RaceProperties { public bool Humanlike = true; }
    public class Map { public MapPawns mapPawns = new MapPawns(); }
    public class MapPawns
    {
        private readonly List<Pawn> freeColonists = new List<Pawn>();
        // 候选成员由场景配置；这里只复现原版共享结果列表的生命周期，不复制完整阵营分类。
        public List<Pawn> FreeColonistsSource = new List<Pawn>();
        public List<Pawn> AllPawns = new List<Pawn>();
        /// <summary>原版分类查询会清空并重填同一临时列表；不能用普通字段掩盖嵌套读取的枚举失效。</summary>
        public List<Pawn> FreeColonists
        {
            get
            {
                freeColonists.Clear();
                freeColonists.AddRange(FreeColonistsSource);
                return freeColonists;
            }
        }
    }
    public class WorkSettings
    {
        public bool Active = true;
        /// <summary>返回测试工作开关，不从 SSC 身份推导。</summary>
        public bool WorkIsActive(WorkTypeDef work) => Active;
    }
    public class Story { public TraitSet traits = new TraitSet(); }
    public class TraitSet
    {
        public List<Trait> allTraits = new List<Trait>();
        /// <summary>为真实身份切换代码提供最小特性接口。</summary>
        public Trait GetTrait(object def) => allTraits.FirstOrDefault(t => t.def == def);
        /// <summary>按引用移除模型特性。</summary>
        public void RemoveTrait(Trait trait) => allTraits.Remove(trait);
    }
    public class Needs
    {
        public Need_Corruption Corruption = new Need_Corruption();
        /// <summary>只提供恶堕需求。</summary>
        public T TryGetNeed<T>() where T : class => Corruption as T;
    }
    public class Hediff { public object def; public float Severity; }
    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        /// <summary>供真实绑定服务查询锁链和缰绳。</summary>
        public Hediff GetFirstHediffOfDef(object def) => hediffs.FirstOrDefault(h => h.def == def);
        /// <summary>判断模型列表是否包含对应状态。</summary>
        public bool HasHediff(object def) => GetFirstHediffOfDef(def) != null;
    }
    public class Health
    {
        public HediffSet hediffSet = new HediffSet();
        /// <summary>供真实解绑服务移除锁链。</summary>
        public void RemoveHediff(Hediff hediff) => hediffSet.hediffs.Remove(hediff);
    }
    public class Game { }
    public class GameComponent
    {
        /// <summary>提供初始化生命周期入口。</summary>
        public virtual void FinalizeInit() { }
    }
    public static class DefDatabase<T> where T : new()
    {
        /// <summary>测试仅请求调教工作定义。</summary>
        public static T GetNamedSilentFail(string name) => new T();
    }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame = 100000; }
    public static class Scribe_Values
    {
        public static bool Loading;
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
        /// <summary>执行真实字段读写方法；字典模型不模拟完整 Scribe 引用恢复。</summary>
        public static void Look<T>(ref T value, string key, T defaultValue)
        {
            if (Loading) value = Data.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
            else Data[key] = value;
        }
    }
    public static class Extensions
    {
        public static Dictionary<string, string> Translations = new Dictionary<string, string>();
        /// <summary>使用真实 XML 加载的测试译文，缺键时保留键名。</summary>
        public static string Translate(this string key, params object[] args) => string.Format(Translations.TryGetValue(key, out string text) ? text : key, args);
        /// <summary>提供冷却提示所需的时间转换。</summary>
        public static string ToStringTicksToPeriod(this int ticks) => ticks.ToString();
    }
    public struct TargetInfo { public bool IsValid; }
    public struct LocalTargetInfo
    {
        /// <summary>仪式位置转换占位，不改变用例状态。</summary>
        public static explicit operator LocalTargetInfo(TargetInfo value) => new LocalTargetInfo();
    }
    public enum Danger { Deadly }
    public enum ThingRequestGroup { Pawn }
    public struct ThingRequest
    {
        /// <summary>供生产工作扫描器声明目标组。</summary>
        public static ThingRequest ForGroup(ThingRequestGroup group) => new ThingRequest();
    }
    public static class Log
    {
        /// <summary>模型不连接游戏日志。</summary>
        public static void Warning(string message) { }
    }
}
namespace Verse.AI
{
    public enum PathEndMode { Touch }
    public class Job { public Pawn target; }
    public static class JobMaker
    {
        /// <summary>记录生产工作入口生成的目标。</summary>
        public static Job MakeJob(JobDef def, Pawn target) => new Job { target = target };
    }
    public static class JobFailReason
    {
        public static string Last;
        /// <summary>记录强制命令的拒绝原因。</summary>
        public static void Is(string reason) => Last = reason;
    }
}
namespace RimWorld
{
    using Verse;
    public class WorkTypeDef { }
    public class JobDef { }
    public class Trait { public object def; public bool Suppressed; }
    public class WorkGiver_Scanner
    {
        public virtual ThingRequest PotentialWorkThingRequest => default;
        public virtual PathEndMode PathEndMode => default;
        public virtual bool ShouldSkip(Pawn pawn, bool forced = false) => false;
        public virtual IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => Array.Empty<Thing>();
        public virtual bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false) => false;
        public virtual Job JobOnThing(Pawn pawn, Thing thing, bool forced = false) => null;
    }
    public static class PawnsFinder { public static List<Pawn> AllMapsWorldAndTemporary_AliveOrDead = new List<Pawn>(); }
    public class LordJob_Ritual { }
    public class Precept_Ritual { }
    public class Precept_Role { }
    public class RitualRoleAssignments
    {
        public Pawn Slave;
        public Pawn FirstAssignedPawn(string role) => Slave;
    }
    public class RitualRole
    {
        public virtual bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false) { reason = null; return true; }
        public virtual bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false) { reason = null; return true; }
        public bool AppliesIfChild(Pawn pawn, out string reason, bool skipReason) { reason = null; return true; }
    }
}
namespace SexSlaveCraft
{
    using Verse;
    public enum PawnIdentity { Unset, Slave, Master }
    public enum TrainingMode { Disabled, Enabled }
    public partial class CompSexSlaveTraining
    {
        public PawnIdentity pawnIdentity;
        public Pawn selectedTrainer;
        public TrainingMode mode = TrainingMode.Enabled;
        public bool AllowsOthersForTrainingOrSex, IsBusSpecialized, BusState;
        public bool isRitualTraining, IsWaitingAfterFailedValidation, scheduledTrainingEnabled, isBeingTrained, IsOnCooldown;
        public bool IsScheduledTrainingDayDue = true, IsWithinScheduledTrainingWindow = true;
        public int scheduledTrainingIntervalDays, scheduledTrainingHour, ScheduledTrainingEndHour, lastTrainingTick;
        public const int CooldownTicks = 100;
        public bool IsEnabled => pawnIdentity == PawnIdentity.Slave && mode == TrainingMode.Enabled;
    }
    public class Need_Corruption { public float HighestCorruptionLevel, CurLevel; }
    public class Hediff_ChainOfSexSlave : Hediff
    {
        public Pawn LinkedPawn;
        /// <summary>只模拟双向绑定使用的锁链存储。</summary>
        public static Hediff_ChainOfSexSlave AddToPawn(Pawn slave, Pawn master)
        {
            var chain = SSCBondUtility.GetChain(slave) ?? new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave };
            chain.LinkedPawn = master;
            if (!slave.health.hediffSet.hediffs.Contains(chain)) slave.health.hediffSet.hediffs.Add(chain);
            return chain;
        }
    }
    public class Hediff_BridleOfSexSlave : Hediff
    {
        public List<Pawn> targets = new List<Pawn>();
        public IEnumerable<Pawn> ValidTargets => targets;
        public void RemoveTarget(Pawn target) => targets.Remove(target);
        public static Hediff_BridleOfSexSlave AddToPawn(Pawn master, Pawn slave)
        {
            var bridle = SSCBondUtility.GetBridle(master) ?? new Hediff_BridleOfSexSlave { def = SSCDefOf.BridleOfSexSlave };
            if (!bridle.targets.Contains(slave)) bridle.targets.Add(slave);
            if (!master.health.hediffSet.hediffs.Contains(bridle)) master.health.hediffSet.hediffs.Add(bridle);
            return bridle;
        }
    }
    public static class SSCDefOf
    {
        public static object SexSlaveTrait = new object(), ChainOfSexSlave = new object(), BridleOfSexSlave = new object(), SSC_BasicTraining = new object();
        public static JobDef SSC_TrainingReceiver = new JobDef(), Training_Ritual = new JobDef(), TrainingSexSlave = new JobDef();
    }
    public static class TraitUtility { public static bool AddOrUpdateTrait(Pawn pawn, object def, int degree) => true; }
    public static class BusSpecializationUtility { public static bool HasAnyBusState(Pawn pawn) => pawn?.Training.BusState == true; }
    public static class ResearchUtils { public static bool IsResearchFinished(object def) => true; }
    public static class BindingRitualStateUtility
    {
        public static void RecoverPawnState(Pawn pawn) { }
        public static void ClearRitualState(CompSexSlaveTraining comp) => comp.isRitualTraining = false;
    }
    public static class TrainingJobUtility
    {
        public static int ValidationCalls;
        public static bool TryValidateTarget(Pawn pawn, bool forced, string prefix, out string reason) { ValidationCalls++; reason = null; return true; }
    }
    public static class Trainjudge
    {
        public static bool TryCanBeFuckedWithReason(Pawn pawn, out string reason, out string details) { reason = details = null; return true; }
    }
    public static class Strings
    {
        public const string ITab_TrainerNone = "none", RJW_Short_TargetNull = "null", Train_Reason_NotHumanlike = "not human", Train_Reason_DeadOrSelf = "dead/self", Train_Reason_InvalidFaction = "faction", Train_Reason_NotEnabled = "disabled", Train_Reason_RitualBusy = "ritual", Train_Reason_ValidationCooldown = "validation cooldown", Train_Reason_AlreadyBeingTrained = "busy", Train_Reason_TrainerLocked = "locked", Train_Reason_NotReservable = "reservation";
        public const string Ritual_MustBeColonist = "colonist", Ritual_NoCompData = "comp", Ritual_NotDesignatedMaster = "master", Ritual_MustBeColonistOrSlave = "status", Ritual_MissingTrainingComp = "comp", Ritual_CannotBeSlaveAsMaster = "identity", Ritual_NoMasterAssigned = "assigned";
        public static string Train_Reason_Cooldown(string time) => time;
        public static string RitualRole_SlaveBoundToOther(string name) => name;
        public static string Ritual_ChainConflict(string name) => name;
    }
}
