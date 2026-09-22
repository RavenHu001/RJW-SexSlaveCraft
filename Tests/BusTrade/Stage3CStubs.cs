using System;
using System.Collections.Generic;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

// 此套件执行真实事件入口、请求适配和许可核心；Job生命周期为边界桩，真实守卫由InteractionProtection验证。
namespace Verse
{
    public partial class Pawn
    {
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public Pawn BoundMaster;
        public ApparelTracker apparel = new ApparelTracker();
        public RaceProperties RaceProps = new RaceProperties();
        public Ownership ownership = new Ownership();
        public JobDef CurJobDef => CurJob?.def;
        public bool Hostile, CanAnimalSex = true, IsFinalDog;
        /// <summary>返回当前角色的独立配置组件，不在查询中初始化或迁移。</summary>
        public T TryGetComp<T>() where T : class => Training as T;
        /// <summary>返回模型的敌对状态，供原有事件资格门槛使用。</summary>
        public bool HostileTo(Pawn pawn) => Hostile;
        /// <summary>返回用例设置的床位可达性。</summary>
        public bool CanReach(Thing target, PathEndMode mode, Danger danger) => Reachable;
    }
    public class RaceProperties { public bool Animal; }
    public class Ownership { public Building_Bed OwnedBed; }
    public partial struct IntVec3
    {
        /// <summary>计算床位选择需要的平方距离，不模拟寻路。</summary>
        public float DistanceToSquared(IntVec3 other) => (x - other.x) * (x - other.x) + (z - other.z) * (z - other.z);
    }
    public static class GenDate { public const int TicksPerDay = 60000, TicksPerHour = 2500; }
    public class TickManager { public int TicksGame = 100000; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public partial class Rand
    {
        public static int Rolls, ChanceCalls;
        public static bool ChanceResult = true;
        /// <summary>记录事件抽签次数，拒绝后是否重复掷骰由真实宠物代码决定。</summary>
        public static bool Chance(float chance) { ChanceCalls++; return ChanceResult; }
    }
    public static class Scribe_Values
    {
        /// <summary>提供核心配置的编译边界；存档往返由独立核心及交互套件执行。</summary>
        public static void Look<T>(ref T value, string key, T defaultValue = default) { }
    }
}
namespace Verse.AI
{
    public partial class Job
    {
        public int loadID;
        public JobDriver CachedDriver;
        /// <summary>按真实JobDef类型创建最小驱动，让生产JobContext决定普通或强制用途。</summary>
        public JobDriver GetCachedDriver(Pawn pawn)
        {
            if (CachedDriver == null && def?.driverClass != null) CachedDriver = (JobDriver)Activator.CreateInstance(def.driverClass);
            if (CachedDriver != null) { CachedDriver.pawn = pawn; CachedDriver.job = this; }
            return CachedDriver;
        }
    }
    public partial class JobMaker
    {
        /// <summary>提供带床位的任务创建边界；许可只读取角色目标。</summary>
        public static Job MakeJob(JobDef def, Pawn target, Building_Bed bed) => MakeJob(def, (LocalTargetInfo)target);
        /// <summary>记录对象池归还边界，本套件不模拟游戏对象池实现。</summary>
        public static void ReturnToPool(Job job) { }
    }
}
namespace RimWorld { public class Building_Bed : Thing { public IntVec3 Position; } }
namespace rjw
{
    public class SexProps
    {
        public Pawn pawn, partner;
        public Pawn initiator => pawn;
        public Pawn recipient => partner;
        public bool isRape;
    }
    public class JobDriver_SexBaseInitiator : JobDriver_Sex { }
    public class JobDriver_SexBaseReciever : JobDriver_Sex { }
    public class JobDriver_SexBaseRecieverRaped : JobDriver_SexBaseReciever { }
    public class JobDriver_Rape : JobDriver_SexBaseInitiator { }
    public class JobDriver_Masturbate : JobDriver_SexBaseInitiator { }
    public static partial class xxx
    {
        public static JobDef bestiality = new JobDef { defName = "Bestiality", driverClass = typeof(JobDriver_Rape) };
        public static JobDef bestialityForFemale = new JobDef { defName = "BestialityForFemale", driverClass = typeof(JobDriver_SexBaseInitiator) };
        /// <summary>返回身体及全局玩法的动物互动资格边界。</summary>
        public static bool can_do_animalsex(Pawn pawn, Pawn animal) => pawn.CanAnimalSex;
        /// <summary>使用模型种族标记识别人类。</summary>
        public static bool is_human(Pawn pawn) => !pawn.RaceProps.Animal;
        /// <summary>使用模型种族标记识别动物。</summary>
        public static bool is_animal(Pawn pawn) => pawn.RaceProps.Animal;
    }
}
namespace UnityEngine
{
    public static partial class Mathf
    {
        /// <summary>提供原有概率及成长计算使用的浮点上限。</summary>
        public static float Clamp01(float value) => Math.Clamp(value, 0, 1);
    }
}
namespace SexSlaveCraft
{
    public enum PawnIdentity { None, Master, Slave }
    public partial class CompSexSlaveTraining
    {
        public PawnIdentity pawnIdentity;
        public Pawn selectedTrainer;
        public bool IsPetDogSpecialized;
        public float specializationProgress;
        public int lastDogAnimalInteractionTick;
    }
    public static class SSCBondUtility
    {
        /// <summary>查询显式绑定引用，不把指定调教者作为主人。</summary>
        public static Pawn GetBoundMaster(Pawn pawn) => pawn?.BoundMaster;
        /// <summary>仅允许正确的有向实际绑定关系。</summary>
        public static bool IsBoundTo(Pawn target, Pawn actor) => actor != null && target?.BoundMaster == actor;
    }
    public static class SSCIdentityUtility
    {
        /// <summary>提供首次准备请求的身份查询边界。</summary>
        public static bool IsMaster(Pawn pawn) => pawn?.Training.pawnIdentity == PawnIdentity.Master;
    }
    public class JobDriver_Training : rjw.JobDriver_SexBaseInitiator { }
    public class JobDriver_RitualTraining : rjw.JobDriver_SexBaseInitiator { }
    public class JobDriver_PE : rjw.JobDriver_SexBaseInitiator { }
    public static class SSCRestrictionTrainingUtility
    {
        /// <summary>事件测试不生成调教，只为生产用途适配器提供编译签名。</summary>
        public static SSCRestrictionRequest CreateRequest(Pawn actor, Pawn target, bool ritual)
            => new SSCRestrictionRequest(actor, target, ritual ? SSCInteractionKind.RitualTraining : SSCInteractionKind.DailyTraining, true);
    }
    internal enum SSCRestrictionEvent { None, TradeConsensual, TradeForced, Dog }
    internal static class SSCRestrictionJobGuard
    {
        public static Dictionary<Job, (Pawn Actor, Pawn Target, SSCRestrictionEvent Source)> Events = new();
        public static List<(Job Interaction, Pawn Target, Job Wait)> Waits = new();
        /// <summary>事件边界使用真实请求适配和真实核心决定许可；记录通知来源但不自动冒充原生Start。</summary>
        public static bool PrepareEvent(Pawn actor, Job job, SSCRestrictionEvent source)
        {
            var driver = SSCRestrictionJobContext.GetDriver(job, actor);
            if (!SSCRestrictionJobContext.TryCreate(driver, out var request) || !SSCRestrictionPolicy.Evaluate(request).Allowed) return false;
            Events[job] = (actor, request.Receiver, source);
            return true;
        }
        /// <summary>保存生产交易入口交付的等待归属，真实清理和序列化由交互套件执行。</summary>
        public static void RegisterEventWait(Pawn actor, Job job, Pawn target, Job wait) => Waits.Add((job, target, wait));
        /// <summary>调度未接收时取消尚未实际开始的事件登记。</summary>
        public static void CancelPendingEvent(Job job) => Events.Remove(job);
        /// <summary>显式模拟外部原生开始边界，验证生产事件入口不再在调度成功后自行提示。</summary>
        public static void NotifyStarted(Job job)
        {
            if (!Events.Remove(job, out var e)) return;
            string key = e.Source == SSCRestrictionEvent.TradeConsensual ? "SSC_Message_TradeConsensualSex" :
                e.Source == SSCRestrictionEvent.TradeForced ? "SSC_Message_TradeRapeOccurred" : "DogStarted";
            Messages.Message(key.Translate(), new LookTargets(e.Actor, e.Target), MessageTypeDefOf.NeutralEvent);
        }
    }
    public static class PetSpecializationUtility
    {
        public static List<float> Gains = new();
        /// <summary>读取当前宠物方向边界，不自动授予事件资格。</summary>
        public static bool HasAnyPetState(Pawn pawn, SexSlaveSpecializationType type) => pawn?.Training.IsPetDogSpecialized == true;
        /// <summary>读取用例显式指定的终极状态。</summary>
        public static bool HasFinalPetState(Pawn pawn, SexSlaveSpecializationType type) => pawn.IsFinalDog;
        /// <summary>记录工作或行为成长，区分拒绝前已完成的工作与未发生的行为。</summary>
        public static bool TryGainPetProgress(Pawn pawn, SexSlaveSpecializationType type, float gain, bool showThresholdMessage)
        { Gains.Add(gain); return true; }
    }
    public static class OnaholeCompatibilityUtility
    {
        /// <summary>此事件套件不创建家具常驻任务，完整家具边界另由交互套件验证。</summary>
        public static bool IsBeOnaholeDriver(object driver) => false;
    }
    public static class SSCDefOf { public static JobDef SSC_Job_PetAffection = new JobDef(); }
    public static class Strings
    {
        /// <summary>提供宠物事件通知的翻译边界。</summary>
        public static string Message_DogAnimalInteractionTriggered(string actor, string target) => "DogStarted";
    }
}
