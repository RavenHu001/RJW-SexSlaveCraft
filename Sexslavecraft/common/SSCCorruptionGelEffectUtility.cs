// AI 辅助生成：普通角色与性奴共用身体受药入口，不创建需求或复用PNA化学规则。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class SSCCorruptionGelEffectUtility
    {
        public const string EffectDefName = "SSC_Hediff_CorruptionGelEffect";
        private static readonly HashSet<Pawn> refreshing = new HashSet<Pawn>();

        public static bool IsEffectDef(HediffDef def) => IsEffectDef(def?.defName);
        public static bool IsEffectDef(string name) => name == EffectDefName;
        public static Hediff_SSCCorruptionGelEffect Find(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs.FirstOrDefault(h => h.pawn == pawn && IsEffectDef(h.def)) as Hediff_SSCCorruptionGelEffect;

        /// <summary>首版沿类人服药范围，不要求SSC身份、锁链、生产能力或恶堕／心情需求。</summary>
        private static bool IsValidBody(Pawn pawn) => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded &&
            pawn.RaceProps?.Humanlike == true && pawn.health?.hediffSet?.pawn == pawn;
        internal static bool CanReceive(Pawn pawn) => IsValidBody(pawn) && !SSCIdentityUtility.IsMaster(pawn) &&
            !SSCMasterProgressionUtility.IsHollow(pawn) && !SSCPersonalityRuntimeUtility.IsMutating(pawn);

        /// <summary>只读有效性，不在属性／思绪getter创建、删除或刷新记录。</summary>
        public static bool IsActive(Hediff_SSCCorruptionGelEffect effect) => Scribe.mode == LoadSaveMode.Inactive && effect != null &&
            CanReceive(effect.pawn) && ReferenceEquals(Find(effect.pawn), effect) && effect.Timer?.ticksToDisappear > 0;

        internal static void ClearEffectCaches(Pawn pawn)
        {
            SSCCorruptionGelProductionUtility.ClearWorkSpeedCache(pawn);
            pawn?.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
        }

        /// <summary>健康回调可以重入；只协调原健康载体，有限重评估，不把旧请求续接到换入身体。</summary>
        public static void RefreshBodyState(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null ||
                !pawn.health.hediffSet.hediffs.Any(h => IsEffectDef(h.def)) || !refreshing.Add(pawn)) return;
            Pawn_HealthTracker health = pawn.health;
            try
            {
                for (int attempt = 0; attempt < 3 && ReferenceEquals(pawn.health, health); attempt++)
                {
                    var records = health.hediffSet.hediffs.Where(h => h.pawn == pawn && IsEffectDef(h.def)).ToList();
                    if (records.Count == 0) return;
                    bool immune = SSCIdentityUtility.IsMaster(pawn);
                    // 异常重复不相加；保留剩余期限最长的合法实例。新生克隆另行拒绝来源药效。
                    var keep = immune ? null : records.OfType<Hediff_SSCCorruptionGelEffect>()
                        .Where(h => h.Timer?.ticksToDisappear > 0).OrderByDescending(h => h.Timer.ticksToDisappear).FirstOrDefault();
                    bool removed = false;
                    foreach (Hediff record in records)
                    {
                        if (!ReferenceEquals(pawn.health, health) || SSCIdentityUtility.IsMaster(pawn) != immune) break;
                        if (record == keep || !health.hediffSet.hediffs.Contains(record)) continue;
                        health.RemoveHediff(record);
                        removed = true;
                    }
                    if (!ReferenceEquals(pawn.health, health)) return;
                    if (SSCIdentityUtility.IsMaster(pawn) != immune || removed) continue;
                    if (keep != null && ReferenceEquals(Find(pawn), keep)) keep.RefreshEffects(true);
                    return;
                }
            }
            finally { refreshing.Remove(pawn); ClearEffectCaches(pawn); }
        }

        /// <summary>实际摄入才应用；制作与添加回调后核对原身体、人格、训练载体与药效owner。</summary>
        public static bool Apply(Pawn pawn, HediffDef effectDef)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || !IsValidBody(pawn)) return false;
            if (SSCIdentityUtility.IsMaster(pawn)) { RefreshBodyState(pawn); return false; }
            var timerProps = effectDef?.comps?.OfType<HediffCompProperties_Disappears>().FirstOrDefault();
            if (!CanReceive(pawn) || !IsEffectDef(effectDef) || effectDef.hediffClass != typeof(Hediff_SSCCorruptionGelEffect) ||
                timerProps == null || timerProps.disappearsAfterTicks.min <= 0 ||
                timerProps.disappearsAfterTicks.min != timerProps.disappearsAfterTicks.max) return false;
            Pawn_HealthTracker health = pawn.health;
            var training = pawn.TryGetComp<CompSexSlaveTraining>();
            object personality = SSCPersonalityRuntimeUtility.Capture(pawn);
            Hediff_SSCCorruptionGelEffect effect = Find(pawn);
            bool created = effect == null, applied = false;
            bool Current() => ReferenceEquals(pawn.health, health) && ReferenceEquals(pawn.TryGetComp<CompSexSlaveTraining>(), training) &&
                SSCPersonalityRuntimeUtility.IsCurrent(pawn, personality) && CanReceive(pawn);
            try
            {
                if (created)
                {
                    effect = HediffMaker.MakeHediff(effectDef, pawn) as Hediff_SSCCorruptionGelEffect;
                    if (!Current() || effect?.Timer == null || Find(pawn) != null) return false;
                    health.AddHediff(effect);
                }
                if (!Current() || effect?.Timer == null || !ReferenceEquals(Find(pawn), effect)) return false;
                effect.Severity = 1f;
                if (!Current() || !ReferenceEquals(Find(pawn), effect)) return false;
                effect.Timer.SetDuration(timerProps.disappearsAfterTicks.max);
                applied = true;
                RefreshBodyState(pawn);
                return true;
            }
            finally
            {
                // 拒绝旧请求时只撤销它刚添加且仍在原身体上的实例，不删除回调换入的新owner。
                if (!applied && created && effect != null && ReferenceEquals(pawn.health, health) && health.hediffSet.hediffs.Contains(effect))
                    health.RemoveHediff(effect);
            }
        }
    }
}
