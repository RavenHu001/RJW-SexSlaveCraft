using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存实际解绑后效的唯一截止点。效果和再绑定都以游戏时间为准。</summary>
    public sealed class Hediff_SSCBondReleaseAftereffect : Hediff
    {
        internal const string DefName = "SSC_Hediff_BondReleaseAftereffect";
        // 暂挂的实例使用 -1。转换提交前才启用期限，读档和凝胶恢复则沿用已保存的绝对 tick。
        internal long ExpiresAt = -1;

        internal static HediffDef Definition => DefDatabase<HediffDef>.GetNamedSilentFail(DefName);
        internal static int Now => Current.Game?.tickManager?.TicksGame ?? -1;
        internal static long NewDeadline(int now) => (long)now + 2 * GenDate.TicksPerDay;
        internal static Hediff_SSCBondReleaseAftereffect Find(Pawn pawn) => Definition is HediffDef def
            ? pawn?.health?.hediffSet?.GetFirstHediffOfDef(def) as Hediff_SSCBondReleaseAftereffect : null;
        internal static long ActiveDeadline(Pawn pawn)
            => Find(pawn) is { } effect && effect.IsActiveAt(Now) ? effect.ExpiresAt : -1;

        // 角色离图或身处容器时，健康列表可能晚于截止点清理。属性、心情和准入仍立即失效。
        private bool IsActiveAt(int now) => now >= 0 && now < ExpiresAt;
        public override HediffStage CurStage => IsActiveAt(Now) ? base.CurStage : null;
        public override bool ShouldRemove => base.ShouldRemove ||
            (Scribe.mode == LoadSaveMode.Inactive && Now >= 0 && ExpiresAt >= 0 && !IsActiveAt(Now));
        // 提交和失败清理都以具体实例为依据，合并会把本次暂挂写进已有记录。
        public override bool TryMergeWith(Hediff other) => false;

        internal static string FormatRemaining(long deadline)
        {
            int remaining = (int)Math.Clamp(deadline - Now, 60L, int.MaxValue);
            return remaining < GenDate.TicksPerHour ? remaining.ToStringSecondsFromTicks("F0")
                : remaining.ToStringTicksToPeriod(shortForm: true, canUseDecimalsShortForm: true);
        }

        public override string LabelInBrackets
            => IsActiveAt(Now) ? FormatRemaining(ExpiresAt) : base.LabelInBrackets;

        internal void Activate(long deadline)
        {
            ExpiresAt = deadline;
            SSCSelfTrainingFeedback.NotifyStatusChanged(pawn);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            SSCSelfTrainingFeedback.NotifyStatusChanged(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ExpiresAt, "sscBondReleaseExpiresAt", -1L);
        }
    }

    public sealed class ThoughtWorker_SSCBondReleaseAftereffect : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
            => Hediff_SSCBondReleaseAftereffect.ActiveDeadline(p) >= 0;
    }
}
