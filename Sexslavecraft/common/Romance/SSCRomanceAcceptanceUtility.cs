// AI 辅助生成
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class SSCRomanceAcceptanceUtility
    {
        [ThreadStatic] private static Attempt current;

        /// <summary>仅观察本次真实互动的拒绝结果，不保存配置、命令或跨读档状态。</summary>
        public sealed class Attempt
        {
            internal Attempt previous;
            internal Pawn actor, target;
            internal SSCRomanceAction action;
            internal bool forcedRejection, completed, command, nativeRomanceMessage;
        }

        /// <summary>每个 worker 建立独立观察帧；点名结果归原命令负责，原版是否发消息由实际调用记录，不能按 Job 快照猜测。</summary>
        public static Attempt Begin(Pawn actor, Pawn target, SSCRomanceAction action)
        {
            var attempt = new Attempt { previous = current, actor = actor, target = target, action = action,
                command = SSCRomanceExecutionScope.IsCommandInvocation(actor, target, action) };
            current = attempt;
            return attempt;
        }

        /// <summary>真实拒绝结算正常完成后发一条左上角消息；原版手动求爱和点名命令沿已有结果反馈，不叠加第二条。</summary>
        public static void Complete(Attempt attempt, bool originalRan)
        {
            if (attempt == null || current != attempt || attempt.completed) return;
            attempt.completed = true;
            if (!originalRan || !attempt.forcedRejection || attempt.command || attempt.nativeRomanceMessage) return;
            string key = attempt.action == SSCRomanceAction.Romance ? "SSC_Romance_ForcedRomanceRefusal" : "SSC_Romance_ForcedProposalRefusal";
            Messages.Message(key.Translate(attempt.actor.LabelShort, attempt.target.LabelShort), attempt.target, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>只释放本次栈顶，异常不能把内层强制拒绝残留给之后的查询或互动。</summary>
        public static void End(Attempt attempt)
        { if (attempt != null && current == attempt) current = attempt.previous; }

        /// <summary>原 worker 的失败消息原样转发并登记本次实际发送；任务在结算中切换也不会漏发或补发第二条。</summary>
        public static void NativeRomanceMessage(string text, LookTargets lookTargets, MessageTypeDef def, bool historical)
        {
            if (current?.action == SSCRomanceAction.Romance) current.nativeRomanceMessage = true;
            Messages.Message(text, lookTargets, def, historical);
        }

        /// <summary>只覆盖本次已发生互动的接受结果；原概率计算和一次随机取样保留，默认结果逐值透传。</summary>
        public static bool Resolve(bool vanillaAccepted, Pawn initiator, Pawn recipient, SSCRomanceAction action)
        {
            // 不修改公共 SuccessChance：它也参与菜单资格，改成零会把被动必拒误变为不可选目标。
            // 原版使用 Rand.Value < chance，且随机值可等于 1；覆盖 bool 才能严格保证接受。
            var decision = SSCRomancePolicy.Evaluate(initiator, recipient, action);
            var reception = decision.Reception;
            bool accepted = reception == SSCRomanceReception.ForceAccept ||
                (reception != SSCRomanceReception.ForceReject && vanillaAccepted);
            // 概率查询和直接调用 Resolve 均不弹消息；只有匹配的实际 worker 帧可登记拒绝。
            if (current != null && current.actor == initiator && current.target == recipient && current.action == action)
                current.forcedRejection = reception == SSCRomanceReception.ForceReject;
            SSCRomanceExecutionScope.ObserveDecision(initiator, recipient, action, accepted);
            SSCRomanceMoodEvents.Observe(initiator, recipient, action, vanillaAccepted, accepted, decision.ReceptionSetting);
            return accepted;
        }
    }
}
