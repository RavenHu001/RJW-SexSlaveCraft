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

            if (SSCMod.settings?.useOldScoring ?? false)
            {
                ExecuteOutcome_Legacy(master, sexSlave);
                return;
            }

            float score = TrainingOutcomeUtility.GetScore(master, sexSlave);
            float corruptionGain = TrainingOutcomeUtility.CalculateCorruptionGain(score);

            float chainCap = TrainingOutcomeUtility.GetChainCap(sexSlave);
            corruptionGain = Mathf.Clamp(corruptionGain, 0.01f, chainCap);

            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            float hediffIncrease = corruptionGain * 0.5f;
            if (BusSpecializationUtility.ShouldProcessBusGrowth(sexSlave))
            {
                BusSpecializationUtility.EnsureBusHediffFromSpecialization(sexSlave);
                IncreaseBusHediffSeverity(sexSlave, hediffIncrease);
            }

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, false);
            CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, false, score);

            int opinionDelta = (int)TrainingOutcomeUtility.CalculateOpinionChange(score, master, sexSlave);
            ApplyTrainingMemories(opinionDelta, master, sexSlave);

            string logMsg = Strings.DailyTrainingOutcome(
                score.ToString("F1"), corruptionGain.ToString("P1"));
            Messages.Message(logMsg, sexSlave, MessageTypeDefOf.NeutralEvent, false);

            CompSexSlaveTraining comp = sexSlave.TryGetComp<CompSexSlaveTraining>();
            if (comp != null)
            {
                comp.lastTrainingScore = score;
            }
        }

        private static void ExecuteOutcome_Legacy(Pawn master, Pawn sexSlave)
        {
            float score = LegacyTrainingUtility.GetScore_Legacy(master, sexSlave);
            int finalLevel = LegacyTrainingUtility.DetermineLevel_Legacy(master, score);

            float corruptionGain = LegacyTrainingUtility.CalculateCorruptionGain_Legacy(score, finalLevel);
            corruptionGain *= SSCMasterBondUtility.GetTrainingCorruptionMultiplier(master);
            CorruptionUtility.AddCorruption(sexSlave, corruptionGain);

            float hediffIncrease = corruptionGain * 0.5f;
            if (BusSpecializationUtility.ShouldProcessBusGrowth(sexSlave))
            {
                BusSpecializationUtility.EnsureBusHediffFromSpecialization(sexSlave);
                IncreaseBusHediffSeverity(sexSlave, hediffIncrease);
            }

            WillReductionUtility.ApplyWillReductionByCorruptionBand(master, sexSlave, corruptionGain, false);
            CorruptionProgressionUtility.ProcessCorruptionProgression(master, sexSlave, false, score);

            string logMsg = Strings.DailyTrainingOutcome_Legacy(
                score.ToString("F1"), finalLevel, corruptionGain.ToString("P1"));
            Messages.Message(logMsg, sexSlave, MessageTypeDefOf.NeutralEvent, false);
            LegacyTrainingUtility.ApplyMemories_Legacy(finalLevel, master, sexSlave, true);
        }

        public static string ExecuteRitualOutcome(Pawn master, Pawn sexSlave, float quality, ThoughtDef ritualOutcomeMemory = null)
        {
            if (sexSlave == null || master == null) return "";

            if (SSCMod.settings?.useOldScoring ?? false)
            {
                return ExecuteRitualOutcome_Legacy(master, sexSlave, quality, ritualOutcomeMemory);
            }

            float finalScore = quality * 75f;
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
            float finalScore = quality * 75f;
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

        private static void ApplyTrainingMemories(int opinionDelta, Pawn master, Pawn sexSlave)
        {
            if (opinionDelta == 0) return;

            var memories = sexSlave.needs.mood?.thoughts.memories;
            if (memories == null) return;

            int displayStage = Thought_MemoryDynamicTraining.GetDisplayStage(opinionDelta);

            if (SSCDefOf.SSC_Training_MoodDynamic != null)
            {
                var moodThought = ThoughtMaker.MakeThought(
                    SSCDefOf.SSC_Training_MoodDynamic, displayStage);
                memories.TryGainMemory(moodThought, master);
            }

            if (SSCDefOf.SSC_Training_OpinionDynamic != null)
            {
                var socialThought = (Thought_MemoryDynamicTraining)ThoughtMaker.MakeThought(
                    SSCDefOf.SSC_Training_OpinionDynamic);
                socialThought.opinionOffset = opinionDelta;
                memories.TryGainMemory(socialThought, master);
            }
        }

        public static float GetScore(Pawn Master, Pawn SexSlave) => TrainingOutcomeUtility.GetScore(Master, SexSlave);
        public static float CalculateSizeDifferenceScore(Pawn master, Pawn slave)
            => TrainingOutcomeUtility.CalculateSizeDifferenceScore(master, slave);
        public static string ProcessCorruptionProgression(Pawn Master, Pawn SexSlave, bool isRitual, float score = 0f)
            => CorruptionProgressionUtility.ProcessCorruptionProgression(Master, SexSlave, isRitual, score);

        public static void IncreaseBusHediffSeverity(Pawn pawn, float amount)
        {
            if (!BusSpecializationUtility.ShouldProcessBusGrowth(pawn)) return;
            BusSpecializationUtility.SyncBusStates(pawn);
            if (BusSpecializationUtility.HasFinalBusState(pawn)) return;

            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            // EN: All bus growth now lands on the single progress track; the hediff severity is derived from it.
            // CN: 公交车成长统一走 progress 单轨，hediff 严重度由 progress 派生，避免双轨互相覆盖。
            comp.AddSpecializationProgress(amount);
            BusSpecializationUtility.EnsureBusHediffFromSpecialization(pawn);
        }
    }
}
