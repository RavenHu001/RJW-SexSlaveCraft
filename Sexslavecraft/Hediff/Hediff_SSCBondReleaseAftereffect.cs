using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原版组件负责计时。保留原类型以便读取仍在角色身上的旧后效。</summary>
    public sealed class Hediff_SSCBondReleaseAftereffect : HediffWithComps
    {
        internal const string DefName = "SSC_Hediff_BondReleaseAftereffect";
        private long legacyExpiresAt = -1;

        internal static HediffDef Definition => DefDatabase<HediffDef>.GetNamedSilentFail(DefName);
        internal static Hediff_SSCBondReleaseAftereffect Find(Pawn pawn) => Definition is HediffDef def
            ? pawn?.health?.hediffSet?.GetFirstHediffOfDef(def) as Hediff_SSCBondReleaseAftereffect : null;
        internal static Hediff_SSCBondReleaseAftereffect Active(Pawn pawn)
            => Find(pawn) is { ShouldRemove: false } effect ? effect : null;

        // 提交和失败清理都以具体实例为依据，合并会把本次暂挂写进已有记录。
        public override bool TryMergeWith(Hediff other) => false;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
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
            // 只读取旧存档字段。后续保存由 Disappears 组件写入剩余 tick。
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyExpiresAt, "sscBondReleaseExpiresAt", -1L);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && legacyExpiresAt >= 0 &&
                GetComp<HediffComp_Disappears>() is { } timer && Current.Game?.tickManager is { } clock)
            {
                timer.disappearsAfterTicks = timer.Props.disappearsAfterTicks.max;
                timer.ticksToDisappear = (int)Math.Clamp(legacyExpiresAt - clock.TicksGame, 0L, timer.disappearsAfterTicks);
                legacyExpiresAt = -1;
            }
        }
    }
}
