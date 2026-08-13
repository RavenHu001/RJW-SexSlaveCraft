using System.Linq;
using RimWorld;
using rjw;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public static class LegacyTrainingUtility
    {
        public static float GetScore_Legacy(Pawn master, Pawn sexSlave)
        {
            float score = master.skills.GetSkill(SkillDefOf.Social).Level * 2f;
            score += sexSlave.relations.OpinionOf(master) / 5f;

            if (sexSlave.guest != null && sexSlave.guest.IsSlave)
            {
                score += 5f;
            }

            int sexSlaveStage = SSCIdentityUtility.GetSexSlaveStage(sexSlave);
            if (sexSlaveStage == 1) score += 5f;
            else if (sexSlaveStage == 2) score += 10f;
            else if (sexSlaveStage == 3) score += 30f;
            else if (sexSlaveStage == 4) score += 60f;

            score *= GetGenitalSizeMultiplier_Legacy(master, sexSlave);
            return score + Rand.Range(-5f, 5f);
        }

        public static int DetermineLevel_Legacy(Pawn master, float score)
        {
            int social = master.skills.GetSkill(SkillDefOf.Social).Level;
            if (social >= 15) return 3;

            float checkScore = score + Rand.Range(-10f, 10f);
            if (social >= 8) return checkScore > 60 ? 3 : 2;

            float luckMultiplier = 1.0f;
            if (Rand.Chance(0.05f)) luckMultiplier = 3.0f;
            else if (Rand.Chance(0.2f)) luckMultiplier = 1.5f;
            if (checkScore > 0) checkScore *= luckMultiplier;

            if (checkScore < 15) return 1;
            if (checkScore < 55) return 2;
            return 3;
        }

        public static float CalculateCorruptionGain_Legacy(float score, int level)
        {
            float baseGain = score / 100f;
            float multiplier = level == 3 ? 0.6f : (level == 2 ? 0.8f : 1.0f);
            return Mathf.Clamp(baseGain * multiplier, 0.005f, 0.08f);
        }

        public static float CalculateSizeDifferenceScore_Legacy(Pawn master, Pawn slave)
        {
            if (!GetBestGenitalSpecs_Legacy(master, true, out _, out float pGirth)) return 3f;
            if (!GetBestGenitalSpecs_Legacy(slave, false, out _, out float vGirth)) return 3f;

            float girthDiff = pGirth - vGirth;
            if (girthDiff >= 5.0f) return 5f;
            if (girthDiff >= 2.5f) return 4f;
            if (girthDiff >= 0.5f) return 3f;
            if (girthDiff >= -1.0f) return 2f;
            if (girthDiff >= -2.5f) return 1f;
            return 0f;
        }

        public static float GetGenitalSizeMultiplier_Legacy(Pawn master, Pawn slave)
        {
            if (!GetBestGenitalSpecs_Legacy(master, true, out float pLength, out float pGirth)) return 1.0f;
            if (!GetBestGenitalSpecs_Legacy(slave, false, out float vDepth, out float vGirth)) return 1.0f;

            float girthDiff = pGirth - vGirth;
            float lengthDiff = pLength - vDepth;

            float girthScore;
            if (girthDiff >= 3.0f) girthScore = 1.3f;
            else if (girthDiff >= 1.0f) girthScore = 1.2f;
            else if (girthDiff >= -0.5f) girthScore = 1.1f;
            else if (girthDiff >= -1.5f) girthScore = 1.0f;
            else if (girthDiff >= -3.0f) girthScore = 0.8f;
            else girthScore = 0.5f;

            float lengthScore;
            if (lengthDiff >= 5.0f) lengthScore = 1.3f;
            else if (lengthDiff >= 0f) lengthScore = 1.1f;
            else if (lengthDiff >= -2.0f) lengthScore = 1.0f;
            else lengthScore = 0.7f;

            float finalMultiplier = (girthScore * 0.6f) + (lengthScore * 0.4f);
            return Mathf.Clamp(finalMultiplier, 0.5f, 1.5f);
        }

        private static bool GetBestGenitalSpecs_Legacy(Pawn pawn, bool isPenis, out float bestLength, out float bestGirth)
        {
            bestLength = 0f;
            bestGirth = 0f;

            if (pawn == null || pawn.health?.hediffSet == null) return false;

            var parts = Genital_Helper.get_AllPartsHediffList(pawn);
            if (parts == null || !parts.Any()) return false;

            bool found = false;
            float maxVolume = -1f;

            foreach (Hediff hediff in parts)
            {
                bool match = isPenis ? Genital_Helper.is_penis(hediff) : Genital_Helper.is_vagina(hediff);
                if (!match) continue;
                if (!PartSizeCalculator.TryGetLength(hediff, out float len)) continue;
                if (!PartSizeCalculator.TryGetGirth(hediff, out float gir)) continue;

                float volume = (gir * gir) * len;
                if (volume <= maxVolume) continue;

                maxVolume = volume;
                bestLength = len;
                bestGirth = gir;
                found = true;
            }

            return found;
        }

        public static void ApplyMemories_Legacy(int level, Pawn master, Pawn sexSlave, bool includeSocialThought)
        {
            var memories = sexSlave.needs.mood?.thoughts.memories;
            if (memories == null) return;
            if (level == 1)
            {
                memories.TryGainMemory(SSCDefOf.SSC_Training_Mood_Lvl1, master);
                if (includeSocialThought) memories.TryGainMemory(SSCDefOf.SSC_Training_Social_Lvl1, master);
            }
            else if (level == 2)
            {
                memories.TryGainMemory(SSCDefOf.SSC_Training_Mood_Lvl2, master);
                if (includeSocialThought) memories.TryGainMemory(SSCDefOf.SSC_Training_Social_Lvl2, master);
            }
            else
            {
                memories.TryGainMemory(SSCDefOf.SSC_Training_Mood_Lvl3, master);
                if (includeSocialThought) memories.TryGainMemory(SSCDefOf.SSC_Training_Social_Lvl3, master);
            }
        }
    }
}
