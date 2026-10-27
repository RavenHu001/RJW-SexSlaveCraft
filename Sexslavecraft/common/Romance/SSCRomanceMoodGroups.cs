// AI 辅助生成：对象语义组、局部提交与历史整理；期限不再决定互斥范围。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>完整互动确认的阶段，不能从中途一次关系增删推断。</summary>
    public enum SSCRomanceMoodPhase { None, RomanceEstablished, ProposalRejected, Engaged, Separated }
    /// <summary>实际提交结果；Kept 只供历史恢复，不再代表较强旧值阻挡。</summary>
    public enum SSCRomanceMoodResultKind { Added, Replaced, Refreshed, Kept, Zero, Cleaned, NotAdmitted, InvalidContext, InvalidData, Conflict, Duplicate, Failed }

    /// <summary>一次真正删除的冻结事实，供实际回执和日志使用。</summary>
    public sealed class SSCRomanceMoodRemoval
    {
        public string BranchId { get; }
        public SSCRomanceSemanticGroup SemanticGroup { get; }
        public Pawn Other { get; }
        public string Reason { get; }
        public int Value { get; }
        public int RemainingTicks { get; }
        /// <summary>提交前冻结资料，最终删除段不查询虚拟期限属性。</summary>
        internal SSCRomanceMoodRemoval(Thought_MemorySSCRomance m, string reason)
        {
            BranchId = m.Data?.BranchId; SemanticGroup = SSCRomanceMoodGroups.SemanticOf(m);
            Other = m.otherPawn; Reason = reason; Value = m.Data?.Value ?? 0;
            RemainingTicks = m.permanent ? -1 : m.DurationTicks - m.age;
        }
        /// <summary>只格式化已有事实，不查询人物标签或关系。</summary>
        public override string ToString() => $"{BranchId ?? "unknown"}:{SemanticGroup}:{Other?.thingIDNumber}:{Value}:{RemainingTicks}:{Reason}";
    }

    /// <summary>不可变回执；计划中的清理只在最终提交成功后报告。</summary>
    public sealed class SSCRomanceMoodResult
    {
        public SSCRomanceMoodResultKind Kind { get; }
        public SSCRomanceMoodGroup Group { get; }
        public SSCRomanceSemanticGroup SemanticGroup { get; }
        public SSCRomanceMoodPhase Phase { get; }
        public long OccurrenceOrder { get; }
        public IReadOnlyList<SSCRomanceMoodRemoval> Removed { get; }
        public int? OldValue { get; }
        public int NewValue { get; }
        public int RemainingTicks { get; }
        public string OldBranch { get; }
        public int OldRemainingTicks { get; }
        public bool Applied => Kind == SSCRomanceMoodResultKind.Added || Kind == SSCRomanceMoodResultKind.Replaced || Kind == SSCRomanceMoodResultKind.Refreshed;
        public bool Completed => Applied || Kind == SSCRomanceMoodResultKind.Kept || Kind == SSCRomanceMoodResultKind.Zero || Kind == SSCRomanceMoodResultKind.Cleaned;
        /// <summary>失败回执不携带尚未执行的删除集合，不暴露可变记忆实例。</summary>
        internal SSCRomanceMoodResult(SSCRomanceMoodResultKind kind, SSCRomanceMoodGroup group = SSCRomanceMoodGroup.None,
            int? oldValue = null, int newValue = 0, int remaining = 0, string oldBranch = null, int oldRemaining = 0,
            SSCRomanceSemanticGroup semantic = SSCRomanceSemanticGroup.None, SSCRomanceMoodPhase phase = SSCRomanceMoodPhase.None,
            long order = 0, IEnumerable<SSCRomanceMoodRemoval> removed = null)
        {
            Kind = kind; Group = group; OldValue = oldValue; NewValue = newValue; RemainingTicks = remaining;
            OldBranch = oldBranch; OldRemainingTicks = oldRemaining; SemanticGroup = semantic; Phase = phase; OccurrenceOrder = order;
            Removed = Array.AsReadOnly((removed ?? Enumerable.Empty<SSCRomanceMoodRemoval>()).ToArray());
        }
    }

    /// <summary>一次请求的局部写集；候选先准入，旧历史留到后置回调结束。</summary>
    internal sealed class SSCRomanceMoodChangePlan
    {
        internal MemoryThoughtHandler Handler;
        internal Pawn Other;
        internal SSCRomanceSemanticGroup Semantic;
        internal SSCRomanceMoodPhase Phase;
        internal long Order;
        internal Thought_MemorySSCRomance Candidate;
        internal SSCRomanceMoodMemoryState[] Before;
        internal SSCRomanceMoodResult Result;
    }

    /// <summary>同对象同语义采用最新事件；强度、正负和期限不再决定互斥范围。</summary>
    public static class SSCRomanceMoodGroups
    {
        // 只保护正在结算的调用栈；零值也留水位，防较早外层覆盖较新的空结果。不保存或复制。
        private sealed class RuntimeOrders { internal readonly Dictionary<(Pawn, SSCRomanceSemanticGroup), long> Values = new Dictionary<(Pawn, SSCRomanceSemanticGroup), long>(); }
        private static readonly ConditionalWeakTable<MemoryThoughtHandler, RuntimeOrders> runtime = new ConditionalWeakTable<MemoryThoughtHandler, RuntimeOrders>();

        /// <summary>仅识别短／长期载体，不能拿期限当作语义键。</summary>
        public static SSCRomanceMoodGroup GroupOf(Thought thought)
        {
            if (!(thought is Thought_MemorySSCCompositeRomance)) return SSCRomanceMoodGroup.None;
            return thought.def?.defName == Thought_MemorySSCCompositeRomance.ShortDefName ? SSCRomanceMoodGroup.Short :
                thought.def?.defName == Thought_MemorySSCCompositeRomance.LongDefName ? SSCRomanceMoodGroup.Long : SSCRomanceMoodGroup.None;
        }
        /// <summary>从保存事件唯一映射语义，不读取当前人物或显示文案。</summary>
        public static SSCRomanceSemanticGroup SemanticOf(Thought thought) => thought is Thought_MemorySSCRomance m &&
            GroupOf(m) != SSCRomanceMoodGroup.None && m.Data?.IsComposite == true ? SSCRomanceMoodSemantics.Of(m.Data.CompositeEvent) : SSCRomanceSemanticGroup.None;
        /// <summary>列表隐含持有者；期限、正负和文案均不拆名额。</summary>
        public static bool SameKey(Thought first, Thought second) => first is Thought_MemorySSCRomance a && second is Thought_MemorySSCRomance b &&
            a.otherPawn != null && SemanticOf(a) != SSCRomanceSemanticGroup.None && SemanticOf(a) == SemanticOf(b) && a.otherPawn == b.otherPawn;
        /// <summary>文案失效不抹去合法冻结值；未知格式不伪装成正常零值。</summary>
        public static bool TryValue(Thought_MemorySSCRomance m, out int value)
        {
            value = 0; var group = GroupOf(m);
            if (group == SSCRomanceMoodGroup.None || m.Data?.IsComposite != true) return false;
            value = m.Data.Value; return value >= -15 && value <= 15 && (group != SSCRomanceMoodGroup.Long || value < 0);
        }
        /// <summary>缺失顺序的损坏历史保留证据，不补造未发布旧版迁移。</summary>
        private static bool Known(Thought_MemorySSCRomance m) => TryValue(m, out int value) && value != 0 && SemanticOf(m) != SSCRomanceSemanticGroup.None && m.Data.OccurrenceOrder > 0;
        /// <summary>自然拒婚只清前一阶段两类求爱，不删没有新结果的求婚组。</summary>
        private static bool PhaseContains(SSCRomanceMoodPhase phase, SSCRomanceSemanticGroup group) =>
            phase == SSCRomanceMoodPhase.RomanceEstablished || phase == SSCRomanceMoodPhase.Engaged || phase == SSCRomanceMoodPhase.Separated ||
            phase == SSCRomanceMoodPhase.ProposalRejected && (group == SSCRomanceSemanticGroup.PassiveRomance || group == SSCRomanceSemanticGroup.CommandRomance);
        /// <summary>写集仅限指定对象；无法解释的身份参与保护，不静默删除。</summary>
        private static bool Affected(Thought_MemorySSCRomance m, Pawn other, SSCRomanceSemanticGroup semantic, SSCRomanceMoodPhase phase)
        {
            if (GroupOf(m) == SSCRomanceMoodGroup.None || m.otherPawn != other) return false;
            var group = SemanticOf(m);
            return group == SSCRomanceSemanticGroup.None || semantic != SSCRomanceSemanticGroup.None && group == semantic || PhaseContains(phase, group);
        }
        /// <summary>相同或更晚事件已经提交时，旧帧不能继续覆盖。</summary>
        private static bool OrderCurrent(SSCRomanceMoodChangePlan plan)
        {
            if (plan.Order <= 0) return false;
            var orders = runtime.GetOrCreateValue(plan.Handler).Values;
            foreach (SSCRomanceSemanticGroup group in Enum.GetValues(typeof(SSCRomanceSemanticGroup)))
                if (group != SSCRomanceSemanticGroup.None && (group == plan.Semantic || PhaseContains(plan.Phase, group)) &&
                    orders.TryGetValue((plan.Other, group), out long value) && value >= plan.Order) return false;
            return true;
        }
        /// <summary>在可回调读取结束后冻结写集，不提前移除旧历史。</summary>
        internal static SSCRomanceMoodChangePlan Prepare(MemoryThoughtHandler handler, Pawn other, SSCRomanceSemanticGroup semantic,
            SSCRomanceMoodPhase phase, long order, Thought_MemorySSCRomance candidate, SSCRomanceMoodGroup durationGroup = SSCRomanceMoodGroup.None)
        {
            if (handler == null || order <= 0 || semantic == SSCRomanceSemanticGroup.None && phase == SSCRomanceMoodPhase.None) return null;
            var items = handler.Memories.OfType<Thought_MemorySSCRomance>().Where(m => m != candidate && Affected(m, other, semantic, phase)).ToArray();
            // 独立构造入口可没有对象，但不能把丢失引用的历史视为同一个可替换目标。
            if (other == null && items.Length != 0) return null;
            if (items.Any(m => !Known(m) || m.Data.OccurrenceOrder >= order)) return null;
            var states = items.Select(m => new SSCRomanceMoodMemoryState(m, handler.Memories.IndexOf(m))).ToArray();
            var old = items.Where(m => SemanticOf(m) == semantic).OrderByDescending(m => m.Data.OccurrenceOrder).FirstOrDefault();
            int remaining = old == null ? 0 : old.permanent ? -1 : old.DurationTicks - old.age;
            var removed = items.Select(m => new SSCRomanceMoodRemoval(m, SemanticOf(m) == semantic ? "SameGroup" : "Phase:" + phase)).ToArray();
            int duration = candidate == null ? 0 : candidate.DurationTicks - candidate.age;
            var kind = candidate == null ? (semantic == SSCRomanceSemanticGroup.None ? SSCRomanceMoodResultKind.Cleaned : SSCRomanceMoodResultKind.Zero) :
                old == null ? SSCRomanceMoodResultKind.Added : old.Data.BranchId == candidate.Data.BranchId && old.Data.Value == candidate.Data.Value ? SSCRomanceMoodResultKind.Refreshed : SSCRomanceMoodResultKind.Replaced;
            var plan = new SSCRomanceMoodChangePlan { Handler = handler, Other = other, Semantic = semantic, Phase = phase, Order = order, Candidate = candidate, Before = states,
                Result = new SSCRomanceMoodResult(kind, durationGroup, old?.Data.Value, candidate?.Data.Value ?? 0, duration, old?.Data.BranchId, remaining, semantic, phase, order, removed) };
            return PlanCurrent(plan) ? plan : null;
        }
        /// <summary>最终复查只比较实例字段和列表，不查询虚拟期限或人物关系。</summary>
        internal static bool PlanCurrent(SSCRomanceMoodChangePlan plan) => plan != null && OrderCurrent(plan) &&
            plan.Handler.Memories.OfType<Thought_MemorySSCRomance>().Count(m => m != plan.Candidate && Affected(m, plan.Other, plan.Semantic, plan.Phase)) == plan.Before.Length &&
            plan.Before.All(s => plan.Handler.Memories.Contains(s.Memory) && s.Matches(s.Memory, false));
        /// <summary>最终无回调提交；候选已经完成准入，零值与自然清理没有候选。</summary>
        internal static void Commit(SSCRomanceMoodChangePlan plan)
        {
            foreach (var state in plan.Before) plan.Handler.Memories.Remove(state.Memory);
            var orders = runtime.GetOrCreateValue(plan.Handler).Values;
            foreach (SSCRomanceSemanticGroup group in Enum.GetValues(typeof(SSCRomanceSemanticGroup)))
                if (group != SSCRomanceSemanticGroup.None && (group == plan.Semantic || PhaseContains(plan.Phase, group))) orders[(plan.Other, group)] = plan.Order;
        }
        /// <summary>自然关系只清 SSC 历史，不获得新思想、不撤销原版反馈。</summary>
        internal static SSCRomanceMoodResult Cleanup(MemoryThoughtHandler handler, Pawn other, SSCRomanceMoodPhase phase, Func<bool> current, long order)
        {
            if (current == null || !current()) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidContext);
            var plan = Prepare(handler, other, SSCRomanceSemanticGroup.None, phase, order, null);
            if (plan == null || !current() || !PlanCurrent(plan)) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict);
            Commit(plan); return plan.Result;
        }
        /// <summary>同序号的重复历史按保存事实固定选择，不拼接背景或有效期。</summary>
        private static string HistoricalKey(Thought_MemorySSCRomance m)
        {
            var d = m.Data;
            return string.Join("|", new object[] { m.def.defName, d?.BranchId, m.age, m.durationTicksOverride, m.moodPowerFactor, m.moodOffset, m.permanent,
                m.sourcePrecept?.GetUniqueLoadID(), d?.Relation, d?.Stage, d?.Masochist, d?.Rule, d?.HasRule, d?.Master?.thingIDNumber, d?.DataVersion,
                d?.CompositeEvent, d?.HasRawOpinion, d?.RawOpinion, d?.OpinionBand, d?.Value, d?.Group, d?.NominalDurationTicks, d?.OccurrenceOrder }
                .Select(value => value is IFormattable n ? n.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? ""));
        }
        /// <summary>整批恢复按对象语义保留最新整条，不执行新事件或关系阶段清理。</summary>
        public static void NormalizeHistory(MemoryThoughtHandler handler, Func<bool> current = null)
        {
            if (handler == null) return;
            var all = handler.Memories.OfType<Thought_MemorySSCRomance>().Where(m => GroupOf(m) != SSCRomanceMoodGroup.None).ToArray();
            foreach (var m in all) SSCRomanceMoodOrder.Observe(m.Data?.OccurrenceOrder ?? 0);
            foreach (var batch in all.Where(m => m.otherPawn != null && SemanticOf(m) != SSCRomanceSemanticGroup.None).GroupBy(m => (m.otherPawn, SemanticOf(m))))
            {
                var items = batch.ToArray();
                if (items.Any(m => !Known(m) || m.IsPendingNewEvent || m.CopiedPendingEvent) ||
                    all.Any(m => m.otherPawn == batch.Key.Item1 && SemanticOf(m) == SSCRomanceSemanticGroup.None)) continue;
                var states = items.Select(m => new SSCRomanceMoodMemoryState(m, -1)).ToArray();
                var winner = items.OrderByDescending(m => m.Data.OccurrenceOrder).ThenBy(HistoricalKey, StringComparer.Ordinal).First();
                // 最新短结果已经过期时，不复活更早但期限更长的旧结果。
                bool expired = winner.ShouldDiscard;
                if (current != null && !current()) return;
                if (handler.Memories.OfType<Thought_MemorySSCRomance>().Count(m => SameKey(m, winner)) != items.Length ||
                    states.Any(s => !handler.Memories.Contains(s.Memory) || !s.Matches(s.Memory, false))) return;
                foreach (var m in items) if (expired || m != winner) handler.Memories.Remove(m);
            }
        }
        /// <summary>历史独立准入直接还原，年龄、期限与顺序不重新生成。</summary>
        internal static void Restore(Thought_MemorySSCRomance memory, Pawn owner, Pawn other, Func<bool> current)
        {
            var handler = owner?.needs?.mood?.thoughts?.memories;
            if (handler == null || memory.CopiedPendingEvent || GroupOf(memory) == SSCRomanceMoodGroup.None || !current()) return;
            var expected = new SSCRomanceMoodMemoryState(memory, -1);
            if (!ThoughtUtility.CanGetThought(owner, memory.def) || !current() || !expected.Matches(memory, false) || memory.GetType() != memory.def.thoughtClass) return;
            memory.pawn = owner; memory.otherPawn = other; memory.ResetRuntime();
            SSCRomanceMoodOrder.Observe(memory.Data?.OccurrenceOrder ?? 0); handler.Memories.Add(memory);
        }
        /// <summary>准入后暂存候选；包装请求延后删除，直接获得立即局部提交。</summary>
        internal static SSCRomanceMoodResult Merge(Thought_MemorySSCRomance memory, bool historical)
        {
            var group = GroupOf(memory); var handler = memory.pawn?.needs?.mood?.thoughts?.memories;
            if (handler == null || memory.CanApply != null && !memory.CanApply()) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidContext, group);
            if (memory.CopiedPendingEvent) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, group);
            if (handler.Memories.Contains(memory)) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Duplicate, group);
            if (memory.FrozenRequest != null && !memory.FrozenRequest()) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, group);
            if (historical)
            {
                handler.Memories.Add(memory); NormalizeHistory(handler, memory.CanApply);
                return new SSCRomanceMoodResult(handler.Memories.Contains(memory) ? SSCRomanceMoodResultKind.Added : SSCRomanceMoodResultKind.Kept, group);
            }
            if (!memory.ValidNewEvent || !TryValue(memory, out _)) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, group);
            if (memory.Data.OccurrenceOrder <= 0)
            {
                foreach (var old in handler.Memories.OfType<Thought_MemorySSCRomance>()) SSCRomanceMoodOrder.Observe(old.Data?.OccurrenceOrder ?? 0);
                memory.Data.OccurrenceOrder = SSCRomanceMoodOrder.Next();
            }
            var plan = Prepare(handler, memory.otherPawn, SemanticOf(memory), memory.Phase, memory.Data.OccurrenceOrder, memory, group);
            if (plan == null) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict, group);
            if (memory.CanApply != null && !memory.CanApply() || memory.FrozenRequest != null && !memory.FrozenRequest() || !PlanCurrent(plan))
                return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict, group);
            memory.PendingPlan = plan; handler.Memories.Add(memory);
            if (memory.FrozenRequest == null) Commit(plan);
            return plan.Result;
        }
    }
}
