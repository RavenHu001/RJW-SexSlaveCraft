using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using SexSlaveCraft;

// Narrow lifecycle model based on the local RimWorld 1.6 / RJW implementation.
// REAL_HARMONY runs the same scenarios through actual patched methods.
namespace Verse
{
    public class Thing { }
    public class JobDef { public string defName; public Type driverClass; }
    public struct LocalTargetInfo { public Thing Thing; }
    public class Pawn : Thing
    {
        public string LabelShort;
        public int thingIDNumber;
        public bool Dead, Destroyed, Downed, IsSlave, IsPrisonerOfColony;
        public bool IsColonist = true;
        public object health = new object();
        public ApparelTracker apparel = new ApparelTracker();
        public Verse.AI.Pawn_JobTracker jobs = new Verse.AI.Pawn_JobTracker();
        /// <summary>从模拟任务跟踪器读取角色的当前任务定义，供生产角色判定逻辑使用。</summary>
        public Verse.AI.Job CurJob => jobs.curDriver?.job;
        /// <summary>读取当前模拟任务定义。</summary>
        public JobDef CurJobDef => jobs.curDriver?.job?.def;
        public Hediff_ChainOfSexSlave Chain;
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public bool IsBus;
        public int ClearedReservations;
        /// <summary>记录取消被拒绝 AI 候选的预约，不模拟地图预约实现。</summary>
        public void ClearReservationsForJob(Verse.AI.Job job) { ClearedReservations++; }
        /// <summary>返回测试角色持有的训练组件；请求其他组件类型时返回 null。</summary>
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class ApparelTracker { public List<Apparel> WornApparel = new List<Apparel>(); }
    public class Apparel
    {
        public ThingDef def = new ThingDef();
    }
    public class LookTargets
    {
        /// <summary>接受生产提示传入的角色列表；测试不模拟镜头定位，因此无需保存目标。</summary>
        public LookTargets(params Pawn[] pawns) { }
    }
    public static class Messages
    {
        public static int Count;
        /// <summary>记录一次游戏提示调用，供用例检查拒绝提示是否重复；不显示真实界面。</summary>
        public static void Message(string message, LookTargets targets, object type) { Count++; }
        /// <summary>记录玩家命令被拒绝时的单角色提示。</summary>
        public static void Message(string message, Pawn target, object type, bool historical) { Count++; }
    }
}
namespace RimWorld
{
    public static class JobDefOf { public static readonly Verse.JobDef GotoMindControlled = new Verse.JobDef { defName = "GotoMindControlled" }, Goto = new Verse.JobDef { defName = "Goto" }, Wait = new Verse.JobDef { defName = "Wait" }; }
    public static class MessageTypeDefOf { public static readonly object PositiveEvent = new object(), NegativeEvent = new object(), NeutralEvent = new object(), RejectInput = new object(); }
}
namespace Verse.AI
{
    public enum JobCondition { Ongoing, Incompletable, Succeeded }
    public class Job
    {
        private static int nextId;
        public int loadID = ++nextId;
        public JobDriver CachedDriver;
        /// <summary>返回用例构造的新任务缓存驱动，不切换角色当前任务。</summary>
        public JobDriver GetCachedDriver(Verse.Pawn pawn) => CachedDriver ??= MakeDriver(pawn);
        /// <summary>按本机原版MakeDriver创建新的运行实例，故意不复用预检缓存驱动。</summary>
        public JobDriver MakeDriver(Verse.Pawn pawn)
        {
            var driver = (JobDriver)Activator.CreateInstance(def.driverClass);
            driver.pawn = pawn; driver.job = this;
            return driver;
        }

        public Verse.JobDef def;
        public Verse.LocalTargetInfo targetA;
        public bool playerForced;
    }
    public struct ThinkResult
    {
        public Job Job;
        public static readonly ThinkResult NoJob = new ThinkResult();
    }
    public class ThinkNode_JobGiver
    {
        public Job Candidate;
        /// <summary>模拟思考树生成候选后的生产补丁，拒绝后上层可继续其他节点。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThinkResult TryIssueJobPackage(Verse.Pawn pawn)
        {
            var result = new ThinkResult { Job = Candidate };
#if !REAL_HARMONY
            SSCRestrictionAutomaticJobHook.Postfix(pawn, ref result);
#endif
            return result;
        }
    }
    public static class JobMaker
    {
        public static Job LastReturned;
        /// <summary>记录未使用候选已归还对象池，避免模型隐去资源释放要求。</summary>
        public static void ReturnToPool(Job job) { LastReturned = job; }
    }
    public class QueuedJob { public Job job; }
    public class JobQueue : List<QueuedJob>
    {
        /// <summary>删除满足条件的排队任务；测试不额外模拟预约资源。</summary>
        public void RemoveAll(Verse.Pawn pawn, Predicate<Job> predicate) => RemoveAll(q => predicate(q.job));
    }
    public class Pawn_JobTracker
    {
        public JobQueue jobQueue = new JobQueue();
        public Verse.Pawn pawn;
        public int OrderedMutations;
        /// <summary>模拟原版命令入口会修改队列；拒绝应在这些副作用发生前返回。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryTakeOrderedJob(Job job)
        {
#if !REAL_HARMONY
            bool result = true;
            if (!SSCRestrictionOrderedJobHook.Prefix(job, pawn, ref result)) return result;
#endif
            job.playerForced = true;
            OrderedMutations++;
            return true;
        }
        public JobDriver curDriver;
        public int ImmediateJobSearches;
        /// <summary>结束当前模拟任务，并记录是否请求立即重新选取任务，用于检测拒绝后的递归风险。</summary>
        public void EndCurrentJob(JobCondition condition, bool startNewJob = true)
        {
            curDriver?.EndJobWith(condition);
            if (startNewJob) ImmediateJobSearches++;
        }
    }
    public class Toil
    {
        public Action initAction;
        public Action finishAction;
    }
    public partial class JobDriver
    {
        public Verse.Pawn pawn;
        public Job job;
        public List<Toil> Toils = new List<Toil>();
        public int Index = -1;
        public bool Ended;
        public JobCondition EndCondition;
        public int EndCalls;

        // Production game calls this before entering each toil, including after walking.
        /// <summary>模拟步骤切换的检查入口：先清理旧步骤，再推进索引并初始化新步骤；步骤耗尽时结束任务。</summary>
        /// <remarks>真实 Harmony 模式由补丁拦截此方法，默认模式在方法内显式调用相同生产前缀。</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TryActuallyStartNextToil()
        {
#if !REAL_HARMONY
            if (!Patch_JobDriver_Sex_ProtectChainOfSexSlave.NextToil_Prefix(this)) return;
#endif
            if (pawn.jobs.curDriver != this || Ended) return;
            CleanupToil();
            Index++;
            if (Index >= Toils.Count) EndJobWith(JobCondition.Succeeded);
            else Toils[Index].initAction?.Invoke();
        }
        /// <summary>仅结束仍为角色当前驱动且未结束的模拟任务，执行当前步骤清理并解除驱动引用。</summary>
        public void EndJobWith(JobCondition condition)
        {
            if (pawn?.jobs.curDriver != this || Ended) return;
            Ended = true;
            EndCondition = condition;
            EndCalls++;
            Cleanup(condition);
            CleanupToil();
            pawn.jobs.curDriver = null;
        }
        /// <summary>当前索引指向有效步骤时调用其收尾委托；未进入步骤或索引已越界时不处理。</summary>
        private void CleanupToil()
        {
            if (Index >= 0 && Index < Toils.Count) Toils[Index].finishAction?.Invoke();
        }
    }
}
namespace rjw
{
    public class SexProps
    {
        public Verse.Pawn pawn, partner;
        /// <summary>在最小行为数据模型中，将 pawn 作为发起者返回。</summary>
        public Verse.Pawn initiator => isReceiver ? partner : pawn;
        /// <summary>在最小行为数据模型中，将 partner 作为接受者返回。</summary>
        public Verse.Pawn recipient => isReceiver ? pawn : partner;
        public bool isReceiver, isRevese, isRape, usedCondom;
    }
    public class JobDriver_Sex : Verse.AI.JobDriver
    {
        public SexProps Sexprops;
        public Verse.Pawn PartnerPawn;
        public int duration = 1000, ticks_left = 1000, orgasms;
        /// <summary>模拟驱动保存入口，在无真实补丁模式下直接调用生产序列化。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual void ExposeData()
        {
#if !REAL_HARMONY
            SSCRestrictionSceneSaveHook.Postfix(this);
#endif
        }
        /// <summary>从模拟任务的 A 目标读取参与角色，缺失目标或类型不匹配时返回 null。</summary>
        public Verse.Pawn Partner => PartnerPawn ?? job?.targetA.Thing as Verse.Pawn;
        /// <summary>模拟 RJW 基类默认成功的预约方法；默认模式显式调用生产预约前缀。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual bool TryMakePreToilReservations(bool errorOnFailed)
        {
#if !REAL_HARMONY
            bool result = true;
            if (!Patch_JobDriver_Sex_ProtectChainOfSexSlave.Prefix(this, ref result)) return result;
#endif
            return true;
        }
    }
    public class JobDriver_SexBaseReciever : JobDriver_Sex
    {
        public List<Verse.Pawn> parteners = new List<Verse.Pawn>();
    }
    public class JobDriver_SexBaseRecieverRaped : JobDriver_SexBaseReciever { }
    public class JobDriver_SexBaseInitiator : JobDriver_Sex
    {
        public int StartCalls, SceneEndCalls, ExternalEndCalls;
        public int ReceiverCreations, ReservationWarnings, AfterStartCalls;
        public int TargetEffects, CompletedEffects;

        // RJW initiators override reservations without calling the patched base method.
        /// <summary>模拟发起者覆盖预约方法且不调用基类的路径，保留原漏洞所依赖的预约绕过条件。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
#if !REAL_HARMONY
            bool result = true;
            if (!SSCRestrictionReservationHook.Prefix(this, ref result)) return result;
#endif
            return true;
        }

        /// <summary>模拟 RJW Start 的参与者登记和行为数据初始化，并记录原方法实际执行次数。</summary>
        /// <remarks>默认模式显式调用生产前后缀；真实 Harmony 模式由 PatchAll 安装这些检查。</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Start()
        {
#if !REAL_HARMONY
            if (!Patch_JobDriver_Sex_ProtectChainOfSexSlave.Start_Prefix(this))
            {
                Patch_JobDriver_Sex_ProtectChainOfSexSlave.Start_Postfix(this, false);
                return;
            }
#endif
            StartCalls++;
            var receiver = Partner?.jobs.curDriver as JobDriver_SexBaseReciever;
            if (receiver != null && !receiver.parteners.Contains(pawn)) receiver.parteners.Add(pawn);
            Sexprops ??= new SexProps { pawn = pawn, partner = Partner, isRape = this is JobDriver_Rape };
#if !REAL_HARMONY
            Patch_JobDriver_Sex_ProtectChainOfSexSlave.Start_Postfix(this, true);
#endif
        }
        /// <summary>模拟 RJW 原生收尾的数据读取和参与者移除，暴露未初始化 Sexprops 导致的清理错误。</summary>
        /// <remarks>同时保留外部动画收尾计数，用于区分跳过原方法和完全未进入收尾入口。</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void End()
        {
#if !REAL_HARMONY
            ExternalEndCalls++;
            if (!Patch_JobDriver_Sex_ProtectChainOfSexSlave.End_Prefix(this)) return;
#endif
            SceneEndCalls++;
            if (Partner?.jobs.curDriver is JobDriver_SexBaseReciever receiver)
            {
                // RJW End reads Sexprops when another participant already exists.
                if (receiver.parteners.Count == 1) _ = Sexprops.isRape;
                receiver.parteners.Remove(pawn);
            }
        }
        /// <summary>按 RJW 关键时序构造行走、创建接收任务、执行场景和结算步骤，供生产保护补丁驱动测试。</summary>
        public void MakeScenarioToils()
        {
            Toils.Add(new Verse.AI.Toil()); // walking
            // 准备回调：复用现有接收任务，否则创建任务并模拟预约失败黄字和提前登记参与者。
            Toils.Add(new Verse.AI.Toil { initAction = () =>
            {
                if (Partner.jobs.curDriver is JobDriver_SexBaseReciever) return;
                ReceiverCreations++;
                JobDriver_SexBaseReciever receiver = this is JobDriver_Rape
                    ? new JobDriver_SexBaseRecieverRaped() : new JobDriver_SexBaseReciever();
                receiver.pawn = Partner;
                receiver.job = new Verse.AI.Job
                {
                    def = new Verse.JobDef { defName = this is JobDriver_Rape ? "GettinRaped" : "GettinLoved" },
                    targetA = new Verse.LocalTargetInfo { Thing = pawn }
                };
                Partner.jobs.curDriver = receiver;
                if (!receiver.TryMakePreToilReservations(false))
                {
                    ReservationWarnings++;
                    receiver.EndJobWith(Verse.AI.JobCondition.Incompletable);
                }
                else receiver.parteners.Add(pawn); // RJW receiver DoSetup runs before initiator Start.
            }});
            Toils.Add(new Verse.AI.Toil
            {
                // 开始回调：保留 Start 前后的可观察副作用，检测仅跳过 void 原方法的不足。
                initAction = () =>
                {
                    TargetEffects++; // RJW changes the target before calling Start.
                    Start();
                    Sexprops.usedCondom = true; // RJW caller continues after a void Start.
                    AfterStartCalls++;
                },
                finishAction = End
            });
            // 结算回调：记录进入成功结算步骤的次数。
            Toils.Add(new Verse.AI.Toil { initAction = () => CompletedEffects++ });
        }
    }
    public class JobDriver_SexQuick : JobDriver_SexBaseInitiator
    {
        /// <summary>提供真实 Harmony 可补丁的方法，实际准备步骤由各用例构造。</summary>
        protected virtual IEnumerable<Verse.AI.Toil> MakeNewToils() => Toils;
    }
    public class JobDriver_Masturbate : JobDriver_SexBaseInitiator { }
    public class JobDriver_Rape : JobDriver_SexBaseInitiator { }
}
namespace SexSlaveCraft
{
    public class Hediff_ChainOfSexSlave { public Verse.Pawn LinkedPawn; }
    public enum PawnIdentity { Unset, Slave, Master }
    public enum SexSlaveSpecializationType { None, Bus, Cow, PetCat, PetDog, PetRabbit }
    public class JobDriver_Training : rjw.JobDriver_SexBaseInitiator { }
    public class JobDriver_RitualTraining : rjw.JobDriver_SexBaseInitiator
    {
        public int CancelCalls;
        /// <summary>记录整场取消请求；真实仪式信号和清理由仪式生命周期套件覆盖。</summary>
        public void AbortForRestriction(string reason) { CancelCalls++; }
    }
    public class JobDriver_PE : rjw.JobDriver_SexBaseInitiator { }
    public static class OnaholeCompatibilityUtility
    {
        public static int UnregisterCalls;
        /// <summary>提供常驻家具类型识别边界，真实参与者许可仍由生产守卫执行。</summary>
        public static bool IsBeOnaholeDriver(object driver) => driver is RJW_Onahole.Jobs.JobDriver_BeOnahole;
        /// <summary>记录家具参与者解除次数，不模拟外部家具模组。</summary>
        public static void TryUnregisterOnaholePartner(Verse.Pawn target, Verse.Pawn actor)
        {
            UnregisterCalls++;
            if (target?.jobs.curDriver is RJW_Onahole.Jobs.JobDriver_BeOnahole receiver && receiver.PartnerPawn == actor)
            { receiver.PartnerPawn = null; receiver.parteners.Remove(actor); }
        }
    }
    public static class TrainingJobUtility
    {
        /// <summary>为生产拒绝清理提供记录验证失败的最小边界。</summary>
        public static void MarkValidationFailure(Verse.Pawn target, string prefix) { }
    }
    public static class SSCIdentityUtility
    {
        /// <summary>从显式身份读出主人资格，不推测关系。</summary>
        public static bool IsMaster(Verse.Pawn pawn) => pawn?.Training.pawnIdentity == PawnIdentity.Master;
        /// <summary>提供身份查询边界；真实身份切换由 TrainerIdentity 套件覆盖。</summary>
        public static bool IsTrainer(Verse.Pawn pawn) => IsMaster(pawn) || (pawn?.Training.pawnIdentity == PawnIdentity.Slave && pawn.Training.slaveTrainerEnabled);
    }
    public static class TrainerAssignmentUtility
    {
        /// <summary>只提供有效指定对象查询，真实指派逻辑由 TrainerIdentity 套件覆盖。</summary>
        public static Verse.Pawn GetActiveAssignedTrainer(Verse.Pawn target)
        {
            var actor = target?.Training.selectedTrainer;
            return actor != null && !actor.Dead && !actor.Destroyed && actor != target && SSCIdentityUtility.IsTrainer(actor) ? actor : null;
        }
    }
    public class CompSexSlaveTraining
    {
        public PawnIdentity pawnIdentity;
        public SexSlaveSpecializationType specializationType;
        public SSCRestrictionConfig restrictionConfig = new SSCRestrictionConfig();
        public Verse.Pawn selectedTrainer;
        public bool AllowsOthersForTrainingOrSex, slaveTrainerEnabled;
    }
    public class Settings
    {
        public bool enableSexSlaveProtectionRules = true;
        public bool enableSpecializationRestrictionOverrides = true;
        public SSCRestrictionRules restrictionDefaults = new SSCRestrictionRules();
        public bool allowSexSlaveRape;
        public bool protectBusAggressorRape = true;
        public bool protectChainedAggressorRape = true;
        public bool protectNonRapeOwnerOnly = true;
    }
    public static class SSCMod { public static Settings settings = new Settings(); }
    public static class SSCBondUtility
    {
        /// <summary>由锁链引用确定实际绑定主人。</summary>
        public static Verse.Pawn GetBoundMaster(Verse.Pawn pawn) => pawn?.Chain?.LinkedPawn;
        /// <summary>仅承认目标指向发起者的有向绑定。</summary>
        public static bool IsBoundTo(Verse.Pawn slave, Verse.Pawn owner) => owner != null && GetBoundMaster(slave) == owner;
        /// <summary>直接读取模拟角色的锁链，代替真实游戏中的健康状态查找。</summary>
        public static Hediff_ChainOfSexSlave GetChain(Verse.Pawn pawn) => pawn?.Chain;
    }
    public static class BusSpecializationUtility
    {
        /// <summary>读取测试用公交车状态标记；空角色不具有该状态。</summary>
        public static bool HasAnyBusState(Verse.Pawn pawn) => pawn?.IsBus == true;
    }
    public static class SSCLog
    {
        public static bool VerboseEnabled = false;
        public static readonly List<string> Warnings = new List<string>();
        /// <summary>接收并忽略生产详细日志，避免测试结果被诊断文本淹没。</summary>
        public static void Verbose(string message) { }
        /// <summary>记录兼容警告，供测试验证缺席安静、接口变化只提示一次。</summary>
        public static void WarningImportant(string message) => Warnings.Add(message);
    }
    public static class Strings {
        public const string Message_SlaveAlreadyLinked = "Protected";
        /// <summary>提供宠物事件翻译边界，实际开始时才会调用。</summary>
        public static string Message_DogAnimalInteractionTriggered(string actor, string target) => "DogStarted";
    }
    public static class SSCDefOf
    {
        public static readonly Verse.JobDef SSC_TrainingReceiver = Def("SSC_TrainingReceiver");
        public static readonly Verse.JobDef RandomRape = Def("RandomRape"),
            RapeComfortPawn = Def("RapeComfortPawn"), RapeEnemy = Def("RapeEnemy"),
            RapeEnemyByAnimal = Def("RapeEnemyByAnimal"), RapeEnemyByInsect = Def("RapeEnemyByInsect"),
            RapeEnemyByMech = Def("RapeEnemyByMech"), RapeEnemyToParasite = Def("RapeEnemyToParasite");
        /// <summary>创建具有稳定实例身份的模拟任务定义，供生产代码的 DefOf 引用比较使用。</summary>
        private static Verse.JobDef Def(string name) => new Verse.JobDef { defName = name };
    }
}
#if REAL_HARMONY
// Models the installed animation mod's normal-priority, ref-instance End prefix.
[HarmonyLib.HarmonyPatch(typeof(rjw.JobDriver_SexBaseInitiator), "End")]
internal static class AnimationEndProbe
{
    /// <summary>模拟外部动画模组的 End 前缀，只记录回调次数以检测拒绝是否误入外部收尾。</summary>
    public static void Prefix(ref rjw.JobDriver_SexBaseInitiator __instance) { __instance.ExternalEndCalls++; }
}
#endif
#if !REAL_HARMONY
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute
    {
        public Type Type;
        public string Method;
        /// <summary>保存补丁目标类型和方法名，供无 Harmony 依赖模式下的注册元数据检查使用。</summary>
        public HarmonyPatch(Type type = null, string method = null) { Type = type; Method = method; }
    }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public class Traverse
    {
        private object instance;
        private string member;
        /// <summary>创建持有目标对象的最小反射适配器，替代本测试所需的 Harmony Traverse 接口。</summary>
        public static Traverse Create(object value) => new Traverse { instance = value };
        /// <summary>记录后续要读取的字段名，并返回当前适配器以支持链式调用。</summary>
        public Traverse Field(string name) { member = name; return this; }
        /// <summary>记录后续要读取的属性名，并返回当前适配器以支持链式调用。</summary>
        public Traverse Property(string name) { member = name; return this; }
        /// <summary>读取指定名称的公开字段或属性并转换为请求类型，仅模拟生产补丁实际使用的查询。</summary>
        public T GetValue<T>() => (T)(instance.GetType().GetField(member)?.GetValue(instance)
            ?? instance.GetType().GetProperty(member)?.GetValue(instance));
    }
}
#endif
