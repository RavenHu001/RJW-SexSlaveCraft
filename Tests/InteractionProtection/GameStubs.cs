using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SexSlaveCraft;

// Narrow lifecycle model based on the local RimWorld 1.6 / RJW implementation.
// REAL_HARMONY runs the same scenarios through actual patched methods.
namespace Verse
{
    public class Thing { }
    public class JobDef { public string defName; }
    public struct LocalTargetInfo { public Thing Thing; }
    public class Pawn : Thing
    {
        public string LabelShort;
        public int thingIDNumber;
        public object health = new object();
        public ApparelTracker apparel = new ApparelTracker();
        public Verse.AI.Pawn_JobTracker jobs = new Verse.AI.Pawn_JobTracker();
        /// <summary>从模拟任务跟踪器读取角色的当前任务定义，供生产角色判定逻辑使用。</summary>
        public JobDef CurJobDef => jobs.curDriver?.job?.def;
        public Hediff_ChainOfSexSlave Chain;
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public bool IsBus;
        /// <summary>返回测试角色持有的训练组件；请求其他组件类型时返回 null。</summary>
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class ApparelTracker { public List<Apparel> WornApparel = new List<Apparel>(); }
    public class Apparel
    {
        public CompSSRapeCheck Protection;
        /// <summary>将模拟服装上的防护组件按请求类型返回，供防护装备策略判断。</summary>
        public T GetComp<T>() where T : class => Protection as T;
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
    }
}
namespace RimWorld
{
    public static class MessageTypeDefOf { public static readonly object RejectInput = new object(); }
}
namespace Verse.AI
{
    public enum JobCondition { Ongoing, Incompletable, Succeeded }
    public class Job
    {
        public Verse.JobDef def;
        public Verse.LocalTargetInfo targetA;
        public bool playerForced;
    }
    public class Pawn_JobTracker
    {
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
    public class JobDriver
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
        public Verse.Pawn initiator => pawn;
        /// <summary>在最小行为数据模型中，将 partner 作为接受者返回。</summary>
        public Verse.Pawn recipient => partner;
        public bool isRape, usedCondom;
    }
    public class JobDriver_Sex : Verse.AI.JobDriver
    {
        public SexProps Sexprops;
        /// <summary>从模拟任务的 A 目标读取参与角色，缺失目标或类型不匹配时返回 null。</summary>
        public Verse.Pawn Partner => job?.targetA.Thing as Verse.Pawn;
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
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

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
            var receiver = Partner.jobs.curDriver as JobDriver_SexBaseReciever;
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
    public class JobDriver_Rape : JobDriver_SexBaseInitiator { }
}
namespace SexSlaveCraft
{
    public class Hediff_ChainOfSexSlave { public Verse.Pawn LinkedPawn; }
    public class CompSSRapeCheck { }
    public class CompSexSlaveTraining
    {
        public Verse.Pawn selectedTrainer;
        public bool AllowsOthersForTrainingOrSex;
    }
    public class Settings
    {
        public bool enableSexSlaveProtectionRules = true;
        public bool allowSexSlaveRape;
        public bool protectBusAggressorRape = true;
        public bool protectChainedAggressorRape = true;
        public bool protectNonRapeOwnerOnly = true;
    }
    public static class SSCMod { public static Settings settings = new Settings(); }
    public static class SSCBondUtility
    {
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
        /// <summary>接收并忽略生产详细日志，避免测试结果被诊断文本淹没。</summary>
        public static void Verbose(string message) { }
    }
    public static class Strings { public const string Message_SlaveAlreadyLinked = "Protected"; }
    public static class SSCDefOf
    {
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
        public HarmonyPatch(Type type, string method) { Type = type; Method = method; }
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
