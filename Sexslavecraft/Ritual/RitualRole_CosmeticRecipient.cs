// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class RitualRole_CosmeticRecipient : RitualRole
    {
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget,
            LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            var report = SSCCosmeticUtility.RitualRecipientReport(p, ritual?.Ritual ?? assignments?.Ritual ?? precept, selectedTarget.Map);
            if (report.Accepted) report = SSCCosmeticUtility.ActiveParticipantReport(p, selectedTarget);
            if (report.Accepted && p == (assignments?.FirstAssignedPawn(SSCCosmeticUtility.HostRole) ?? ritual?.PawnWithRole(SSCCosmeticUtility.HostRole)))
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
