// AI 辅助生成
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // 显示层只把两种负荷的原始严重度换算为百分比，不改保存值或当前阶段。
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.SeverityLabel), MethodType.Getter)]
    internal static class SSCPnaSeverityLabelPatch
    {
        private static void Postfix(Hediff __instance, ref string __result)
        {
            if (SSCPnaExposureUtility.IsBodyLoadDef(__instance.def))
                __result = SSCPnaMath.GetLoad(__instance.Severity).ToStringPercent();
        }
    }

    // 拦住直接 AddHediff 的旁路：负荷按有效链分流，有链身体拒绝普通依赖。
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    internal static class SSCPnaHealthAdditionPatch
    {
        private static bool Prefix(Hediff hediff)
        {
            // 其他健康状态及存读档初始化保留原流程；这里只处理运行期 PNA 来件。
            if (hediff == null || !SSCPnaExposureUtility.IsBodyLoadDef(hediff.def) &&
                !SSCPnaExposureUtility.IsAddictionDef(hediff.def) || Scribe.mode != LoadSaveMode.Inactive) return true;
            // 先协调当前身体状态，再判断依赖保护；直接添加不承担满值成瘾判定。
            SSCPnaExposureUtility.RefreshBodyState(hediff.pawn);
            if (SSCPnaExposureUtility.IsAddictionDef(hediff.def)) return !SSCPnaExposureUtility.IsRooted(hediff.pawn);
            // 来件合并前结算旧载体的半批有效时间，并选择当前应使用的负荷 Def。
            (SSCPnaExposureUtility.GetBodyRecord(hediff.pawn) as HediffWithComps)
                ?.GetComp<HediffComp_SSCPnaLoad>()?.SettlePending();
            HediffDef desired = SSCPnaExposureUtility.IsRooted(hediff.pawn) ? SSCDefOf.SSC_PnaRootedLoad : SSCDefOf.SSC_PnaLoad;
            if (desired == null) return false;
            // 保留调用者的原实例，避免 AddHediff(Def) 返回一份未入健康列表的旧对象。
            hediff.def = desired;
            hediff.Severity = SSCPnaMath.NormalizeSeverity(hediff.Severity);
            return true;
        }

        private static void Postfix(Hediff hediff)
        {
            // 添加负荷或链后重评估形态，覆盖添加过程中的健康回调和链状态变化。
            if (hediff != null && (hediff is Hediff_ChainOfSexSlave || SSCPnaExposureUtility.IsBodyLoadDef(hediff.def)))
                SSCPnaExposureUtility.RefreshBodyState(hediff.pawn, hediff is Hediff_ChainOfSexSlave);
        }
    }

    // AddToPawn 也可能只更新既有链的主人引用，因此单独在该入口完成后刷新。
    [HarmonyPatch(typeof(Hediff_ChainOfSexSlave), nameof(Hediff_ChainOfSexSlave.AddToPawn))]
    internal static class SSCPnaChainAdditionPatch
    {
        private static void Postfix(Hediff_ChainOfSexSlave __result)
            => SSCPnaExposureUtility.RefreshBodyState(__result?.pawn, true);
    }

    // 解绑即时恢复普通负荷；依赖解除即时交给原版撤销对应需求。
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class SSCPnaChainRemovalPatch
    {
        private static void Postfix(Hediff hediff)
        {
            // 刷新发生在原版移除完成后，查询使用剩余实例，兼容仍有其他有效链的情况。
            if (hediff is Hediff_ChainOfSexSlave) SSCPnaExposureUtility.RefreshBodyState(hediff.pawn, true);
            else if (SSCPnaExposureUtility.IsAddictionDef(hediff?.def)) hediff.pawn?.needs?.AddOrRemoveNeedsAsAppropriate();
        }
    }

    // 未成瘾的适配物品由满负荷规则建立依赖；已有成瘾直接走原版服药行为。
    [HarmonyPatch(typeof(CompDrug), nameof(CompDrug.PrePostIngested))]
    internal static class SSCPnaRootedDrugPatch
    {
        private static bool Prefix(CompDrug __instance, Pawn ingester)
        {
            // 其他化学物不受影响；有链 PNA 保持 P1 保护并清理已有普通依赖。
            if (!SSCPnaExposureUtility.IsAddictionDef(__instance.Props.chemical?.addictionHediff)) return true;
            bool rooted = SSCPnaExposureUtility.IsRooted(ingester);
            if (rooted) SSCPnaExposureUtility.RefreshBodyState(ingester);
            // 已有依赖放行原版严重度增加及化学需求补充，不再保护已有恢复进度。
            // 第三方未适配来源仍走旧入口，不据此宣称完成所有 PNA 来源替换。
            return !rooted && (SSCPnaExposureUtility.GetAddiction(ingester) != null ||
                __instance.parent.def.ingestible?.outcomeDoers == null ||
                !__instance.parent.def.ingestible.outcomeDoers.Exists(doer => doer is IngestionOutcomeDoer_SSCPna));
        }
    }

}
