using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

// EN: Harmony adapters for SSC sex-interaction protection.
// EN: Reservation, pre-toil and start checks delegate to one shared policy.
// CN: 这是 SSC 性行为保护规则的 Harmony 适配层。
// CN: 已接管任务先转交统一限制入口；剩余专项兼容保留后续批次的旧路径。
namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(JobDriver_Sex), "TryMakePreToilReservations")]
    public static class Patch_JobDriver_Sex_ProtectChainOfSexSlave
    {
        private sealed class StartState
        {
            public bool Started;
            public bool Rejected;
        }

        // Weak keys avoid retaining finished jobs or adding save fields.
        private static readonly ConditionalWeakTable<JobDriver_SexBaseInitiator, StartState> StartStates =
            new ConditionalWeakTable<JobDriver_SexBaseInitiator, StartState>();

        private static readonly HashSet<string> KnownConsensualReceiverJobs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GettinLoved",
                "CB_SexBaseReceiverClient"
            };

        private static readonly HashSet<string> KnownWhoringJobs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WhoreIsServingVisitors",
                "CB_ServingVisitorsForFree"
            };

        private static readonly HashSet<string> KnownDirectAllowJobs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "rjw_genes_lifeforce_randomrape",
                "rjw_genes_lifeforce_seduced"
            };

        /// <summary>生成日志中的角色名称与实例编号；空引用使用固定占位文本。</summary>
        private static string PawnInfo(Pawn pawn)
        {
            return pawn == null ? "null" : $"{pawn.LabelShort}#{pawn.thingIDNumber}";
        }

        /// <summary>返回锁链所指向主人的日志标识；无锁链或未绑定主人时返回 none。</summary>
        private static string ChainInfo(Hediff_ChainOfSexSlave chain)
        {
            return chain?.LinkedPawn == null ? "none" : PawnInfo(chain.LinkedPawn);
        }

        /// <summary>读取目标当前选择的调教师，仅用于日志诊断，不参与归属判定。</summary>
        private static string TrainerInfo(Pawn pawn)
        {
            Pawn trainer = pawn?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            return trainer == null ? "none" : PawnInfo(trainer);
        }

        /// <summary>依次尝试从字段或属性读取 Sexprops，兼容不同驱动实现；缺失或读取失败时返回 false。</summary>
        private static bool TryGetSexProps(JobDriver_Sex jobDriver, out SexProps props)
        {
            props = null;
            if (jobDriver == null) return false;

            try
            {
                props = Traverse.Create(jobDriver).Field("Sexprops").GetValue<SexProps>();
                if (props != null) return true;
            }
            catch
            {
            }

            try
            {
                props = Traverse.Create(jobDriver).Property("Sexprops").GetValue<SexProps>();
                return props != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>检查任务定义是否属于已知强制行为；空定义不与尚未初始化的 DefOf 字段匹配。</summary>
        private static bool IsKnownRapeJob(JobDef jobDef)
        {
            return jobDef != null && (jobDef == SSCDefOf.RandomRape ||
                   jobDef == SSCDefOf.RapeComfortPawn ||
                   jobDef == SSCDefOf.RapeEnemy ||
                   jobDef == SSCDefOf.RapeEnemyByAnimal ||
                   jobDef == SSCDefOf.RapeEnemyByInsect ||
                   jobDef == SSCDefOf.RapeEnemyByMech ||
                   jobDef == SSCDefOf.RapeEnemyToParasite);
        }

        /// <summary>检查任一参与者的当前任务是否属于已知服务任务，供接收方角色识别使用。</summary>
        private static bool IsKnownWhoringContext(Pawn initiator, Pawn receiver)
        {
            string initiatorJob = initiator?.CurJobDef?.defName;
            string receiverJob = receiver?.CurJobDef?.defName;
            return (!string.IsNullOrEmpty(initiatorJob) && KnownWhoringJobs.Contains(initiatorJob)) ||
                   (!string.IsNullOrEmpty(receiverJob) && KnownWhoringJobs.Contains(receiverJob));
        }

        /// <summary>通过接收任务名或双方的服务任务上下文，识别已知自愿接收行为。</summary>
        private static bool IsKnownConsensualReceiverJob(JobDriver_Sex jobDriver, Pawn initiator, Pawn receiver)
        {
            string currentJob = jobDriver?.job?.def?.defName;
            return (!string.IsNullOrEmpty(currentJob) && KnownConsensualReceiverJobs.Contains(currentJob)) ||
                   IsKnownWhoringContext(initiator, receiver);
        }

        /// <summary>判断当前任务是否在直接放行白名单内；这些兼容任务跳过 SSC 保护策略。</summary>
        private static bool ShouldDirectAllow(JobDriver_Sex jobDriver)
        {
            string currentJob = jobDriver?.job?.def?.defName;
            return !string.IsNullOrEmpty(currentJob) && KnownDirectAllowJobs.Contains(currentJob);
        }

        /// <summary>优先使用 Sexprops 确定双方角色和行为性质；未初始化时依据驱动、目标及任务定义推断。</summary>
        /// <remarks>接收方驱动的 pawn 是接受者，需要反转角色，避免将接收任务误判为其主动发起。</remarks>
        private static (Pawn aggressor, Pawn victim, bool isRape) DetermineRoles(JobDriver_Sex jobDriver)
        {
            Pawn initiator = jobDriver?.pawn;
            Pawn receiver = jobDriver?.job?.targetA.Thing as Pawn;

            if (TryGetSexProps(jobDriver, out SexProps props) && props?.pawn != null)
            {
                return (props.initiator ?? initiator, props.recipient ?? receiver, props.isRape);
            }

            if (jobDriver is JobDriver_SexBaseReciever)
            {
                bool isRapeReceiver = jobDriver is JobDriver_SexBaseRecieverRaped ||
                                      IsKnownRapeJob(jobDriver.job?.def) ||
                                      IsKnownRapeJob(receiver?.CurJobDef) ||
                                      receiver?.jobs?.curDriver is JobDriver_Rape;

                if (!isRapeReceiver && IsKnownConsensualReceiverJob(jobDriver, initiator, receiver))
                    return (receiver, initiator, false);

                return (receiver, initiator, isRapeReceiver);
            }

            // Match RJW Start's classification even for derived/mod-added JobDefs.
            return (initiator, receiver, jobDriver is JobDriver_Rape ||
                receiver?.jobs?.curDriver is JobDriver_SexBaseRecieverRaped ||
                IsKnownRapeJob(jobDriver?.job?.def));
        }

        /// <summary>记录保护检查阶段、决策原因及双方归属，便于定位放行或拒绝所经过的路径。</summary>
        private static void GuardLog(
            string phase,
            string decision,
            string reason,
            JobDriver_Sex jobDriver,
            SSCSexInteractionDecision result)
        {
            Pawn aggressor = result?.Aggressor;
            Pawn victim = result?.Victim;
            string jobDef = jobDriver?.job?.def?.defName ?? "null";
            bool playerForced = jobDriver?.job?.playerForced ?? false;
            bool allowRapeSetting = SSCMod.settings?.allowSexSlaveRape ?? false;
            SSCLog.Verbose(
                $"[SSC GuardDbg] phase={phase} decision={decision} reason={reason} " +
                $"job={jobDef} forced={playerForced} isRape={result?.IsRape ?? false} allowSexSlaveRape={allowRapeSetting} " +
                $"aggressor={PawnInfo(aggressor)} aggressorChain={ChainInfo(result?.AggressorChain)} aggressorBus={result?.IsAggressorBus ?? false} " +
                $"victim={PawnInfo(victim)} victimChain={ChainInfo(result?.VictimChain)} victimBus={result?.IsVictimBus ?? false} " +
                $"victimSelectedTrainer={TrainerInfo(victim)}");
        }

        /// <summary>将任务中的角色信息转换为共享策略输入，返回完整的保护决策而不结束任务。</summary>
        private static SSCSexInteractionDecision Evaluate(JobDriver_Sex jobDriver)
        {
            var roles = DetermineRoles(jobDriver);
            return SSCSexInteractionPolicy.Evaluate(roles.aggressor, roles.victim, roles.isRape);
        }

        /// <summary>先转交已接管普通及调教任务的预约许可；只有尚未接管的任务继续执行下面的旧保护分支。</summary>
        /// <returns>允许继续执行原预约方法时返回 true；拒绝时把预约结果设为 false 并跳过原方法。</returns>
        public static bool Prefix(JobDriver_Sex __instance, ref bool __result)
        {
            if (SSCRestrictionJobGuard.TryReserve(__instance, out bool allowed))
            {
                if (!allowed) __result = false;
                return allowed;
            }
            if (ShouldDirectAllow(__instance))
            {
                GuardLog("Reserve", "ALLOW", "direct_allow_whitelist", __instance, null);
                return true;
            }

            if (SSCMod.settings != null && !SSCMod.settings.enableSexSlaveProtectionRules)
            {
                GuardLog("Reserve", "ALLOW", "global_setting_disabled", __instance, null);
                return true;
            }

            // EN: Forced initiators are still checked before their toils and Start.
            // CN: 玩家强制指派的发起任务仍须通过步骤前与 Start 检查。
            if (__instance.job.playerForced)
            {
                GuardLog("Reserve", "ALLOW", "player_forced", __instance, null);
                return true;
            }

            SSCSexInteractionDecision decision = Evaluate(__instance);
            GuardLog("Reserve", decision.Allowed ? "ALLOW" : "BLOCK", decision.Reason, __instance, decision);
            if (decision.Allowed) return true;

            __result = false;
            return false;
        }

        /// <summary>执行开始前的共享校验；拒绝时标记未开始场景、显示提示，并结束仍属于该驱动的任务。</summary>
        /// <remarks>不立即重新选取 AI 任务，避免同一调用栈反复选择被拒绝的任务；不会结束角色的新任务。</remarks>
        private static bool CheckBeforeStart(JobDriver_SexBaseInitiator driver, string phase)
        {
            if (ShouldDirectAllow(driver))
            {
                GuardLog(phase, "ALLOW", "direct_allow_whitelist", driver, null);
                return true;
            }

            if (SSCMod.settings != null && !SSCMod.settings.enableSexSlaveProtectionRules)
            {
                GuardLog(phase, "ALLOW", "global_setting_disabled", driver, null);
                return true;
            }

            SSCSexInteractionDecision decision = Evaluate(driver);
            GuardLog(phase, decision.Allowed ? "ALLOW" : "BLOCK", decision.Reason, driver, decision);
            if (decision.Allowed) return true;

            StartStates.GetOrCreateValue(driver).Rejected = true;
            Messages.Message(
                Strings.Message_SlaveAlreadyLinked,
                new LookTargets(decision.Aggressor, decision.Victim),
                MessageTypeDefOf.RejectInput);
            // End this driver only. Defer the next AI selection until the next tick,
            // otherwise an AI can immediately select the same rejected job recursively.
            if (driver.pawn?.jobs?.curDriver == driver)
                driver.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
            return false;
        }

        /// <summary>已接管的发起和接收任务调用统一守卫；剩余发起者继续原有步骤保护。</summary>
        /// <remarks>已开始的场景继续正常收尾；读档恢复不能仅凭接收方提前登记的参与者名单判定开始。</remarks>
        /// <returns>返回 false 时阻止步骤切换，使被拒绝者不进入接收任务创建或行为初始化。</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(JobDriver), "TryActuallyStartNextToil")]
        public static bool NextToil_Prefix(JobDriver __instance)
        {
            if (__instance is JobDriver_Sex managed &&
                SSCRestrictionJobGuard.TryCheck(managed, "BeforeToil", out bool allowed)) return allowed;
            if (!(__instance is JobDriver_SexBaseInitiator initiator)) return true;
            if (initiator.pawn?.jobs?.curDriver != initiator) return true;

            if (!StartStates.TryGetValue(initiator, out StartState state))
            {
                state = StartStates.GetOrCreateValue(initiator);
                // Only recover on first seeing this driver after loading. Receiver DoSetup
                // also adds the first participant BEFORE Start, so membership alone is
                // insufficient and must not turn an observed preparation into a started scene.
                state.Started = initiator.Sexprops != null &&
                    initiator.Partner?.jobs?.curDriver is JobDriver_SexBaseReciever receiver &&
                    receiver.parteners.Contains(initiator.pawn);
            }
            if (state.Started) return true;

            // Check before advancing the toil index. In particular, do not enter the
            // receiver-creation toil or the sex toil (whose cleanup assumes Start ran).
            // Recheck after walking so a setting/owner change en route is respected.
            return CheckBeforeStart(initiator, "BeforeToil");
        }

        /// <summary>在 RJW Start 原方法前执行兜底校验；拒绝时跳过原生场景初始化。</summary>
        /// <remarks>返回 false 不会停止调用方后续语句或所有外部补丁，常规任务应由步骤前检查提前拒绝。</remarks>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), "Start")]
        public static bool Start_Prefix(JobDriver_SexBaseInitiator __instance)
        {
            if (SSCRestrictionJobGuard.TryCheck(__instance, "Start", out bool allowed)) return allowed;
            return CheckBeforeStart(__instance, "Start");
        }

        /// <summary>仅在 Start 原方法实际执行后记录开始状态，让之后的步骤保留正常收尾流程。</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), "Start")]
        public static void Start_Postfix(JobDriver_SexBaseInitiator __instance, bool __runOriginal)
        {
            if (SSCRestrictionJobGuard.TryMarkStarted(__instance, __runOriginal)) return;
            if (__runOriginal) StartStates.GetOrCreateValue(__instance).Started = true;
        }

        /// <summary>对被拒绝且尚未开始的场景跳过 RJW 原生收尾，避免访问未初始化的 Sexprops。</summary>
        /// <remarks>已开始的场景仍需正常清理；本前缀不能保证其他模组的 End 回调也被跳过。</remarks>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), "End")]
        public static bool End_Prefix(JobDriver_SexBaseInitiator __instance)
        {
            if (SSCRestrictionJobGuard.TryEnd(__instance, out bool allowed)) return allowed;
            // A rejected Start has no scene to finish. RJW End otherwise dereferences
            // uninitialized Sexprops and can enqueue partner jobs for somebody else's scene.
            return !StartStates.TryGetValue(__instance, out StartState state) || !state.Rejected || state.Started;
        }
    }
}
