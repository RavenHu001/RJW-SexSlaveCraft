// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCPnaExposureSource { Debug, OrdinaryExposure, Ingestion, Weapon }

    /// <summary>真实正暴露和钳制后的增量分开；P1 报告满值，但尚不建立 P2 的新依赖。</summary>
    public struct SSCPnaExposureResult
    {
        public bool Accepted;
        public bool Rooted;
        public float RequestedDose;
        public float AddedLoad;
        public float FinalLoad;
        public bool IsFull;
    }

    /// <summary>只管理 PNA 身体负荷，不读取身份、恶堕、主人开关或当前任务。</summary>
    public static class SSCPnaExposureUtility
    {
        public const string LoadDefName = "Hediff_PNA_Debuff";
        public const string RootedLoadDefName = "SSC_Hediff_PnaRootedLoad";
        public const string AddictionDefName = "PNA_Addiction";
        private static readonly HashSet<Pawn> protecting = new HashSet<Pawn>();

        public static bool IsLoadDef(HediffDef def) => def != null && def.defName == LoadDefName;
        public static bool IsRootedLoadDef(HediffDef def) => def != null && def.defName == RootedLoadDefName;
        public static bool IsBodyLoadDef(HediffDef def) => IsLoadDef(def) || IsRootedLoadDef(def);
        public static bool IsAddictionDef(HediffDef def) => def != null && def.defName == AddictionDefName;

        public static Hediff GetRecord(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs.FirstOrDefault(h => IsLoadDef(h?.def));

        public static Hediff GetRootedRecord(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs.FirstOrDefault(h => IsRootedLoadDef(h?.def));

        public static Hediff GetBodyRecord(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs.FirstOrDefault(h => IsBodyLoadDef(h?.def));

        public static float GetLoad(Pawn pawn) => SSCPnaMath.GetLoad(GetBodyRecord(pawn)?.Severity ?? 0f);

        /// <summary>零阶段有效链仍保护；失去目标引用的残链不被推断成绑定，查询不修复关系。</summary>
        public static bool IsRooted(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            // 重复链中的第一个可能是无效残留；任一真实有效引用即可保持原绑定保护。
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                var chain = hediff as Hediff_ChainOfSexSlave;
                Pawn master = chain?.LinkedPawn;
                if (master != null && !master.Destroyed && !master.Discarded) return true;
            }
            return false;
        }

        private static bool IsValidTarget(Pawn pawn)
            => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && !pawn.Suspended &&
               pawn.RaceProps?.IsFlesh == true && pawn.health?.hediffSet != null;

        /// <summary>各来源自行认领事件后调用；同 tick 的两个独立输入不会被合并或丢弃。</summary>
        public static SSCPnaExposureResult ApplyExposure(Pawn pawn, float dose, SSCPnaExposureSource source)
        {
            var result = new SSCPnaExposureResult();
            if (Scribe.mode != LoadSaveMode.Inactive || !IsValidTarget(pawn) || !SSCPnaMath.IsFinite(dose) || dose <= 0f ||
                source < SSCPnaExposureSource.Debug || source > SSCPnaExposureSource.Weapon) return result;
            result.Accepted = true;
            result.RequestedDose = dose;
            RefreshBodyState(pawn);
            Hediff record = GetBodyRecord(pawn);
            (record as HediffWithComps)?.GetComp<HediffComp_SSCPnaLoad>()?.SettlePending();
            // 健康回调可能移走旧实例或建立锁链；必须重新取得真实载体。
            if (!IsValidTarget(pawn)) { result.Accepted = false; return result; }
            RefreshBodyState(pawn);
            record = GetBodyRecord(pawn);
            float before = SSCPnaMath.NormalizeSeverity(record?.Severity ?? 0f);
            float after = SSCPnaMath.AddDose(before, dose);
            if (record == null)
            {
                HediffDef def = IsRooted(pawn) ? SSCDefOf.SSC_PnaRootedLoad : SSCDefOf.SSC_PnaLoad;
                if (def == null) { result.Accepted = false; return result; }
                record = HediffMaker.MakeHediff(def, pawn);
                // MakeHediff 的 Def 初值不构成另一份隐藏剂量。
                record.Severity = after;
                pawn.health.AddHediff(record);
            }
            else record.Severity = after;

            RefreshBodyState(pawn);
            if (!IsValidTarget(pawn) || GetBodyRecord(pawn) != record ||
                IsRootedLoadDef(record.def) != IsRooted(pawn))
            {
                result.Accepted = false;
                return result;
            }
            result.FinalLoad = SSCPnaMath.GetLoad(record.Severity);
            result.AddedLoad = System.Math.Max(0f, result.FinalLoad - before / SSCPnaMath.MaximumSeverity);
            result.IsFull = record.Severity >= SSCPnaMath.MaximumSeverity;
            result.Rooted = IsRootedLoadDef(record.def);
            return result;
        }

        /// <summary>实际链决定负荷形态；双向转换保留原实例、原始严重度及半批时间，绑定只清依赖。</summary>
        public static void RefreshBodyState(Pawn pawn, bool forceRefresh = false)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null || !protecting.Add(pawn)) return;
            try
            {
                // 健康通知可能再次改链；有限重评估避免简单回调使最终形态错配或无限递归。
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    bool rooted = IsRooted(pawn);
                    if (rooted)
                    {
                        var addictions = pawn.health.hediffSet.hediffs.Where(h => IsAddictionDef(h?.def)).ToList();
                        foreach (Hediff addiction in addictions)
                            pawn.health.RemoveHediff(addiction);
                        // 稳态健康更新不反复重建全部需求；绑定／初始化强制刷新，清依赖后即时撤销需求。
                        if (addictions.Count > 0 || forceRefresh) pawn.needs?.AddOrRemoveNeedsAsAppropriate();
                    }
                    var records = pawn.health.hediffSet.hediffs.Where(h => IsBodyLoadDef(h?.def)).ToList();
                    if (records.Count == 0) return;
                    // 重复身体记录只取最大合法单份，不相加。两种 Def 均使用同一类和同一清除组件。
                    Hediff keep = records.OrderByDescending(h => SSCPnaMath.NormalizeSeverity(h.Severity)).First();
                    foreach (Hediff duplicate in records.Where(h => h != keep)) pawn.health.RemoveHediff(duplicate);
                    float severity = SSCPnaMath.NormalizeSeverity(keep.Severity);
                    if (severity <= SSCPnaMath.RemovalSeverity) { pawn.health.RemoveHediff(keep); return; }
                    rooted = IsRooted(pawn);
                    HediffDef desired = rooted ? SSCDefOf.SSC_PnaRootedLoad : SSCDefOf.SSC_PnaLoad;
                    if (desired == null || !pawn.health.hediffSet.hediffs.Contains(keep)) return;
                    bool changed = keep.def != desired || keep.Severity != severity || records.Count > 1;
                    // 原位换 Def 保留 loadID、组件和未结算时间；不先移除再创建，不增加隐藏初值。
                    keep.def = desired;
                    keep.Severity = severity;
                    if (changed || forceRefresh) pawn.health.Notify_HediffChanged(keep);
                    if (rooted == IsRooted(pawn)) return;
                }
            }
            finally { protecting.Remove(pawn); }
        }

        /// <summary>调试清负荷不会清普通对象已有依赖；旧依赖行为仍归 P2 重做。</summary>
        public static void ClearLoad(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null) return;
            foreach (Hediff record in pawn.health.hediffSet.hediffs.Where(h => IsBodyLoadDef(h?.def)).ToList())
                pawn.health.RemoveHediff(record);
        }
    }
}
