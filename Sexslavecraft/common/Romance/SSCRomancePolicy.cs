// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>发起与接收分别表达；允许发起不代表接受，更不表示原版条件或任务可执行。</summary>
    public sealed class SSCRomanceDecision
    {
        public bool CanInitiate { get; internal set; } = true;
        public SSCRomanceReception Reception { get; internal set; } = SSCRomanceReception.Vanilla;
        public SSCRomanceReason InitiatorReason { get; internal set; }
        public SSCRomanceReason RecipientReason { get; internal set; }
    }
    public static class SSCRomancePolicy
    {
        /// <summary>只算 SSC 规则；未知配置退回原版并返回诊断原因，不伪造成功或拒绝。</summary>
        public static SSCRomanceDecision Evaluate(Pawn initiator, Pawn recipient, SSCRomanceAction action,
            SSCRomanceAuthorization authorization = null)
        {
            var result = new SSCRomanceDecision();
            // 请求必须携带真实方向与已知行为，自指或缺引用不能按普通放行继续。
            if (initiator == null || recipient == null || initiator == recipient ||
                action < SSCRomanceAction.Romance || action > SSCRomanceAction.Breakup)
            { result.CanInitiate = false; result.InitiatorReason = SSCRomanceReason.InvalidPawn; return result; }
            var actor = SSCRomanceResolver.Resolve(initiator);
            // 两端独立解析；第一阶段的发起者也可能遇到第三阶段受控的接收者。
            var target = SSCRomanceResolver.Resolve(recipient);
            result.InitiatorReason = actor.Reason;
            result.RecipientReason = target.Reason;
            bool command = authorization != null && authorization.IsCurrent(initiator, recipient, action);
            // 无效命令不能悄悄降为普通请求后继续；自然入口不提供授权对象。
            if (authorization != null && !command)
            { result.CanInitiate = false; result.InitiatorReason = SSCRomanceReason.InvalidAuthorization; }
            else if (actor.Applicable && actor.Stage >= 2 && !command)
            {
                // 先读本行为许可，再统一筛选对象；对象选中不能重新开放已经禁止的主动行为。
                var setting = action == SSCRomanceAction.Romance ? SSCRomanceSetting.InitiateRomance :
                    action == SSCRomanceAction.Proposal ? SSCRomanceSetting.InitiateProposal : SSCRomanceSetting.InitiateBreakup;
                if (!actor.GetSaved(setting))
                { result.CanInitiate = false; result.InitiatorReason = SSCRomanceReason.RuleDenied; }
                // 行为与目标同时满足才可发起；目标只选一个类别，不让主人身份再被性奴类别重复否决。
                // 分手／离婚只读自身行为许可，不能因旧伴侣不在新关系对象范围中而无法结束关系。
                else if (action != SSCRomanceAction.Breakup &&
                    !actor.GetSaved(SSCRomanceResolver.TargetSetting(recipient, actor.Master)))
                { result.CanInitiate = false; result.InitiatorReason = SSCRomanceReason.TargetDenied; }
            }
            // 即使主动被阻止也保留接收端诊断；执行层先看 CanInitiate，不能把接收保证当发起许可。
            // 被动必拒仅决定真实互动后的结果，不能据此从自然候选或菜单中移除目标。
            if (action == SSCRomanceAction.Breakup) result.Reception = SSCRomanceReception.NotApplicable;
            else if (target.Applicable && target.Stage >= 3)
            {
                // 主人例外只影响接收者，不替发起者绕过其限制。
                if (initiator == target.Master && target.GetSaved(SSCRomanceSetting.AlwaysAcceptMaster))
                    result.Reception = SSCRomanceReception.ForceAccept;
                // 与主人保证同属本人的被动接受保证，优先于本人的普通被动禁止；不能解除发起方限制。
                else if (SSCIdentityUtility.IsSexSlave(initiator) && target.GetSaved(SSCRomanceSetting.AlwaysAcceptSexSlave))
                    result.Reception = SSCRomanceReception.ForceAccept;
                // 复用互斥对象分类，明确排除本人主人及 SSC 性奴；不能把前两项关闭当成“其他人”兜底。
                // 仅保证目标自己的接受，不反向开放发起方许可，也不改变前三阶门槛或原版必要条件。
                else if (target.GetSaved(SSCRomanceSetting.AlwaysAcceptOthers) &&
                    SSCRomanceResolver.TargetSetting(initiator, target.Master) == SSCRomanceSetting.InitiateToOthers)
                    result.Reception = SSCRomanceReception.ForceAccept;
                else if (!target.GetSaved(action == SSCRomanceAction.Romance ? SSCRomanceSetting.AcceptRomance : SSCRomanceSetting.AcceptProposal))
                    result.Reception = SSCRomanceReception.ForceReject;
            }
            return result;
        }
    }
}
