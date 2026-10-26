// AI 辅助生成：真实互动事实与心情准入分离；日志、任务反馈与心情共用一次确认。
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一方已经发生的受控经历；只保存事实，不携带心情分支、命令权限或运行期凭据。</summary>
    public sealed class SSCRomanceInteractionFact : IExposable
    {
        private SSCRomanceCompositeEvent kind;
        private SSCRomanceOldRelation before;
        public SSCRomanceCompositeEvent Kind => kind;
        public SSCRomanceOldRelation Before => before;
        public bool IsCommand => kind >= SSCRomanceCompositeEvent.CommandRomanceAccepted && kind <= SSCRomanceCompositeEvent.CommandDivorce;
        public SSCRomanceAction Action => kind == SSCRomanceCompositeEvent.ForcedRomanceAccepted || kind == SSCRomanceCompositeEvent.ForcedRomanceRejected ||
            kind == SSCRomanceCompositeEvent.CommandRomanceAccepted || kind == SSCRomanceCompositeEvent.CommandRomanceRejected ? SSCRomanceAction.Romance :
            kind >= SSCRomanceCompositeEvent.CommandEndLover && kind <= SSCRomanceCompositeEvent.CommandDivorce ? SSCRomanceAction.Breakup : SSCRomanceAction.Proposal;
        public bool WasRejected => kind == SSCRomanceCompositeEvent.ForcedRomanceRejected || kind == SSCRomanceCompositeEvent.ForcedProposalRejected ||
            kind == SSCRomanceCompositeEvent.ForcedProposalRejectedAndSeparated || kind == SSCRomanceCompositeEvent.CommandRomanceRejected ||
            kind == SSCRomanceCompositeEvent.CommandProposalRejected || kind == SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated;
        public bool IsValid => kind >= SSCRomanceCompositeEvent.ForcedRomanceAccepted && kind <= SSCRomanceCompositeEvent.CommandDivorce &&
            (Action == SSCRomanceAction.Romance ? before == SSCRomanceOldRelation.None :
             Action == SSCRomanceAction.Proposal ? before == SSCRomanceOldRelation.Lover :
             kind == SSCRomanceCompositeEvent.CommandDivorce ? before == SSCRomanceOldRelation.Spouse :
             kind == SSCRomanceCompositeEvent.CommandEndFiance ? before == SSCRomanceOldRelation.Fiance : before == SSCRomanceOldRelation.Lover);
        /// <summary>Scribe 创建入口；默认空值不是已确认事件。</summary>
        public SSCRomanceInteractionFact() { }
        /// <summary>仅共用结果确认器创建事实；与心情能否获得无关。</summary>
        internal SSCRomanceInteractionFact(SSCRomanceCompositeEvent kind, SSCRomanceOldRelation before)
        { this.kind = kind; this.before = before; }
        /// <summary>消费者各持独立副本，不能通过返回值改写原日志或命令结果。</summary>
        public SSCRomanceInteractionFact Copy() => new SSCRomanceInteractionFact(kind, before);
        /// <summary>保存发生时的事件与原关系，不在读取时重新推断、授权或结算。</summary>
        public void ExposeData()
        { Scribe_Values.Look(ref kind, "kind"); Scribe_Values.Look(ref before, "before"); }
    }

    /// <summary>确认完整原生结果；不读取心情准入、记忆目录或原版心情替换钩子的可用性。</summary>
    internal static class SSCRomanceInteractionFacts
    {
        /// <summary>只接受原调用帧、正常 worker、原人格及双向最终关系；重复与不确定结果均不补造事实。</summary>
        internal static bool TryConfirm(SSCRomanceMoodFrame frame, bool originalRan, Exception exception, out string reason)
        {
            reason = "UnconfirmedResult";
            if (!frame.Entered) { reason = "DirectWorkerWithoutSnapshot"; return false; }
            if (!originalRan) { reason = "WorkerSkipped"; return false; }
            if (exception != null) { reason = "WorkerException:" + exception.GetType().Name; return false; }
            if (frame.FactsConfirmed) { reason = "Duplicate"; return false; }
            if (!SSCRomanceMoodEvents.CurrentInteractionPerson(frame.ActorState) ||
                !SSCRomanceMoodEvents.CurrentInteractionPerson(frame.TargetState))
            { reason = "InvalidContext"; return false; }
            // 双方快照必须属于同一关系阶段；采集中的回调换婚约不能把真正的离婚记成分手。
            var before = frame.ActorState.Data.Relation;
            if (before != frame.TargetState.Data.Relation ||
                (frame.Action == SSCRomanceAction.Romance ? before != SSCRomanceOldRelation.None :
                 frame.Action == SSCRomanceAction.Proposal ? before != SSCRomanceOldRelation.Lover :
                 frame.Action != SSCRomanceAction.Breakup || before < SSCRomanceOldRelation.Lover || before > SSCRomanceOldRelation.Spouse)) return false;
            var after = SSCRomanceMoodEvents.Relation(frame.Actor, frame.Target);
            if (SSCRomanceMoodEvents.Relation(frame.Target, frame.Actor) != after) return false;
            if (frame.Action == SSCRomanceAction.Breakup)
            {
                if (frame.ActorState.Data.Relation == SSCRomanceOldRelation.None || after != SSCRomanceOldRelation.None) return false;
            }
            else
            {
                if (!frame.DecisionKnown || frame.Accepted != (after == (frame.Action == SSCRomanceAction.Romance ?
                    SSCRomanceOldRelation.Lover : SSCRomanceOldRelation.Fiance))) return false;
                if (frame.Action == SSCRomanceAction.Romance && !frame.Accepted && after != SSCRomanceOldRelation.None) return false;
                if (frame.Action == SSCRomanceAction.Proposal && !frame.Accepted &&
                    (!Harmony_SSCRomanceProposalFacts.Ready || !frame.ProposalRejected ||
                     frame.ProposalSeparated != (after == SSCRomanceOldRelation.None) ||
                     (!frame.ProposalSeparated && after != SSCRomanceOldRelation.Lover))) return false;
            }
            // 最后直接核对同一关系载体中的实际列表，不再调用可被补丁回调的 DirectRelationExists。
            // 第二次方向查询若更改了第一方向，旧查询返回值不能成为已发生事实。
            if (ListedRelation(frame.ActorState, frame.Target) != after || ListedRelation(frame.TargetState, frame.Actor) != after) return false;
            // 关系查询可能由外部补丁回调；完成所有证据读取后再核对原人格，不重新捕获凭据。
            if (!SSCRomanceMoodEvents.CurrentInteractionPerson(frame.ActorState) ||
                !SSCRomanceMoodEvents.CurrentInteractionPerson(frame.TargetState)) { reason = "InvalidContext"; return false; }
            // 先冻结完整事实，再进入可被其他模组回调的心情处理；后续拒绝准入不会抹去已经发生的行为。
            frame.Completed = frame.FactsConfirmed = true;
            frame.ConfirmedRelation = after;
            if (frame.Command && frame.ActorState.Applicable && frame.ActorState.Data.Stage == 4)
                frame.ActorFact = new SSCRomanceInteractionFact(CommandEvent(frame), frame.ActorState.Data.Relation);
            if (SSCRomanceMoodEvents.HasControlledReception(frame))
                frame.TargetFact = new SSCRomanceInteractionFact(PassiveEvent(frame), frame.TargetState.Data.Relation);
            SSCRomanceExecutionScope.ObserveFacts(frame);
            reason = "Confirmed";
            return true;
        }

        /// <summary>读取已捕获关系实例的当前直接列表；与原 Relation 相同的配偶／婚约／恋人优先级。</summary>
        private static SSCRomanceOldRelation ListedRelation(SSCRomanceMoodFrame.Person person, Pawn other)
        {
            var result = SSCRomanceOldRelation.None;
            foreach (var relation in person.Relations.DirectRelations)
            {
                if (relation.otherPawn != other) continue;
                if (relation.def == RimWorld.PawnRelationDefOf.Spouse) return SSCRomanceOldRelation.Spouse;
                if (relation.def == RimWorld.PawnRelationDefOf.Fiance) result = SSCRomanceOldRelation.Fiance;
                else if (relation.def == RimWorld.PawnRelationDefOf.Lover && result == SSCRomanceOldRelation.None) result = SSCRomanceOldRelation.Lover;
            }
            return result;
        }

        /// <summary>命令按本次真实结果和互动前关系唯一分类，拒婚分手不再制造第二次事件。</summary>
        private static SSCRomanceCompositeEvent CommandEvent(SSCRomanceMoodFrame frame)
        {
            if (frame.Action == SSCRomanceAction.Romance) return frame.Accepted ? SSCRomanceCompositeEvent.CommandRomanceAccepted : SSCRomanceCompositeEvent.CommandRomanceRejected;
            if (frame.Action == SSCRomanceAction.Proposal) return frame.Accepted ? SSCRomanceCompositeEvent.CommandProposalAccepted :
                frame.ProposalSeparated ? SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated : SSCRomanceCompositeEvent.CommandProposalRejected;
            return frame.ActorState.Data.Relation == SSCRomanceOldRelation.Spouse ? SSCRomanceCompositeEvent.CommandDivorce :
                frame.ActorState.Data.Relation == SSCRomanceOldRelation.Fiance ? SSCRomanceCompositeEvent.CommandEndFiance : SSCRomanceCompositeEvent.CommandEndLover;
        }

        /// <summary>生效禁止或强制接受均记录实际结果；不要求覆盖前后的接受 bool 不同。</summary>
        private static SSCRomanceCompositeEvent PassiveEvent(SSCRomanceMoodFrame frame)
            => frame.Action == SSCRomanceAction.Romance ? (frame.Accepted ? SSCRomanceCompositeEvent.ForcedRomanceAccepted : SSCRomanceCompositeEvent.ForcedRomanceRejected) :
                frame.Accepted ? SSCRomanceCompositeEvent.ForcedProposalAccepted : frame.ProposalSeparated ?
                    SSCRomanceCompositeEvent.ForcedProposalRejectedAndSeparated : SSCRomanceCompositeEvent.ForcedProposalRejected;
    }
}
