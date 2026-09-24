using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>在人格凝胶中保存和恢复训导官的互斥健康状态标签。</summary>
    /// <remarks>方向及各方向进度仍由 CompSexSlaveTraining 的人格快照保存；这里仅搬运当前普通状态和终极化事实。</remarks>
    public static class TrainerSpecializationGelUtility
    {
        private const float InitialOrdinarySeverity = 0.01f;

        /// <summary>判断一个定义是否属于训导官的普通、有效终极或禁用终极标签。</summary>
        public static bool IsTrainerTag(HediffDef def)
        {
            // 空定义既不能作为标签读取，也不能传给人格凝胶的 Dictionary 查询。
            // 显式判断可使缺失 Def 的坏档安全跳过，而不影响其他方向标签。
            return def != null && (def == SSCDefOf.SSC_Hediff_TrainerOfficer ||
                def == SSCDefOf.SSC_Hediff_TrainerOfficer_Final ||
                def == SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
        }

        /// <summary>从角色状态显式保存一个训导官标签，覆盖配方枚举可能留下的旧标签。</summary>
        public static void StoreExclusiveTrainerTags(CompPersonalityStore store, Pawn pawn)
        {
            if (store == null) return;

            // 禁用终极标记没有配方产出，通用的配方枚举不会保存它。
            // 先清空三个同系键，再从角色身上选出一个权威状态；这样复制或
            // 重复保存凝胶时也不会保留以前的普通/终极混合快照。
            RemoveStoredTag(store, SSCDefOf.SSC_Hediff_TrainerOfficer);
            RemoveStoredTag(store, SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            RemoveStoredTag(store, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
            if (pawn?.health?.hediffSet == null) return;

            // 双终极标记属于待维护的异常状态。保存时保守选择禁用记录，
            // 后续植入会依据接收身体的完整持续条件重新决定能否生效。
            Hediff disabled = GetMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
            if (disabled != null)
            {
                store.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled, NormalizeFinalSeverity(disabled.Severity));
                return;
            }

            Hediff active = GetMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            if (active != null)
            {
                store.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final, NormalizeFinalSeverity(active.Severity));
                return;
            }

            // 普通标记只代表当前培养方向。残留标记不能让一个已切换到
            // 其他方向的人格在恢复时被生命周期自动认领回训导官。
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp?.specializationType != SexSlaveSpecializationType.TrainerOfficer) return;
            Hediff ordinary = GetMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer);
            if (ordinary != null)
            {
                store.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer,
                    NormalizeOrdinarySeverity(comp.specializationProgress));
            }
        }

        /// <summary>清除接收身体原有的全部训导官标记，包含重复实例。</summary>
        public static void RemoveAllTrainerStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            // 清理必须发生在源人格进度恢复之前，否则普通 Hediff 的严重度
            // 会被生命周期中的“取较大值”规则并进新的人格历史。
            RemoveAll(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer);
            RemoveAll(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            RemoveAll(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
        }

        /// <summary>只恢复凝胶保存的一个状态，终极化后的生效条件留给外层恢复作用域结束时对账。</summary>
        public static void ApplyExclusiveTrainerTags(Pawn pawn, CompPersonalityStore data)
        {
            if (pawn?.health?.hediffSet == null || data == null) return;

            // 即使旧凝胶没有标签表，也要保持接收身体中同系标签为空。
            // 与植入前清理形成双保险，使此方法单独调用时仍遵守互斥规则。
            RemoveAllTrainerStates(pawn);
            if (data.hediffTags == null) return;

            // 完成事实独立于当前培养方向。禁用与有效标记同时存在时，
            // 暂按禁用恢复；外层 RestoreScope 会在身份与绑定完整后归并。
            if (HasStoredTag(data, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled))
            {
                ApplyTag(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled,
                    NormalizeFinalSeverity(data.GetTagSeverity(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled)));
                return;
            }
            if (HasStoredTag(data, SSCDefOf.SSC_Hediff_TrainerOfficer_Final))
            {
                ApplyTag(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final,
                    NormalizeFinalSeverity(data.GetTagSeverity(SSCDefOf.SSC_Hediff_TrainerOfficer_Final)));
                return;
            }

            // 组件当前进度是普通培养的权威值；标签只表示当前方向确实
            // 处于普通培养中。旧凝胶的历史进度或过高 Hediff 严重度不能
            // 在这里自动选择方向，也不能覆盖刚整体导入的源人格进度。
            if (data.specializationType == SexSlaveSpecializationType.TrainerOfficer &&
                HasStoredTag(data, SSCDefOf.SSC_Hediff_TrainerOfficer))
            {
                ApplyTag(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer,
                    NormalizeOrdinarySeverity(data.specializationProgress));
            }
        }

        /// <summary>只在定义存在时访问人格标签字典。</summary>
        private static bool HasStoredTag(CompPersonalityStore data, HediffDef def)
        {
            return def != null && data.HasTag(def);
        }

        /// <summary>只在定义存在时移除旧标签，避免空 Def 成为字典键。</summary>
        private static void RemoveStoredTag(CompPersonalityStore store, HediffDef def)
        {
            if (def != null) store.RemoveTag(def);
        }

        /// <summary>只读取现有健康状态，定义缺失时视为不存在。</summary>
        private static Hediff GetMarker(Pawn pawn, HediffDef def)
        {
            return def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
        }

        /// <summary>移除同一标签的所有残留实例，避免其中一个被重新认领。</summary>
        private static void RemoveAll(Pawn pawn, HediffDef def)
        {
            if (def == null) return;
            Hediff existing;
            while ((existing = pawn.health.hediffSet.GetFirstHediffOfDef(def)) != null)
                pawn.health.RemoveHediff(existing);
        }

        /// <summary>添加互斥标签并恢复经过校验的严重度。</summary>
        private static void ApplyTag(Pawn pawn, HediffDef def, float severity)
        {
            if (def == null) return;
            Hediff added = pawn.health.AddHediff(def);
            if (added != null) added.Severity = severity;
        }

        /// <summary>普通标签显示从 0.01 开始；损坏值不能污染导入的训练进度。</summary>
        private static float NormalizeOrdinarySeverity(float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress)) return InitialOrdinarySeverity;
            return Math.Max(InitialOrdinarySeverity, Math.Min(1f, progress));
        }

        /// <summary>终极标记只用存在性表示完成，异常严重度改为安全的默认值。</summary>
        private static float NormalizeFinalSeverity(float severity)
        {
            return float.IsNaN(severity) || float.IsInfinity(severity) || severity <= 0f ? 1f : severity;
        }
    }
}
