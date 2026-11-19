// AI 辅助生成：发作冷却迁移到身体 Hediff，满值入口的回落记录继续随组件存档。
using System;
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        private long legacySlaveBreakdownCooldownUntil;
        internal bool SlaveBreakdownFullTriggerUsed;

        private Hediff_SSCSlaveBreakdownCooldown SlaveBreakdownCooldown
            => parent is Pawn pawn
                ? pawn.health?.hediffSet.hediffs.Find(hediff => hediff is Hediff_SSCSlaveBreakdownCooldown)
                    as Hediff_SSCSlaveBreakdownCooldown
                : null;

        // 旧档在完整加载前仍可读取原截止时间。新记录只保存在 Hediff。
        internal long SlaveBreakdownCooldownUntil
            => Math.Max(legacySlaveBreakdownCooldownUntil, SlaveBreakdownCooldown?.ExpiresAt ?? 0L);

        internal void RecordSlaveBreakdownCooldown(long expiresAt)
        {
            if (parent is not Pawn pawn || pawn.health == null) return;
            // 死亡清理期间保留截止时间，身体恢复后再安装标记。
            legacySlaveBreakdownCooldownUntil = Math.Max(legacySlaveBreakdownCooldownUntil, expiresAt);
            MigrateSlaveBreakdownCooldown();
        }

        internal void MigrateSlaveBreakdownCooldown()
        {
            if (legacySlaveBreakdownCooldownUntil == 0L || SSCSlaveBreakdown.Now < 0) return;
            if (legacySlaveBreakdownCooldownUntil <= SSCSlaveBreakdown.Now)
            {
                legacySlaveBreakdownCooldownUntil = 0L;
                return;
            }
            if (parent is not Pawn pawn || pawn.Dead || pawn.health == null) return;
            if (SlaveBreakdownCooldown is Hediff_SSCSlaveBreakdownCooldown cooldown)
            {
                cooldown.ExpiresAt = Math.Max(cooldown.ExpiresAt, legacySlaveBreakdownCooldownUntil);
                legacySlaveBreakdownCooldownUntil = 0L;
                return;
            }
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(Hediff_SSCSlaveBreakdownCooldown.DefName);
            if (def?.hediffClass != typeof(Hediff_SSCSlaveBreakdownCooldown)) return;
            var marker = (Hediff_SSCSlaveBreakdownCooldown)HediffMaker.MakeHediff(def, pawn);
            marker.ExpiresAt = legacySlaveBreakdownCooldownUntil;
            pawn.health.AddHediff(marker);
            if (SlaveBreakdownCooldown == marker) legacySlaveBreakdownCooldownUntil = 0L;
        }

        private void ExposeSlaveBreakdown()
        {
            Scribe_Values.Look(ref legacySlaveBreakdownCooldownUntil, "sscSlaveBreakdownCooldownUntil", 0L);
            Scribe_Values.Look(ref SlaveBreakdownFullTriggerUsed, "sscSlaveBreakdownFullTriggerUsed", false);
        }
    }
}
