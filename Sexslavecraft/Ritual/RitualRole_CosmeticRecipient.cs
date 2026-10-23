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
            // 受塑者既要能升档且属于本文化，也须能主动参加仪式；此限制不用于麻醉中的手术患者。
            var report = SSCCosmeticUtility.RitualRecipientReport(p, ritual?.Ritual ?? assignments?.Ritual ?? precept, selectedTarget.Map);
            if (report.Accepted) report = SSCCosmeticUtility.ActiveParticipantReport(p, selectedTarget);
            // 与主持者一侧对称检查，保证不同选角顺序都不能让同一人占两槽。
            if (report.Accepted && p == (assignments?.FirstAssignedPawn(SSCCosmeticUtility.HostRole) ?? ritual?.PawnWithRole(SSCCosmeticUtility.HostRole)))
                report = "SSC_Cosmetic_DifferentPawns".Translate();
            reason = skipReason ? null : report.Reason;
            return report.Accepted;
        }

        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn pawn = null, bool skipReason = false)
        {
            // 文化职位不构成额外门槛；只使用人选自身的参与与受塑条件。
            reason = null;
            return true;
        }
    }
}
