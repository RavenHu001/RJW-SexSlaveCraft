// AI 辅助生成
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>普通心情记忆，不派生社交好感；同对象同语义最多一条，保留原版准入及显示倍率。</summary>
    public abstract class Thought_MemorySSCRomance : Thought_Memory, ISSCRomanceHistoricalRestore
    {
        public SSCRomanceMoodData Data;
        // 仅新事件准入期间使用的原上下文守卫；不保存、不进入凝胶或原生克隆副本。
        internal Func<bool> CanApply;
        internal Func<bool> FrozenRequest;
        internal SSCRomanceMoodResult MergeResult;
        // 准入阶段只暂存候选与删除计划；外部后置回调结束后由原请求完成最终提交。
        internal SSCRomanceMoodChangePlan PendingPlan;
        internal SSCRomanceMoodPhase Phase;
        // 临时入列不等于完成获得；副本必须记住来源仍待提交，不能把它洗成普通历史。
        internal bool CopiedPendingEvent;
        internal bool IsPendingNewEvent => PendingPlan != null && FrozenRequest != null;
        // 新方案历史经 CopyFrom 后的单条恢复只消费一次；真实复制／读档完成后另有局部收尾。
        internal bool HistoricalRestore;
        /// <summary>正常存读档仅恢复历史，绝不调用事件发放或按新阶段重算。</summary>
        public override void ExposeData()
        {
            // 强制存档也可能由准入后置回调触发；只保存“应丢弃候选”的标记，不保存计划、授权或待执行工作。
            bool pendingAtSave = IsPendingNewEvent || CopiedPendingEvent;
            base.ExposeData();
            Scribe_Deep.Look(ref Data, "sscRomanceMood");
            Scribe_Values.Look(ref pendingAtSave, "sscRomancePendingAtSave", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                ResetRuntime();
                CopiedPendingEvent = pendingAtSave;
            }
        }
        /// <summary>原版 PawnDuplicator 走 CopyFrom；必须复制自定义快照，不能共享可写引用。</summary>
        public override void CopyFrom(Thought_Memory other)
        {
            var source = other as Thought_MemorySSCRomance;
            bool copiedPending = source?.IsPendingNewEvent == true || source?.CopiedPendingEvent == true;
            base.CopyFrom(other); Data = source?.Data?.Copy(); permanent = other.permanent; ResetRuntime();
            CopiedPendingEvent = copiedPending;
            HistoricalRestore = !SSCRomanceMoodCopyScope.Active;
            SSCRomanceMoodOrder.Observe(Data?.OccurrenceOrder ?? 0L);
        }
        /// <summary>只比较保存对象和事件语义；跨短／长期仍同组，不依赖文案或当前人物状态。</summary>
        public override bool GroupsWith(Thought other)
            => SSCRomanceMoodGroups.SameKey(this, other);

        /// <summary>已知载体保留自身副作用契约；未知补丁添加关联思想时不继续本系统的局部提交。</summary>
        internal virtual bool ValidNewEvent => SSCRomanceMoodGroups.GroupOf(this) != SSCRomanceMoodGroup.None &&
            def.thoughtClass == GetType() && def.stackLimit == 1 && def.stackLimitForSameOtherPawn != 0 &&
            def.thoughtToMake == null && (def.replaceThoughts == null || def.replaceThoughts.Count == 0) && !def.showBubble;

        /// <summary>清除的是实例临时状态，绝不清除保存的快照、年龄或实际期限。</summary>
        internal void ResetRuntime() { CanApply = null; FrozenRequest = null; MergeResult = null; PendingPlan = null; Phase = default; CopiedPendingEvent = false; HistoricalRestore = false; }
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

        /// <summary>此时 TryGainMemory 已完成原版准入；新事件采用最新结果，历史恢复仍走独立整理。</summary>
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

}
