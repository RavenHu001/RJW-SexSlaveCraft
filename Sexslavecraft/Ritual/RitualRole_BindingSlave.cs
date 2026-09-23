using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public class RitualRole_BindingSlave : RitualRole
    {
        /// <summary>保留身体和身份条件，与主持端共用配对检查，保证先后选角结果一致。</summary>
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget,
            LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            if (!AppliesToCandidate(p, out reason, skipReason)) return false;
            if (ritual == null && BindingRitualSelectionUtility.IsPreview(assignments)) return true;
            Pawn host = assignments?.FirstAssignedPawn("master") ?? ritual?.PawnWithRole("master");
            string failure;
            bool paired = host != null ? CanPair(host, p, out failure, skipReason)
                : CanPair(SSCBondUtility.GetBoundMaster(p), p, out failure, skipReason)
                    || CanPair(TrainerAssignmentUtility.GetActiveAssignedTrainer(p), p, out failure, skipReason);
            if (!paired)
            {
                if (!skipReason) reason = failure;
                return false;
            }
            return ValidateIndividual(p, out reason, selectedTarget, skipReason);
        }

        /// <summary>候选预览保留基础身份和原版年龄规则，不触发 RJW、寻路或配对查询。</summary>
        internal bool AppliesToCandidate(Pawn p, out string reason, bool skipReason)
        {
            reason = null;
            if (p == null || !p.Spawned || !p.RaceProps.Humanlike || p.Dead || p.Downed) return false;
            if (!SSCIdentityUtility.IsSupportedVanillaStatus(p))
            {
                if (!skipReason) reason = Strings.Ritual_MustBeColonistOrSlave;
                return false;
            }
            if (!base.AppliesIfChild(p, out reason, skipReason)) return false;
            if (p.TryGetComp<CompSexSlaveTraining>() == null || SSCIdentityUtility.IsMaster(p))
            {
                if (!skipReason) reason = "SSC_Restrictions_TrainingTargetInvalid".Translate();
                return false;
            }
            return true;
        }

        /// <summary>执行者被选入槽位或正式开始时才进行可达性及一次身体校验，不记录正常拒绝日志。</summary>
        internal bool ValidateIndividual(Pawn p, out string reason, TargetInfo selectedTarget, bool skipReason = false)
        {
            if (!AppliesToCandidate(p, out reason, skipReason)) return false;
            if (selectedTarget.IsValid && !p.CanReach((LocalTargetInfo)selectedTarget, PathEndMode.Touch, Danger.Deadly))
            {
                if (!skipReason) reason = "MessageRitualRoleCannotReach".Translate();
                return false;
            }
            return Trainjudge.TryCanBeFuckedWithReason(p, out reason, skipReason);
        }

        /// <summary>转换为正式仪式请求，不把身份或公交车状态当成许可。</summary>
        private static bool CanPair(Pawn host, Pawn target, out string reason, bool skipReason)
        {
            SSCTrainingAdmission admission = SSCRestrictionTrainingUtility.Evaluate(
                SSCRestrictionTrainingUtility.CreateRequest(host, target, true), false);
            reason = skipReason ? null : admission.Reason;
            return admission.Allowed;
        }

        /// <summary>不额外要求原版文化职位，目标资格由实际身份及配对检查决定。</summary>
        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false)
        {
            reason = null;
            return true;
        }
    }
}
