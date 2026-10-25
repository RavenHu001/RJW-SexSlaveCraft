// AI 辅助生成
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一个共同互动的快照与 worker 观察；只活在调用栈上，不存档、不重试。</summary>
    public sealed class SSCRomanceMoodFrame
    {
        internal Pawn Actor, Target;
        internal SSCRomanceAction Action;
        internal Person ActorState, TargetState;
        internal bool Command, Entered, Completed, Settled, DecisionKnown, OriginalAccepted, Accepted, NativeLoss;
        internal SSCRomanceSetting? Rule;
        internal SSCRomanceMoodFrame Previous;
        internal SSCRomanceMoodData ActorEvent, TargetEvent;

        internal sealed class Person
        {
            internal Pawn Pawn;
            internal object Token;
            internal CompSexSlaveTraining Comp;
            internal Need_Mood Mood;
            internal MemoryThoughtHandler Handler;
            internal bool Applicable;
            internal SSCRomanceMoodData Data;
        }
    }

    /// <summary>真实互动驱动心情；查询和 UI 不调用此类，消息标志与心情结算完全分开。</summary>
    public static class SSCRomanceMoodEvents
    {
        [ThreadStatic] private static SSCRomanceMoodFrame current;

        /// <summary>在共同入口首个副作用之前冻结双方状态；原版概率比较稍后单独观察。</summary>
        public static SSCRomanceMoodFrame Capture(Pawn actor, Pawn target, SSCRomanceAction action, bool command)
            => new SSCRomanceMoodFrame { Actor = actor, Target = target, Action = action, Command = command,
                ActorState = CapturePerson(actor, target), TargetState = CapturePerson(target, actor) };

        /// <summary>持有原需求和处理器实例，防止同一身体更换人格后领取前一人格的事件。</summary>
        private static SSCRomanceMoodFrame.Person CapturePerson(Pawn pawn, Pawn other)
        {
            var state = SSCRomanceResolver.Resolve(pawn);
            return new SSCRomanceMoodFrame.Person { Pawn = pawn, Token = SSCPersonalityRuntimeUtility.Capture(pawn), Comp = state.Comp,
                Mood = pawn?.needs?.mood, Handler = pawn?.needs?.mood?.thoughts?.memories, Applicable = state.Applicable,
                Data = new SSCRomanceMoodData { Stage = state.Stage, Master = state.Master,
                    Masochist = SSCRomanceMoodDefOf.Masochist != null && pawn?.story?.traits?.HasTrait(SSCRomanceMoodDefOf.Masochist) == true,
                    Corruption = pawn?.needs?.TryGetNeed<Need_Corruption>()?.CurLevel,
                    Opinion = SSCRomanceMoodRules.OpinionCategory(pawn?.relations?.OpinionOf(other) ?? 0), Relation = Relation(pawn, other) } };
        }

        /// <summary>明确按配偶、婚约、恋人顺序冻结旧关系，不从事后 Ex 关系反推。</summary>
        private static SSCRomanceOldRelation Relation(Pawn pawn, Pawn other)
            => pawn?.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, other) == true ? SSCRomanceOldRelation.Spouse :
                pawn?.relations?.DirectRelationExists(PawnRelationDefOf.Fiance, other) == true ? SSCRomanceOldRelation.Fiance :
                pawn?.relations?.DirectRelationExists(PawnRelationDefOf.Lover, other) == true ? SSCRomanceOldRelation.Lover : SSCRomanceOldRelation.None;

        /// <summary>只有当前 tracker 帧能领一次原 worker；直接调用和嵌套调用不能借外层命令。</summary>
        public static SSCRomanceMoodFrame EnterWorker(SSCRomanceMoodFrame frame, Pawn actor, Pawn target, SSCRomanceAction action)
        {
            // 无匹配 tracker 的直接 worker 也压一个空帧，屏蔽外层观察，避免嵌套借用原始判定。
            if (frame == null || frame.Entered || frame.Actor != actor || frame.Target != target || frame.Action != action)
            { var empty = new SSCRomanceMoodFrame { Previous = current }; current = empty; return empty; }
            frame.Entered = true; frame.Previous = current; current = frame; return frame;
        }

        /// <summary>仅登记真实 worker 的这一次比较；不重抽随机、不以规则开启代替结果改变。</summary>
        public static void Observe(Pawn actor, Pawn target, SSCRomanceAction action, bool original, bool accepted, SSCRomanceSetting? rule)
        {
            var f = current;
            if (f == null || !ReferenceEquals(f, SSCRomanceExecutionScope.MoodFrame) || f.Actor != actor || f.Target != target || f.Action != action || f.DecisionKnown) return;
            f.DecisionKnown = true; f.OriginalAccepted = original; f.Accepted = accepted; f.Rule = rule;
        }

        /// <summary>原 worker 及同链补丁都未抛异常才结算；后续 Job 清理不撤销已确认经历。</summary>
        public static void Finish(SSCRomanceMoodFrame frame, bool originalRan, Exception exception)
        {
            if (frame == null || current != frame) return;
            try
            {
                if (!frame.Entered || !originalRan || exception != null || frame.Settled) return;
                // 外部 worker 补丁可能替换人格或关系载体；必须先判无效，不能在 Finalizer 中新增异常。
                if (!CurrentPerson(frame.ActorState) || !CurrentPerson(frame.TargetState) ||
                    frame.Actor?.relations == null || frame.Target?.relations == null) return;
                frame.Completed = true;
                bool changed = frame.Action == SSCRomanceAction.Breakup ? Relation(frame.Actor, frame.Target) == SSCRomanceOldRelation.None &&
                    frame.ActorState.Data.Relation != SSCRomanceOldRelation.None : frame.Actor.relations.DirectRelationExists(
                        frame.Action == SSCRomanceAction.Romance ? PawnRelationDefOf.Lover : PawnRelationDefOf.Fiance, frame.Target);
                if (frame.Action == SSCRomanceAction.Breakup ? !changed : !frame.DecisionKnown || frame.Accepted != changed) return;
                // 一次占用在任何记忆回调之前；外部补丁重入也不能为本事件再次发放。
                frame.Settled = true;
                if (!CurrentPerson(frame.ActorState) || !CurrentPerson(frame.TargetState)) return;
                if (frame.Command && frame.ActorState.Applicable && frame.ActorState.Data.Stage >= 4)
                {
                    var kind = frame.Action == SSCRomanceAction.Romance ? SSCRomanceMoodEvent.CommandRomance :
                        frame.Action == SSCRomanceAction.Proposal ? SSCRomanceMoodEvent.CommandProposal : SSCRomanceMoodEvent.CommandBreakup;
                    // 发起者经历来自合法命令；接收者命中的保证不能记成发起者自己的控制规则。
                    frame.ActorEvent = Award(frame.ActorState, frame.Target, kind, null);
                    if (frame.Action == SSCRomanceAction.Breakup && !frame.NativeLoss)
                        Award(frame.ActorState, frame.Target, SSCRomanceMoodEvent.RelationshipLoss, null);
                }
                if (frame.DecisionKnown && frame.OriginalAccepted != frame.Accepted && frame.Rule.HasValue &&
                    frame.TargetState.Applicable && frame.TargetState.Data.Stage >= 3)
                    frame.TargetEvent = Award(frame.TargetState, frame.Actor, frame.Accepted ? SSCRomanceMoodEvent.ForcedAcceptance : SSCRomanceMoodEvent.ForcedRefusal, frame.Rule);
            }
            finally { current = frame.Previous; }
        }

        /// <summary>复查原人格、原组件和原需求，不要求历史事件在结算后仍维持原绑定或开关。</summary>
        private static bool CurrentPerson(SSCRomanceMoodFrame.Person person)
            => person.Pawn != null && !person.Pawn.Destroyed && !person.Pawn.Discarded && !SSCPersonalityRuntimeUtility.IsMutating(person.Pawn) &&
                SSCPersonalityRuntimeUtility.IsCurrent(person.Pawn, person.Token) && person.Pawn.TryGetComp<CompSexSlaveTraining>() == person.Comp &&
                person.Pawn.needs?.mood == person.Mood && person.Pawn.needs?.mood?.thoughts?.memories == person.Handler;

        /// <summary>原生准入负责获得资格，记忆自身负责组内替换；零值只保留事件背景，不刷新历史。</summary>
        private static SSCRomanceMoodData Award(SSCRomanceMoodFrame.Person person, Pawn other, SSCRomanceMoodEvent kind, SSCRomanceSetting? rule)
        {
            var data = person.Data.Copy(); data.Kind = kind; data.Rule = rule ?? default; data.HasRule = rule.HasValue;
            bool loss = kind == SSCRomanceMoodEvent.RelationshipLoss;
            data.Value = loss ? SSCRomanceMoodRules.Loss(data.Relation, data.Opinion) :
                SSCRomanceMoodRules.Experience(kind, data.Stage, data.Masochist, data.Corruption, data.Opinion);
            var def = loss ? SSCRomanceMoodDefOf.SSC_Romance_Loss : SSCRomanceMoodDefOf.SSC_Romance_Experience;
            if (!CurrentPerson(person)) return null;
            if (data.Value == 0 || def == null || person.Handler == null) return data;
            var memory = (Thought_MemorySSCRomance)ThoughtMaker.MakeThought(def, data.Value + (loss ? 5 : 6));
            // ThoughtMaker.Init 也允许外部回调；重新核对但不重新捕获原人格 token。
            if (!CurrentPerson(person)) return null;
            memory.Data = data.Copy(); memory.durationTicksOverride = SSCRomanceMoodRules.Duration(kind, data.Relation);
            memory.CanApply = () => CurrentPerson(person);
            try { person.Handler.TryGainMemory(memory, other); }
            finally { memory.CanApply = null; }
            return CurrentPerson(person) ? data : null;
        }

        /// <summary>仅在原版准入后实际调用合并时登记；已有零龄记忆本身不能证明本次获得。</summary>
        public static void ObserveAdmittedLoss(Thought_Memory memory)
        {
            var f = current;
            if (f == null || !ReferenceEquals(f, SSCRomanceExecutionScope.MoodFrame) || f.Action != SSCRomanceAction.Breakup || !f.Command || memory?.pawn != f.Actor || memory.otherPawn != f.Target ||
                memory.pawn.needs?.mood?.thoughts?.memories != f.ActorState.Handler) return;
            string name = memory.def?.defName;
            if (name == "BrokeUpWithMeMood" || name == "DivorcedMeMood") f.NativeLoss = true;
        }
    }
}
