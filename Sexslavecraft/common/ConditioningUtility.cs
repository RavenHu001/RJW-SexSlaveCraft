using RimWorld;
using rjw;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SexSlaveCraft
{
    public static class ConditioningUtility
    {
        public static void ExecuteOutcome(Pawn master, Pawn sexSlave)
        {
            if (sexSlave == null || master == null) return;
            ExecuteOutcome(master, sexSlave, GetScore(master, sexSlave));
        }

        /// <summary>日常驱动传入本次唯一评分，旧评分的随机项也只采样一次。</summary>
        public static void ExecuteOutcome(Pawn master, Pawn sexSlave, float score)
            => ExecuteOutcomeCore(master, sexSlave, score, score, false);

#if SSC_RW17
        internal const float BreakdownScoreBonus = 50f;

        /// <summary>本场采用传入加分，常规模式以基础分写历史。有链和无链特批均跳过建链。</summary>
        internal static void ExecuteBreakdownOutcome(Pawn trainer, Pawn subject, float baseScore,
            float scoreBonus = BreakdownScoreBonus)
            => ExecuteOutcomeCore(trainer, subject, baseScore + scoreBonus, baseScore, true);
#endif

        private static void ExecuteOutcomeCore(Pawn master, Pawn sexSlave, float score, float historyScore, bool breakdown)
        {
            if (sexSlave == null || master == null) return;

            // 一次结算只读取一次套别：进入效果应用后不再重查当前任职，
            // 避免同一次结算前后选择两套记忆，也避免给无效执行者发放新奖励。
            TrainingMemorySet memorySet = TrainerTrainingMemoryUtility.Resolve(master);
            if (SSCMod.settings?.useOldScoring ?? false)
            {
                ExecuteOutcome_Legacy(master, sexSlave, score, memorySet, breakdown);
                return;
            }

            float corruptionGain = TrainingOutcomeUtility.CalculateCorruptionGain(score);

#if !SSC_RW17
            float chainCap = TrainingOutcomeUtility.GetChainCap(sexSlave);
            corruptionGain = Mathf.Clamp(corruptionGain, 0.01f, chainCap);
#endif

            // 单次收益由评分决定，需求裁剪实际入账。意志结算仍用本次评分给出的收益基准。
            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, false);
            // 日常进展入口达到门槛会尝试首次绑定。特批只写受训收益。
            if (!breakdown) CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, false, score);

            // 保留原算法的取整结果决定档位与是否变化；弱化只在后续按基准整数换算，
            // 因此主人路径的 D 与所有原有效果完全不变。
            int opinionDelta = (int)TrainingOutcomeUtility.CalculateOpinionChange(score, master, sexSlave);
            // 套别与发放集中在共用入口，生产路径与回归用例共用同一条实现。
            TrainerTrainingMemoryUtility.ApplyDynamicMemories(memorySet, opinionDelta, master, sexSlave);

            string logMsg = Strings.DailyTrainingOutcome(
                score.ToString("F1"), corruptionGain.ToString("P1"));
            Messages.Message(logMsg, sexSlave, MessageTypeDefOf.NeutralEvent, false);

            CompSexSlaveTraining comp = sexSlave.TryGetComp<CompSexSlaveTraining>();
            if (comp != null)
            {
                comp.lastTrainingScore = historyScore;
            }
        }

        private static void ExecuteOutcome_Legacy(Pawn master, Pawn sexSlave, float score, TrainingMemorySet memorySet, bool breakdown)
        {
            int finalLevel = LegacyTrainingUtility.DetermineLevel_Legacy(master, score);

            float corruptionGain = LegacyTrainingUtility.CalculateCorruptionGain_Legacy(score, finalLevel);
            corruptionGain *= SSCMasterBondUtility.GetTrainingCorruptionMultiplier(master);
            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, false);
            // 旧评分的特批也跳过绑定，且沿用旧评分的历史写入行为。
            if (!breakdown) CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, false, score);

            string logMsg = Strings.DailyTrainingOutcome_Legacy(
                score.ToString("F1"), finalLevel, corruptionGain.ToString("P1"));
            Messages.Message(logMsg, sexSlave, MessageTypeDefOf.NeutralEvent, false);
            // 日常显式传入已确定的套别；仪式仍走原套且不发社交记忆。
            LegacyTrainingUtility.ApplyMemories_Legacy(finalLevel, master, sexSlave, true, memorySet);
        }

        public static string ExecuteRitualOutcome(Pawn master, Pawn sexSlave, float quality, ThoughtDef ritualOutcomeMemory = null)
#if SSC_RW17
            => ExecuteRitualOutcome(master, sexSlave, quality, ritualOutcomeMemory,
                new CorruptionProgressionUtility.RitualChainProgress(master, sexSlave, null));

        internal static string ExecuteRitualOutcome(Pawn master, Pawn sexSlave, float quality, ThoughtDef ritualOutcomeMemory,
            CorruptionProgressionUtility.RitualChainProgress progress)
#endif
        {
            if (sexSlave == null || master == null) return "";

#if SSC_RW17
            // 绑定可优先消化满值分化待办，未建链时由本场结算退出统一处理。
            using var imprintSettlement = SSCFullCorruptionImprint.BeginRitual(sexSlave);
#endif
            // 两种评分沿用各自的收益和记忆，整个结算共用仪式入口捕获的参与者和原锁链。
            if (SSCMod.settings?.useOldScoring ?? false)
            {
                return ExecuteRitualOutcome_Legacy(master, sexSlave, quality, ritualOutcomeMemory
#if SSC_RW17
                    , progress
#endif
                );
            }

            float finalScore = quality * SpecializationTrainingProgressUtility.RitualScorePerQuality;
            int finalLevel = (quality >= 0.8f) ? 3 : ((quality <= 0.25f) ? 1 : 2);
            float corruptionGain = quality * 0.08f * 2.0f;

            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

#if SSC_RW17
            // 恶堕写入会同步特性。回调若使原参与者失效，余下的意志、记忆和经验结算在此中止。
            if (!progress.CanContinueSettlement()) return "";
#endif
            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, true);
            string progressionMsg = CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, true, finalScore
#if SSC_RW17
                , progress
#endif
            );

#if SSC_RW17
            // 建链、转奴和严重度写入均可触发游戏回调。记忆也可能触发回调，派发经验之前再检查。
            if (!progress.CanContinueSettlement()) return "";
#endif
            ApplyRitualMemories(finalLevel, master, sexSlave);
#if SSC_RW17
            if (!progress.CanContinueSettlement()) return "";
#endif
            var memories = sexSlave.needs.mood?.thoughts.memories;
            if (memories != null)
            {
                memories.TryGainMemory(CorruptionProgressionUtility.GetRitualEuphoriaThought(ritualOutcomeMemory), master);
            }

#if SSC_RW17
            if (!progress.CanContinueSettlement()) return "";
#endif
            float virtualScore = quality * 1000f;
            float expMultiplier = 0.5f;

            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Handjob, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Footjob, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Oral, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Boobjob, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Anal, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Vaginal, virtualScore, expMultiplier);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(Strings.Ritual_MasterSlaveHeader(master.LabelShort, sexSlave.LabelShort));
            sb.AppendLine(Strings.Ritual_ScoreLevel(finalScore.ToString("F1"), finalLevel));
            sb.AppendLine(Strings.Ritual_CorruptionGain(corruptionGain.ToString("P1")));
            sb.AppendLine(Strings.Ritual_FullBodyTraining(sexSlave.LabelShort));

            if (!string.IsNullOrEmpty(progressionMsg))
            {
                sb.AppendLine("\n" + Strings.Ritual_OutcomeHeader);
                sb.AppendLine(progressionMsg);
            }
            else
            {
                sb.AppendLine("\n" + Strings.Ritual_NoNewStage);
            }

            return sb.ToString();
        }

        private static string ExecuteRitualOutcome_Legacy(Pawn master, Pawn sexSlave, float quality, ThoughtDef ritualOutcomeMemory
#if SSC_RW17
            , CorruptionProgressionUtility.RitualChainProgress progress
#endif
        )
        {
            float finalScore = quality * SpecializationTrainingProgressUtility.RitualScorePerQuality;
            int finalLevel = (quality >= 0.8f) ? 3 : ((quality <= 0.25f) ? 1 : 2);
            float corruptionGain = quality * 0.08f * 2.0f;
            // 旧评分保留主持者的成长倍率。发放收益后沿用与常规评分相同的仪式上下文与限阶规则。
            corruptionGain *= SSCMasterBondUtility.GetTrainingCorruptionMultiplier(master);
            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

#if SSC_RW17
            if (!progress.CanContinueSettlement()) return "";
#endif
            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, true);
            string progressionMsg = CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, true, finalScore
#if SSC_RW17
                , progress
#endif
            );

#if SSC_RW17
            if (!progress.CanContinueSettlement()) return "";
#endif
            LegacyTrainingUtility.ApplyMemories_Legacy(finalLevel, master, sexSlave, false);
#if SSC_RW17
            if (!progress.CanContinueSettlement()) return "";
#endif
            var memories = sexSlave.needs.mood?.thoughts.memories;
            if (memories != null)
            {
                memories.TryGainMemory(CorruptionProgressionUtility.GetRitualEuphoriaThought(ritualOutcomeMemory), master);
            }

#if SSC_RW17
            if (!progress.CanContinueSettlement()) return "";
#endif
            float virtualScore = quality * 1000f;
            float expMultiplier = 0.5f;
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Handjob, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Footjob, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Oral, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Boobjob, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Anal, virtualScore, expMultiplier);
            TrainingExpUtility.ApplyExperienceFromScore(sexSlave, xxx.rjwSextype.Vaginal, virtualScore, expMultiplier);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(Strings.Ritual_MasterSlaveHeader(master.LabelShort, sexSlave.LabelShort));
            sb.AppendLine(Strings.Ritual_ScoreLevel(finalScore.ToString("F1"), finalLevel));
            sb.AppendLine(Strings.Ritual_CorruptionGain(corruptionGain.ToString("P1")));
            sb.AppendLine(Strings.Ritual_FullBodyTraining(sexSlave.LabelShort));

            if (!string.IsNullOrEmpty(progressionMsg))
            {
                sb.AppendLine("\n" + Strings.Ritual_OutcomeHeader);
                sb.AppendLine(progressionMsg);
            }
            else
            {
                sb.AppendLine("\n" + Strings.Ritual_NoNewStage);
            }

            return sb.ToString();
        }

        private static void ApplyRitualMemories(int level, Pawn master, Pawn sexSlave)
        {
            var memories = sexSlave.needs.mood?.thoughts.memories;
            if (memories == null) return;
            if (level == 1)
                memories.TryGainMemory(SSCDefOf.SSC_Training_Mood_Lvl1, master);
            else if (level == 2)
                memories.TryGainMemory(SSCDefOf.SSC_Training_Mood_Lvl2, master);
            else
                memories.TryGainMemory(SSCDefOf.SSC_Training_Mood_Lvl3, master);
        }

        public static float GetScore(Pawn Master, Pawn SexSlave) => TrainingOutcomeUtility.GetScore(Master, SexSlave);
        public static float CalculateSizeDifferenceScore(Pawn master, Pawn slave)
            => TrainingOutcomeUtility.CalculateSizeDifferenceScore(master, slave);
        public static string ProcessCorruptionProgression(Pawn Master, Pawn SexSlave, bool isRitual, float score = 0f)
            => CorruptionProgressionUtility.ProcessCorruptionProgression(Master, SexSlave, isRitual, score);

        /// <summary>公交车交易保留独立的特色成长；日常受训改由公共基础经验结算。</summary>
        public static void IncreaseBusHediffSeverity(Pawn pawn, float amount)
        {
            if (!BusSpecializationUtility.ShouldProcessBusGrowth(pawn)) return;
            BusSpecializationUtility.SyncBusStates(pawn);
            if (BusSpecializationUtility.HasFinalBusState(pawn)) return;

            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;
            if (comp.specializationProgress >= CompSexSlaveTraining.SpecializationCompletionProgress) return;

            comp.AddSpecializationProgress(amount);
            BusSpecializationUtility.EnsureBusHediffFromSpecialization(pawn);
        }
    }
}
