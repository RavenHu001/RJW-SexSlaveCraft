// AI 辅助生成
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>普通心情记忆，不派生社交好感；同组跨目标最多一条，保留原版准入及显示倍率。</summary>
    public class Thought_MemorySSCRomance : Thought_Memory
    {
        public SSCRomanceMoodData Data;
        // 仅新事件准入期间使用的原上下文守卫；不保存、不进入凝胶或原生克隆副本。
        internal Func<bool> CanApply;
        /// <summary>正常存读档仅恢复历史，绝不调用事件发放或按新阶段重算。</summary>
        public override void ExposeData() { base.ExposeData(); Scribe_Deep.Look(ref Data, "sscRomanceMood"); }
        /// <summary>原版 PawnDuplicator 走 CopyFrom；必须复制自定义快照，不能共享可写引用。</summary>
        public override void CopyFrom(Thought_Memory other)
        { base.CopyFrom(other); Data = (other as Thought_MemorySSCRomance)?.Data?.Copy(); CanApply = null; }
        /// <summary>明确跨对象同组；不依赖尚未定稿的标签或关联人物来判定组别。</summary>
        public override bool GroupsWith(Thought other) => other is Thought_MemorySSCRomance && other.def == def;

        /// <summary>旧格式仍按原事件判定；新载体仅覆盖期限类型，不改变旧 Def 的组身份。</summary>
        protected virtual bool IsLongTermGroup => Data?.Kind == SSCRomanceMoodEvent.RelationshipLoss;

        /// <summary>此时 TryGainMemory 已完成原版准入；拒绝较弱事件时不续期，不先删后验。</summary>
        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            showBubble = false;
            // 原版 CanGetThought 及外部 Prefix 均可能替换人格；准入通过后再核对原载体。
            // 返回已合并可阻止新条目加入，同时保持旧记忆的数值与年龄完全不变。
            if (CanApply != null && !CanApply()) return true;
            var handler = pawn.needs.mood.thoughts.memories;
            var old = handler.Memories.OfType<Thought_MemorySSCRomance>().FirstOrDefault(m => m.def == def && !m.ShouldDiscard);
            if (old == null) return false;
            int oldValue = old.Data?.Value ?? (int)old.CurStage.baseMoodEffect;
            int newValue = Data?.Value ?? (int)CurStage.baseMoodEffect;
            bool loss = IsLongTermGroup;
            if (!SSCRomanceMoodRules.Replace(oldValue, newValue, loss)) return true;
            int remaining = old.DurationTicks - old.age;
            // 同等损失不得缩短仍有效的旧期限；更强损失按本次关系时长重新计时。
            if (loss && newValue == oldValue) durationTicksOverride = System.Math.Max(DurationTicks - age, remaining) + age;
            handler.RemoveMemory(old);
            return false;
        }
    }

    [DefOf]
    public static class SSCRomanceMoodDefOf
    {
        public static TraitDef Masochist;
        public static ThoughtDef SSC_Romance_Control, SSC_Romance_Experience, SSC_Romance_Loss;
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
