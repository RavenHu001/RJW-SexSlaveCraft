using Verse;

namespace SexSlaveCraft
{
    /// <summary>为日常与仪式提供用途和工作关系适配；行为许可始终由统一策略决定。</summary>
    internal static class SSCRestrictionTrainingUtility
    {
        /// <summary>按当前调教条目计算选择锁定；无效配置不擅自改关系，总开关关闭时解除锁定。</summary>
        public static Pawn GetForcedTrainer(Pawn pawn)
        {
            if (!(SSCMod.settings?.enableSexSlaveProtectionRules ?? true)) return null;
            SSCRestrictionConfig config = pawn?.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            if (!SSCRestrictionResolver.IsApplicable(pawn) || config?.IsValid() != true) return null;
            SSCRestrictionResolution rule = SSCRestrictionResolver.Resolve(pawn, config.rules, SSCRestrictionRule.ReceiveTraining);
            return rule.Valid && rule.Value == SSCRestrictionValue.Deny ? SSCBondUtility.GetBoundMaster(pawn) : null;
        }

        /// <summary>未绑定目标沿用首次建绑准备；已绑定目标使用独立调教条目，不查询普通双人条目。</summary>
        public static SSCRestrictionRequest CreateRequest(Pawn actor, Pawn target, bool ritual)
        {
            SSCInteractionKind kind = SSCBondUtility.GetBoundMaster(target) == null
                ? SSCInteractionKind.BindingPreparation
                : ritual ? SSCInteractionKind.RitualTraining : SSCInteractionKind.DailyTraining;
            return new SSCRestrictionRequest(actor, target, kind, actor != null && target != null && actor != target);
        }

        /// <summary>识别需要额外检查调教资格与唯一指派的请求，不将人格排泄归为调教。</summary>
        public static bool IsTraining(SSCRestrictionRequest request)
        {
            return request.Kind == SSCInteractionKind.DailyTraining || request.Kind == SSCInteractionKind.RitualTraining ||
                request.Kind == SSCInteractionKind.BindingPreparation;
        }

        /// <summary>先查询统一许可，再核对工作资格；自动日常工作只交给指定者，主人手动发起及主持不受指派排除。</summary>
        /// <remarks>总开关停用不取消首次建绑资质与唯一指派；这里没有公交车、装备或旧开放字段的许可分支。</remarks>
        public static bool TryEvaluate(SSCRestrictionRequest request, bool requireAssignment,
            out SSCRestrictionDecision decision, out string reason)
        {
            decision = SSCRestrictionPolicy.Evaluate(request);
            reason = null;
            if (!decision.Allowed)
            {
                reason = "SSC_Restrictions_JobRejected".Translate(("SSC_Restrictions_Reason_" + decision.Reason).Translate());
                return false;
            }
            if (!IsTraining(request)) return true;
            Pawn actor = request.Initiator;
            Pawn target = request.Receiver;
            if (actor == null || target == null || actor == target || actor.Dead || actor.Destroyed ||
                actor.Downed || !actor.IsColonist || actor.IsSlave || actor.IsPrisonerOfColony || !SSCIdentityUtility.IsTrainer(actor))
            {
                reason = "SSC_TrainerIdentity_Required".Translate();
                return false;
            }
            if (target.TryGetComp<CompSexSlaveTraining>() == null || SSCIdentityUtility.IsMaster(target))
            {
                reason = "SSC_Restrictions_TrainingTargetInvalid".Translate();
                return false;
            }
            // 首次准备只能由指定且可建立所有权的主人完成；停用行为限制也不能授予新主人身份。
            if (request.Kind == SSCInteractionKind.BindingPreparation && !SSCIdentityUtility.IsMaster(actor))
            {
                reason = "SSC_Restrictions_BindingMasterRequired".Translate();
                return false;
            }
            if ((requireAssignment || decision.Reason != SSCRestrictionReason.BoundOwner) &&
                TrainerAssignmentUtility.GetActiveAssignedTrainer(target) != actor)
            {
                reason = "SSC_Restrictions_TrainingAssignmentRequired".Translate();
                return false;
            }
            return true;
        }
    }
}
