// AI 辅助生成：准入、零值结清和关系阶段清理的一次局部提交。
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>捕获原人格后只执行一次；历史恢复和命令授权不能借用此请求。</summary>
    public sealed class SSCRomanceMoodRequest
    {
        private readonly Pawn owner, other;
        private readonly object token;
        private readonly CompSexSlaveTraining comp;
        private readonly Need_Mood mood;
        private readonly MemoryThoughtHandler handler;
        private readonly SSCRomanceMoodSelection selection;
        private readonly SSCRomanceMoodData context;
        private readonly SSCRomanceMoodPhase phase;
        private long occurrenceOrder;
        private bool submitted;
        internal string DiagnosticReason { get; private set; }
        private readonly Func<bool> settlementCurrent, commitNative;
        private readonly Func<Action> prepareNative;

        /// <summary>在工厂／准入回调前冻结原载体；无显式事件顺序的独立新请求在执行前分配。</summary>
        public SSCRomanceMoodRequest(Pawn owner, Pawn other, SSCRomanceMoodSelection selection, SSCRomanceMoodData context = null,
            SSCRomanceMoodPhase phase = SSCRomanceMoodPhase.None, long occurrenceOrder = 0)
        {
            this.owner = owner; this.other = other; this.selection = selection; this.context = context?.Copy() ?? new SSCRomanceMoodData();
            this.phase = phase; this.occurrenceOrder = occurrenceOrder > 0 ? occurrenceOrder : this.context.OccurrenceOrder;
            token = SSCPersonalityRuntimeUtility.Capture(owner); comp = owner?.TryGetComp<CompSexSlaveTraining>();
            mood = owner?.needs?.mood; handler = mood?.thoughts?.memories;
        }

        /// <summary>真实事件沿用互动前人格、原生变更账本及完整结果，不重新签发上下文。</summary>
        internal SSCRomanceMoodRequest(SSCRomanceMoodFrame.Person original, Pawn other, SSCRomanceMoodSelection selection,
            SSCRomanceMoodData context, Func<bool> current, Func<bool> commit, SSCRomanceMoodPhase phase = SSCRomanceMoodPhase.None,
            long occurrenceOrder = 0, Func<Action> prepareNative = null)
        {
            owner = original.Pawn; this.other = other; this.selection = selection; this.context = context?.Copy() ?? new SSCRomanceMoodData();
            token = original.Token; comp = original.Comp; mood = original.Mood; handler = original.Handler;
            this.phase = phase; this.occurrenceOrder = occurrenceOrder > 0 ? occurrenceOrder : this.context.OccurrenceOrder;
            settlementCurrent = current; commitNative = commit; this.prepareNative = prepareNative;
        }

        /// <summary>只比较捕获实例，不查询当前阶段／好感，也不重新捕获人格权限。</summary>
        private bool Current() => owner != null && !owner.Destroyed && !owner.Discarded && !owner.Dead &&
            !SSCPersonalityRuntimeUtility.IsMutating(owner) && SSCPersonalityRuntimeUtility.IsCurrent(owner, token) &&
            owner.TryGetComp<CompSexSlaveTraining>() == comp && owner.needs?.mood == mood && mood?.thoughts?.memories == handler && handler != null;

        /// <summary>经原版准入暂存候选；后置回调完成后再提交清理和原版精确撤销。</summary>
        public SSCRomanceMoodResult Execute()
        {
            if (submitted) return Fail(SSCRomanceMoodResultKind.Duplicate, "RequestAlreadyConsumed");
            submitted = true;
            if (!Current()) return Fail(SSCRomanceMoodResultKind.InvalidContext, "OriginalContextLost");
            Thought_MemorySSCCompositeRomance memory = null;
            try
            {
                if (selection == null || selection.SemanticGroup == SSCRomanceSemanticGroup.None) return Fail(SSCRomanceMoodResultKind.InvalidData, "MissingSelection");
                if (settlementCurrent != null && !settlementCurrent()) return Fail(SSCRomanceMoodResultKind.Conflict, "NativeUndoPrecheckFailed");
                if (!Harmony_SSCRomanceMoodAdmissionTail.Ready) return Fail(SSCRomanceMoodResultKind.InvalidData, "AdmissionTailUnavailable");
                if (!Current()) return Fail(SSCRomanceMoodResultKind.InvalidContext, "OriginalContextLostAfterPrecheck");
                // 真实事件已带顺序。独立调用只在任何工厂／准入回调之前分配，不能在返回后重签为更新事件。
                foreach (var old in handler.Memories.OfType<Thought_MemorySSCRomance>()) SSCRomanceMoodOrder.Observe(old.Data?.OccurrenceOrder ?? 0);
                if (occurrenceOrder <= 0) occurrenceOrder = SSCRomanceMoodOrder.Next();
                SSCRomanceMoodOrder.Observe(occurrenceOrder); context.OccurrenceOrder = occurrenceOrder;
                SSCRomanceMoodChangePlan plan;
                SSCRomanceMoodMemoryState expected = null;
                if (!selection.HasMemory)
                {
                    if (!Thought_MemorySSCCompositeRomance.DefinitionsReady) return Fail(SSCRomanceMoodResultKind.InvalidData, "InvalidZeroDefinitions");
                    // 零值仍有事件语义与阶段写集；不创建假的零值思想、不把失败当成结清。
                    plan = SSCRomanceMoodGroups.Prepare(handler, other, selection.SemanticGroup, phase, occurrenceOrder, null);
                    if (plan == null) return Fail(SSCRomanceMoodResultKind.Conflict, "ZeroHistoryConflict");
                }
                else
                {
                    if (!Thought_MemorySSCCompositeRomance.TryCreate(selection, context, out memory) || !memory.ValidNewEvent)
                        return Fail(SSCRomanceMoodResultKind.InvalidData, "FactoryOrCarrierInvalid");
                    if (!Current()) return Fail(SSCRomanceMoodResultKind.InvalidContext, "OriginalContextLostAfterFactory");
                    memory.otherPawn = other; memory.Phase = phase;
                    expected = new SSCRomanceMoodMemoryState(memory, -1);
                    memory.CanApply = Current;
                    memory.FrozenRequest = () => expected.Matches(memory, false) && memory.ValidNewEvent;
                    handler.TryGainMemory(memory, other);
                    var provisional = memory.MergeResult;
                    if (provisional == null) return Fail(SSCRomanceMoodResultKind.NotAdmitted, "NativeAdmissionOrExternalSkip");
                    if (!provisional.Applied) return Fail(provisional.Kind, "GroupDecision:" + provisional.Kind);
                    plan = memory.PendingPlan;
                }

                // 候选的虚拟期限读取必须早于原版撤销准备；否则它能使刚生成的逆向计划失效。
                bool candidateValid = memory == null || memory.pawn == owner && handler.Memories.Contains(memory) &&
                    expected.Matches(memory, false) && memory.ValidNewEvent && memory.DurationTicks - memory.age == plan.Result.RemainingTicks;
                if (!candidateValid) return Fail(SSCRomanceMoodResultKind.Failed, "CandidateChangedAfterAdmission");
                // 原版撤销计划的读取可能回调；准备完成后最后只复查字段／列表，不再触发外部虚方法。
                Action applyNative = null;
                if (prepareNative != null)
                {
                    applyNative = prepareNative();
                    if (applyNative == null) return Fail(SSCRomanceMoodResultKind.Conflict, "NativeUndoPrecheckFailed");
                }
                else if (settlementCurrent != null && !settlementCurrent())
                    return Fail(SSCRomanceMoodResultKind.Conflict, "NativeUndoPrecheckFailed");
                if (plan == null || plan.Other != other || plan.Semantic != selection.SemanticGroup || plan.Phase != phase || plan.Order != occurrenceOrder ||
                    memory != null && (memory.pawn != owner || !handler.Memories.Contains(memory) || !expected.Matches(memory, false)) ||
                    !Current() || !SSCRomanceMoodGroups.PlanCurrent(plan))
                    return Fail(SSCRomanceMoodResultKind.Failed, "FinalReceiptOrContextChanged");
                // 保留独立请求的旧回调接口；真实事件使用上面已验证的无回调 applyNative。
                if (prepareNative == null && commitNative != null && !commitNative())
                    return Fail(SSCRomanceMoodResultKind.Conflict, "NativeCommitFailed");
                if (prepareNative == null && (!Current() || !SSCRomanceMoodGroups.PlanCurrent(plan)))
                    return Fail(SSCRomanceMoodResultKind.Failed, "CommitCallbackChangedHistory");
                applyNative?.Invoke();
                SSCRomanceMoodGroups.Commit(plan);
                DiagnosticReason = selection.HasMemory ? "GroupDecision:" + plan.Result.Kind : "ZeroBranchCommit";
                memory?.ResetRuntime();
                memory = null; // 成功后 finally 不移除已提交候选。
                return plan.Result;
            }
            catch (Exception exception)
            { return Fail(SSCRomanceMoodResultKind.Failed, "RequestException:" + exception.GetType().Name); }
            finally
            {
                // 提交前没有删除旧历史，失败只撤本请求唯一候选，不复活外部清理或覆盖更新事件。
                if (memory != null) { handler?.Memories.Remove(memory); memory.ResetRuntime(); }
            }
        }

        /// <summary>失败回执只说明本次退出，不把计划清理作为实际删除报告。</summary>
        private SSCRomanceMoodResult Fail(SSCRomanceMoodResultKind kind, string reason)
        {
            DiagnosticReason = reason;
            return new SSCRomanceMoodResult(kind, selection?.Group ?? SSCRomanceMoodGroup.None,
                semantic: selection?.SemanticGroup ?? SSCRomanceSemanticGroup.None, phase: phase, order: occurrenceOrder);
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
            a.Group == b.Group && a.NominalDurationTicks == b.NominalDurationTicks && a.OccurrenceOrder == b.OccurrenceOrder;
    }
}
