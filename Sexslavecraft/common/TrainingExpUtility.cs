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
            float severityAmount = (rawScore / 1000f) * multiplier;
            severityAmount *= GetCorruptionStageMultiplier(pawn);
            severityAmount *= TrainingOutcomeUtility.GetChainStageMult(pawn);

            // 如果算出来是 0 或负数，就不加了
            if (severityAmount <= 0) return;

            string targetPartDefName = null;
            HediffDef hediffToApply = null;
            BodyPartRecord directTargetPart = null;

            switch (sexType)
            {
                // ... (中间的 switch 逻辑保持不变，为了节省篇幅省略) ...

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

        private static float GetCorruptionStageMultiplier(Pawn pawn)
        {
            Need_Corruption corruptionNeed = pawn?.needs?.TryGetNeed<Need_Corruption>();
            float corruption = corruptionNeed?.CurLevelPercentage ?? 0f;

            if (corruption >= 0.75f) return 2.0f;
            if (corruption >= 0.5f) return 1.8f;
            if (corruption >= 0.25f) return 1.5f;
            if (corruption >= 0.05f) return 1.3f;
            return 1.0f;
        }

        private static void AddHediffToPart(Pawn pawn, string partDefName, HediffDef hediffDef, float amount)
        {
            // 获取小人所有身体部位
            System.Collections.Generic.IEnumerable<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;

            foreach (BodyPartRecord part in parts)
            {
                // 1. 检查部位名称是否匹配 (例如 "Hand" 匹配 "LeftHand", "RightHand")
                if (part.def.defName.Contains(partDefName))
                {
                    // 2. 确保部位没有缺失 (没被切掉)
                    if (!pawn.health.hediffSet.PartIsMissing(part))
                    {
                        // ==========================================================
                        // 【修复核心】 精确查找当前部位上的 Hediff
                        // ==========================================================

                        Hediff targetHediff = null;

                        // 遍历该小人所有的 Hediff，找到 Def 匹配 且 部位也匹配 的那个
                        foreach (Hediff h in pawn.health.hediffSet.hediffs)
                        {
                            if (h.def == hediffDef && h.Part == part)
                            {
                                targetHediff = h;
                                break; // 找到了就停止
                            }
                        }

                        // ==========================================================
                        // 3. 执行添加或增加逻辑
                        // ==========================================================

                        if (targetHediff != null)
                        {
                            // A. 如果已经有了，直接增加严重度 (Severity)
                            targetHediff.Severity += amount;
                        }
                        else
                        {
                            // B. 如果没有，创建一个新的
                            Hediff newHediff = HediffMaker.MakeHediff(hediffDef, pawn, part);
                            newHediff.Severity = amount; // 设置初始值
                            pawn.health.AddHediff(newHediff, part, null);
                        }
                    }
                }
            }
        }

        private static void AddHediffToSpecificPart(Pawn pawn, BodyPartRecord part, HediffDef hediffDef, float amount)
        {
            if (pawn == null || part == null || hediffDef == null) return;
            if (pawn.health.hediffSet.PartIsMissing(part)) return;

            Hediff targetHediff = null;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h.def == hediffDef && h.Part == part)
                {
                    targetHediff = h;
                    break;
                }
            }

            if (targetHediff != null)
            {
                targetHediff.Severity += amount;
            }
            else
            {
                Hediff newHediff = HediffMaker.MakeHediff(hediffDef, pawn, part);
                newHediff.Severity = amount;
                pawn.health.AddHediff(newHediff, part, null);
            }
        }
    }
}
