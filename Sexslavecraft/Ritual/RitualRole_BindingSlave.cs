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
            reason = null;
            if (p == null || !p.Spawned || !p.RaceProps.Humanlike || p.Dead || p.Downed) return false;
            if (!SSCIdentityUtility.IsSupportedVanillaStatus(p))
            {
                if (!skipReason) reason = Strings.Ritual_MustBeColonistOrSlave;
                return false;
            }
            if (selectedTarget.IsValid && !p.CanReach((LocalTargetInfo)selectedTarget, PathEndMode.Touch, Danger.Deadly))
            {
                if (!skipReason) reason = "MessageRitualRoleCannotReach".Translate();
                return false;
            }
            if (!base.AppliesIfChild(p, out reason, skipReason)) return false;
            if (!Trainjudge.TryCanBeFuckedWithReason(p, out string shortReason, out string detailedReport))
            {
                if (!skipReason) reason = shortReason;
                Log.Warning($"[SSC_RITUAL_ROLE] Candidate blocked: {p.LabelShort}. {shortReason}\n{detailedReport}");
                return false;
            }
            if (p.TryGetComp<CompSexSlaveTraining>() == null || SSCIdentityUtility.IsMaster(p))
            {
                if (!skipReason) reason = "SSC_Restrictions_TrainingTargetInvalid".Translate();
                return false;
            }
            Pawn host = assignments?.FirstAssignedPawn("master") ?? ritual?.PawnWithRole("master");
            string failure;
            if (host != null)
            {
                if (CanPair(host, p, out failure)) return true;
            }
            else
            {
                // 未选主持者时至少有一位关系候选；空指派或停用指派不能排除实际主人。
                if (CanPair(SSCBondUtility.GetBoundMaster(p), p, out failure) ||
                    CanPair(TrainerAssignmentUtility.GetActiveAssignedTrainer(p), p, out failure)) return true;
            }
            if (!skipReason) reason = failure;
            return false;
        }

        /// <summary>转换为正式仪式请求，不把身份或公交车状态当成许可。</summary>
        private static bool CanPair(Pawn host, Pawn target, out string reason)
        {
            return SSCRestrictionTrainingUtility.TryEvaluate(
                SSCRestrictionTrainingUtility.CreateRequest(host, target, true), false, out _, out reason);
        }

        /// <summary>不额外要求原版文化职位，目标资格由实际身份及配对检查决定。</summary>
        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false)
        {
            reason = null;
            return true;
        }
    }
}
