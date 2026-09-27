using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存一次死亡的事件快照；不记录历史攻击者，也不保留跨存档的击杀名单。</summary>
    public static class CombatantKillUtility
    {
        [ThreadStatic] private static Dictionary<Pawn, DeathEvent> activeDeaths;
        [ThreadStatic] private static int indirectDamageDepth;

        /// <summary>仅最外层死亡调用拥有结算权；嵌套和同一尸体的后续调用不生成新奖励。</summary>
        public static DeathEvent BeginDeath(Pawn victim, DamageInfo? damage)
        {
            if (victim == null || victim.Dead || (activeDeaths != null && activeDeaths.ContainsKey(victim))) return null;
            var record = new DeathEvent(victim);
            (activeDeaths ?? (activeDeaths = new Dictionary<Pawn, DeathEvent>())).Add(victim, record);
            // 先登记重入保护；额外体型采样失败不能阻止游戏自己的死亡处理。
            try
            {
                if (indirectDamageDepth > 0 || SSCMasterBondUtility.IsRedirectingDamage || !damage.HasValue) return record;
                DamageInfo info = damage.Value;
                Pawn killer = info.Instigator as Pawn;
                if (killer == null || killer == victim || info.Def == null || !info.Def.harmsHealth) return record;

                // 屠宰和处决共用 ExecutionCut，必须同时核对具体任务及任务目标。
                if (info.Def == DamageDefOf.ExecutionCut && killer.jobs?.curDriver is JobDriver_Slaughter &&
                    killer.CurJob?.targetA.Thing == victim) return record;

                CompSexSlaveTraining comp = killer.TryGetComp<CompSexSlaveTraining>();
                if (comp == null || comp.specializationType != SexSlaveSpecializationType.Combatant) return record;
                record.Killer = killer;
                record.BodySize = victim.BodySize;
                return record;
            }
            catch (Exception error)
            {
                record.Killer = null;
                Log.ErrorOnce("[SSC Combatant] Could not capture kill experience: " + error, 194706021);
                return record;
            }
        }

        /// <summary>燃烧等来源即便保留 Pawn instigator，也不能被当作直接攻击。</summary>
        public static IDisposable BeginIndirectDamage()
        {
            indirectDamageDepth++;
            return new IndirectDamageScope();
        }

        public sealed class DeathEvent : IDisposable
        {
            private readonly Pawn victim;
            internal Pawn Killer;
            internal float BodySize;
            private bool resolved, disposed;

            internal DeathEvent(Pawn victim) { this.victim = victim; }

            public void Complete()
            {
                if (disposed || resolved) return;
                resolved = true;
                if (!victim.Dead || Killer == null) return;
                CombatantSpecializationProgressUtility.TryGainProgress(Killer,
                    CombatantSpecializationProgressUtility.KillProgress(BodySize));
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                activeDeaths.Remove(victim);
            }
        }

        private sealed class IndirectDamageScope : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                indirectDamageDepth--;
            }
        }
    }
}
