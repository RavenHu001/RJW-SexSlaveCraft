// AI 辅助生成：发作冷却迁移到身体 Hediff，满值入口的回落记录继续随组件存档。
using System;
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        private long legacySlaveBreakdownCooldownUntil;
        private Hediff_SSCSlaveBreakdownCooldown stagedSlaveBreakdownCooldown;
        internal bool SlaveBreakdownFullTriggerUsed;

        private Hediff_SSCSlaveBreakdownCooldown SlaveBreakdownCooldown
            => parent is Pawn pawn
                ? pawn.health?.hediffSet.hediffs.Find(hediff => hediff is Hediff_SSCSlaveBreakdownCooldown)
                    as Hediff_SSCSlaveBreakdownCooldown
                : null;

        private int LegacyCooldownRemainingTicks => SSCSlaveBreakdown.Now is var now && now >= 0
            ? (int)Math.Clamp(legacySlaveBreakdownCooldownUntil - now, 0L, int.MaxValue) : 0;

        // 健康添加的同步回调也必须读到冷却。已挂载的时长由原版组件保存。
        internal int SlaveBreakdownCooldownRemainingTicks => Math.Max(LegacyCooldownRemainingTicks,
            (stagedSlaveBreakdownCooldown ?? SlaveBreakdownCooldown)?.Timer?.ticksToDisappear ?? 0);

        internal void RecordSlaveBreakdownCooldown() => ApplySlaveBreakdownCooldown();

        internal void MigrateSlaveBreakdownCooldown()
        {
            if (legacySlaveBreakdownCooldownUntil == 0L || SSCSlaveBreakdown.Now < 0) return;
            int remaining = LegacyCooldownRemainingTicks;
            if (remaining == 0 || ApplySlaveBreakdownCooldown(remaining)) legacySlaveBreakdownCooldownUntil = 0;
        }

        private bool ApplySlaveBreakdownCooldown(int? legacyRemaining = null)
        {
            if (parent is not Pawn pawn || pawn.health == null || stagedSlaveBreakdownCooldown != null) return false;
            if (SlaveBreakdownCooldown?.Timer is { } timer)
            {
                timer.ticksToDisappear = legacyRemaining is int remaining
                    ? Math.Max(timer.ticksToDisappear, Math.Min(remaining, timer.disappearsAfterTicks))
                    : timer.disappearsAfterTicks;
                return true;
            }
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(Hediff_SSCSlaveBreakdownCooldown.DefName);
            if (def?.hediffClass != typeof(Hediff_SSCSlaveBreakdownCooldown)) return false;
            var marker = (Hediff_SSCSlaveBreakdownCooldown)HediffMaker.MakeHediff(def, pawn);
            if (marker.Timer is not { } newTimer) return false;
            if (legacyRemaining is int ticks) newTimer.ticksToDisappear = Math.Min(ticks, newTimer.ticksToDisappear);
            stagedSlaveBreakdownCooldown = marker;
            try
            {
                pawn.health.AddHediff(marker);
                return SlaveBreakdownCooldown == marker;
            }
            finally { stagedSlaveBreakdownCooldown = null; }
        }

        private void ExposeSlaveBreakdown()
        {
            Scribe_Values.Look(ref legacySlaveBreakdownCooldownUntil, "sscSlaveBreakdownCooldownUntil", 0L);
            Scribe_Values.Look(ref SlaveBreakdownFullTriggerUsed, "sscSlaveBreakdownFullTriggerUsed", false);
        }
    }
}
