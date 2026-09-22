using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using rjw;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(TradeDeal), "TryExecute")]
    public static class Patch_TradeDeal_TryExecute_BusSex
    {
        // 保持既有接近时间；等待只是准备，不能作为事件实际发生的证据。
        private const int PartnerWaitTicks = 600;

        /// <summary>仅成功成交后掷骰一次并保留既有交易成长；互动许可失败不撤销交易、不重掷。</summary>
        public static void Postfix(bool __result, bool actuallyTraded)
        {
            // 1. 基础检查：交易必须成功
            if (!__result || !actuallyTraded) return;

            // 2. 获取主角
            Pawn busPawn = TradeSession.playerNegotiator; // 你的小人
            if (busPawn == null || busPawn.Dead || busPawn.Downed) return;

            // 3. 检查公交车系状态
            if (!BusSpecializationUtility.HasAnyBusState(busPawn)) return;

            // 4. 获取交易者
            object trader = TradeSession.trader;

            // 5. 根据交易类型处理
            if (trader is TradeShip)
            {
                // 飞船交易：什么都不做
                return;
            }
            else if (trader is Faction faction)
            {
                // 派系交易：增加0.10 hediff，发消息
                float increaseAmount = 0.10f;
                if (!BusSpecializationUtility.HasFinalBusState(busPawn))
                {
                    ConditioningUtility.IncreaseBusHediffSeverity(busPawn, increaseAmount);
                }
                
                Messages.Message("SSC_Message_TradeFactionHappy".Translate(busPawn.LabelShort),
                    busPawn, MessageTypeDefOf.PositiveEvent);
                return;
            }
            else if (trader is Pawn traderPawn)
            {
                // Pawn商人：检查对方状态
                if (traderPawn.Dead || traderPawn.Downed) return;

                // ====================================================
                // 5. 获取恶堕率并计算性行为概率
                // ====================================================
                Need_Corruption corruptionNeed = busPawn.needs?.TryGetNeed<Need_Corruption>();
                float corruptionLevel = corruptionNeed != null ? corruptionNeed.CurLevel : 0f;

                // 计算主动概率
                int voluntaryChance;
                if (corruptionLevel >= 0.20f)
                {
                    voluntaryChance = 100;
                }
                else
                {
                    // 0% 到 20% 之间：10 + (当前值 / 0.20) * 90
                    voluntaryChance = Mathf.RoundToInt(10f + (corruptionLevel / 0.20f) * 90f);
                }

                // 计算剩余概率的分配 (按 8:1 分配给强暴和原谅)
                int remainingChance = 100 - voluntaryChance;
                int rapeChance = Mathf.RoundToInt(remainingChance * (8f / 9f));

                // 掷骰子 1-100
                int roll = Rand.RangeInclusive(1, 100);

                // ====================================================
                // 6. 根据概率执行不同性行为逻辑
                // ====================================================
                if (roll <= voluntaryChance)
                {
                    // 主动做爱
                    StartConsensualSex(busPawn, traderPawn);
                }
                else if (roll <= voluntaryChance + rapeChance)
                {
                    // 拒绝，但被强暴 (注意：施暴者是 trader, 受害者是 busPawn)
                    StartRapeSex(traderPawn, busPawn);
                }
                else
                {
                    // 被商人原谅
                    Messages.Message("SSC_Message_TradeRejectedForgiven".Translate(traderPawn.LabelShort, busPawn.LabelShort, busPawn.gender.GetPronoun()),
                        new LookTargets(busPawn, traderPawn), MessageTypeDefOf.NeutralEvent);
                }

                // ====================================================
                // 7. 增加公交车hediff严重程度
                // ====================================================
                if (!BusSpecializationUtility.HasFinalBusState(busPawn))
                {
                    float score = TrainingOutcomeUtility.GetScore(traderPawn, busPawn);
                    float corruptionGain = TrainingOutcomeUtility.CalculateCorruptionGain(score);
                    float cappedGain = Mathf.Min(corruptionGain, 0.15f);
                    float increaseAmount = 0.10f + (cappedGain >= 0.15f ? cappedGain * 0.5f : cappedGain);
                    ConditioningUtility.IncreaseBusHediffSeverity(busPawn, increaseAmount);
                }
            }
        }

        /// <summary>保留双方身体条件，按公交车发起的真实Quickie调度，实际开始提示交给场景守卫。</summary>
        private static void StartConsensualSex(Pawn bus, Pawn trader)
        {
            if (!CanStartTradeInteraction(bus, trader)) return;

            // can_fuck 只检查插入能力；可作为接受方的角色同样能发起 Quickie。
            if ((!xxx.can_fuck(bus) && !xxx.can_be_fucked(bus)) ||
                (!xxx.can_fuck(trader) && !xxx.can_be_fucked(trader)))
            {
                LogSkipped(bus, trader, "rjw_consensual_eligibility");
                return;
            }

            // RJW 没有名为 Sex 的 JobDef；Quickie 支持地图内、无需床位的互动。
            TryStartTradeJob(bus, trader, xxx.quick_sex, SSCRestrictionEvent.TradeConsensual);
        }

        /// <summary>保留强制行为能力门槛，按商人发起的RandomRape调度，不把谈判者误当发起者。</summary>
        private static void StartRapeSex(Pawn rapist, Pawn victim)
        {
            if (!CanStartTradeInteraction(rapist, victim)) return;

            if (!xxx.can_rape(rapist) || !xxx.can_get_raped(victim))
            {
                LogSkipped(rapist, victim, "rjw_rape_eligibility");
                return;
            }

            // 使用 RJW 注册的 RandomRape 定义，保留商人为发起方。
            TryStartTradeJob(rapist, victim, xxx.RapeRandom, SSCRestrictionEvent.TradeForced);
        }

        /// <summary>检查双方可用性、地图距离、可中断性及寻路；这些执行条件独立于统一行为许可。</summary>
        private static bool CanStartTradeInteraction(Pawn initiator, Pawn partner)
        {
            if (initiator == null || partner == null || initiator == partner) return false;
            if (initiator.Dead || partner.Dead || initiator.Downed || partner.Downed ||
                initiator.Drafted || partner.Drafted || initiator.jobs == null || partner.jobs == null)
            {
                LogSkipped(initiator, partner, "pawn_unavailable");
                return false;
            }

            if (!initiator.Spawned || !partner.Spawned || initiator.Map == null || initiator.Map != partner.Map ||
                !initiator.Position.InHorDistOf(partner.Position, 15f))
            {
                LogSkipped(initiator, partner, "map_or_distance");
                return false;
            }

            if (initiator.InMentalState || partner.InMentalState || initiator.IsFighting() || partner.IsFighting())
            {
                LogSkipped(initiator, partner, "mental_state_or_combat");
                return false;
            }

            if (initiator.jobs.curDriver is JobDriver_Sex || partner.jobs.curDriver is JobDriver_Sex ||
                !initiator.jobs.IsCurrentJobPlayerInterruptible() || !partner.jobs.IsCurrentJobPlayerInterruptible() ||
                initiator.CurJob?.def.forceCompleteBeforeNextJob == true || partner.CurJob?.def.forceCompleteBeforeNextJob == true)
            {
                LogSkipped(initiator, partner, "busy_with_other_job");
                return false;
            }

            if (!initiator.CanReserveAndReach(partner, PathEndMode.OnCell, Danger.Deadly))
            {
                LogSkipped(initiator, partner, "cannot_reserve_or_reach");
                return false;
            }

            return true;
        }

        /// <summary>先预检许可再安排等待和行为任务；只撤销本事件创建的任务，不清空无关队列。</summary>
        private static bool TryStartTradeJob(Pawn initiator, Pawn partner, JobDef jobDef, SSCRestrictionEvent source)
        {
            if (jobDef == null)
            {
                LogSkipped(initiator, partner, "missing_job_def");
                return false;
            }

            // 先按实际驱动判定方向及强制/自愿性质。拒绝发生在第一次StartJob之前，双方原工作保持原样。
            // 事件已经掷骰，不改走另一分支；成功成交的成长仍由Postfix统一处理。
            Job interactionJob = JobMaker.MakeJob(jobDef, partner);
            interactionJob.playerForced = true;
            if (!SSCRestrictionJobGuard.PrepareEvent(initiator, interactionJob, source))
            {
                LogSkipped(initiator, partner, "restriction_preflight");
                JobMaker.ReturnToPool(interactionJob);
                return false;
            }

            // 留出走近对方的时间；RJW 驱动到达后会接管接受方任务。
            // 事件直接启动任务，避免玩家按住 Shift 时被当作排队命令，
            // 也避免 TryTakeOrderedJob 将已有同类 Wait 视为成功却不刷新时长。
            Job waitJob = JobMaker.MakeJob(JobDefOf.Wait);
            waitJob.expiryInterval = PartnerWaitTicks;
            waitJob.playerForced = true;
            partner.jobs.StartJob(waitJob, JobCondition.InterruptForced, tag: JobTag.Misc, preToilReservationsCanFail: true);
            if (partner.CurJob != waitJob)
            {
                // Wait 可能被旧任务的收尾步骤推迟，不能把排队当成已启动。
                partner.jobs.jobQueue.RemoveAll(partner, queued => queued == waitJob);
                SSCRestrictionJobGuard.CancelPendingEvent(interactionJob);
                JobMaker.ReturnToPool(interactionJob);
                LogSkipped(initiator, partner, "partner_wait_rejected");
                return false;
            }

            // 在启动前登记等待归属，立即预约失败和之后走位失败均能清理相同任务。
            SSCRestrictionJobGuard.RegisterEventWait(initiator, interactionJob, partner, waitJob);
            initiator.jobs.StartJob(interactionJob, JobCondition.InterruptForced, tag: JobTag.Misc, preToilReservationsCanFail: true);
            if (initiator.CurJob != interactionJob)
            {
                SSCRestrictionJobGuard.CancelPendingEvent(interactionJob);
                initiator.jobs.jobQueue.RemoveAll(initiator, queued => queued == interactionJob);
                // 只清理本次创建的等待任务，避免结束已被其他逻辑替换的工作。
                if (partner.CurJob == waitJob)
                    partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
                else
                    partner.jobs.jobQueue.RemoveAll(partner, queued => queued == waitJob);
                LogSkipped(initiator, partner, "initiator_job_rejected");
                return false;
            }

            SSCLog.Verbose($"[SSC BusTrade] scheduled job={jobDef.defName} initiator={initiator.LabelShort} partner={partner.LabelShort}");
            return true;
        }

        /// <summary>只写详细诊断，自动交易事件失败不刷屏或伪报行为发生。</summary>
        private static void LogSkipped(Pawn initiator, Pawn partner, string reason)
        {
            SSCLog.Verbose($"[SSC BusTrade] skipped reason={reason} initiator={initiator?.LabelShort} partner={partner?.LabelShort}");
        }
    }
}
