// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;
using rjw; // 引用 RJW

namespace SexSlaveCraft
{
    public static class TrainingExpUtility
    {
        /// <summary>
        /// 根据评分增加经验
        /// </summary>
        public static void ApplyExperienceFromScore(Pawn pawn, xxx.rjwSextype sexType, float rawScore, float multiplier = 1.0f)
        {
            if (pawn == null || pawn.Dead) return;

            // ==========================================================
            // 【新增】 研究判定
            // 如果 "部位调教" (SSC_BodyPartTraining) 还没研究完，直接不加经验，退出方法。
            // 使用你写好的工具集和静态字段
            // ==========================================================
            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BodyPartTraining))
            {
                return;
            }

            // ==========================================================
            // 【公式】 经验 = (分数 / 1000) * 倍率
            // ==========================================================
            float severityAmount = CalculateExperienceBudget(rawScore, multiplier,
                GetCorruptionStageMultiplier(pawn), TrainingOutcomeUtility.GetChainStageMult(pawn));

            // 如果算出来是 0 或负数，就不加了
            if (severityAmount <= 0) return;

            string targetPartDefName = null;
            HediffDef hediffToApply = null;
            BodyPartRecord directTargetPart = null;

            switch (sexType)
            {
                // 1. 手部
                case xxx.rjwSextype.Handjob:
                case xxx.rjwSextype.MutualMasturbation:
                    targetPartDefName = "Hand";
                    hediffToApply = DefDatabase<HediffDef>.GetNamed("SSC_Exp_Hand"); // 建议也放入 SSCDefOf
                    break;

                // 2. 足部
                case xxx.rjwSextype.Footjob:
                    targetPartDefName = "Foot";
                    hediffToApply = DefDatabase<HediffDef>.GetNamed("SSC_Exp_Foot");
                    break;

                // 3. 生殖器
                case xxx.rjwSextype.Vaginal:
                case xxx.rjwSextype.Scissoring:
                case xxx.rjwSextype.Fingering:
                case xxx.rjwSextype.Fisting:
                case xxx.rjwSextype.Sixtynine:
                    targetPartDefName = "Genitals";
                    hediffToApply = DefDatabase<HediffDef>.GetNamed("SSC_Exp_Genitals");
                    break;

                // 4. 后庭
                case xxx.rjwSextype.Anal:
                case xxx.rjwSextype.Rimming:
                    targetPartDefName = "Anus";
                    hediffToApply = DefDatabase<HediffDef>.GetNamed("SSC_Exp_Anus");
                    break;

                // 5. 口部
                case xxx.rjwSextype.Oral:
                    targetPartDefName = "Jaw";
                    hediffToApply = DefDatabase<HediffDef>.GetNamed("SSC_Exp_Oral");
                    break;

                // 6. 胸部
                case xxx.rjwSextype.Boobjob:
                    // EN: Breast exp should follow RJW's own breast lookup instead of guessing by body-part name.
                    // CN: 胸部经验应直接跟随 RJW 自己的乳房定位，而不是靠身体部位名去猜。
                    directTargetPart = Genital_Helper.get_breastsBPR(pawn);
                    targetPartDefName = "Chest";
                    hediffToApply = DefDatabase<HediffDef>.GetNamed("SSC_Exp_Breast");
                    break;
            }

            if (hediffToApply != null)
            {
                if (directTargetPart != null)
                {
                    AddHediffToSpecificPart(pawn, directTargetPart, hediffToApply, severityAmount);
                    return;
                }

                if (targetPartDefName != null)
                {
                    if (sexType == xxx.rjwSextype.Boobjob)
                    {
                        Log.Warning($"[SSC BreastExp] RJW 未找到 {pawn.LabelShort} 的乳房部位，回退到 body part 名称匹配: {targetPartDefName}");
                    }
                    AddHediffToPart(pawn, targetPartDefName, hediffToApply, severityAmount);
                }
            }
        }

        /// <summary>按明确的快照输入计算额度；不读写角色，供日常及按真实部位分配的能力共用。</summary>
        public static float CalculateExperienceBudget(float score, float multiplier, float corruptionMultiplier, float chainMultiplier)
        {
            if (!IsFinite(score) || !IsFinite(multiplier) || !IsFinite(corruptionMultiplier) || !IsFinite(chainMultiplier) ||
                score <= 0f || multiplier <= 0f || corruptionMultiplier <= 0f || chainMultiplier <= 0f) return 0f;
            // 保留既有乘法顺序；侵染把 0.20 作为自身倍率，日常/仪式继续传入原倍率。
            float amount = (score / 1000f) * multiplier;
            amount *= corruptionMultiplier;
            amount *= chainMultiplier;
            return IsFinite(amount) && amount > 0f ? amount : 0f;
        }

        public static float GetCorruptionStageMultiplier(Pawn pawn)
        {
            Need_Corruption corruptionNeed = pawn?.needs?.TryGetNeed<Need_Corruption>();
            float corruption = corruptionNeed?.CurLevelPercentage ?? 0f;
            if (!IsFinite(corruption)) return 0f;

            if (corruption >= 0.75f) return 2.0f;
            if (corruption >= 0.5f) return 1.8f;
            if (corruption >= 0.25f) return 1.5f;
            if (corruption >= 0.05f) return 1.3f;
            return 1.0f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool IsPresentPart(Pawn pawn, BodyPartRecord part) => pawn != null && !pawn.Dead &&
            !pawn.Destroyed && !pawn.Discarded && part != null && pawn.health?.hediffSet != null &&
            pawn.RaceProps?.body?.AllParts.Contains(part) == true && !pawn.health.hediffSet.PartIsMissing(part);

        /// <summary>精确部位入口不按名称寻找替代部位；调用方决定分配，接口只验证当前身体并限制单条进度。</summary>
        public static bool TryApplyExperienceToPart(Pawn pawn, BodyPartRecord part, HediffDef def, float amount,
            Func<bool> isCurrent = null)
        {
            bool CanWrite() => Scribe.mode == LoadSaveMode.Inactive && IsPresentPart(pawn, part) &&
                ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BodyPartTraining) && (isCurrent == null || isCurrent());
            if (!IsFinite(amount) || amount <= 0f || def == null || !CanWrite()) return false;
            Hediff existing = pawn.health.hediffSet.hediffs.Find(h => h.def == def && h.Part == part);
            float current = existing?.Severity ?? 0f, cap = def.maxSeverity;
            if (!IsFinite(current) || !IsFinite(cap)) return false;
            float gain = Math.Max(0f, Math.Min(amount, cap - current));
            if (gain <= 0f) return false;
            if (existing != null) existing.Severity = current + gain;
            else
            {
                // 新记录先覆盖 XML initialSeverity，微小奖励不能被默认 1% 抬高。
                Hediff created = HediffMaker.MakeHediff(def, pawn, part);
                if (!CanWrite()) return false;
                created.Severity = gain;
                // MakeHediff 和初始 Severity 都可能触发外部健康回调，失效的记录不能再加入新人格身体。
                if (!CanWrite()) return false;
                pawn.health.AddHediff(created, part, null);
            }
            return true;
        }

        /// <summary>保留日常/仪式按动作名称匹配全部部位的分配方式，写入与能力入口共用。</summary>
        private static void AddHediffToPart(Pawn pawn, string partDefName, HediffDef hediffDef, float amount)
        {
            // 同动作的双手/双足仍分别领取完整原额度；只有侵染调用方负责 20% 和均分。
            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
                if (part.def.defName.Contains(partDefName))
                    AddHediffToSpecificPart(pawn, part, hediffDef, amount);
        }

        private static void AddHediffToSpecificPart(Pawn pawn, BodyPartRecord part, HediffDef hediffDef, float amount)
        {
            TryApplyExperienceToPart(pawn, part, hediffDef, amount);
        }
    }
}
