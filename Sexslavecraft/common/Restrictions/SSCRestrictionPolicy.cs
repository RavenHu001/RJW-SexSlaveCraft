using System;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCInteractionKind
    {
        Unknown, Masturbation, Consensual, Forced, DailyTraining, RitualTraining, PersonalityExcretion, BindingPreparation
    }

    public enum SSCRestrictionReason
    {
        BoundOwner, SystemDisabled, NotApplicable, Allowed, IncompleteContext,
        ConfigurationMissing, ConfigurationInvalid, RuleDenied, OwnerOnly, InvalidBindingPreparation
    }

    /// <summary>适配器必须提供实际发起方向和用途，不从性别、姿势或主人身份猜测方向。</summary>
    public sealed class SSCRestrictionRequest
    {
        public readonly Pawn Initiator;
        public readonly Pawn Receiver;
        public readonly SSCInteractionKind Kind;
        public readonly bool DirectionKnown;
        // Only the read-only development preview may use temporary defaults for missing configurations.
        internal bool PreviewDefaults;

        /// <summary>记录调用方确认的实际发起者、接收者和用途；方向不明时必须显式传入 false。</summary>
        /// <remarks>接收者为空不会被自动解释为单人行为，单人请求仍需明确指定用途。</remarks>
        public SSCRestrictionRequest(Pawn initiator, Pawn receiver, SSCInteractionKind kind, bool directionKnown = true)
        {
            Initiator = initiator; Receiver = receiver; Kind = kind; DirectionKnown = directionKnown;
        }
    }

    public sealed class SSCRestrictionDecision
    {
        public bool Allowed { get; internal set; }
        public SSCRestrictionReason Reason { get; internal set; }
        public Pawn Subject { get; internal set; }
        public SSCRestrictionResolution Entry { get; internal set; }
    }

    /// <summary>新系统唯一行为许可入口。阶段 1 只供测试/预览调用，旧任务尚未接管。</summary>
    public static class SSCRestrictionPolicy
    {
        /// <summary>统一判定行为许可；方向明确的已绑定主人对自身目标发起请求时立即放行。</summary>
        /// <remarks>
        /// 主人放行先于用途、配置、特化及装备校验，后续条目不能否决；其他请求按用途检查主动或接收条目。
        /// 本入口只判定新系统许可，不替代任务自身的身体条件、可达性及训练资格检查。
        /// </remarks>
        public static SSCRestrictionDecision Evaluate(SSCRestrictionRequest request)
        {
            if (request == null) return Result(false, SSCRestrictionReason.IncompleteContext);
            Pawn actor = request.Initiator;
            Pawn target = request.Receiver;
            // Must precede config reads, purpose validation, equipment and all per-entry restrictions.
            if (request.DirectionKnown && actor != null && target != null && actor != target && SSCBondUtility.IsBoundTo(target, actor))
                return Result(true, SSCRestrictionReason.BoundOwner);
            if (SSCMod.settings != null && !SSCMod.settings.enableSexSlaveProtectionRules)
                return Result(true, SSCRestrictionReason.SystemDisabled);

            bool actorApplies = SSCRestrictionResolver.IsApplicable(actor);
            bool targetApplies = SSCRestrictionResolver.IsApplicable(target);
            bool preparation = request.Kind == SSCInteractionKind.BindingPreparation;
            if (!actorApplies && !targetApplies && !(preparation && target?.TryGetComp<CompSexSlaveTraining>() != null))
                return Result(true, SSCRestrictionReason.NotApplicable);
            if (!request.DirectionKnown || actor == null || request.Kind == SSCInteractionKind.Unknown ||
                !Enum.IsDefined(typeof(SSCInteractionKind), request.Kind))
                return Result(false, SSCRestrictionReason.IncompleteContext);

            if (request.Kind == SSCInteractionKind.Masturbation)
            {
                if (target != null && target != actor) return Result(false, SSCRestrictionReason.IncompleteContext);
                return actorApplies ? Check(actor, SSCRestrictionRule.Masturbation, request) : Result(true, SSCRestrictionReason.NotApplicable);
            }
            if (target == null || actor == target) return Result(false, SSCRestrictionReason.IncompleteContext);

            if (preparation)
            {
                CompSexSlaveTraining comp = target.TryGetComp<CompSexSlaveTraining>();
                bool valid = comp != null && comp.pawnIdentity != PawnIdentity.Master &&
                    SSCBondUtility.GetBoundMaster(target) == null && comp.selectedTrainer == actor && SSCIdentityUtility.IsMaster(actor);
                return Result(valid, valid ? SSCRestrictionReason.Allowed : SSCRestrictionReason.InvalidBindingPreparation);
            }
            if (request.Kind == SSCInteractionKind.DailyTraining || request.Kind == SSCInteractionKind.RitualTraining)
                return targetApplies ? Check(target, SSCRestrictionRule.ReceiveTraining, request) : Result(true, SSCRestrictionReason.NotApplicable);

            bool forced = request.Kind == SSCInteractionKind.Forced;
            SSCRestrictionDecision active = null;
            if (actorApplies)
            {
                active = Check(actor,
                    forced ? SSCRestrictionRule.ForcedInitiation : SSCRestrictionRule.ConsensualInitiation, request);
                if (!active.Allowed) return active;
                if (active.Entry.Value == SSCRestrictionValue.OwnerOnly && !SSCBondUtility.IsBoundTo(actor, target))
                {
                    active.Allowed = false;
                    active.Reason = SSCRestrictionReason.OwnerOnly;
                    return active;
                }
            }
            return targetApplies ? Check(target, forced ? SSCRestrictionRule.ReceiveForced : SSCRestrictionRule.ReceiveConsensual, request)
                : active ?? Result(true, SSCRestrictionReason.Allowed);
        }

        /// <summary>校验角色配置并解析单项许可，返回拒绝原因及规则来源；OwnerOnly 的关系判断由主入口完成。</summary>
        /// <remarks>仅开发预览可临时使用默认配置；正式查询遇到未初始化配置时拒绝，始终不写入角色。</remarks>
        private static SSCRestrictionDecision Check(Pawn subject, SSCRestrictionRule rule, SSCRestrictionRequest request)
        {
            SSCRestrictionConfig config = subject.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            if (config == null && request.PreviewDefaults)
                config = SSCRestrictionResolver.CreateInitialConfiguration(subject, SSCMod.settings?.restrictionDefaults);
            if (config == null) return Result(false, SSCRestrictionReason.ConfigurationMissing, subject);
            if (config.version != SSCRestrictionConfig.CurrentVersion || config.rules == null)
                return Result(false, SSCRestrictionReason.ConfigurationInvalid, subject);
            SSCRestrictionResolution entry = SSCRestrictionResolver.Resolve(subject, config.rules, rule);
            bool allowed = entry.Valid && entry.Value != SSCRestrictionValue.Deny;
            var result = Result(allowed, !entry.Valid ? SSCRestrictionReason.ConfigurationInvalid :
                allowed ? SSCRestrictionReason.Allowed : SSCRestrictionReason.RuleDenied, subject);
            result.Entry = entry;
            return result;
        }

        /// <summary>创建带许可、原因和相关角色的判定结果；具体规则来源由条目检查另行填入。</summary>
        private static SSCRestrictionDecision Result(bool allowed, SSCRestrictionReason reason, Pawn subject = null)
        {
            return new SSCRestrictionDecision { Allowed = allowed, Reason = reason, Subject = subject };
        }
    }
}
