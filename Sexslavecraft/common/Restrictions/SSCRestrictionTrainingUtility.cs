using Verse;

namespace SexSlaveCraft
{
    /// <summary>工作准入的失败类型；行为许可与身体、身份、指派条件保持分层。</summary>
    internal enum SSCTrainingFailure { None, PermissionDenied, TrainerRequired, TargetInvalid, BindingMasterRequired, AssignmentRequired }

    /// <summary>明确区分整体工作准入与限制系统许可，避免主人许可通过被误读为全部工作条件通过。</summary>
    internal sealed class SSCTrainingAdmission
    {
        public SSCRestrictionDecision Permission { get; }
        public SSCTrainingFailure Failure { get; }
        public bool Allowed => Failure == SSCTrainingFailure.None;

        /// <summary>保留完整许可结果与工作失败类型；成功时无需构造任何本地化原因。</summary>
        public SSCTrainingAdmission(SSCRestrictionDecision permission, SSCTrainingFailure failure)
        {
            Permission = permission;
            Failure = failure;
        }

        /// <summary>仅在调用者需要显示或记录时生成失败文字，不把翻译作为业务判断条件。</summary>
        public string Reason
        {
            get
            {
                switch (Failure)
                {
                    case SSCTrainingFailure.PermissionDenied:
                        return "SSC_Restrictions_JobRejected".Translate(("SSC_Restrictions_Reason_" + Permission.Reason).Translate());
                    case SSCTrainingFailure.TrainerRequired: return "SSC_TrainerIdentity_Required".Translate();
                    case SSCTrainingFailure.TargetInvalid: return "SSC_Restrictions_TrainingTargetInvalid".Translate();
                    case SSCTrainingFailure.BindingMasterRequired: return "SSC_Restrictions_BindingMasterRequired".Translate();
                    case SSCTrainingFailure.AssignmentRequired: return "SSC_Restrictions_TrainingAssignmentRequired".Translate();
                    default: return null;
                }
            }
        }
    }

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
        public static SSCTrainingAdmission Evaluate(SSCRestrictionRequest request, bool requireAssignment)
        {
            SSCRestrictionDecision decision = SSCRestrictionPolicy.Evaluate(request);
            if (!decision.Allowed)
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.PermissionDenied);
            if (!IsTraining(request)) return new SSCTrainingAdmission(decision, SSCTrainingFailure.None);
            Pawn actor = request.Initiator;
            Pawn target = request.Receiver;
            if (actor == null || target == null || actor == target || actor.Dead || actor.Destroyed ||
                actor.Downed || !actor.IsColonist || actor.IsSlave || actor.IsPrisonerOfColony || !SSCIdentityUtility.IsTrainer(actor))
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.TrainerRequired);
            if (target.TryGetComp<CompSexSlaveTraining>() == null || SSCIdentityUtility.IsMaster(target))
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.TargetInvalid);
            // 首次准备只能由指定且可建立所有权的主人完成；停用行为限制也不能授予新主人身份。
            if (request.Kind == SSCInteractionKind.BindingPreparation && !SSCIdentityUtility.IsMaster(actor))
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.BindingMasterRequired);
            if ((requireAssignment || decision.Reason != SSCRestrictionReason.BoundOwner) &&
                TrainerAssignmentUtility.GetActiveAssignedTrainer(target) != actor)
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.AssignmentRequired);
            return new SSCTrainingAdmission(decision, SSCTrainingFailure.None);
        }

    }
}
