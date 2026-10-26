// AI 辅助生成：十三类真实综合事件；查询、菜单及命令创建不生成心情。
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一次共同互动的原人格快照和完成证据；只在调用栈存在，不保存或重试。</summary>
    public sealed class SSCRomanceMoodFrame
    {
        internal long DiagnosticId;
        internal bool DiagnosticFinished;
        internal Pawn Actor, Target;
        internal SSCRomanceAction Action;
        internal Person ActorState, TargetState;
        internal bool Command, Entered, Completed, Settled, DecisionKnown, OriginalAccepted, Accepted;
        internal bool ProposalRejected, ProposalSeparated;
        internal SSCRomanceSetting? Rule;
        internal SSCRomanceMoodFrame Previous;
        internal SSCRomanceMoodData ActorEvent, TargetEvent;
        internal readonly List<SSCRomanceNativeMood.Site> NativeSites = new List<SSCRomanceNativeMood.Site>();
        internal readonly List<SSCRomanceNativeMood.Entry> NativeEntries = new List<SSCRomanceNativeMood.Entry>();

        internal sealed class Person
        {
            internal Pawn Pawn;
            internal object Token;
            internal CompSexSlaveTraining Comp;
            internal Need_Mood Mood;
            internal MemoryThoughtHandler Handler;
            internal Pawn_RelationsTracker Relations;
            internal bool Applicable, NativeApplied;
            internal int NativeAddsUndone, NativeRemovesUndone, NativeRenewsUndone;
            internal SSCRomanceMoodData Data;
        }
    }

    /// <summary>沿现有 tracker／worker／接受覆盖入口观察实际事件，不修改关系或额外抽概率。</summary>
    public static class SSCRomanceMoodEvents
    {
        [ThreadStatic] private static SSCRomanceMoodFrame current;
        internal static SSCRomanceMoodFrame CurrentWorker => current?.Entered == true &&
            ReferenceEquals(current, SSCRomanceExecutionScope.MoodFrame) ? current : null;

        /// <summary>首个副作用之前冻结双方；任意查询回调后仍由共同入口进行最终准入复查。</summary>
        public static SSCRomanceMoodFrame Capture(Pawn actor, Pawn target, SSCRomanceAction action, bool command)
            => new SSCRomanceMoodFrame { DiagnosticId = SSCRomanceExecutionScope.DiagnosticId != 0 ? SSCRomanceExecutionScope.DiagnosticId : SSCRomanceMoodDiagnostics.NextId(), Actor = actor, Target = target, Action = action, Command = command,
                ActorState = CapturePerson(actor, target), TargetState = CapturePerson(target, actor) };

        /// <summary>先保存载体再读取可回调业务属性；受虐狂使用原版已排除 Suppressed 的 HasTrait。</summary>
        private static SSCRomanceMoodFrame.Person CapturePerson(Pawn pawn, Pawn other)
        {
            var person = new SSCRomanceMoodFrame.Person { Pawn = pawn, Token = SSCPersonalityRuntimeUtility.Capture(pawn),
                Comp = pawn?.TryGetComp<CompSexSlaveTraining>(), Mood = pawn?.needs?.mood,
                Handler = pawn?.needs?.mood?.thoughts?.memories, Relations = pawn?.relations };
            var state = SSCRomanceResolver.Resolve(pawn);
            person.Applicable = state.Applicable;
            int raw = pawn?.relations?.OpinionOf(other) ?? 0;
            person.Data = new SSCRomanceMoodData { Stage = state.Stage, Master = state.Master, RawOpinion = raw,
                HasRawOpinion = true, OpinionBand = SSCRomanceMoodCatalog.ClassifyOpinion(raw),
                Masochist = SSCRomanceMoodDefOf.Masochist != null && pawn?.story?.traits?.HasTrait(SSCRomanceMoodDefOf.Masochist) == true,
                Relation = Relation(pawn, other) };
            return person;
        }

        /// <summary>按配偶、婚约、恋人顺序读取直接关系，不从事后 Ex 关系猜测原关系。</summary>
        private static SSCRomanceOldRelation Relation(Pawn pawn, Pawn other)
            => pawn?.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, other) == true ? SSCRomanceOldRelation.Spouse :
                pawn?.relations?.DirectRelationExists(PawnRelationDefOf.Fiance, other) == true ? SSCRomanceOldRelation.Fiance :
                pawn?.relations?.DirectRelationExists(PawnRelationDefOf.Lover, other) == true ? SSCRomanceOldRelation.Lover : SSCRomanceOldRelation.None;

        /// <summary>一次匹配 worker 占用一帧；直接和嵌套调用也压空帧，不能借外层命令。</summary>
        public static SSCRomanceMoodFrame EnterWorker(SSCRomanceMoodFrame frame, Pawn actor, Pawn target, SSCRomanceAction action)
        {
            if (frame == null || frame.Entered || frame.Actor != actor || frame.Target != target || frame.Action != action)
            { var empty = new SSCRomanceMoodFrame { Previous = current }; current = empty; return empty; }
            frame.Entered = true; frame.Previous = current; current = frame; return frame;
        }

        /// <summary>登记原版一次比较及本次覆盖；开关开启但判定相同不代表受控被动事件。</summary>
        public static void Observe(Pawn actor, Pawn target, SSCRomanceAction action, bool original, bool accepted, SSCRomanceSetting? rule)
        {
            var frame = CurrentWorker;
            if (frame == null || frame.Actor != actor || frame.Target != target || frame.Action != action || frame.DecisionKnown) return;
            frame.DecisionKnown = true; frame.OriginalAccepted = original; frame.Accepted = accepted; frame.Rule = rule;
        }

        /// <summary>包装原生求婚语句包 Add，确认真正进入的拒婚／附带分手分支，不增加随机取样。</summary>
        internal static void ObserveProposalPack(List<RulePackDef> packs, RulePackDef pack)
        {
            packs.Add(pack);
            var frame = CurrentWorker;
            if (frame?.Action != SSCRomanceAction.Proposal) return;
            if (pack == RulePackDefOf.Sentence_MarriageProposalRejected) frame.ProposalRejected = true;
            if (pack == RulePackDefOf.Sentence_MarriageProposalRejectedBrokeUp) frame.ProposalSeparated = true;
        }

        /// <summary>正常完成、原判定及关系证据一致才结算；先占用去重，双方各自准入和替换。</summary>
        public static void Finish(SSCRomanceMoodFrame frame, bool originalRan, Exception exception)
        {
            if (frame == null || current != frame) return;
            string reason = "UnconfirmedResult";
            try
            {
                if (!frame.Entered) { reason = "DirectWorkerWithoutSnapshot"; return; }
                if (!originalRan) { reason = "WorkerSkipped"; return; }
                if (exception != null) { reason = "WorkerException:" + exception.GetType().Name; return; }
                if (frame.Settled) { reason = "Duplicate"; return; }
                if (!CurrentPerson(frame.ActorState) || !CurrentPerson(frame.TargetState)) { reason = "InvalidContext"; return; }
                if (frame.Actor?.relations == null || frame.Target?.relations == null || !SSCRomanceNativeMoodHooks.Ready)
                { reason = "UnsupportedNativeHooks"; return; }
                var after = Relation(frame.Actor, frame.Target);
                if (frame.Action == SSCRomanceAction.Breakup)
                {
                    if (frame.ActorState.Data.Relation == SSCRomanceOldRelation.None || after != SSCRomanceOldRelation.None) return;
                }
                else
                {
                    if (!frame.DecisionKnown || frame.Accepted != (after == (frame.Action == SSCRomanceAction.Romance ?
                        SSCRomanceOldRelation.Lover : SSCRomanceOldRelation.Fiance))) return;
                    if (frame.Action == SSCRomanceAction.Proposal && !frame.Accepted &&
                        (!frame.ProposalRejected || frame.ProposalSeparated != (after == SSCRomanceOldRelation.None) ||
                         (!frame.ProposalSeparated && after != SSCRomanceOldRelation.Lover))) return;
                }
                frame.Completed = frame.Settled = true;
                bool command = frame.Command && frame.ActorState.Applicable && frame.ActorState.Data.Stage == 4;
                bool passive = frame.DecisionKnown && frame.OriginalAccepted != frame.Accepted && frame.Rule.HasValue &&
                    frame.TargetState.Applicable && frame.TargetState.Data.Stage >= 3;
                if (command) frame.ActorEvent = Award(frame, frame.ActorState, frame.Target, CommandEvent(frame), null);
                if (passive) frame.TargetEvent = Award(frame, frame.TargetState, frame.Actor, PassiveEvent(frame), frame.Rule);
                reason = command || passive ? "Settled" : frame.Rule.HasValue && frame.OriginalAccepted == frame.Accepted ? "DecisionUnchanged" : "NaturalUncontrolled";
            }
            finally
            {
                // 日志只消费已捕获事实；先释放业务作用域，再输出不会重放的退出原因。
                current = frame.Previous;
                SSCRomanceMoodDiagnostics.Finish(frame, reason);
            }
        }

        /// <summary>命令按真实结果及互动前关系唯一分类，失败附带分手不再追加独立损失。</summary>
        private static SSCRomanceCompositeEvent CommandEvent(SSCRomanceMoodFrame frame)
        {
            if (frame.Action == SSCRomanceAction.Romance) return frame.Accepted ? SSCRomanceCompositeEvent.CommandRomanceAccepted : SSCRomanceCompositeEvent.CommandRomanceRejected;
            if (frame.Action == SSCRomanceAction.Proposal) return frame.Accepted ? SSCRomanceCompositeEvent.CommandProposalAccepted :
                frame.ProposalSeparated ? SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated : SSCRomanceCompositeEvent.CommandProposalRejected;
            return frame.ActorState.Data.Relation == SSCRomanceOldRelation.Spouse ? SSCRomanceCompositeEvent.CommandDivorce :
                frame.ActorState.Data.Relation == SSCRomanceOldRelation.Fiance ? SSCRomanceCompositeEvent.CommandEndFiance : SSCRomanceCompositeEvent.CommandEndLover;
        }
        /// <summary>被动只分类实际改变的接受结果；拒婚结果来自原生附带分手分支观察。</summary>
        private static SSCRomanceCompositeEvent PassiveEvent(SSCRomanceMoodFrame frame)
            => frame.Action == SSCRomanceAction.Romance ? (frame.Accepted ? SSCRomanceCompositeEvent.ForcedRomanceAccepted : SSCRomanceCompositeEvent.ForcedRomanceRejected) :
                frame.Accepted ? SSCRomanceCompositeEvent.ForcedProposalAccepted : frame.ProposalSeparated ?
                    SSCRomanceCompositeEvent.ForcedProposalRejectedAndSeparated : SSCRomanceCompositeEvent.ForcedProposalRejected;

        /// <summary>继续复查互动前的实例，不捕获新 token；关系载体替换也不能借用原事件。</summary>
        internal static bool CurrentPerson(SSCRomanceMoodFrame.Person person)
            => person?.Pawn != null && !person.Pawn.Dead && !person.Pawn.Destroyed && !person.Pawn.Discarded &&
                !SSCPersonalityRuntimeUtility.IsMutating(person.Pawn) && SSCPersonalityRuntimeUtility.IsCurrent(person.Pawn, person.Token) &&
                person.Pawn.TryGetComp<CompSexSlaveTraining>() == person.Comp && person.Pawn.needs?.mood == person.Mood &&
                person.Pawn.needs?.mood?.thoughts?.memories == person.Handler && person.Pawn.relations == person.Relations;

        /// <summary>目录只决定分支，原生准入及第二批提交负责获得；原版撤销纳入同一请求提交边界。</summary>
        private static SSCRomanceMoodData Award(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person person, Pawn other,
            SSCRomanceCompositeEvent kind, SSCRomanceSetting? rule)
        {
            SSCRomanceMoodSelection selection = null;
            SSCRomanceMoodResult result = null;
            string reason = "InvalidContext";
            try
            {
                if (!CurrentPerson(person) || person.Handler == null) return null;
                if (!SSCRomanceMoodCatalog.TryResolve(kind, person.Data.Stage, person.Data.Masochist, person.Data.RawOpinion, out selection))
                { reason = "UnknownBranch"; return null; }
                var context = person.Data.Copy(); context.Rule = rule ?? default; context.HasRule = rule.HasValue;
                var request = new SSCRomanceMoodRequest(person, other, selection, context,
                    () => SSCRomanceNativeMood.TryUndo(frame, person, false), () => SSCRomanceNativeMood.TryUndo(frame, person, true));
                result = request.Execute(); reason = request.DiagnosticReason ?? result.Kind.ToString();
                return result.Completed ? selection.CreateSnapshot(context) : null;
            }
            catch (Exception exception)
            {
                // 技术失败不从 Finalizer 制造新异常；日志保留失败类型，不重试或重放原调用。
                reason = "TechnicalException:" + exception.GetType().Name;
                return null;
            }
            finally { SSCRomanceMoodDiagnostics.Settlement(frame, person, other, selection, result, reason, rule); }
        }
    }
}
