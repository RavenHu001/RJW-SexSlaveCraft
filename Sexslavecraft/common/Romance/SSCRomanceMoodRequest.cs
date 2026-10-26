// AI 辅助生成：单次新记忆提交、回执及原版撤销协调；不判定互动成立或发放命令。
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>捕获原人格后只能执行一次；相同分支的两个真实事件必须分别创建请求。</summary>
    public sealed class SSCRomanceMoodRequest
    {
        private readonly Pawn owner, other;
        private readonly object token;
        private readonly CompSexSlaveTraining comp;
        private readonly Need_Mood mood;
        private readonly MemoryThoughtHandler handler;
        private readonly SSCRomanceMoodSelection selection;
        private readonly SSCRomanceMoodData context;
        private bool submitted;
        // 只解释本次退出路径，不参与 Completed、原版撤销或历史选择。
        internal string DiagnosticReason { get; private set; }
        private readonly Func<bool> settlementCurrent, commitNative;

        /// <summary>在工厂／准入回调前冻结原载体和上下文；此对象不进入存档或人格副本。</summary>
        public SSCRomanceMoodRequest(Pawn owner, Pawn other, SSCRomanceMoodSelection selection, SSCRomanceMoodData context = null)
        {
            this.owner = owner; this.other = other; this.selection = selection; this.context = context?.Copy();
            token = SSCPersonalityRuntimeUtility.Capture(owner); comp = owner?.TryGetComp<CompSexSlaveTraining>();
            mood = owner?.needs?.mood; handler = mood?.thoughts?.memories;
        }

        /// <summary>真实事件沿用互动前人格；原版逆向计划先复查，最后无回调提交，不重新签发人格上下文。</summary>
        internal SSCRomanceMoodRequest(SSCRomanceMoodFrame.Person original, Pawn other, SSCRomanceMoodSelection selection,
            SSCRomanceMoodData context, Func<bool> current, Func<bool> commit)
        {
            owner = original.Pawn; this.other = other; this.selection = selection; this.context = context?.Copy();
            token = original.Token; comp = original.Comp; mood = original.Mood; handler = original.Handler;
            settlementCurrent = current; commitNative = commit;
        }

        /// <summary>仅复查捕获时的实例；不重新获取 token，不要求历史在事后仍维持原开关／阶段。</summary>
        private bool Current() => owner != null && !owner.Destroyed && !owner.Discarded && !owner.Dead &&
            !SSCPersonalityRuntimeUtility.IsMutating(owner) && SSCPersonalityRuntimeUtility.IsCurrent(owner, token) &&
            owner.TryGetComp<CompSexSlaveTraining>() == comp && owner.needs?.mood == mood && mood?.thoughts?.memories == handler && handler != null;

        /// <summary>经原版准入发放；合并回执与最终列表、冻结字段都一致后才报告成功。</summary>
        public SSCRomanceMoodResult Execute()
        {
            if (submitted) return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Duplicate), "RequestAlreadyConsumed");
            submitted = true;
            if (!Current()) return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidContext), "OriginalContextLost");
            if (settlementCurrent != null && !settlementCurrent()) return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict), "NativeUndoPrecheckFailed");
            if (selection == null) return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData), "MissingSelection");
            if (!Harmony_SSCRomanceMoodAdmissionTail.Ready) return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, selection.Group), "AdmissionTailUnavailable");
            if (!selection.HasMemory && !Thought_MemorySSCCompositeRomance.DefinitionsReady)
                return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData), "InvalidZeroDefinitions");
            if (!selection.HasMemory) return Report(new SSCRomanceMoodResult(commitNative == null || commitNative() ?
                SSCRomanceMoodResultKind.Zero : SSCRomanceMoodResultKind.Conflict), "ZeroBranchCommit");
            Thought_MemorySSCCompositeRomance memory = null;
            SSCRomanceMoodMemoryState[] before = null;
            SSCRomanceMoodMemoryState expected = null;
            try
            {
                if (!Thought_MemorySSCCompositeRomance.TryCreate(selection, context, out memory) || !memory.ValidNewEvent)
                    return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidData, selection.Group), "FactoryOrCarrierInvalid");
                if (!Current()) return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidContext, selection.Group), "OriginalContextLostAfterCallback");
                before = handler.Memories.OfType<Thought_MemorySSCRomance>().Where(m => SSCRomanceMoodGroups.GroupOf(m) == selection.Group)
                    .Select(m => new SSCRomanceMoodMemoryState(m, handler.Memories.IndexOf(m))).ToArray();
                // 未知历史可能有意超过一条；进入原生新获得会按 Def 淘汰它，故在任何新获得副作用之前停下。
                if (before.Any(s => !SSCRomanceMoodGroups.TryValue(s.Memory, out _)))
                    return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Conflict, selection.Group), "OpaqueGroupHistory");
                memory.otherPawn = other;
                expected = new SSCRomanceMoodMemoryState(memory, -1);
                // 准入和合并可能被兼容补丁包裹；只绑定这一个请求，不把守卫复制进历史。
                memory.CanApply = Current;
                memory.FrozenRequest = () => expected.Matches(memory, false) && memory.ValidNewEvent;
                handler.TryGainMemory(memory, other);
                if (!Current())
                {
                    RemoveOwnCandidate(memory);
                    return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.InvalidContext, selection.Group), "OriginalContextLostAfterCallback");
                }
                var result = memory.MergeResult;
                // 准入回调可合法完成另一事件；正常保留的见证必须来自实际合并时，而非调用原生前。
                var observed = memory.ObservedHistory ?? before;
                bool valid = result != null && (!result.Completed || expected.Matches(memory, result.Applied) && memory.ValidNewEvent) &&
                    (result.Applied ? memory.pawn == owner && handler.Memories.Contains(memory) && handler.Memories.OfType<Thought_MemorySSCRomance>()
                        .Count(m => SSCRomanceMoodGroups.GroupOf(m) == selection.Group) == 1 &&
                        memory.DurationTicks - memory.age == result.RemainingTicks :
                        !handler.Memories.Contains(memory) && observed.All(s => handler.Memories.Contains(s.Memory) && s.Matches(s.Memory, false)) &&
                        handler.Memories.OfType<Thought_MemorySSCRomance>().Count(m => SSCRomanceMoodGroups.GroupOf(m) == selection.Group) == observed.Length);
                // 全部工厂／准入／后置补丁返回后统一复查；原版撤销失败时仍可撤销本次 SSC 局部写入。
                if (valid && Current() && (settlementCurrent == null || settlementCurrent()) &&
                    (!result.Completed || commitNative == null || commitNative())) return Report(result, "GroupDecision:" + result.Kind);
                Rollback(memory);
                return Report(new SSCRomanceMoodResult(result == null ? SSCRomanceMoodResultKind.NotAdmitted : SSCRomanceMoodResultKind.Failed, selection.Group),
                    result == null ? "NativeAdmissionOrExternalSkip" : "FinalReceiptOrNativeCommitFailed");
            }
            catch (Exception exception)
            {
                // 不重试准入、不重放互动；只撤销本请求的局部结果，其他系统的写入不回滚。
                Rollback(memory);
                if (!Current()) RemoveOwnCandidate(memory);
                return Report(new SSCRomanceMoodResult(SSCRomanceMoodResultKind.Failed, selection.Group), "RequestException:" + exception.GetType().Name);
            }
            finally { memory?.ResetRuntime(); }
        }

        /// <summary>附加运行期退出原因；返回值和业务判定完全沿用原结果。</summary>
        private SSCRomanceMoodResult Report(SSCRomanceMoodResult result, string reason)
        { DiagnosticReason = reason; return result; }

        /// <summary>只在原载体仍有效且没有后来同组事件时恢复原条目；不向接管身体的新人格写回旧历史。</summary>
        private void Rollback(Thought_MemorySSCRomance memory)
        {
            if (!Current() || memory == null) return;
            // 候选是本请求刚构造的唯一实例；后置补丁改写其字段也不能把失败候选留在列表中。
            handler.Memories.Remove(memory);
            // 未提交时没有本系统移除日志；其他补丁主动清理的历史不能由失败请求复活。
            var before = memory.ReplacedHistory;
            if (before == null) return;
            var group = selection.Group;
            if (handler.Memories.OfType<Thought_MemorySSCRomance>().Any(m => SSCRomanceMoodGroups.GroupOf(m) == group &&
                m != memory && !before.Any(s => s.Memory == m))) return;
            foreach (var state in before.OrderBy(s => s.Index))
                if (!handler.Memories.Contains(state.Memory) && state.Matches(state.Memory, false))
                    handler.Memories.Insert(Math.Min(state.Index, handler.Memories.Count), state.Memory);
        }

        /// <summary>人格已替换时只撤销本请求新建的唯一实例，绝不把旧人格的其他历史搬回新载体。</summary>
        private void RemoveOwnCandidate(Thought_MemorySSCRomance memory)
        {
            if (memory != null) handler?.Memories.Remove(memory);
        }
    }

    /// <summary>局部写入见证；比较保存事实而非显示心情，不反射、序列化或读取人物当前状态。</summary>
    internal sealed class SSCRomanceMoodMemoryState
    {
        internal readonly Thought_MemorySSCRomance Memory;
        internal readonly int Index;
        private readonly ThoughtDef def;
        private readonly Pawn other;
        private readonly Precept precept;
        private readonly int age, duration, stage, offset;
        private readonly float power;
        private readonly bool permanent;
        private readonly SSCRomanceMoodData data;
        /// <summary>在首个原生准入回调前记录条目本身及实际期限。</summary>
        internal SSCRomanceMoodMemoryState(Thought_MemorySSCRomance memory, int index)
        {
            Memory = memory; Index = index; def = memory.def; other = memory.otherPawn; precept = memory.sourcePrecept;
            age = memory.age; duration = memory.durationTicksOverride; stage = memory.CurStageIndex;
            offset = memory.moodOffset; power = memory.moodPowerFactor; permanent = memory.permanent; data = memory.Data?.Copy();
        }
        /// <summary>提交可以延长实际期限，其余实例字段及完整分支必须保持冻结。</summary>
        internal bool Matches(Thought_MemorySSCRomance memory, bool allowDuration) => memory.def == def && memory.otherPawn == other &&
            memory.sourcePrecept == precept && memory.age == age && memory.CurStageIndex == stage && memory.moodOffset == offset &&
            memory.moodPowerFactor == power && memory.permanent == permanent && (allowDuration || memory.durationTicksOverride == duration) && Same(data, memory.Data);
        /// <summary>明确列出全部历史字段，缺快照与零值不同；不比较临时授权或日志状态。</summary>
        private static bool Same(SSCRomanceMoodData a, SSCRomanceMoodData b) => a == null || b == null ? a == b :
            a.Relation == b.Relation && a.Stage == b.Stage && a.Value == b.Value &&
            a.Masochist == b.Masochist && a.Rule == b.Rule && a.HasRule == b.HasRule && a.Master == b.Master &&
            a.DataVersion == b.DataVersion && a.BranchId == b.BranchId && a.CompositeEvent == b.CompositeEvent &&
            a.HasRawOpinion == b.HasRawOpinion && a.RawOpinion == b.RawOpinion && a.OpinionBand == b.OpinionBand &&
            a.Group == b.Group && a.NominalDurationTicks == b.NominalDurationTicks;
    }
}
