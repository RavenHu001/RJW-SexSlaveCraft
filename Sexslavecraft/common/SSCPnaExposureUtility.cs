// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // 来源只决定需求补充规则；事件认领由调用方完成，核心不读取来源任务。
    public enum SSCPnaExposureSource { Debug, OrdinaryExposure, Ingestion, Weapon }

    /// <summary>真实正暴露和钳制后的增量分开；依赖只由成功的满值正输入建立。</summary>
    public struct SSCPnaExposureResult
    {
        // Accepted 表示处理成功；Rooted 表示最终按有效锁链保护分流。
        public bool Accepted;
        public bool Rooted;
        // 剂量采用 0～1 负荷尺度；真实请求与上限钳制后的实际增加量分别报告。
        public float RequestedDose;
        public float AddedLoad;
        public float FinalLoad;
        // 满值不等于首次依赖；重复满值及有链满值都可能不创建新依赖。
        public bool IsFull;
        public bool AddictionCreated;
    }

    /// <summary>管理 PNA 身体负荷、依赖及需求补充，不读取身份、恶堕、主人开关或当前任务。</summary>
    public static class SSCPnaExposureUtility
    {
        // 保留既有 DefName 以兼容存档；普通与性奴形态共用一份身体负荷。
        public const string LoadDefName = "Hediff_PNA_Debuff";
        public const string RootedLoadDefName = "SSC_Hediff_PnaRootedLoad";
        public const string AddictionDefName = "PNA_Addiction";
        public const string NeedDefName = "Need_PNA";
        // 仅在协调调用期间防止健康回调重入，退出时释放，不保存或缓存角色状态。
        private static readonly HashSet<Pawn> protecting = new HashSet<Pawn>();

        // 静态分类也用于人格标签过滤，避免身体 PNA 状态进入人格载荷。
        public static bool IsLoadDef(HediffDef def) => def != null && def.defName == LoadDefName;
        public static bool IsRootedLoadDef(HediffDef def) => def != null && def.defName == RootedLoadDefName;
        public static bool IsBodyLoadDef(HediffDef def) => IsLoadDef(def) || IsRootedLoadDef(def);
        public static bool IsAddictionDef(HediffDef def) => def != null && def.defName == AddictionDefName;
        public static bool IsBodyStateDef(HediffDef def) => IsBodyLoadDef(def) || IsAddictionDef(def);

        // 以下查询只读取当前健康／需求实例，不补算清除、不创建需求或依赖。
        public static Hediff GetAddiction(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs.FirstOrDefault(h => IsAddictionDef(h?.def));

        public static Need_Chemical GetNeed(Pawn pawn)
            => SSCDefOf.SSC_PnaNeed == null ? null : pawn?.needs?.TryGetNeed(SSCDefOf.SSC_PnaNeed) as Need_Chemical;

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

        /// <summary>统一输入准入：只处理可正常更新健康状态的存活肉体。</summary>
        internal static bool IsValidTarget(Pawn pawn)
            => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && !pawn.Suspended &&
               pawn.RaceProps?.IsFlesh == true && pawn.health?.hediffSet != null;

        /// <summary>各来源自行认领事件后调用；同 tick 的两个独立输入不会被合并或丢弃。</summary>
        public static SSCPnaExposureResult ApplyExposure(Pawn pawn, float dose, SSCPnaExposureSource source, bool refillIngestionNeed = true)
        {
            // 1. 校验真实正剂量、身体与来源；存读档阶段不能把恢复过程当成新暴露。
            var result = new SSCPnaExposureResult();
            if (Scribe.mode != LoadSaveMode.Inactive || !IsValidTarget(pawn) || !SSCPnaMath.IsFinite(dose) || dose <= 0f ||
                source < SSCPnaExposureSource.Debug || source > SSCPnaExposureSource.Weapon) return result;
            result.Accepted = true;
            result.RequestedDose = dose;

            // 2. 先协调普通／性奴形态并结算已累计的有效健康时间，再处理本次剂量。
            // 不用全局 tick 追补暂停时间，也不让新剂量承担输入前那段清除。
            RefreshBodyState(pawn);
            Hediff record = GetBodyRecord(pawn);
            (record as HediffWithComps)?.GetComp<HediffComp_SSCPnaLoad>()?.SettlePending();
            // 健康回调可能移走旧实例或建立锁链；必须重新取得真实载体。
            if (!IsValidTarget(pawn)) { result.Accepted = false; return result; }
            RefreshBodyState(pawn);
            record = GetBodyRecord(pawn);

            // 3. 在原始严重度尺度上加量并钳制；无记录时精确写入计算值。
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

            // 4. 严重度写入可能触发健康通知，重新确认载体仍存在且链分流一致。
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
            Hediff createdAddiction = null;

            // 5. 只有普通身体的满值正输入可以新建依赖；已有实例不重复创建，服药增加由原版 CompDrug 处理。
            // 原版依赖需要需求管理器，但不要求一定具备合法的 PNA 需求条。
            if (!result.Rooted)
            {
                Need_Chemical previousNeed = GetNeed(pawn);
                if (result.IsFull && pawn.needs != null && GetAddiction(pawn) == null && SSCDefOf.SSC_PnaAddiction != null)
                {
                    Hediff addiction = HediffMaker.MakeHediff(SSCDefOf.SSC_PnaAddiction, pawn);
                    createdAddiction = addiction;
                    addiction.Severity = 1f;
                    pawn.health.AddHediff(addiction);
                    // AddHediff 的第三方回调可能同时绑定或移走记录，不能保留无效的新依赖。
                    RefreshBodyState(pawn);
                    if (!IsValidTarget(pawn) || IsRooted(pawn) || GetBodyRecord(pawn) != record ||
                        record.Severity < SSCPnaMath.MaximumSeverity)
                    {
                        if (pawn.health?.hediffSet?.hediffs.Contains(addiction) == true) pawn.health.RemoveHediff(addiction);
                    }
                    else result.AddictionCreated = pawn.health.hediffSet.hediffs.Contains(addiction);
                    if (result.AddictionCreated)
                    {
                        // 由原版重新评估需求准入，只将本次首次生成的需求设满。
                        // 已有需求值留给下方来源补充处理，不能因新依赖而额外灌满。
                        pawn.needs?.AddOrRemoveNeedsAsAppropriate();
                        Need_Chemical createdNeed = GetNeed(pawn);
                        if (previousNeed == null && createdNeed != null) createdNeed.CurLevel = createdNeed.MaxLevel;
                    }
                }
                // 6. 原版服药已补充需求时不再重复加量；首次摄入与独立来源仍在这里补充。
                // 摄入 +1、普通侵蚀 +0.12，调试不额外补充。
                // 枪械按钳制后的实际增量补充；满值输入可建依赖，但不虚构负荷增量。
                float refill = source == SSCPnaExposureSource.Ingestion ? (refillIngestionNeed ? 1f : 0f) :
                    source == SSCPnaExposureSource.OrdinaryExposure ? .12f :
                    source == SSCPnaExposureSource.Weapon ? result.AddedLoad * 2f : 0f;
                Need_Chemical need = GetNeed(pawn);
                if (IsValidTarget(pawn) && !IsRooted(pawn) && need != null && refill > 0f)
                    need.CurLevel = System.Math.Min(need.MaxLevel, need.CurLevel + refill);
            }
            // 7. 依赖和需求变更后再次核对身体；失效清理只撤销本次新依赖。
            // 有链依赖清除仍由协调方法处理，已执行的身体加量不在此回滚。
            RefreshBodyState(pawn);
            if (!IsValidTarget(pawn) || GetBodyRecord(pawn) != record ||
                IsRootedLoadDef(record.def) != IsRooted(pawn))
            {
                if (createdAddiction != null && pawn.health?.hediffSet?.hediffs.Contains(createdAddiction) == true)
                    pawn.health.RemoveHediff(createdAddiction);
                result.Accepted = false;
                result.AddictionCreated = false;
                return result;
            }
            result.FinalLoad = SSCPnaMath.GetLoad(record.Severity);
            result.IsFull = record.Severity >= SSCPnaMath.MaximumSeverity;
            result.Rooted = IsRooted(pawn);
            result.AddictionCreated &= !result.Rooted && GetAddiction(pawn) != null;
            // 返回实际最终状态，只有仍有效的首次依赖向玩家发送一次通知。
            if (result.AddictionCreated && PawnUtility.ShouldSendNotificationAbout(pawn))
                Messages.Message("SSC_Pna_NewDependence".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeHealthEvent, true);
            return result;
        }

        /// <summary>旧档只保留最大合法单份进度；不相加、不灌满需求、不从满负荷补建。</summary>
        public static void NormalizeAddiction(Pawn pawn)
        {
            // 完整引用恢复后才执行；没有旧依赖时直接结束，不依据负荷推断历史成瘾。
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null) return;
            var records = pawn.health.hediffSet.hediffs.Where(h => IsAddictionDef(h?.def)).ToList();
            if (records.Count == 0) return;
            // 重复记录取最大合法 D，保留一份实际实例，避免相加制造完整依赖。
            Hediff keep = records.OrderByDescending(h => NormalizeDependency(h.Severity)).First();
            foreach (Hediff duplicate in records.Where(h => h != keep)) pawn.health.RemoveHediff(duplicate);
            float value = NormalizeDependency(keep.Severity);
            if (value <= 0f) pawn.health.RemoveHediff(keep);
            else keep.Severity = value;
            // 原版组件会保存速率；迁移只校正到当前 XML 速率，不改变已经恢复的 D。
            var recovery = (keep as HediffWithComps)?.GetComp<HediffComp_SeverityPerDay>();
            var recoveryProps = keep.def.CompProps<HediffCompProperties_SeverityPerDay>();
            if (value > 0f && recovery != null && recoveryProps != null)
                recovery.severityPerDay = recoveryProps.severityPerDay;
            // 交给原版更新需求存在性；已有满足度只做范围清理，不重置到新依赖初值。
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
            Need_Chemical need = GetNeed(pawn);
            if (need != null) need.CurLevel = SSCPnaMath.IsFinite(need.CurLevel)
                ? System.Math.Max(0f, System.Math.Min(need.MaxLevel, need.CurLevel)) : 0f;
        }

        /// <summary>D 仅表示剩余依赖进度；非有限值归零，有限值限制在 0～1。</summary>
        private static float NormalizeDependency(float value)
            => SSCPnaMath.IsFinite(value) ? System.Math.Max(0f, System.Math.Min(1f, value)) : 0f;

        /// <summary>实际链决定负荷形态；双向转换保留原实例、原始严重度及半批时间，绑定只清依赖。</summary>
        public static void RefreshBodyState(Pawn pawn, bool forceRefresh = false)
        {
            // 同一身体的健康通知可能同步回到此方法；重入调用由外层协调处理。
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null || !protecting.Add(pawn)) return;
            try
            {
                // 健康通知可能再次改链；有限重评估避免简单回调使最终形态错配或无限递归。
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    bool rooted = IsRooted(pawn);
                    if (rooted)
                    {
                        // 有效链直接清除普通依赖，需求撤销仍遵循原版准入刷新。
                        var addictions = pawn.health.hediffSet.hediffs.Where(h => IsAddictionDef(h?.def)).ToList();
                        foreach (Hediff addiction in addictions)
                            pawn.health.RemoveHediff(addiction);
                        // 稳态健康更新不反复重建全部需求；绑定／初始化强制刷新，清依赖后即时撤销需求。
                        if (addictions.Count > 0 || forceRefresh) pawn.needs?.AddOrRemoveNeedsAsAppropriate();
                    }
                    // 两种负荷形态一起去重；移除极低／非法负荷后才确定应保留的形态。
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
            // 正常返回、提前结束及异常都释放调用标记，避免后续输入永远被挡住。
            finally { protecting.Remove(pawn); }
        }

        /// <summary>调试清负荷不会清普通对象已有依赖，依赖继续按原版组件恢复。</summary>
        public static void ClearLoad(Pawn pawn)
        {
            // 同时覆盖普通／性奴形态，只移除身体负荷；依赖通过原版严重度衰减退出。
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null) return;
            foreach (Hediff record in pawn.health.hediffSet.hediffs.Where(h => IsBodyLoadDef(h?.def)).ToList())
                pawn.health.RemoveHediff(record);
        }
    }
}
