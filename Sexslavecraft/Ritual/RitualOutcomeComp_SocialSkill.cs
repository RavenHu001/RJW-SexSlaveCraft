using System;
using RimWorld;
using Verse;


namespace SexSlaveCraft
{
    public class RitualOutcomeComp_SocialSkill : RitualOutcomeComp_Quality
    {
        public string roleId = "master";

        private const string Label_Incompetent = "SSC_Ritual_Social_Incompetent";
        private const string Label_Average = "SSC_Ritual_Social_Average";
        private const string Label_Skilled = "SSC_Ritual_Social_Skilled";
        private const string Label_Masterful = "SSC_Ritual_Social_Masterful";

        public override bool DataRequired => false;

        // 【必须】防止被父类屏蔽
        public override bool Applies(LordJob_Ritual ritual)
        {
            return true;
        }

        // 辅助方法：安全地在双端获取角色
        private Pawn GetTargetPawn(string role, LordJob_Ritual ritual, RitualRoleAssignments assignments = null)
        {
            if (assignments != null) return assignments.FirstAssignedPawn(role);
            return ritual?.PawnWithRole(role);
        }

        // 核心算分逻辑
        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            try
            {
                Pawn pawn = GetTargetPawn(roleId, ritual);
                if (pawn == null || pawn.skills == null) return 0f;
                return (float)pawn.skills.GetSkill(SkillDefOf.Social).Level;
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC Debug - Social] Count() 内部崩溃: {ex.Message}");
                return 0f;
            }
        }

        // 接管准备界面的 UI 渲染，完美统一风格
        public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation, RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
        {
            Pawn pawn = GetTargetPawn(roleId, null, assignments);

            float level = 0f;
            float expectedQualityOffset = 0f;
            string countText = Strings.Ritual_Unassigned; // 默认占位

            if (pawn != null && pawn.skills != null)
            {
                level = pawn.skills.GetSkill(SkillDefOf.Social).Level;
                expectedQualityOffset = this.curve.Evaluate(level);

                // 【核心改动】改回使用你的本地化文本键值进行翻译
                countText = GetLabelKeyFromLevel(level).Translate();
            }

            return new QualityFactor
            {
                label = label, // 左侧：比如 "主人的调教技巧"
                count = countText, // 中间：比如 "炉火纯青"
                present = false, // 强制关闭绿对号
                uncertainOutcome = false,
                qualityChange = expectedQualityOffset.ToStringWithSign("0.#%"), // 右侧：比如 "+30%"
                quality = expectedQualityOffset,
                positive = expectedQualityOffset >= 0
            };
        }

        // 结算弹窗文本
        public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
        {
            try
            {
                Pawn pawn = GetTargetPawn(roleId, ritual);

                if (pawn == null || pawn.skills == null)
                {
                    return Strings.Ritual_UnassignedDesc(label, QualityOffset(ritual, data).ToStringWithSign("0.#%"));
                }

                float level = Count(ritual, data);
                float bonus = QualityOffset(ritual, data);
                string textTranslated = GetLabelKeyFromLevel(level).Translate();

                // 统一结算格式，例如："主人的调教技巧 - 炉火纯青: +30%"
                return $"{label} - {textTranslated}: {bonus.ToStringWithSign("0.#%")}";
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC Debug - Social] GetDesc() 崩溃: {ex.Message}");
                return Strings.Ritual_UIError(label);
            }
        }

        private string GetLabelKeyFromLevel(float level)
        {
            if (level >= 15f) return Label_Masterful;
            if (level >= 8f) return Label_Skilled;
            if (level >= 3f) return Label_Average;
            return Label_Incompetent;
        }
    }
}