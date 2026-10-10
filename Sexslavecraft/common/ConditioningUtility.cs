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
        {
            if (sexSlave == null || master == null) return;

            // 一次结算只读取一次套别：进入效果应用后不再重查当前任职，
            // 避免同一次结算前后选择两套记忆，也避免给无效执行者发放新奖励。
            TrainingMemorySet memorySet = TrainerTrainingMemoryUtility.Resolve(master);
            if (SSCMod.settings?.useOldScoring ?? false)
            {
                ExecuteOutcome_Legacy(master, sexSlave, score, memorySet);
                return;
            }

            float corruptionGain = TrainingOutcomeUtility.CalculateCorruptionGain(score);

            float chainCap = TrainingOutcomeUtility.GetChainCap(sexSlave);
            corruptionGain = Mathf.Clamp(corruptionGain, 0.01f, chainCap);

            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, false);
            CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, false, score);

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
                comp.lastTrainingScore = score;
            }
        }

        private static void ExecuteOutcome_Legacy(Pawn master, Pawn sexSlave, float score, TrainingMemorySet memorySet)
        {
            int finalLevel = LegacyTrainingUtility.DetermineLevel_Legacy(master, score);

            float corruptionGain = LegacyTrainingUtility.CalculateCorruptionGain_Legacy(score, finalLevel);
            corruptionGain *= SSCMasterBondUtility.GetTrainingCorruptionMultiplier(master);
            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, false);
            CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, false, score);

            string logMsg = Strings.DailyTrainingOutcome_Legacy(
                score.ToString("F1"), finalLevel, corruptionGain.ToString("P1"));
            Messages.Message(logMsg, sexSlave, MessageTypeDefOf.NeutralEvent, false);
            // 日常显式传入已确定的套别；仪式仍走原套且不发社交记忆。
            LegacyTrainingUtility.ApplyMemories_Legacy(finalLevel, master, sexSlave, true, memorySet);
        }

        public static string ExecuteRitualOutcome(Pawn master, Pawn sexSlave, float quality, ThoughtDef ritualOutcomeMemory = null)
        {
            if (sexSlave == null || master == null) return "";

            if (SSCMod.settings?.useOldScoring ?? false)
            {
                return ExecuteRitualOutcome_Legacy(master, sexSlave, quality, ritualOutcomeMemory);
            }

            float finalScore = quality * SpecializationTrainingProgressUtility.RitualScorePerQuality;
            int finalLevel = (quality >= 0.8f) ? 3 : ((quality <= 0.25f) ? 1 : 2);
            float corruptionGain = quality * 0.08f * 2.0f;

            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, true);
            string progressionMsg = CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, true, finalScore);

            ApplyRitualMemories(finalLevel, master, sexSlave);
            var memories = sexSlave.needs.mood?.thoughts.memories;
            if (memories != null)
            {
                memories.TryGainMemory(CorruptionProgressionUtility.GetRitualEuphoriaThought(ritualOutcomeMemory), master);
            }

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

        private static string ExecuteRitualOutcome_Legacy(Pawn master, Pawn sexSlave, float quality, ThoughtDef ritualOutcomeMemory)
        {
            float finalScore = quality * SpecializationTrainingProgressUtility.RitualScorePerQuality;
            int finalLevel = (quality >= 0.8f) ? 3 : ((quality <= 0.25f) ? 1 : 2);
            float corruptionGain = quality * 0.08f * 2.0f;
            corruptionGain *= SSCMasterBondUtility.GetTrainingCorruptionMultiplier(master);
            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, true);
            string progressionMsg = CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, true, finalScore);

            LegacyTrainingUtility.ApplyMemories_Legacy(finalLevel, master, sexSlave, false);
            var memories = sexSlave.needs.mood?.thoughts.memories;
            if (memories != null)
            {
                memories.TryGainMemory(CorruptionProgressionUtility.GetRitualEuphoriaThought(ritualOutcomeMemory), master);
            }

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
