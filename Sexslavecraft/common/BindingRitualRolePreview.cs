using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>基础资格和当前拟定配对分开显示；配对通过不代表身体和寻路检查已经通过。</summary>
    internal sealed class BindingRitualRolePreview
    {
        public readonly bool Candidate, Assigned, PairAllowed;
        public readonly string Reason;

        public BindingRitualRolePreview(bool candidate, bool assigned, bool pairAllowed, string reason)
        { Candidate = candidate; Assigned = assigned; PairAllowed = pairAllowed; Reason = reason; }
    }

    /// <summary>常驻标记只查询基础条件和低成本调教许可，不改变角色、绑定、指派或操作缓存。</summary>
    internal static class BindingRitualRolePreviewUtility
    {
        public static BindingRitualRolePreview Inspect(RitualRoleAssignments assignments, RitualRole role, Pawn pawn,
            Pawn replacing = null)
        {
            if (!BindingRitualSelectionUtility.TryGetProposedPair(assignments, role, pawn, replacing,
                out Pawn master, out Pawn slave, out _, out string reason))
                return new BindingRitualRolePreview(false, false, false, reason);
            bool assigned = assignments.FirstAssignedPawn(role is RitualRole_BindingMaster ? "master" : "slave") == pawn;
            if (!Candidate(role, pawn, out reason))
                return new BindingRitualRolePreview(false, assigned, false, reason);
            if (!BindingRitualSelectionUtility.CanPreviewAssignment(assignments, role, pawn, out reason, replacing))
                return new BindingRitualRolePreview(true, assigned, false, reason);

            // 特别是角色交换，需要检查被换去另一槽位的人，而不只检查拖拽候选。
            if (master != null && slave != null)
            {
                RitualRole_BindingMaster masterRole = null;
                RitualRole_BindingSlave slaveRole = null;
                foreach (RitualRole definedRole in assignments.AllRolesForReading)
                {
                    if (definedRole is RitualRole_BindingMaster m) masterRole = m;
                    if (definedRole is RitualRole_BindingSlave s) slaveRole = s;
                }
                if (master == slave || !Candidate(masterRole, master, out reason) || !Candidate(slaveRole, slave, out reason))
                    return new BindingRitualRolePreview(true, assigned, false, reason ?? BindingRitualSelectionUtility.InvalidReason());
                SSCTrainingAdmission admission = SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(master, slave, true), false);
                if (!admission.Allowed)
                    return new BindingRitualRolePreview(true, assigned, false, admission.Reason ?? BindingRitualSelectionUtility.InvalidReason());
            }
            return new BindingRitualRolePreview(true, assigned, true, null);
        }

        private static bool Candidate(RitualRole role, Pawn pawn, out string reason)
        {
            reason = null;
            bool allowed = role is RitualRole_BindingMaster master
                ? master.AppliesToCandidate(pawn, out reason, false)
                : role is RitualRole_BindingSlave slave && slave.AppliesToCandidate(pawn, out reason, false);
            if (!allowed && string.IsNullOrEmpty(reason)) reason = BindingRitualSelectionUtility.InvalidReason();
            return allowed;
        }
    }
}
