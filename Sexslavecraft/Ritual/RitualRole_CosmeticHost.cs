// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class RitualRole_CosmeticHost : RitualRole
    {
        public static AcceptanceReport Report(Pawn pawn, TargetInfo target, Precept_Ritual ritual)
        {
            var report = SSCCosmeticUtility.ActiveParticipantReport(pawn, target);
            if (!report.Accepted) return report;
            if (!pawn.IsColonistPlayerControlled || pawn.IsSlave || pawn.IsPrisoner)
                return "SSC_Cosmetic_FreeColonist".Translate();
            var medicine = pawn.skills?.GetSkill(SkillDefOf.Medicine);
            if (medicine == null || medicine.TotallyDisabled || medicine.Level < 6)
                return "SSC_Cosmetic_MedicineRequired".Translate(6);
            return SSCCosmeticUtility.CultureReport(pawn, ritual);
        }

        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget,
            LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            var report = Report(p, selectedTarget, ritual?.Ritual ?? assignments?.Ritual ?? precept);
            if (report.Accepted && p == (assignments?.FirstAssignedPawn(SSCCosmeticUtility.RecipientRole) ?? ritual?.PawnWithRole(SSCCosmeticUtility.RecipientRole)))
                report = "SSC_Cosmetic_DifferentPawns".Translate();
            reason = skipReason ? null : report.Reason;
            return report.Accepted;
        }

        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn pawn = null, bool skipReason = false)
        {
            reason = null;
            return true;
        }
    }
}
