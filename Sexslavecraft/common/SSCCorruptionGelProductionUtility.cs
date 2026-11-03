// AI 辅助生成
using System;
using System.Linq;
using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>凝胶生产的身体资格与权威写入口；所有查询保持只读，缺记录不默认开启。</summary>
    public static class SSCCorruptionGelProductionUtility
    {
        public const string AbilityId = "SSC_CorruptionGelProduction";
        public const string ProducerDefName = "SSC_Hediff_CorruptionGelProduction";
        public static bool IsBodyStateDef(HediffDef def) => def?.defName == ProducerDefName;

        /// <summary>健康DirtyCache不清StatWorker临时缓存；精确失效本项工作属性，不刷新所有角色。</summary>
        internal static void ClearWorkSpeedCache(Pawn pawn)
        {
            if (pawn != null) DefDatabase<StatDef>.GetNamedSilentFail("WorkSpeedGlobal")?.Worker.ClearCacheForThing(pawn);
        }

        /// <summary>生命周期可能在移除后已找不到载体；仍清理原身体的工作属性缓存。</summary>
        internal static void NotifyEffectsChanged(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn == null) return;
            var owner = Find(pawn);
            if (owner != null) owner.RefreshEffects(true);
            else ClearWorkSpeedCache(pawn);
        }

        public static HediffComp_SSCCorruptionGelProduction Find(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            foreach (Hediff record in pawn.health.hediffSet.hediffs)
                if (record?.pawn == pawn && IsBodyStateDef(record.def) && record.TryGetComp<HediffComp_SSCCorruptionGelProduction>() is var comp &&
                    comp != null && ReferenceEquals(comp.parent, record))
                    return comp;
            return null;
        }

        /// <summary>校验当前身体的满级记录及实际器官；历史开发保留不能绕过缺失部位。</summary>
        private static bool HasCompletedAnus(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            foreach (Hediff record in pawn.health.hediffSet.hediffs)
            {
                if (record.pawn != pawn || record.def?.defName != "SSC_Exp_Anus" || !float.IsFinite(record.Severity) || record.Severity < 1f ||
                    record.Part == null || pawn.RaceProps?.body?.AllParts.Contains(record.Part) != true) continue;
                bool present = true;
                for (BodyPartRecord part = record.Part; part != null; part = part.parent)
                    if (pawn.health.hediffSet.PartIsMissing(part)) { present = false; break; }
                // 同部位必须仍有真实RJW肛门器官，不信任可能滞后的器官缓存。
                if (present && pawn.health.hediffSet.hediffs.Any(organ => organ.pawn == pawn &&
                    organ.Part == record.Part && Genital_Helper.is_anus(organ))) return true;
            }
            return false;
        }

        /// <summary>通用准入不包含位置、营养或当前恶堕值；位置仅影响实际积累和交付。</summary>
        public static string ActivationFailure(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded || Find(pawn) == null)
                return SSCAbilityToggleUtility.Reason("Unavailable");
            if (!SSCIdentityUtility.IsSexSlave(pawn)) return SSCAbilityToggleUtility.Reason("Identity");
            if (SSCMasterProgressionUtility.IsHollow(pawn)) return SSCAbilityToggleUtility.Reason("Hollow");
            if (SSCPersonalityRuntimeUtility.IsMutating(pawn)) return SSCAbilityToggleUtility.Reason("Mutating");
            return HasCompletedAnus(pawn) ? null : "SSC_CorruptionGel_AnusRequired".Translate().ToString();
        }

        /// <summary>完成阶段与完整读档共用幂等补授；不重置已有选择、进度，移除异常重复载体而不合并库存。</summary>
        public static void EnsureProducer(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded || !HasCompletedAnus(pawn)) return;
            var records = pawn.health.hediffSet.hediffs.Where(h => IsBodyStateDef(h.def)).ToList();
            if (records.Count == 0)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(ProducerDefName);
                if (def != null) pawn.health.AddHediff(def);
            }
            else foreach (Hediff duplicate in records.Skip(1)) pawn.health.RemoveHediff(duplicate);
        }

        /// <summary>失格时允许安全关闭，开启须复查当前资格；原组件替换后拒绝原请求。</summary>
        public static bool CanSet(Pawn pawn, bool enabled, out string reason)
            => ChangeReport(pawn, enabled, Find(pawn), out reason);

        private static bool ChangeReport(Pawn pawn, bool enabled, HediffComp_SSCCorruptionGelProduction owner, out string reason)
        {
            var training = pawn?.TryGetComp<CompSexSlaveTraining>();
            reason = null;
            if (Scribe.mode != LoadSaveMode.Inactive) reason = SSCAbilityToggleUtility.Reason("Loading");
            else if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded || owner == null || !ReferenceEquals(Find(pawn), owner))
                reason = SSCAbilityToggleUtility.Reason("Unavailable");
            else if (training == null ||
                (!SSCIdentityUtility.IsSupportedVanillaStatus(pawn) && !SSCSelfTrainingEligibility.IsEligible(pawn)))
                reason = SSCAbilityToggleUtility.Reason("PlayerOnly");
            else if (enabled) reason = ActivationFailure(pawn);
            if (reason == null && (!ReferenceEquals(Find(pawn), owner) || !ReferenceEquals(pawn.TryGetComp<CompSexSlaveTraining>(), training)))
                reason = SSCAbilityToggleUtility.Reason("Changed");
            return reason == null;
        }

        internal static bool TrySet(Pawn pawn, bool enabled, HediffComp_SSCCorruptionGelProduction owner, out string reason)
        {
            if (!ChangeReport(pawn, enabled, owner, out reason)) return false;
            if (owner.ProductionEnabled != enabled) owner.SetProductionEnabled(enabled);
            return true;
        }
    }

    /// <summary>阶段Refresh持续补授已完成角色；生产资格仍由实际消费者复查。</summary>
    public sealed class HediffGiver_SSCCorruptionGelProducer : HediffGiver
    {
        public override void OnIntervalPassed(Pawn pawn, Hediff cause) => SSCCorruptionGelProductionUtility.EnsureProducer(pawn);
    }

    /// <summary>所有引用恢复后补授旧档；不以显示查询写健康状态，也不增加全图Tick扫描。</summary>
    public sealed class SSCCorruptionGelGameComponent : GameComponent
    {
        public SSCCorruptionGelGameComponent(Game game) { }
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (Pawn pawn in SSCRestrictionGameComponent.AllPawns())
            {
                SSCCorruptionGelProductionUtility.EnsureProducer(pawn);
                SSCCorruptionGelEffectUtility.RefreshBodyState(pawn);
            }
        }
    }
}
