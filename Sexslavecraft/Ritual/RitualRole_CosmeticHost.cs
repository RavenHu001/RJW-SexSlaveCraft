// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class RitualRole_CosmeticHost : RitualRole
    {
        public static AcceptanceReport Report(Pawn pawn, TargetInfo target, Precept_Ritual ritual)
        {
            // 主持者需能主动到场，是自由殖民者且医术至少 6；无需满足受塑者的 Beauty 条件。
            var report = SSCCosmeticUtility.ActiveParticipantReport(pawn, target);
            if (!report.Accepted) return report;
            if (!pawn.IsColonistPlayerControlled || pawn.IsSlave || pawn.IsPrisoner)
                return "SSC_Cosmetic_FreeColonist".Translate();
            var medicine = pawn.skills?.GetSkill(SkillDefOf.Medicine);
            if (medicine == null || medicine.TotallyDisabled || medicine.Level < 6)
                return "SSC_Cosmetic_MedicineRequired".Translate(6);
            // 有完整仪式上下文时要求同文化；无上下文的原版候选查询只核对个人条件。
            return SSCCosmeticUtility.CultureReport(pawn, ritual);
        }

        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget,
            LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            // 兼容预览、分配和进行中查询提供的不同上下文，并禁止同一人占两种必需角色。
            var report = Report(p, selectedTarget, ritual?.Ritual ?? assignments?.Ritual ?? precept);
            if (report.Accepted && p == (assignments?.FirstAssignedPawn(SSCCosmeticUtility.RecipientRole) ?? ritual?.PawnWithRole(SSCCosmeticUtility.RecipientRole)))
                report = "SSC_Cosmetic_DifferentPawns".Translate();
            reason = skipReason ? null : report.Reason;
            return report.Accepted;
        }

        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn pawn = null, bool skipReason = false)
        {
            // 不要求领袖或精神领袖等文化职位；具体人选由 AppliesToPawn 决定。
            reason = null;
            return true;
        }
    }
}
