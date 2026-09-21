using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

// 游戏边界模型只重现已由本机程序集核对的回调顺序；许可、方向传递、存档及清理由生产代码决定。
namespace Verse.AI
{
    public partial class JobDriver
    {
        public int SuccessfulCleanup;
        public int SuccessfulCleanupPrefix;
        public int CurToilIndex => Index;
        /// <summary>模拟原生清理入口；成功结算探针以实际传入的结束条件计数。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Cleanup(JobCondition condition)
        {
#if !REAL_HARMONY
            SSCRestrictionJobCleanupHook.Prefix(this, ref condition);
#endif
            if (condition == JobCondition.Succeeded) SuccessfulCleanup++;
#if !REAL_HARMONY
            SSCRestrictionJobCleanupHook.Postfix(this);
#endif
        }
    }
}
namespace RimWorld
{
    public class Ability { public Pawn pawn; }
    public class CompAbilityEffect { public Ability parent; }
    public class JobDriver_Lovin : JobDriver
    {
        private int ticksLeft;
        public bool RejectReceiver;
        public int Initializations;
        /// <summary>模拟原版自身声明的预约方法，真实Harmony模式验证生产前缀安装。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryMakePreToilReservations(bool errorOnFailed)
        {
#if !REAL_HARMONY
            bool result = true;
            if (!SSCRestrictionLovinReservationHook.Prefix(this, ref result)) return result;
#endif
            return true;
        }
        /// <summary>保留原版四步骤及直接声明在Lovin类上的初始化委托，不复制生产方向或许可逻辑。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IEnumerable<Toil> MakeNewToils()
        {
            IEnumerable<Toil> result = new[] { new Toil(), new Toil(), new Toil { initAction = InitializeNativePair }, new Toil() };
#if !REAL_HARMONY
            SSCRestrictionLovinToilsHook.Postfix(this, ref result);
#endif
            return result;
        }
        /// <summary>模拟原版初始化：发起者同步创建反向接收任务，接收者使用哨兵计时，不再创建第三个任务。</summary>
        private void InitializeNativePair()
        {
            Initializations++;
            var target = (Pawn)job.targetA.Thing;
            if (target.jobs.curDriver is JobDriver_Lovin) { ticksLeft = 9999999; return; }
            if (!RejectReceiver)
            {
                var next = new Job { def = job.def, targetA = new LocalTargetInfo { Thing = pawn } };
                var receiver = (JobDriver_Lovin)next.GetCachedDriver(target);
                target.jobs.curDriver = receiver;
                if (receiver.TryMakePreToilReservations(false))
                {
                    receiver.Toils = receiver.MakeNewToils().ToList();
                    for (int i = 0; i < 4 && !receiver.Ended; i++) receiver.TryActuallyStartNextToil();
                }
                else target.jobs.EndCurrentJob(JobCondition.Incompletable, false);
            }
            ticksLeft = 500;
        }
        /// <summary>模拟原版计时保存并调用生产后缀，让旧档与新档用相同的真实守卫恢复。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ExposeData()
        {
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
#if !REAL_HARMONY
            SSCRestrictionLovinSaveHook.Postfix(this, ticksLeft);
#endif
        }
    }
}
#if NO_LIFEFORCE
namespace SSCMissingGenes
#else
namespace RJW_Genes
#endif
{
    // 类型名与可选模组一致，让生产动态发现及Harmony目标验证真正运行；不引入实际基因程序集硬依赖。
    public class JobDriver_Seduced : JobDriver
    {
        /// <summary>提供非RJW准备驱动的真实预约签名。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryMakePreToilReservations(bool errorOnFailed)
        {
#if !REAL_HARMONY
            bool result = true;
            if (!SSCRestrictionSeducedReservationHook.Prefix(this, ref result)) return result;
#endif
            return true;
        }
    }
    public class JobDriver_SexOnSpotReciever : rjw.JobDriver_SexBaseReciever { }
    public class CompAbilityEffect_Seduce : CompAbilityEffect
    {
        public bool NativeValid = true;
        public int ApplyCalls;
        /// <summary>只提供原生能力条件，SSC生产后缀负责新增许可及提示。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Valid(LocalTargetInfo target, bool throwMessages)
        {
            bool result = NativeValid;
#if !REAL_HARMONY
            SSCRestrictionSeduceValidHook.Postfix(this, target, throwMessages, ref result);
#endif
            return result;
        }
        /// <summary>计数施法副作用；前缀拒绝时不得进入此方法体。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
#if !REAL_HARMONY
            if (!SSCRestrictionSeduceApplyHook.Prefix(this, target)) return;
#endif
            ApplyCalls++;
        }
    }
}
namespace rjw
{
    public class JobDriver_BestialityForFemale : JobDriver_SexBaseInitiator
    {
        public IEnumerable<Toil> Preparation;
        /// <summary>提供宠物床上任务的步骤包装目标，清理逻辑完全使用生产守卫。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IEnumerable<Toil> MakeNewToils()
        {
            IEnumerable<Toil> result = Preparation ?? new Toil[0];
#if !REAL_HARMONY
            SSCRestrictionQuickiePreparationHook.Postfix(this, ref result);
#endif
            return result;
        }
    }
}
#if !REAL_HARMONY
namespace HarmonyLib
{
    public class HarmonyPriority : Attribute
    {
        /// <summary>提供直接调用模式所需的优先级特性，真实顺序由Harmony模式验证。</summary>
        public HarmonyPriority(int value) { }
    }
    public static class Priority { public const int First = 800; }
    public static class AccessTools
    {
        /// <summary>仅从测试程序集查找可选类型，模拟未加载类型返回null。</summary>
        public static Type TypeByName(string name) => typeof(AccessTools).Assembly.GetType(name);
        /// <summary>返回真实声明的方法签名，用于动态目标枚举。</summary>
        public static System.Reflection.MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null)
            => type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == name && (parameters == null || m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters)));
    }
}
#endif

namespace RJW_Onahole.Jobs { public class JobDriver_BeOnahole : rjw.JobDriver_SexBaseRecieverRaped { } }

#if REAL_HARMONY
[HarmonyLib.HarmonyPatch(typeof(JobDriver), "Cleanup")]
internal static class NativeLovinCleanupProbe
{
    /// <summary>模拟RJW正常优先级的成功结算前缀，验证SSC必须在其他前缀运行前修正结束条件。</summary>
    public static void Prefix(JobDriver __instance, JobCondition condition)
    {
        if (__instance is JobDriver_Lovin && condition == JobCondition.Succeeded) __instance.SuccessfulCleanupPrefix++;
    }
}
#endif
