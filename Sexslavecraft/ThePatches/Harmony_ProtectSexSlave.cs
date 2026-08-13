using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

// EN: Harmony adapters for SSC sex-interaction protection.
// EN: Both reservation-time and start-time checks delegate to one shared policy.
// CN: 这是 SSC 性行为保护规则的 Harmony 适配层。
// CN: 预约阶段与启动阶段都会委托给同一套共享决策规则。
namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(JobDriver_Sex), "TryMakePreToilReservations")]
    public static class Patch_JobDriver_Sex_ProtectChainOfSexSlave
    {
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

        private static string PawnInfo(Pawn pawn)
        {
            return pawn == null ? "null" : $"{pawn.LabelShort}#{pawn.thingIDNumber}";
        }

        private static string ChainInfo(Hediff_ChainOfSexSlave chain)
        {
            return chain?.LinkedPawn == null ? "none" : PawnInfo(chain.LinkedPawn);
        }

        private static string TrainerInfo(Pawn pawn)
        {
            Pawn trainer = pawn?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            return trainer == null ? "none" : PawnInfo(trainer);
        }

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

        private static bool IsKnownRapeJob(JobDef jobDef)
        {
            return jobDef == SSCDefOf.RandomRape ||
                   jobDef == SSCDefOf.RapeComfortPawn ||
                   jobDef == SSCDefOf.RapeEnemy ||
                   jobDef == SSCDefOf.RapeEnemyByAnimal ||
                   jobDef == SSCDefOf.RapeEnemyByInsect ||
                   jobDef == SSCDefOf.RapeEnemyByMech ||
                   jobDef == SSCDefOf.RapeEnemyToParasite;
        }

        private static bool IsKnownWhoringContext(Pawn initiator, Pawn receiver)
        {
            string initiatorJob = initiator?.CurJobDef?.defName;
            string receiverJob = receiver?.CurJobDef?.defName;
            return (!string.IsNullOrEmpty(initiatorJob) && KnownWhoringJobs.Contains(initiatorJob)) ||
                   (!string.IsNullOrEmpty(receiverJob) && KnownWhoringJobs.Contains(receiverJob));
        }

        private static bool IsKnownConsensualReceiverJob(JobDriver_Sex jobDriver, Pawn initiator, Pawn receiver)
        {
            string currentJob = jobDriver?.job?.def?.defName;
            return (!string.IsNullOrEmpty(currentJob) && KnownConsensualReceiverJobs.Contains(currentJob)) ||
                   IsKnownWhoringContext(initiator, receiver);
        }

        private static bool ShouldDirectAllow(JobDriver_Sex jobDriver)
        {
            string currentJob = jobDriver?.job?.def?.defName;
            return !string.IsNullOrEmpty(currentJob) && KnownDirectAllowJobs.Contains(currentJob);
        }

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

            return (initiator, receiver, IsKnownRapeJob(jobDriver?.job?.def));
        }

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

        private static SSCSexInteractionDecision Evaluate(JobDriver_Sex jobDriver)
        {
            var roles = DetermineRoles(jobDriver);
            return SSCSexInteractionPolicy.Evaluate(roles.aggressor, roles.victim, roles.isRape);
        }

        public static bool Prefix(JobDriver_Sex __instance, ref bool __result)
        {
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

            // EN: Player-forced jobs are checked by the Start adapter after RimWorld creates the job.
            // CN: 玩家强制指派的 Job 会在创建后由 Start 适配层检查。
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

        [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), "Start")]
        public static bool Start_Prefix(JobDriver_SexBaseInitiator __instance)
        {
            if (ShouldDirectAllow(__instance))
            {
                GuardLog("Start", "ALLOW", "direct_allow_whitelist", __instance, null);
                return true;
            }

            if (SSCMod.settings != null && !SSCMod.settings.enableSexSlaveProtectionRules)
            {
                GuardLog("Start", "ALLOW", "global_setting_disabled", __instance, null);
                return true;
            }

            SSCSexInteractionDecision decision = Evaluate(__instance);
            GuardLog("Start", decision.Allowed ? "ALLOW" : "BLOCK", decision.Reason, __instance, decision);
            if (decision.Allowed) return true;

            Messages.Message(
                Strings.Message_SlaveAlreadyLinked,
                new LookTargets(decision.Aggressor, decision.Victim),
                MessageTypeDefOf.RejectInput);
            __instance.pawn.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
            return false;
        }
    }
}
