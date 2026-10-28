// AI 辅助生成：普通 PNA 的身体生产选择；进度与保存仍归原产出 HediffComp。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只管理普通 PNA 物品生产，不改变身体来源识别、性交供给或兔分身产出。</summary>
    public static class SSCPnaProductionUtility
    {
        public const string ProducerDefName = "SSC_Purple_HyperLactation";

        /// <summary>只读查找当前身体原产出组件；不补建记录，不把同 Def 的替代实例当作原 owner。</summary>
        public static HediffComp_SeverityProduct Find(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            foreach (Hediff record in pawn.health.hediffSet.hediffs)
            {
                if (record?.def?.defName != ProducerDefName || record.pawn != pawn) continue;
                var production = record.TryGetComp<HediffComp_SeverityProduct>();
                if (production != null && ReferenceEquals(production.parent, record)) return production;
            }
            return null;
        }

        /// <summary>仅取得保存选择；没有产出记录时沿原生产默认放行，不创建组件或人格字典键。</summary>
        public static bool GetProductionEnabled(Pawn pawn) => Find(pawn)?.ProductionEnabled ?? true;

        /// <summary>只读显示当前批次积累；与健康页沿用同一 Severity 百分比，暂停不会隐藏或改写进度。</summary>
        public static string GetStorageDetail(Pawn pawn)
        {
            var production = Find(pawn);
            if (production == null) return null;
            // 这是下一批物品的生产积累，不是性交供给或可消耗物品的库存。
            string progress = (production.parent.Severity * 100f).ToString("F0") + "%";
            return (production.ProductionEnabled ? "SSC_PnaProduction_Storage" : "SSC_PnaProduction_StorageDisabled").Translate(progress);
        }

        /// <summary>通用准入只检查存活身体与当前产出记录；营养和满进度结算仍由生产事件判断。</summary>
        internal static string ActivationFailure(Pawn pawn)
            => IsUnavailable(pawn, Find(pawn))
                ? SSCAbilityToggleUtility.Reason("Unavailable") : null;

        /// <summary>仅核对已捕获的身体生产记录，不在资格回调之后更换该请求的 owner。</summary>
        private static bool IsUnavailable(Pawn pawn, HediffComp_SeverityProduct production)
            => pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded || production == null;

        /// <summary>按实际管理范围检查编辑资格，不添加 SSC 身份、研究、营养或空壳门槛。</summary>
        public static bool CanSetProductionEnabled(Pawn pawn, bool enabled, out string reason)
            => ChangeReport(pawn, Find(pawn), out reason);

        /// <summary>只写当前身体组件的选择；同值请求不写入，不结算产品或改动原进度。</summary>
        public static bool TrySetProductionEnabled(Pawn pawn, bool enabled, out string reason)
            => TrySetProductionEnabled(pawn, enabled, Find(pawn), out reason);

        /// <summary>带原生产 owner 的权威写入口；资格变化不能让请求改写后来替换的新记录。</summary>
        internal static bool TrySetProductionEnabled(Pawn pawn, bool enabled, HediffComp_SeverityProduct production, out string reason)
        {
            if (!ChangeReport(pawn, production, out reason)) return false;
            if (production.ProductionEnabled != enabled) production.SetProductionEnabled(enabled);
            return true;
        }

        /// <summary>预检与权威写入口共享同一资格；保存／加载期间不接受更改。</summary>
        private static bool ChangeReport(Pawn pawn, HediffComp_SeverityProduct production, out string reason)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            { reason = SSCAbilityToggleUtility.Reason("Loading"); return false; }
            if (IsUnavailable(pawn, production))
            { reason = SSCAbilityToggleUtility.Reason("Unavailable"); return false; }
            var training = pawn.TryGetComp<CompSexSlaveTraining>();
            if (training == null)
            { reason = SSCAbilityToggleUtility.Reason("Unavailable"); return false; }
            // 与调教页入口一致，殖民地囚犯等可管理角色不要求阵营已经变为玩家。
            if (!SSCIdentityUtility.IsSupportedVanillaStatus(pawn) && !SSCSelfTrainingEligibility.IsEligible(pawn))
            { reason = SSCAbilityToggleUtility.Reason("PlayerOnly"); return false; }
            // 自我资格查询属于外部模块；完成后仍须是原训练组件和原产出 owner 才能写入。
            if (!ReferenceEquals(Find(pawn), production) || !ReferenceEquals(pawn.TryGetComp<CompSexSlaveTraining>(), training))
            { reason = SSCAbilityToggleUtility.Reason("Changed"); return false; }
            reason = null;
            return true;
        }
    }
}
