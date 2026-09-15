using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using rjw; // 假设 xxx 等辅助类在这个命名空间

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(TradeDeal), "TryExecute")]
    public static class Patch_TradeDeal_TryExecute_BusSex
    {
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
                Need_Corruption corruptionNeed = busPawn.needs.TryGetNeed<Need_Corruption>();
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
                int forgiveChance = remainingChance - rapeChance; // 兜底，确保总和为 100

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

        private static void StartConsensualSex(Pawn bus, Pawn trader)
        {
            // A1. 同图检查
            if (bus.Map == null || trader.Map == null || bus.Map != trader.Map) return;

            // A2. 距离检查 (太远了就算了，别跑半个地图去送)
            if (!bus.Position.InHorDistOf(trader.Position, 15f)) return;

            // A3. 敌对状态检查 (既然是两情相悦，不能在打架)
            if (trader.InMentalState || trader.IsFighting()) return;

            // A4. RJW 能力检查
            if (!xxx.can_fuck(bus) || !xxx.can_fuck(trader)) return;

            JobDef sexJobDef = DefDatabase<JobDef>.GetNamedSilentFail("Sex");
            if (sexJobDef != null)
            {
                trader.jobs.StopAll();
                Job waitJob = JobMaker.MakeJob(JobDefOf.Wait_Wander, 120); // 等待约 2秒
                trader.jobs.TryTakeOrderedJob(waitJob, JobTag.Misc);

                Job sexJob = JobMaker.MakeJob(sexJobDef, trader);
                bus.jobs.TryTakeOrderedJob(sexJob, JobTag.Misc);

                Messages.Message("SSC_Message_TradeConsensualSex".Translate(bus.LabelShort, trader.LabelShort),
                    new LookTargets(bus, trader), MessageTypeDefOf.PositiveEvent);
            }
        }

        // ====================================================
        // 新增：强暴逻辑
        // ====================================================
        private static void StartRapeSex(Pawn rapist, Pawn victim)
        {
            if (rapist.Map == null || victim.Map == null || rapist.Map != victim.Map) return;
            if (!rapist.Position.InHorDistOf(victim.Position, 15f)) return;
            if (rapist.InMentalState || rapist.IsFighting()) return;

            // RJW 的强暴能力检查 (根据你安装的RJW版本，方法名可能略有不同，通常为 can_rape 和 can_get_raped)
            if (!xxx.can_rape(rapist) || !xxx.can_get_raped(victim)) return;

            // 获取 RJW 的强暴 Job (通常叫 "Rape" 或 "RandomRape")
            JobDef rapeJobDef = DefDatabase<JobDef>.GetNamedSilentFail("Rape");

            if (rapeJobDef != null)
            {
                // 停下受害者当前的工作，让其僵直一下等待被强暴
                victim.jobs.StopAll();
                Job waitJob = JobMaker.MakeJob(JobDefOf.Wait_Wander, 60);
                victim.jobs.TryTakeOrderedJob(waitJob, JobTag.Misc);

                // 让商人（施暴者）发起强暴任务，目标是你的小人（受害者）
                rapist.jobs.StopAll();
                Job rapeJob = JobMaker.MakeJob(rapeJobDef, victim);
                rapist.jobs.TryTakeOrderedJob(rapeJob, JobTag.Misc);

                Messages.Message("SSC_Message_TradeRapeOccurred".Translate(victim.LabelShort, rapist.LabelShort),
                    new LookTargets(rapist, victim), MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}
