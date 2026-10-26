// AI 辅助生成：十三类真实综合事件；查询、菜单及命令创建不生成心情。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一次共同互动的原人格快照和完成证据；只在调用栈存在，不保存或重试。</summary>
    public sealed class SSCRomanceMoodFrame
    {
        internal long DiagnosticId;
        // 真正发生的先后顺序可进入历史；诊断编号仍仅用于本次调用关联。
        internal long OccurrenceOrder;
        internal bool DiagnosticFinished;
        internal Pawn Actor, Target;
        internal SSCRomanceAction Action;
        internal Person ActorState, TargetState;
        internal bool Command, Entered, Completed, Settled, DecisionKnown, OriginalAccepted, Accepted;
        internal bool ProposalRejected, ProposalSeparated;
        internal SSCRomanceSetting? Rule;
        internal SSCRomanceMoodFrame Previous;
        // 已发生事实与心情回执分别保存；日志不依赖 Award 返回值。
        internal bool FactsConfirmed;
        internal SSCRomanceInteractionFact ActorFact, TargetFact;
        internal SSCRomanceOldRelation ConfirmedRelation;
        internal readonly List<FormerPair> FormerPairs = new List<FormerPair>();
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

        /// <summary>求爱前已存在的旧关系；只保存原载体见证，不授予旧伴侣受控事件。</summary>
        internal sealed class FormerPair
        {
            internal Person Owner, Other;
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
        {
            var frame = new SSCRomanceMoodFrame { DiagnosticId = SSCRomanceExecutionScope.DiagnosticId != 0 ? SSCRomanceExecutionScope.DiagnosticId : SSCRomanceMoodDiagnostics.NextId(), Actor = actor, Target = target, Action = action, Command = command,
                ActorState = CapturePerson(actor, target), TargetState = CapturePerson(target, actor) };
            if (action == SSCRomanceAction.Romance)
            {
                // 原版按关系数量与意愿选择实际结束者；此处仅冻结候选，不能先认定所有旧伴侣都会分手。
                CaptureFormerPairs(frame, frame.ActorState, target);
                CaptureFormerPairs(frame, frame.TargetState, actor);
            }
            return frame;
        }

        /// <summary>捕获人格、心情及关系实例；旧伴侣清理无需重新查询阶段、特质或好感。</summary>
        private static SSCRomanceMoodFrame.Person CaptureCarrier(Pawn pawn)
            => new SSCRomanceMoodFrame.Person { Pawn = pawn, Token = SSCPersonalityRuntimeUtility.Capture(pawn),
                Comp = pawn?.TryGetComp<CompSexSlaveTraining>(), Mood = pawn?.needs?.mood,
                Handler = pawn?.needs?.mood?.thoughts?.memories, Relations = pawn?.relations };

        /// <summary>只采集 Lover／Fiance 候选并排除主互动对象，结束后还要检查最终真实关系。</summary>
        private static void CaptureFormerPairs(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person owner, Pawn mainOther)
        {
            if (owner.Relations == null) return;
            var others = owner.Relations.DirectRelations.Where(r => r.def == PawnRelationDefOf.Lover || r.def == PawnRelationDefOf.Fiance)
                .Select(r => r.otherPawn).Where(p => p != null && p != mainOther).Distinct().ToArray();
            foreach (var other in others)
                frame.FormerPairs.Add(new SSCRomanceMoodFrame.FormerPair { Owner = owner, Other = CaptureCarrier(other) });
        }

        /// <summary>先保存载体再读取可回调业务属性；受虐狂使用原版已排除 Suppressed 的 HasTrait。</summary>
        private static SSCRomanceMoodFrame.Person CapturePerson(Pawn pawn, Pawn other)
        {
            var person = CaptureCarrier(pawn);
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
        internal static SSCRomanceOldRelation Relation(Pawn pawn, Pawn other)
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

        /// <summary>登记原版一次比较及本次生效规则；结果相同仍属于该规则接管的回应。</summary>
        public static void Observe(Pawn actor, Pawn target, SSCRomanceAction action, bool original, bool accepted, SSCRomanceSetting? rule)
        {
            var frame = CurrentWorker;
            if (frame == null || frame.Actor != actor || frame.Target != target || frame.Action != action || frame.DecisionKnown) return;
            frame.DecisionKnown = true; frame.OriginalAccepted = original; frame.Accepted = accepted; frame.Rule = rule;
        }

        /// <summary>按本次命中的被动规则识别受控者，不要求原版抽签结果被改变；普通许可透传不算接管。</summary>
        internal static bool HasControlledReception(SSCRomanceMoodFrame frame)
            => frame?.DecisionKnown == true && frame.Rule.HasValue && frame.TargetState?.Applicable == true &&
                frame.TargetState.Data.Stage >= 3;

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
                if (!SSCRomanceInteractionFacts.TryConfirm(frame, originalRan, exception, out reason)) return;
                // 原版心情接管不支持或原心情载体被替换时，保留已确认日志事实，停止本次心情写入。
                if (!CurrentPerson(frame.ActorState) || !CurrentPerson(frame.TargetState)) { reason = "InvalidMoodContext"; return; }
                if (!SSCRomanceNativeMoodHooks.Ready) { reason = "UnsupportedNativeHooks"; return; }
                frame.Settled = true;
                // 顺序在真实结果确认时分配，早于准入回调；内嵌的新互动因此总有更晚的发生顺序。
                frame.OccurrenceOrder = SSCRomanceMoodOrder.Next();
                bool command = frame.ActorFact != null;
                bool passive = frame.TargetFact != null;
                var phase = Phase(frame);
                // 受控方把阶段清理与新记忆一并提交；准入失败不能再降级为自然清理而删除原历史。
                if (command) Award(frame, frame.ActorState, frame.Target, frame.ActorFact.Kind, null, phase);
                else CleanupNatural(frame, frame.ActorState, frame.TargetState, phase, false);
                if (passive) Award(frame, frame.TargetState, frame.Actor, frame.TargetFact.Kind, frame.Rule, phase);
                else CleanupNatural(frame, frame.TargetState, frame.ActorState, phase, false);
                if (frame.Action == SSCRomanceAction.Romance && frame.Accepted) CleanupFormerPairs(frame);
                reason = command || passive ? "Settled" : "NaturalUncontrolled";
            }
            finally
            {
                // 日志只消费已捕获事实；先释放业务作用域，再输出不会重放的退出原因。
                current = frame.Previous;
                SSCRomanceMoodDiagnostics.Finish(frame, reason);
            }
        }

        /// <summary>只按完整结果选择阶段；拒婚附带分手不会同时执行拒婚保留关系的清理。</summary>
        private static SSCRomanceMoodPhase Phase(SSCRomanceMoodFrame frame)
            => frame.Action == SSCRomanceAction.Breakup || frame.ProposalSeparated ? SSCRomanceMoodPhase.Separated :
                frame.Action == SSCRomanceAction.Romance ? (frame.Accepted ? SSCRomanceMoodPhase.RomanceEstablished : SSCRomanceMoodPhase.None) :
                    frame.Accepted ? SSCRomanceMoodPhase.Engaged : SSCRomanceMoodPhase.ProposalRejected;

        /// <summary>准入回调后仍核对原双方与最终关系，不让旧事件按后来已改变的关系清理历史。</summary>
        private static bool ConfirmedPairCurrent(SSCRomanceMoodFrame frame)
            => CurrentPerson(frame.ActorState) && CurrentPerson(frame.TargetState) &&
                Relation(frame.Actor, frame.Target) == frame.ConfirmedRelation && Relation(frame.Target, frame.Actor) == frame.ConfirmedRelation;

        /// <summary>原版逆向计划可能读取可回调属性；计划完成后再次核对双方，执行闭包本身不再查询关系。</summary>
        private static Action PrepareNativeUndo(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person person)
        {
            if (!ConfirmedPairCurrent(frame)) return null;
            var prepared = SSCRomanceNativeMood.PrepareUndo(frame, person);
            return ConfirmedPairCurrent(frame) ? prepared : null;
        }

        /// <summary>没有本人受控事件的自然侧只结束旧 SSC 阶段，保留全部原版反馈，不生成新记忆。</summary>
        private static void CleanupNatural(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person person,
            SSCRomanceMoodFrame.Person other, SSCRomanceMoodPhase phase, bool former)
        {
            if (phase == SSCRomanceMoodPhase.None || person?.Handler == null) return;
            bool Current() => CurrentPerson(person) && (former ? CurrentCarrier(other, false) : CurrentPerson(other)) &&
                (former ? Relation(person.Pawn, other.Pawn) == SSCRomanceOldRelation.None &&
                    Relation(other.Pawn, person.Pawn) == SSCRomanceOldRelation.None : ConfirmedPairCurrent(frame));
            SSCRomanceMoodResult result = null;
            try
            {
                if (!Current()) return;
                result = SSCRomanceMoodGroups.Cleanup(person.Handler, other.Pawn, phase, Current, frame.OccurrenceOrder);
            }
            catch (Exception exception)
            {
                // 自然清理不是替换原版的前置条件；局部故障只记录，不重放互动或重试清理。
                SSCRomanceMoodDiagnostics.Cleanup(frame, person, other.Pawn, phase, null, former, "CleanupException:" + exception.GetType().Name);
                return;
            }
            SSCRomanceMoodDiagnostics.Cleanup(frame, person, other.Pawn, phase, result, former, "NaturalPhase");
        }

        /// <summary>只处理本次实际结束的旧关系；幸存伴侣及不相关对象不清理，不追加命令分手事件。</summary>
        private static void CleanupFormerPairs(SSCRomanceMoodFrame frame)
        {
            foreach (var pair in frame.FormerPairs)
            {
                if (!CurrentPerson(pair.Owner) || !CurrentCarrier(pair.Other, false) ||
                    Relation(pair.Owner.Pawn, pair.Other.Pawn) != SSCRomanceOldRelation.None ||
                    Relation(pair.Other.Pawn, pair.Owner.Pawn) != SSCRomanceOldRelation.None) continue;
                CleanupNatural(frame, pair.Owner, pair.Other, SSCRomanceMoodPhase.Separated, true);
                CleanupNatural(frame, pair.Other, pair.Owner, SSCRomanceMoodPhase.Separated, true);
            }
        }

        /// <summary>继续复查互动前的实例，不捕获新 token；关系载体替换也不能借用原事件。</summary>
        internal static bool CurrentPerson(SSCRomanceMoodFrame.Person person)
            => CurrentCarrier(person, true);

        /// <summary>原版可实际移除已死亡旧伴侣的关系；对象只需保留原载体，记忆持有者仍须存活。</summary>
        private static bool CurrentCarrier(SSCRomanceMoodFrame.Person person, bool requireAlive)
            => CurrentInteractionPerson(person, requireAlive) && person.Pawn.needs?.mood == person.Mood &&
                person.Pawn.needs?.mood?.thoughts?.memories == person.Handler;

        /// <summary>互动事实只要求原人格、组件及关系载体；没有心情或心情载体变化不否认已经发生的行为。</summary>
        internal static bool CurrentInteractionPerson(SSCRomanceMoodFrame.Person person, bool requireAlive = true)
            => person?.Pawn != null && (!requireAlive || !person.Pawn.Dead) && !person.Pawn.Destroyed && !person.Pawn.Discarded &&
                !SSCPersonalityRuntimeUtility.IsMutating(person.Pawn) && SSCPersonalityRuntimeUtility.IsCurrent(person.Pawn, person.Token) &&
                person.Pawn.TryGetComp<CompSexSlaveTraining>() == person.Comp && person.Relations != null && person.Pawn.relations == person.Relations;

        /// <summary>目录只决定分支，原生准入及第二批提交负责获得；原版撤销纳入同一请求提交边界。</summary>
        private static void Award(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person person, Pawn other,
            SSCRomanceCompositeEvent kind, SSCRomanceSetting? rule, SSCRomanceMoodPhase phase)
        {
            SSCRomanceMoodSelection selection = null;
            SSCRomanceMoodResult result = null;
            string reason = "InvalidContext";
            try
            {
                if (!CurrentPerson(person) || person.Handler == null) return;
                if (!SSCRomanceMoodCatalog.TryResolve(kind, person.Data.Stage, person.Data.Masochist, person.Data.RawOpinion, out selection))
                { reason = "UnknownBranch"; return; }
                var context = person.Data.Copy(); context.Rule = rule ?? default; context.HasRule = rule.HasValue;
                context.OccurrenceOrder = frame.OccurrenceOrder;
                var request = new SSCRomanceMoodRequest(person, other, selection, context,
                    () => ConfirmedPairCurrent(frame) && SSCRomanceNativeMood.TryUndo(frame, person, false),
                    () => ConfirmedPairCurrent(frame) && SSCRomanceNativeMood.TryUndo(frame, person, true), phase, frame.OccurrenceOrder,
                    () => PrepareNativeUndo(frame, person));
                result = request.Execute(); reason = request.DiagnosticReason ?? result.Kind.ToString();
            }
            catch (Exception exception)
            {
                // 技术失败不从 Finalizer 制造新异常；日志保留失败类型，不重试或重放原调用。
                reason = "TechnicalException:" + exception.GetType().Name;
                return;
            }
            finally { SSCRomanceMoodDiagnostics.Settlement(frame, person, other, selection, result, reason, rule); }
        }
    }
}
