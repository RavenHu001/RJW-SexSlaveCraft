using System.Linq;
using System.Collections.Generic;
using RimWorld;
using rjw;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public static class TrainingOutcomeUtility
    {
        private static readonly float[] FallbackChainStageThresholds = { 0.1f, 0.3f, 0.5f, 0.9f };

        public static float GetScore(Pawn master, Pawn sexSlave)
        {
            if (SSCMod.settings?.useOldScoring ?? false)
                return LegacyTrainingUtility.GetScore_Legacy(master, sexSlave);

            float baseScore = (master.skills.GetSkill(SkillDefOf.Social).Level * 2.5f)
                            + (sexSlave.relations.OpinionOf(master) / 40f)
                            + (sexSlave.guest != null && sexSlave.guest.IsSlave ? 3f : 0f);

            return baseScore + GetSizeScore(master, sexSlave);
        }

        public static float GetSizeScore(Pawn master, Pawn slave)
        {
            if (!GetBestGenitalSpecs(master, true, out float pLength, out float pGirth)) return 0f;
            if (!GetBestGenitalSpecs(slave, false, out float vDepth, out float vGirth)) return 0f;

            float girthDiff = pGirth - vGirth;
            float lengthDiff = pLength - vDepth;

            int girthScore;
            if (girthDiff >= 3.0f) girthScore = 10;
            else if (girthDiff >= 1.0f) girthScore = 5;
            else if (girthDiff >= -0.5f) girthScore = 1;
            else if (girthDiff >= -1.5f) girthScore = 0;
            else if (girthDiff >= -3.0f) girthScore = -3;
            else girthScore = -5;

            int lengthScore;
            if (lengthDiff >= 5.0f) lengthScore = 10;
            else if (lengthDiff >= 0f) lengthScore = 5;
            else if (lengthDiff >= -2.0f) lengthScore = 0;
            else lengthScore = -5;

            return girthScore + lengthScore;
        }

        public static float CalculateSizeDifferenceScore(Pawn master, Pawn slave)
        {
            // RJW's calculator resolves the sex-part comp, its effective size, and body-size scaling.
            // Do not compare raw Hediff.Severity: it is not a physical measurement.
            if (!GetBestGenitalSpecs(master, true, out _, out float penisGirth)) return 3f;
            if (!GetBestGenitalSpecs(slave, false, out _, out float vaginaGirth)) return 3f;

            float girthDifference = penisGirth - vaginaGirth;
            if (girthDifference >= 5.0f) return 5f;
            if (girthDifference >= 2.5f) return 4f;
            if (girthDifference >= 0.5f) return 3f;
            if (girthDifference >= -1.0f) return 2f;
            if (girthDifference >= -2.5f) return 1f;
            return 0f;
        }

        public static float CalculateCorruptionGain(float score)
        {
            if (SSCMod.settings?.useOldScoring ?? false)
                return LegacyTrainingUtility.CalculateCorruptionGain_Legacy(score, 2);

            return Mathf.Clamp(score / 500f + 0.025f, 0.01f, 0.12f);
        }

        public static float CalculateOpinionChange(float score, Pawn master, Pawn sexSlave)
        {
            if (SSCMod.settings?.useOldScoring ?? false)
                return 0f;

            int social = master.skills.GetSkill(SkillDefOf.Social).Level;
            int sizeScore = (int)GetSizeScore(master, sexSlave);
            return Mathf.Round(social * 0.4f + sizeScore * 0.3f + score / 8f);
        }

        private static bool GetBestGenitalSpecs(Pawn pawn, bool isPenis, out float bestLength, out float bestGirth)
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

        public static float GetChainStageMult(Pawn pawn)
        {
            Hediff chain = GetChainHediff(pawn);
            if (chain == null) return 1.0f;

            float stageFloor = GetCurrentChainStageFloor(chain);
            if (stageFloor >= 0.9f) return 2.0f;
            if (stageFloor >= 0.5f) return 1.6f;
            if (stageFloor >= 0.3f) return 1.3f;
            if (stageFloor >= 0.1f) return 1.1f;
            return 1.0f;
        }

        public static float GetChainCap(Pawn pawn)
        {
            Hediff chain = GetChainHediff(pawn);
            if (chain == null) return 0.12f;

            return GetCurrentChainStageUpper(chain);
        }

        public static float GetChainDecayFactor(Pawn pawn)
        {
            Hediff chain = GetChainHediff(pawn);
            if (chain == null) return 0.1f;

            float stageFloor = GetCurrentChainStageFloor(chain);
            if (stageFloor >= 0.9f) return 0.8f;
            if (stageFloor >= 0.5f) return 0.5f;
            if (stageFloor >= 0.3f) return 0.3f;
            if (stageFloor >= 0.1f) return 0.1f;
            return 0.1f;
        }

        public static float GetPartDecayFactor(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return 1f;

            string[] partDefs = { "SSC_Exp_Hand", "SSC_Exp_Oral", "SSC_Exp_Foot",
                                 "SSC_Exp_Genitals", "SSC_Exp_Breast", "SSC_Exp_Anus" };

            float totalReduction = 0f;
            foreach (string defName in partDefs)
            {
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(
                    DefDatabase<HediffDef>.GetNamedSilentFail(defName));
                if (hediff == null) continue;

                float sev = hediff.Severity;
                if (sev >= 1.0f)       totalReduction += 0.075f;
                else if (sev >= 0.7f)  totalReduction += 0.045f;
                else if (sev >= 0.3f)  totalReduction += 0.03f;
                else                   totalReduction += 0.005f;
            }

            return Mathf.Max(1f - totalReduction, 0.1f);
        }

        public static float GetCorruptionStageFloor(Pawn pawn)
        {
            Hediff chain = GetChainHediff(pawn);
            if (chain == null) return 0f;

            return GetCurrentChainStageFloor(chain);
        }

        public static float GetCorruptionStageMinSeverity(Pawn pawn)
        {
            Hediff chain = GetChainHediff(pawn);
            if (chain == null) return 0f;

            return GetCurrentChainStageFloor(chain);
        }

        /// <summary>
        /// 返回当前锁链阶段的前一级起点，供自然衰减退阶使用；本方法只查询，不修改 Pawn。
        /// 使用当前锁链的 HediffDef 阶段表，而不是用严重度减去固定数值模拟“退一级”。
        /// 默认阶段表为 0.1、0.3、0.5、0.9，因此 0.9 档的前一级是 0.5，而非 0.8。
        /// 无锁链、尚未进入第一阶段或处于第一阶段时返回 0，表示没有更低的已定义阶段。
        /// </summary>
        public static float GetPreviousChainStageFloor(Pawn pawn)
        {
            Hediff chain = GetChainHediff(pawn);
            if (chain == null) return 0f;

            // 先把连续严重度归入当前阶段：例如 0.8 的阶段底线是 0.5。
            // 复用已有的阶段识别和浮点容差，保持与恶堕底线、刹车上限的判定一致。
            float currentFloor = GetCurrentChainStageFloor(chain);
            float previousFloor = 0f;
            // GetChainStageThresholds 已将 XML 阈值过滤、去重、升序排列；
            // XML 未提供可用阶段时，它会使用原有的默认阈值表。
            // 扫描到当前阶段即停止，previousFloor 留下的就是最后一个严格更低的阈值。
            foreach (float threshold in GetChainStageThresholds(chain))
            {
                if (threshold >= currentFloor) break;
                previousFloor = threshold;
            }

            return previousFloor;
        }

        private static Hediff GetChainHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            return pawn.health.hediffSet.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_ChainOfSexSlave"));
        }

        private static List<float> GetChainStageThresholds(Hediff chain)
        {
            List<float> thresholds = chain?.def?.stages?
                .Select(stage => Mathf.Clamp01(stage.minSeverity))
                .Where(minSeverity => minSeverity > 0f)
                .Distinct()
                .OrderBy(minSeverity => minSeverity)
                .ToList();

            if (thresholds == null || thresholds.Count == 0)
            {
                thresholds = FallbackChainStageThresholds.ToList();
            }

            return thresholds;
        }

        private static float GetCurrentChainStageFloor(Hediff chain)
        {
            if (chain == null) return 0f;

            float currentFloor = 0f;
            float severity = chain.Severity;
            foreach (float threshold in GetChainStageThresholds(chain))
            {
                if (severity + 0.0001f < threshold) break;
                currentFloor = threshold;
            }

            return currentFloor;
        }

        private static float GetCurrentChainStageUpper(Hediff chain)
        {
            if (chain == null) return 0.12f;

            List<float> thresholds = GetChainStageThresholds(chain);
            float severity = chain.Severity;
            if (thresholds.Count > 0 && severity + 0.0001f < thresholds[0])
            {
                return 0.12f;
            }

            for (int i = 0; i < thresholds.Count; i++)
            {
                bool currentStage = i == thresholds.Count - 1 || severity + 0.0001f < thresholds[i + 1];
                if (currentStage)
                {
                    return i + 1 < thresholds.Count ? thresholds[i + 1] : 1f;
                }
            }

            return 1f;
        }
    }
}
