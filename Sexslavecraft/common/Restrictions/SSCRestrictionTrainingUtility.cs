using Verse;

namespace SexSlaveCraft
{
    /// <summary>工作准入的失败类型；行为许可与身体、身份、指派条件保持分层。</summary>
    internal enum SSCTrainingFailure { None, PermissionDenied, TrainerRequired, TargetInvalid, BindingMasterRequired, AssignmentRequired, TargetIdentityRequired, BindingCooldown }

    /// <summary>明确区分整体工作准入与限制系统许可，避免主人许可通过被误读为全部工作条件通过。</summary>
    internal sealed class SSCTrainingAdmission
    {
        public SSCRestrictionDecision Permission { get; }
        public SSCTrainingFailure Failure { get; }
        public bool Allowed => Failure == SSCTrainingFailure.None;
        private readonly string detail;

        /// <summary>保留完整许可结果与工作失败类型；成功时无需构造任何本地化原因。</summary>
        public SSCTrainingAdmission(SSCRestrictionDecision permission, SSCTrainingFailure failure, string detail = null)
        {
            Permission = permission;
            Failure = failure;
            this.detail = detail;
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
                    case SSCTrainingFailure.TargetIdentityRequired: return "SSC_Training_TargetIdentityRequired".Translate();
                    case SSCTrainingFailure.BindingMasterRequired: return "SSC_Restrictions_BindingMasterRequired".Translate();
                    case SSCTrainingFailure.AssignmentRequired: return "SSC_Restrictions_TrainingAssignmentRequired".Translate();
                    case SSCTrainingFailure.BindingCooldown: return "SSC_BondRelease_BindingCooldown".Translate(detail);
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
#if SSC_RW17
            // 拒绝接收且锁定为主人的协调只适用于实际绑定。无主名单独立于此刻的接收许可。
            if (SSCRestrictionResolver.GetOwnerlessStage(pawn) > 0) return null;
#endif
            SSCRestrictionConfig config = pawn?.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            if (!SSCRestrictionResolver.IsApplicable(pawn) || config?.IsValid() != true) return null;
            SSCRestrictionResolution rule = SSCRestrictionResolver.Resolve(pawn, config.rules, SSCRestrictionRule.ReceiveTraining);
            return rule.Valid && rule.Value == SSCRestrictionValue.Deny ? SSCBondUtility.GetBoundMaster(pawn) : null;
        }

        /// <summary>按任务用途选择准入规则，供选角、开场和训练任务共用。</summary>
        /// <remarks>
        /// 日常有无锁链都走 DailyTraining，由统一校验判断身份、许可和指派。
        /// 主人为无链目标准备新绑定。训导官主持正式无主目标时，继续读取接收调教的实际生效条目。
        /// 当前用途只决定后续检查，任职和指派均由 Evaluate 核验。零阶无链目标保留原先的建绑准入。
        /// </remarks>
        public static SSCRestrictionRequest CreateRequest(Pawn actor, Pawn target, bool ritual)
        {
#if SSC_RW17
            SSCInteractionKind kind = !ritual ? SSCInteractionKind.DailyTraining
                : SSCBondUtility.GetBoundMaster(target) != null ||
                    (!SSCIdentityUtility.IsMaster(actor) && SSCRestrictionResolver.GetOwnerlessStage(target) > 0)
                    ? SSCInteractionKind.RitualTraining : SSCInteractionKind.BindingPreparation;
#else
            SSCInteractionKind kind = SSCBondUtility.GetBoundMaster(target) == null
                ? SSCInteractionKind.BindingPreparation
                : ritual ? SSCInteractionKind.RitualTraining : SSCInteractionKind.DailyTraining;
#endif
            return new SSCRestrictionRequest(actor, target, kind, actor != null && target != null && actor != target);
        }

        /// <summary>识别需要额外检查调教资格与唯一指派的请求，不将人格排泄归为调教。</summary>
        public static bool IsTraining(SSCRestrictionRequest request)
        {
            return request != null && (request.Kind == SSCInteractionKind.DailyTraining || request.Kind == SSCInteractionKind.RitualTraining ||
                request.Kind == SSCInteractionKind.BindingPreparation);
        }

        /// <summary>先查询统一许可，再核对工作资格；自动日常工作只交给指定者，主人手动发起及主持不受指派排除。</summary>
        /// <remarks>总开关停用不取消首次建绑资质与唯一指派；这里没有公交车、装备或旧开放字段的许可分支。</remarks>
        public static SSCTrainingAdmission Evaluate(SSCRestrictionRequest request, bool requireAssignment)
            => EvaluateCore(request, requireAssignment, false);

#if SSC_RW17
        /// <summary>候选阶段尚未创建驱动，用请求涉及的两名角色构造预检方向。</summary>
        internal static SSCTrainingAdmission EvaluateBreakdown(Pawn trainer, Pawn subject)
            => EvaluateBreakdown(new SSCRestrictionRequest(trainer, subject, SSCInteractionKind.DailyTraining,
                trainer != null && subject != null && trainer != subject));

        /// <summary>特批只绕过日常任务的调度条件。发起和接收必须符合本场 Job 的实际方向。</summary>
        internal static SSCTrainingAdmission EvaluateBreakdown(SSCRestrictionRequest request)
            => request.DirectionKnown ? EvaluateCore(request, false, true)
                : new SSCTrainingAdmission(SSCRestrictionPolicy.Evaluate(null), SSCTrainingFailure.PermissionDenied);
#endif

        private static SSCTrainingAdmission EvaluateCore(SSCRestrictionRequest request, bool requireAssignment, bool breakdown)
        {
            SSCRestrictionDecision decision = SSCRestrictionPolicy.Evaluate(request);
            // 身份是工作资格，不受主人最高许可、总限制开关或强制命令豁免。
            // SSC 身份独立于成长、指派、锁链和原版身份。
            if (IsTraining(request) && request.Receiver != null && !SSCIdentityUtility.IsSexSlave(request.Receiver))
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.TargetIdentityRequired);
            if (!decision.Allowed)
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.PermissionDenied);
            if (!IsTraining(request)) return new SSCTrainingAdmission(decision, SSCTrainingFailure.None);
            Pawn actor = request.Initiator;
            Pawn target = request.Receiver;
            if (actor == null || target == null || actor == target || actor.Dead || actor.Destroyed ||
                actor.Downed || !actor.IsColonist || actor.IsSlave || actor.IsPrisonerOfColony || !SSCIdentityUtility.IsTrainer(actor))
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.TrainerRequired);
            if (target.TryGetComp<CompSexSlaveTraining>() == null || target.Dead || target.Destroyed)
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.TargetInvalid);
            // 特批授权由发作和 Job 验证。此处保留普通调教许可与任职要求，跳过日常指派。
            if (breakdown) return new SSCTrainingAdmission(decision, SSCTrainingFailure.None);
            // 首次准备只能由指定且可建立所有权的主人完成；停用行为限制也不能授予新主人身份。
            if (request.Kind == SSCInteractionKind.BindingPreparation && !SSCIdentityUtility.IsMaster(actor))
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.BindingMasterRequired);
#if SSC_RW17
            if (request.Kind == SSCInteractionKind.BindingPreparation)
            {
                // 零阶沿用原首次建链算法，但由旧锁链解绑得到的零阶仍须完成冷却。
                // 训导官的成长仪式使用 RitualTraining，因此不受再次建绑期限约束。
                long deadline = Hediff_SSCBondReleaseAftereffect.ActiveDeadline(target);
                if (deadline >= 0)
                    return new SSCTrainingAdmission(decision, SSCTrainingFailure.BindingCooldown,
                        Hediff_SSCBondReleaseAftereffect.FormatRemaining(deadline));
                // 正式无主的绑定和爆发共用新绑定资格。零阶仪式仍保留原首次建链的其他检查。
                if (SSCRestrictionResolver.GetOwnerlessStage(target) > 0 && !SSCBondUtility.CanBindNewWithProgress(actor, target))
                    return new SSCTrainingAdmission(decision, SSCTrainingFailure.TargetInvalid);
            }
#endif
            // 自动日常任务始终检查指派。手动发起时只有当前实际主人享有既有的指派豁免。
            if ((requireAssignment || decision.Reason != SSCRestrictionReason.BoundOwner) &&
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
                (!TrainerAssignmentUtility.IsAssignedTrainer(target.TryGetComp<CompSexSlaveTraining>(), actor) || actor.Discarded))
#else
                TrainerAssignmentUtility.GetActiveAssignedTrainer(target) != actor)
#endif
                return new SSCTrainingAdmission(decision, SSCTrainingFailure.AssignmentRequired);
            return new SSCTrainingAdmission(decision, SSCTrainingFailure.None);
        }

    }
}
