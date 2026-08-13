using RimWorld;
using rjw; // 确保引用了 RJW
using System.Collections.Generic;
using System.Linq;
using Verse;
using System;

namespace SexSlaveCraft
{
    public class RitualOutcomeComp_GenitalDifference : RitualOutcomeComp_Quality
    {
        public string masterRoleId = "master";
        public string slaveRoleId = "slave";

        private const string Label_Huge = "SSC_Ritual_Genital_Huge";
        private const string Label_Satisfying = "SSC_Ritual_Genital_Satisfying";
        private const string Label_Appropriate = "SSC_Ritual_Genital_Appropriate";
        private const string Label_SlightlySmall = "SSC_Ritual_Genital_SlightlySmall";
        private const string Label_TooSmall = "SSC_Ritual_Genital_TooSmall";
        private const string Label_Pathetic = "SSC_Ritual_Genital_Pathetic";

        public override bool DataRequired => false;

        // 【必须】防止被原版父类静默拦截
        public override bool Applies(LordJob_Ritual ritual)
        {
            return true;
        }

        // 辅助方法：安全地在“准备界面”或“仪式执行中”获取角色
        private Pawn GetTargetPawn(string role, LordJob_Ritual ritual, RitualRoleAssignments assignments = null)
        {
            if (assignments != null) return assignments.FirstAssignedPawn(role);
            return ritual?.PawnWithRole(role);
        }

        // 核心算分逻辑（结算时调用）
        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            try
            {
                Pawn master = GetTargetPawn(masterRoleId, ritual);
                Pawn slave = GetTargetPawn(slaveRoleId, ritual);

                // 假设 3f 是默认基础分（Appropriate）
                if (master == null || slave == null) return 3f;

                return ConditioningUtility.CalculateSizeDifferenceScore(master, slave);
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC Debug - Genital] Count() 内部崩溃: {ex.Message}");
                return 3f;
            }
        }

        // ✨ 核心改造：接管准备界面，消除绿对号，显示本地化文本
        public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation, RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
        {
            Pawn master = GetTargetPawn(masterRoleId, null, assignments);
            Pawn slave = GetTargetPawn(slaveRoleId, null, assignments);

            float score = 3f;
            float expectedQualityOffset = 0f;
            string countText = Strings.Ritual_Unassigned; // 如果没选人，中间显示的默认字

            if (master != null && slave != null)
            {
                score = ConditioningUtility.CalculateSizeDifferenceScore(master, slave);
                expectedQualityOffset = this.curve.Evaluate(score);

                // 【重点】把算出的分数转成本地化文本，填入中间的 count 位置
                countText = GetLabelKeyFromScore(score).Translate();
            }

            return new QualityFactor
            {
                label = label, // 左侧：XML里的 <label>，比如 "性器官契合度"
                count = countText, // 中间：比如 "巨大"、"合适"
                present = false, // 【重点】设为 false，彻底抹除违和的绿色对号
                uncertainOutcome = false,
                qualityChange = expectedQualityOffset.ToStringWithSign("0.#%"), // 右侧：比如 "+20%"
                quality = expectedQualityOffset,
                positive = expectedQualityOffset >= 0
            };
        }

        // 结算弹窗文本（保持同样的格式化风格）
        public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
        {
            try
            {
                Pawn master = GetTargetPawn(masterRoleId, ritual);
                Pawn slave = GetTargetPawn(slaveRoleId, ritual);

                if (master == null || slave == null)
                {
                    return Strings.Ritual_UnassignedDesc(label, QualityOffset(ritual, data).ToStringWithSign("0.#%"));
                }

                float score = Count(ritual, data);
                float bonus = QualityOffset(ritual, data);
                string textTranslated = GetLabelKeyFromScore(score).Translate();

                // 统一结算格式，例如："性器官契合度 - 恰到好处: +10%"
                return $"{label} - {textTranslated}: {bonus.ToStringWithSign("0.#%")}";
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC Debug - Genital] GetDesc() 崩溃: {ex.Message}");
                return Strings.Ritual_UIError(label);
            }
        }

        private string GetLabelKeyFromScore(float score)
        {
            if (score >= 4.5f) return Label_Huge;
            if (score >= 3.5f) return Label_Satisfying;
            if (score >= 2.5f) return Label_Appropriate;
            if (score >= 1.5f) return Label_SlightlySmall;
            if (score >= 0.5f) return Label_TooSmall;
            return Label_Pathetic;
        }
    }
}