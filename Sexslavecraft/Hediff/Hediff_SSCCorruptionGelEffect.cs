// AI 辅助生成：身体药效与原生倒计时，期限不借用Severity或人格能力字典。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class Hediff_SSCCorruptionGelEffect : HediffWithComps
    {
        private static readonly HediffStage NeutralStage = new HediffStage();
        private bool? observedActive;
        public HediffComp_Disappears Timer => this.TryGetComp<HediffComp_Disappears>();
        public override bool ShouldRemove => base.ShouldRemove || SSCIdentityUtility.IsMaster(pawn);

        /// <summary>原实例、期限与当前身份共同决定属性，外部迟到清理不能让主人短暂受药。</summary>
        public override HediffStage CurStage => SSCCorruptionGelEffectUtility.IsActive(this) ? base.CurStage : NeutralStage;

        /// <summary>同类全身药效只合并期限，不叠加严重度；实际续药通过共享入口精确刷新一天。</summary>
        public override bool TryMergeWith(Hediff other)
        {
            if (other is not Hediff_SSCCorruptionGelEffect incoming || other.def != def || other.Part != Part) return false;
            if (Timer != null && incoming.Timer != null && incoming.Timer.ticksToDisappear > Timer.ticksToDisappear)
                Timer.SetDuration(incoming.Timer.ticksToDisappear);
            Severity = 1f;
            RefreshEffects(true);
            return true;
        }

        /// <summary>添加／移除和真实有效性转换时清局部属性及思绪缓存，不按tick反复清缓存。</summary>
        internal void RefreshEffects(bool force = false)
        {
            bool active = SSCCorruptionGelEffectUtility.IsActive(this);
            if (!force && observedActive == active) return;
            observedActive = active;
            SSCCorruptionGelEffectUtility.ClearEffectCaches(pawn);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            Severity = 1f;
            RefreshEffects(true);
        }
        public override void PostRemoved() { base.PostRemoved(); RefreshEffects(true); }
        public override void PostTick() { base.PostTick(); RefreshEffects(); }
        public override void PostTickInterval(int delta) { base.PostTickInterval(delta); RefreshEffects(); }
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) observedActive = null;
        }
    }

    /// <summary>延续原生状态说明，但不把主人、空壳或已到期的尚未清理记录当作有效心情。</summary>
    public sealed class ThoughtWorker_SSCCorruptionGel : ThoughtWorker_Hediff
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
            => SSCCorruptionGelEffectUtility.IsActive(SSCCorruptionGelEffectUtility.Find(p))
                ? base.CurrentStateInternal(p) : ThoughtState.Inactive;
    }
}
