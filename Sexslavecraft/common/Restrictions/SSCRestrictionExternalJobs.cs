using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>接入不继承 RJW 场景驱动的已知入口；这里只还原方向，不另行定义许可。</summary>
    internal static class SSCRestrictionExternalJobs
    {
        // 使用完整类型名探测可选模组，程序集不存在时不会触发加载依赖。
        // Seduced 的 pawn 是被吸引后移动的一方；targetA 才是随后启动 sex_on_spot 的施法者。
        internal const string SeducedType = "RJW_Genes.JobDriver_Seduced";
        internal const string SeduceEffectType = "RJW_Genes.CompAbilityEffect_Seduce";
        internal const string SpotReceiverType = "RJW_Genes.JobDriver_SexOnSpotReciever";

        /// <summary>检查实际驱动类型而非仅凭 JobDef 名称，避免其他模组复用名称时获得错误方向。</summary>
        public static bool IsSeduced(JobDriver driver) => driver?.GetType().FullName == SeducedType;

        /// <summary>按已核对的 RJW_Genes SexOnSpot 普通发起驱动分类，保持准备与实际场景的许可一致。</summary>
        /// <remarks>虽然能力界面受 RJW rape_enabled 开关控制，实际 SexOnSpot 不继承 Rape 且使用普通接收者，故沿用 RJW 非强制分类。</remarks>
        public static SSCRestrictionRequest SeduceRequest(Pawn caster, Pawn target)
            => new SSCRestrictionRequest(caster, target, SSCInteractionKind.Consensual,
                caster != null && target != null && caster != target);

        /// <summary>返回玩家可读的统一拒绝原因；不在高频 AI 查询中主动弹出提示。</summary>
        public static string Rejection(SSCRestrictionDecision decision)
            => "SSC_Restrictions_JobRejected".Translate(("SSC_Restrictions_Reason_" + decision.Reason).Translate());

        /// <summary>查询已知独立任务的许可；false 仅表示该工作不在本适配器范围内。</summary>
        public static bool TryReserve(Job job, Pawn pawn, out bool allowed)
        {
            allowed = true;
            Type type = job?.def?.driverClass;
            if (type == null) return false;
            if (typeof(JobDriver_Lovin).IsAssignableFrom(type))
            {
                allowed = SSCRestrictionLovinGuard.Check((JobDriver_Lovin)job.GetCachedDriver(pawn), false);
                return true;
            }
            if (type.FullName != SeducedType) return false;
            allowed = SSCRestrictionPolicy.Evaluate(SeduceRequest(job.targetA.Thing as Pawn, pawn)).Allowed;
            return true;
        }

        /// <summary>为已拒绝的玩家命令生成同一有向请求的具体原因，AI候选查询不调用提示。</summary>
        public static string RejectionReason(Job job, Pawn pawn)
        {
            if (job.GetCachedDriver(pawn) is JobDriver_Lovin lovin) return SSCRestrictionLovinGuard.RejectionReason(lovin);
            return Rejection(SSCRestrictionPolicy.Evaluate(SeduceRequest(job.targetA.Thing as Pawn, pawn)));
        }

        /// <summary>独立驱动在步骤切换前重新查询；Seduced 全程只是准备，走近成功不等于场景已开始。</summary>
        public static bool CheckBeforeToil(JobDriver driver)
        {
            if (driver is JobDriver_Lovin lovin) return SSCRestrictionLovinGuard.Check(lovin, true);
            if (!IsSeduced(driver)) return true;
            if (driver.pawn?.jobs?.curDriver != driver) return false;
            SSCRestrictionDecision decision = SSCRestrictionPolicy.Evaluate(SeduceRequest(driver.job.targetA.Thing as Pawn, driver.pawn));
            if (decision.Allowed) return true;
            // 在交接步骤前失败，不执行原模组的取消征召、增加记忆或启动另一方任务。
            SSCLog.Verbose($"[SSC Restrictions] Seduced rejected reason={decision.Reason}");
            driver.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class SSCRestrictionSeducedReservationHook
    {
        /// <summary>缺失可选驱动时跳过整个补丁类；仅返回空TargetMethods仍会使Harmony尝试无目标补丁。</summary>
        public static bool Prepare() => TargetMethods().Any();

        /// <summary>只为当前已安装的 Seduced 驱动登记预约补丁；缺模组时返回空目标集。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type type = AccessTools.TypeByName(SSCRestrictionExternalJobs.SeducedType);
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, "TryMakePreToilReservations", new[] { typeof(bool) });
            if (method != null) yield return method;
        }

        /// <summary>准备任务也执行同一请求，拒绝时不产生预约。</summary>
        public static bool Prefix(JobDriver __instance, ref bool __result)
        {
            if (!SSCRestrictionExternalJobs.TryReserve(__instance.job, __instance.pawn, out bool allowed) || allowed) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class SSCRestrictionSeduceValidHook
    {
        /// <summary>能力不存在或版本签名改变时不安装补丁，避免缺少可选模组导致整批PatchAll失败。</summary>
        public static bool Prepare() => TargetMethods().Any();

        /// <summary>动态定位可选能力的目标验证方法，避免编译时引用基因模组。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type type = AccessTools.TypeByName(SSCRestrictionExternalJobs.SeduceEffectType);
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, "Valid", new[] { typeof(LocalTargetInfo), typeof(bool) });
            if (method != null) yield return method;
        }

        /// <summary>保留能力自身的身体及目标条件，再添加统一许可；只在原调用要求提示时显示拒绝原因。</summary>
        public static void Postfix(CompAbilityEffect __instance, LocalTargetInfo target, bool throwMessages, ref bool __result)
        {
            if (!__result) return;
            SSCRestrictionDecision decision = SSCRestrictionPolicy.Evaluate(SSCRestrictionExternalJobs.SeduceRequest(__instance.parent?.pawn, target.Thing as Pawn));
            __result = decision.Allowed;
            if (!__result && throwMessages)
                Messages.Message(SSCRestrictionExternalJobs.Rejection(decision), __instance.parent.pawn, MessageTypeDefOf.RejectInput, false);
        }
    }

    [HarmonyPatch]
    internal static class SSCRestrictionSeduceApplyHook
    {
        /// <summary>只在存在已核对的施法方法时启用补丁，不把缺席情况作为许可白名单。</summary>
        public static bool Prepare() => TargetMethods().Any();

        /// <summary>动态定位实际施法入口；Apply 前缀必须早于原方法中的 StopAll 执行。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type type = AccessTools.TypeByName(SSCRestrictionExternalJobs.SeduceEffectType);
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, "Apply", new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
            if (method != null) yield return method;
        }

        /// <summary>施法时再查一次，覆盖选中目标后配置改变或外部直接调用 Apply 的路径，拒绝不打断目标工作。</summary>
        public static bool Prefix(CompAbilityEffect __instance, LocalTargetInfo target)
        {
            SSCRestrictionDecision decision = SSCRestrictionPolicy.Evaluate(SSCRestrictionExternalJobs.SeduceRequest(__instance.parent?.pawn, target.Thing as Pawn));
            if (!decision.Allowed)
                SSCLog.Verbose($"[SSC Restrictions] Seduce Apply rejected reason={decision.Reason}");
            return decision.Allowed;
        }
    }
}
