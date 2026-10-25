// AI 辅助生成
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只把一张票交给一次 tracker 调用；每次嵌套调用都有自己的帧，不能借用外层豁免。</summary>
    public sealed class SSCRomanceExecutionScope
    {
        [ThreadStatic] private static SSCRomanceExecutionScope ticket;
        [ThreadStatic] private static Invocation current;
        private readonly SSCRomanceCommandRecord record;
        private bool taken;
        /// <summary>票据只能由真实驱动执行入口创建，不提供外部全局强制 bool。</summary>
        private SSCRomanceExecutionScope(SSCRomanceCommandRecord record) { this.record = record; }
        /// <summary>运行时调用帧；仅保存进程内归属，由 Finalizer 成对释放。</summary>
        public sealed class Invocation
        {
            internal Invocation previous;
            internal SSCRomanceCommandRecord record;
            internal Pawn actor, target;
            internal InteractionDef interaction;
        }
        /// <summary>Prefix 为所有调用建立帧；票据精确匹配后消费一次，后续重入没有可用票。</summary>
        public static Invocation Enter(Pawn actor, Pawn target, InteractionDef interaction)
        {
            var frame = new Invocation { previous = current, actor = actor, target = target, interaction = interaction };
            var pending = ticket;
            if (pending != null && !pending.taken && pending.record.Stamp.Actor == actor && pending.record.Stamp.Target == target &&
                SSCRomanceInteractionUtility.TryGetAction(interaction, out var action) && pending.record.Stamp.Action == action)
            { pending.taken = true; frame.record = pending.record; }
            current = frame; return frame;
        }
        /// <summary>异常也释放本次帧；不吞掉游戏或其他模组异常，不把外层票据重新开放。</summary>
        public static void Exit(Invocation frame)
        { if (frame != null && current == frame) current = frame.previous; }
        /// <summary>仅当前原驱动、Job、参与者及实际 Def 一致才能使用本帧授权。</summary>
        private static bool Valid(Invocation frame, Pawn actor, Pawn target, InteractionDef interaction)
        {
            var r = frame?.record;
            return r != null && frame.actor == actor && frame.target == target && frame.interaction == interaction &&
                r.Status == SSCRomanceCommandStatus.Executing && r.runtimeDriver != null &&
                SSCRomanceCommandGameComponent.Current?.Records.Contains(r) == true &&
                actor.jobs?.curDriver == r.runtimeDriver && actor.CurJob == r.runtimeJob &&
                SSCRomanceCommandGameComponent.Matches(r, actor.CurJob) && SSCRomanceCommandGameComponent.IsCurrent(r);
        }
        /// <summary>返回是否存在命令帧；存在但失效时明确拒绝，不能降为普通允许继续。</summary>
        public static bool TryCanEnter(Pawn actor, Pawn target, InteractionDef interaction, out bool allowed)
        {
            allowed = false;
            if (current?.record == null) return false;
            allowed = Valid(current, actor, target, interaction) &&
                SSCRomanceEligibility.Check(actor, target, current.record.Stamp.Action) == SSCRomanceEligibilityReason.None &&
                SSCRomanceCommandUtility.ActorFailure(actor) == null && SSCRomanceCommandUtility.TaskFailure(actor, target, current.record.Stamp.Action) == null &&
                SSCRomancePolicy.Evaluate(actor, target, current.record.Stamp.Action, current.record.authorization).CanInitiate &&
                // 路径/许可查询可被其他补丁回调；完成这些查询后仍须复查原目标人格和必要关系。
                Valid(current, actor, target, interaction) &&
                SSCRomanceEligibility.Check(actor, target, current.record.Stamp.Action) == SSCRomanceEligibilityReason.None;
            return true;
        }
        /// <summary>共同入口原生检查全部通过后占用副作用边界；一次记录绝不能二次进入。</summary>
        public static bool BeforeEffects(Pawn actor, Pawn target, InteractionDef interaction)
        {
            if (!SSCRomanceInteractionUtility.CanEnter(actor, target, interaction)) return false;
            if (current?.record == null) return true;
            if (current.record.effectsStarted) return false;
            current.record.effectsStarted = true; return true;
        }
        /// <summary>只观察当前命令的原 worker，普通互动及外层嵌套帧均不记入结果。</summary>
        public static SSCRomanceCommandRecord WorkerEnter(Pawn actor, Pawn target, SSCRomanceAction action)
        {
            var r = current?.record;
            if (r == null || !r.effectsStarted || r.workerEntered || r.Stamp.Actor != actor || r.Stamp.Target != target || r.Stamp.Action != action) return null;
            r.workerEntered = true; return r;
        }
        /// <summary>原 worker 正常返回才标记完成；Finalizer 的异常路径不能伪装成功。</summary>
        public static void WorkerExit(SSCRomanceCommandRecord record)
        { if (record != null) record.workerCompleted = true; }
        /// <summary>记录已经解析的接受 bool，不修改概率、不抽随机数、不写任何关系。</summary>
        public static void ObserveDecision(Pawn actor, Pawn target, SSCRomanceAction action, bool accepted)
        {
            var r = current?.record;
            if (r == null || !r.workerEntered || r.Stamp.Actor != actor || r.Stamp.Target != target || r.Stamp.Action != action) return;
            r.decisionKnown = true; r.accepted = accepted;
        }
        /// <summary>求婚公式只覆盖命令发起者的意识形态意愿；接收者意愿和普通调用逐值透传。</summary>
        public static bool InitiatorWilling(bool vanilla, Pawn actor, Pawn target)
            => vanilla || (current?.record?.Stamp.Action == SSCRomanceAction.Proposal &&
                Valid(current, actor, target, InteractionDefOf.MarriageProposal));
        /// <summary>真实驱动执行一次；先占用再回调，任何异常/部分结算都留下不可重放终态。</summary>
        public static void Execute(JobDriver_SSCRomanceCommand driver, SSCRomanceCommandRecord r)
        {
            if (r == null || r.Status != SSCRomanceCommandStatus.Claimed || r.runtimeDriver != driver ||
                driver.pawn.jobs?.curDriver != driver || driver.pawn.CurJob != r.runtimeJob || !SSCRomanceCommandGameComponent.IsCurrent(r)) return;
            var previous = ticket;
            var scope = new SSCRomanceExecutionScope(r); ticket = scope; r.status = SSCRomanceCommandStatus.Executing;
            try
            {
                bool occurred = driver.pawn.interactions.TryInteractWith(r.Stamp.Target, r.runtimeJob.interaction);
                if (r.Terminal) return;
                if (!occurred && !r.effectsStarted)
                { r.status = SSCRomanceCommandStatus.Failed; r.outcome = SSCRomanceCommandOutcome.NotOccurred; r.reason = "UnableToInteract"; return; }
                bool relationChanged = RelationChanged(r);
                if (!occurred || !r.workerCompleted || (r.Stamp.Action != SSCRomanceAction.Breakup && !r.decisionKnown) ||
                    (r.Stamp.Action == SSCRomanceAction.Breakup ? !relationChanged : r.accepted != relationChanged))
                { r.status = SSCRomanceCommandStatus.Indeterminate; r.outcome = SSCRomanceCommandOutcome.Unconfirmed; return; }
                r.status = SSCRomanceCommandStatus.Completed;
                r.outcome = relationChanged ? SSCRomanceCommandOutcome.RelationshipChanged : SSCRomanceCommandOutcome.Rejected;
            }
            finally
            {
                ticket = previous;
                if (r.status == SSCRomanceCommandStatus.Executing)
                { r.status = SSCRomanceCommandStatus.Indeterminate; r.outcome = SSCRomanceCommandOutcome.Unconfirmed; }
            }
        }
        /// <summary>只核对期望关系，不据此推导接受判定；拒婚附带分手仍是 Rejected。</summary>
        private static bool RelationChanged(SSCRomanceCommandRecord r)
        {
            var actor = r.Stamp.Actor; var target = r.Stamp.Target;
            if (r.Stamp.Action == SSCRomanceAction.Romance) return actor.relations.DirectRelationExists(PawnRelationDefOf.Lover, target);
            if (r.Stamp.Action == SSCRomanceAction.Proposal) return actor.relations.DirectRelationExists(PawnRelationDefOf.Fiance, target);
            return !actor.relations.DirectRelationExists(PawnRelationDefOf.Lover, target) &&
                !actor.relations.DirectRelationExists(PawnRelationDefOf.Fiance, target) && !actor.relations.DirectRelationExists(PawnRelationDefOf.Spouse, target);
        }
    }
}
