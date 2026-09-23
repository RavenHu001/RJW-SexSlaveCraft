using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>主持资格与所有权分离；已有绑定可由实际主人或获准的指定调教员主持。</summary>
    public class RitualRole_BindingMaster : RitualRole
    {
        /// <summary>核对主持者正常资格；目标已选时复用统一调教许可及关系检查。</summary>
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget,
            LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            if (!AppliesToCandidate(p, out reason, skipReason)) return false;
            if (ritual == null && BindingRitualSelectionUtility.IsPreview(assignments)) return true;
            Pawn slave = assignments?.FirstAssignedPawn("slave") ?? ritual?.PawnWithRole("slave");
            if (slave != null)
            {
                SSCTrainingAdmission admission = SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(p, slave, true), false);
                if (!admission.Allowed)
                {
                    if (!skipReason) reason = admission.Reason;
                    return false;
                }
            }
            return ValidateIndividual(p, out reason, selectedTarget, skipReason);
        }

        /// <summary>候选列表只读取基本状态和身份，不做寻路或双人许可查询。</summary>
        internal bool AppliesToCandidate(Pawn p, out string reason, bool skipReason)
        {
            reason = null;
            if (p == null || p.Dead || p.Downed || !p.Spawned || !p.RaceProps.Humanlike) return false;
            if (!p.IsColonist || p.IsSlave || p.IsPrisonerOfColony)
            {
                if (!skipReason) reason = Strings.Ritual_MustBeColonist;
                return false;
            }
            if (!SSCIdentityUtility.IsTrainer(p))
            {
                if (!skipReason) reason = "SSC_TrainerIdentity_Required".Translate();
                return false;
            }
            return true;
        }

        /// <summary>实际选择及开始时核对本人的完整条件；配对由提交服务按拟定组合单独校验。</summary>
        internal bool ValidateIndividual(Pawn p, out string reason, TargetInfo selectedTarget, bool skipReason = false)
        {
            if (!AppliesToCandidate(p, out reason, skipReason)) return false;
            if (selectedTarget.IsValid && !p.CanReach((LocalTargetInfo)selectedTarget, PathEndMode.Touch, Danger.Deadly))
            {
                if (!skipReason) reason = "MessageRitualRoleCannotReach".Translate();
                return false;
            }
            return true;
        }

        /// <summary>不额外要求原版文化职位，实际资格由角色配对检查决定。</summary>
        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false)
        {
            reason = null;
            return true;
        }
    }
}
