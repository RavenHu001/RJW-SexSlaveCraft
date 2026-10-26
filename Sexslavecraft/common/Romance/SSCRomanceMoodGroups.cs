// AI 辅助生成：短／长期语义、冻结值和历史整理；不推断真实互动或替换原版思想。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>一次提交的实际结果；正常组阻挡与技术失败分开，供后续事件及诊断使用。</summary>
    public enum SSCRomanceMoodResultKind
    {
        Added, Replaced, Refreshed, Kept, Zero, NotAdmitted, InvalidContext, InvalidData, Conflict, Duplicate, Failed
    }

    /// <summary>不保存、不携带授权；字段来自冻结基准及实际写入，不把 TryGainMemory 返回当作成功。</summary>
    public sealed class SSCRomanceMoodResult
    {
        public SSCRomanceMoodResultKind Kind { get; }
        public SSCRomanceMoodGroup Group { get; }
        public int? OldValue { get; }
        public int NewValue { get; }
        public int RemainingTicks { get; }
        public string OldBranch { get; }
        public int OldRemainingTicks { get; }
        public bool Applied => Kind == SSCRomanceMoodResultKind.Added || Kind == SSCRomanceMoodResultKind.Replaced || Kind == SSCRomanceMoodResultKind.Refreshed;
        public bool Completed => Applied || Kind == SSCRomanceMoodResultKind.Kept || Kind == SSCRomanceMoodResultKind.Zero;
        /// <summary>仅由分组／提交层构造；不公开可变记忆或原人格凭据。</summary>
        internal SSCRomanceMoodResult(SSCRomanceMoodResultKind kind, SSCRomanceMoodGroup group = SSCRomanceMoodGroup.None,
            int? oldValue = null, int newValue = 0, int remaining = 0, string oldBranch = null, int oldRemaining = 0)
        { Kind = kind; Group = group; OldValue = oldValue; NewValue = newValue; RemainingTicks = remaining; OldBranch = oldBranch; OldRemainingTicks = oldRemaining; }
    }

    /// <summary>只识别综合方案两个载体；没有旧方案别名或阶段数值回退。</summary>
    public static class SSCRomanceMoodGroups
    {
        /// <summary>只按综合载体归组，不以剩余天数、对象或名称猜分组。</summary>
        public static SSCRomanceMoodGroup GroupOf(Thought thought)
        {
            if (!(thought is Thought_MemorySSCCompositeRomance)) return SSCRomanceMoodGroup.None;
            switch (thought.def?.defName)
            {
                case Thought_MemorySSCCompositeRomance.ShortDefName: return SSCRomanceMoodGroup.Short;
                case Thought_MemorySSCCompositeRomance.LongDefName: return SSCRomanceMoodGroup.Long;
                default: return SSCRomanceMoodGroup.None;
            }
        }

        /// <summary>文案 ID 失效不抹去合法冻结值；未来格式不借旧阶段或目录值猜测其计算契约。</summary>
        public static bool TryValue(Thought_MemorySSCRomance memory, out int value)
        {
            value = 0;
            var group = GroupOf(memory);
            if (group == SSCRomanceMoodGroup.None) return false;
            if (memory.Data == null || !memory.Data.IsComposite) return false;
            value = memory.Data.Value;
            return value >= -15 && value <= 15 && (group != SSCRomanceMoodGroup.Long || value < 0);
        }

        /// <summary>纯数值比较；短期负面优先，长期更负优先，零值从不刷新。</summary>
        public static bool MayReplace(SSCRomanceMoodGroup group, int oldValue, int newValue)
            => (group == SSCRomanceMoodGroup.Short || group == SSCRomanceMoodGroup.Long) && newValue != 0 &&
                (group == SSCRomanceMoodGroup.Long ? newValue < 0 && newValue <= oldValue :
                    oldValue < 0 ? newValue <= oldValue : newValue < 0 || newValue >= oldValue);

        /// <summary>历史选择保留整条实例；强度相同选择较晚到期者，不合成新背景、不更新年龄。</summary>
        private static Thought_MemorySSCRomance Winner(IEnumerable<Thought_MemorySSCRomance> source, SSCRomanceMoodGroup group)
        {
            Thought_MemorySSCRomance winner = null;
            foreach (var item in source)
            {
                if (item.ShouldDiscard || !TryValue(item, out int value)) continue;
                if (winner == null) { winner = item; continue; }
                TryValue(winner, out int old);
                int remaining = item.DurationTicks - item.age, oldRemaining = winner.DurationTicks - winner.age;
                if (value != old ? MayReplace(group, old, value) :
                    item.permanent != winner.permanent ? item.permanent : remaining != oldRemaining ? remaining > oldRemaining :
                    string.CompareOrdinal(HistoricalKey(item), HistoricalKey(winner)) < 0)
                    winner = item;
            }
            return winner;
        }

        /// <summary>完全同强度／到期的历史用保存事实固定排序；不依赖恢复顺序、当前语言或人物实时属性。</summary>
        private static string HistoricalKey(Thought_MemorySSCRomance memory)
        {
            var d = memory.Data;
            return string.Join("|", new object[] { memory.def.defName, d?.BranchId, memory.otherPawn?.thingIDNumber,
                memory.age, memory.durationTicksOverride, memory.moodPowerFactor, memory.moodOffset, memory.sourcePrecept?.GetUniqueLoadID(),
                d?.Relation, d?.Stage, d?.Masochist, d?.Rule, d?.HasRule, d?.Master?.thingIDNumber,
                d?.DataVersion, d?.CompositeEvent, d?.HasRawOpinion, d?.RawOpinion, d?.OpinionBand, d?.Group, d?.NominalDurationTicks }
                .Select(value => value is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? ""));
        }

        /// <summary>只整理可解释的本系统历史；未知格式使该组暂停整理，其他系统及原版历史完全不动。</summary>
        public static void NormalizeHistory(MemoryThoughtHandler handler, Func<bool> current = null)
        {
            if (handler == null) return;
            foreach (var group in new[] { SSCRomanceMoodGroup.Short, SSCRomanceMoodGroup.Long })
            {
                var items = handler.Memories.OfType<Thought_MemorySSCRomance>().Where(m => GroupOf(m) == group).ToArray();
                var states = items.Select(m => new SSCRomanceMoodMemoryState(m, -1)).ToArray();
                if (items.Any(m => !TryValue(m, out _))) continue;
                var winner = Winner(items, group);
                // ShouldDiscard 及外部补丁也能回调；完成所有读取后，提交前再核对原载体。
                if (current != null && !current()) return;
                if (!Unchanged(handler, group, states)) return;
                foreach (var item in items) if (item != winner) handler.Memories.Remove(item);
            }
        }

        /// <summary>历史不是新获得：沿原版 CanGetThought 准入后直接还原，避开 Renew、thoughtToMake 及逐条容量淘汰。</summary>
        internal static void Restore(Thought_MemorySSCRomance memory, Verse.Pawn owner, Verse.Pawn other, Func<bool> current)
        {
            var handler = owner?.needs?.mood?.thoughts?.memories;
            if (handler == null || GroupOf(memory) == SSCRomanceMoodGroup.None || !current()) return;
            // 这是快照安装后的第二次准入；回调不可改写年龄、实际期限、载体或已保存的分支事实。
            var expected = new SSCRomanceMoodMemoryState(memory, -1);
            if (!ThoughtUtility.CanGetThought(owner, memory.def) || !current() || !expected.Matches(memory, false) ||
                memory.GetType() != memory.def.thoughtClass) return;
            memory.pawn = owner; memory.otherPawn = other;
            memory.ResetRuntime();
            handler.Memories.Add(memory);
            // 完整人格批次结束后统一整理，不能在未知条目尚未恢复时先删已知历史。
        }

        /// <summary>原生准入后一次局部提交；先将候选落位，再移除同组旧条目，期间不调用外部处理器方法。</summary>
        internal static SSCRomanceMoodResult Merge(Thought_MemorySSCRomance memory, bool historical)
        {
            var group = GroupOf(memory);
            var handler = memory.pawn?.needs?.mood?.thoughts?.memories;
            if (handler == null || (memory.CanApply != null && !memory.CanApply())) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidContext, group);
            if (handler.Memories.Contains(memory)) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Duplicate, group);
            if (memory.FrozenRequest != null && !memory.FrozenRequest()) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, group);
            if (historical)
            {
                handler.Memories.Add(memory);
                NormalizeHistory(handler, memory.CanApply);
                return new SSCRomanceMoodResult(handler.Memories.Contains(memory) ? SSCRomanceMoodResultKind.Added : SSCRomanceMoodResultKind.Kept, group);
            }
            if (!memory.ValidNewEvent || !TryValue(memory, out int value)) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, group);
            var oldItems = handler.Memories.OfType<Thought_MemorySSCRomance>().Where(m => GroupOf(m) == group).ToArray();
            var states = oldItems.Select(m => new SSCRomanceMoodMemoryState(m, handler.Memories.IndexOf(m))).ToArray();
            memory.ObservedHistory = states;
            if (oldItems.Any(m => !TryValue(m, out _))) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict, group);
            var old = Winner(oldItems, group);
            if (!Unchanged(handler, group, states)) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict, group);
            int? oldValue = old == null ? (int?)null : (TryValue(old, out int frozen) ? frozen : (int?)null);
            bool keep = oldValue.HasValue && (old.permanent || !MayReplace(group, oldValue.Value, value));
            int remaining = old == null ? 0 : old.permanent ? -1 : old.DurationTicks - old.age;
            int duration = memory.DurationTicks;
            if (group == SSCRomanceMoodGroup.Long && oldValue == value) duration = Math.Max(duration, remaining);
            // 所有虚方法、资格及快照读取结束后再检查一次；以下列表操作不会触发准入或工厂回调。
            if (memory.CanApply != null && !memory.CanApply()) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidContext, group);
            if (memory.FrozenRequest != null && !memory.FrozenRequest()) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, group);
            if (!Unchanged(handler, group, states)) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict, group);
            if (keep) return new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Kept, group, oldValue, value, remaining, old?.Data?.BranchId, remaining);
            memory.ReplacedHistory = states;
            memory.durationTicksOverride = duration;
            handler.Memories.Add(memory);
            foreach (var item in oldItems) handler.Memories.Remove(item);
            return new SSCRomanceMoodResult(old == null ? SSCRomanceMoodResultKind.Added : oldValue == value ?
                SSCRomanceMoodResultKind.Refreshed : SSCRomanceMoodResultKind.Replaced, group, oldValue, value, duration - memory.age, old?.Data?.BranchId, remaining);
        }

        /// <summary>读取虚方法期间若同组记录被回调改动，放弃旧计划而不是删除后来写入的历史。</summary>
        private static bool Unchanged(MemoryThoughtHandler handler, SSCRomanceMoodGroup group, SSCRomanceMoodMemoryState[] states)
            => handler.Memories.OfType<Thought_MemorySSCRomance>().Count(m => GroupOf(m) == group) == states.Length &&
                states.All(s => handler.Memories.Contains(s.Memory) && s.Matches(s.Memory, false));
    }
}
