// AI 辅助生成
using System;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>普通心情记忆，不派生社交好感；同组跨目标最多一条，保留原版准入及显示倍率。</summary>
    public abstract class Thought_MemorySSCRomance : Thought_Memory, ISSCRomanceHistoricalRestore
    {
        public SSCRomanceMoodData Data;
        // 仅新事件准入期间使用的原上下文守卫；不保存、不进入凝胶或原生克隆副本。
        internal Func<bool> CanApply;
        internal Func<bool> FrozenRequest;
        internal SSCRomanceMoodResult MergeResult;
        // 只记录本次真正提交时移除的条目；不得用准入前快照回滚回调已完成的另一事件。
        internal SSCRomanceMoodMemoryState[] ReplacedHistory;
        internal SSCRomanceMoodMemoryState[] ObservedHistory;
        // 新方案历史经 CopyFrom 后的单条恢复只消费一次；真实复制／读档完成后另有局部收尾。
        internal bool HistoricalRestore;
        /// <summary>正常存读档仅恢复历史，绝不调用事件发放或按新阶段重算。</summary>
        public override void ExposeData()
        { base.ExposeData(); Scribe_Deep.Look(ref Data, "sscRomanceMood"); if (Scribe.mode == LoadSaveMode.LoadingVars) ResetRuntime(); }
        /// <summary>原版 PawnDuplicator 走 CopyFrom；必须复制自定义快照，不能共享可写引用。</summary>
        public override void CopyFrom(Thought_Memory other)
        { base.CopyFrom(other); Data = (other as Thought_MemorySSCRomance)?.Data?.Copy(); permanent = other.permanent; ResetRuntime(); HistoricalRestore = !SSCRomanceMoodCopyScope.Active; }
        /// <summary>明确跨对象同组；不依赖尚未定稿的标签或关联人物来判定组别。</summary>
        public override bool GroupsWith(Thought other)
        { var group = SSCRomanceMoodGroups.GroupOf(this); return group != SSCRomanceMoodGroup.None && group == SSCRomanceMoodGroups.GroupOf(other); }

        /// <summary>已知载体保留自身副作用契约；未知补丁添加关联思想时不继续本系统的局部提交。</summary>
        internal virtual bool ValidNewEvent => SSCRomanceMoodGroups.GroupOf(this) != SSCRomanceMoodGroup.None &&
            def.thoughtClass == GetType() && def.stackLimit == 1 && def.stackLimitForSameOtherPawn != 0 &&
            def.thoughtToMake == null && (def.replaceThoughts == null || def.replaceThoughts.Count == 0) && !def.showBubble;

        /// <summary>清除的是实例临时状态，绝不清除保存的快照、年龄或实际期限。</summary>
        internal void ResetRuntime() { CanApply = null; FrozenRequest = null; MergeResult = null; ReplacedHistory = null; ObservedHistory = null; HistoricalRestore = false; }
        /// <summary>新方案的逐条历史恢复入口；作用域不进入保存数据或生成新事件授权。</summary>
        void ISSCRomanceHistoricalRestore.BeginHistoricalRestore() => HistoricalRestore = true;
        /// <summary>异常和提前返回也结束临时领取。</summary>
        void ISSCRomanceHistoricalRestore.EndHistoricalRestore() => HistoricalRestore = false;
        /// <summary>人格历史使用独立准入与整条实例整理，避免逐条原生容量淘汰或新事件续期。</summary>
        void ISSCRomanceHistoricalRestore.RestoreHistorical(Pawn owner, Pawn other, Func<bool> current)
            => SSCRomanceMoodGroups.Restore(this, owner, other, current);
        /// <summary>全部条目恢复后只整理一次；后出现的未知历史不能让早先条目已被不可逆淘汰。</summary>
        void ISSCRomanceHistoricalRestore.CompleteHistoricalRestore(Pawn owner, Func<bool> current)
        { if (current()) SSCRomanceMoodGroups.NormalizeHistory(owner.needs?.mood?.thoughts?.memories, current); }

        /// <summary>此时 TryGainMemory 已完成原版准入；拒绝较弱事件时不续期，不先删后验。</summary>
        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            showBubble = false;
            bool historical = HistoricalRestore; HistoricalRestore = false;
            MergeResult = SSCRomanceMoodGroups.Merge(this, historical);
            // 返回 true 表示本类已完成新增／替换或明确保留，不让原生再次添加候选。
            return true;
        }
    }

    [DefOf]
    public static class SSCRomanceMoodDefOf
    {
        public static TraitDef Masochist;
        public static ThoughtDef SSC_Romance_Control;
        /// <summary>沿 DefOf 加载生命周期引用 SSC 定义及原版 Masochist，不复制原版 TraitDef。</summary>
        static SSCRomanceMoodDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SSCRomanceMoodDefOf));
    }

    /// <summary>只读持续思想，不创建事件；阶段和选择实时解析，不存第二份有效状态。</summary>
    public sealed class ThoughtWorker_SSCRomanceControl : ThoughtWorker
    {
        /// <summary>引擎沿原版思想准入查询；只有一个综合阶段，不创建或保存事件。</summary>
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
            => TryStage(pawn, out int stage) ? ThoughtState.ActiveAtStage(stage) : ThoughtState.Inactive;

        /// <summary>独立只读查询供需求阈值刷新复用；有效零值仍是存在的受限状态。</summary>
        public static bool TryStage(Pawn pawn, out int stage)
        {
            stage = -1;
            var state = SSCRomanceResolver.Resolve(pawn);
            if (!state.Applicable || pawn.Dead || state.Stage < 2) return false;
            // 目标过滤只有至少一种主动浪漫可用时参与；分手许可不依赖当前有无伴侣。
            bool active = !state.GetSaved(SSCRomanceSetting.InitiateRomance) || !state.GetSaved(SSCRomanceSetting.InitiateProposal) ||
                !state.GetSaved(SSCRomanceSetting.InitiateBreakup) ||
                ((state.GetSaved(SSCRomanceSetting.InitiateRomance) || state.GetSaved(SSCRomanceSetting.InitiateProposal)) &&
                 (!state.GetSaved(SSCRomanceSetting.InitiateToMaster) || !state.GetSaved(SSCRomanceSetting.InitiateToSexSlave) || !state.GetSaved(SSCRomanceSetting.InitiateToOthers)));
            // 三项保证逻辑或成一个 B，不能按多选个数叠加心情。
            bool passive = state.Stage >= 3 && (!state.GetSaved(SSCRomanceSetting.AcceptRomance) || !state.GetSaved(SSCRomanceSetting.AcceptProposal) ||
                state.GetSaved(SSCRomanceSetting.AlwaysAcceptMaster) || state.GetSaved(SSCRomanceSetting.AlwaysAcceptSexSlave) || state.GetSaved(SSCRomanceSetting.AlwaysAcceptOthers));
            if (!active && !passive) return false;
            int value = SSCRomanceMoodRules.Continuous(state.Stage, active, passive,
                SSCRomanceMoodDefOf.Masochist != null && pawn.story?.traits?.HasTrait(SSCRomanceMoodDefOf.Masochist) == true, pawn.needs?.TryGetNeed<Need_Corruption>()?.CurLevel);
            stage = value + 4; return true;
        }
    }

    /// <summary>局部缓存只服务持续思想失效，不保存、不扫描世界、不操作历史记忆。</summary>
    public static class SSCRomanceMoodRefresh
    {
        private sealed class State { internal int Signature = int.MinValue; }
        private static readonly ConditionalWeakTable<Pawn, State> states = new ConditionalWeakTable<Pawn, State>();
        /// <summary>恶堕跨阈值或特质／阶段导致综合值变化时才通知原版缓存。</summary>
        public static void Notify(Pawn pawn)
        {
            if (pawn == null || Scribe.mode != LoadSaveMode.Inactive) return;
            int signature = ThoughtWorker_SSCRomanceControl.TryStage(pawn, out int stage) ? stage : -1;
            var state = states.GetOrCreateValue(pawn);
            if (state.Signature == signature) return;
            state.Signature = signature;
            pawn.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
        }
    }
}
