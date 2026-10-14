// AI 辅助生成
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.SeverityLabel), MethodType.Getter)]
    internal static class SSCPnaSeverityLabelPatch
    {
        private static void Postfix(Hediff __instance, ref string __result)
        {
            if (SSCPnaExposureUtility.IsBodyLoadDef(__instance.def))
                __result = SSCPnaMath.GetLoad(__instance.Severity).ToStringPercent();
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    internal static class SSCPnaHealthAdditionPatch
    {
        private static bool Prefix(Hediff hediff)
        {
            if (hediff == null || !SSCPnaExposureUtility.IsBodyLoadDef(hediff.def) &&
                !SSCPnaExposureUtility.IsAddictionDef(hediff.def) || Scribe.mode != LoadSaveMode.Inactive) return true;
            SSCPnaExposureUtility.RefreshBodyState(hediff.pawn);
            if (SSCPnaExposureUtility.IsAddictionDef(hediff.def)) return !SSCPnaExposureUtility.IsRooted(hediff.pawn);
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
            if (hediff != null && (hediff is Hediff_ChainOfSexSlave || SSCPnaExposureUtility.IsBodyLoadDef(hediff.def)))
                SSCPnaExposureUtility.RefreshBodyState(hediff.pawn, hediff is Hediff_ChainOfSexSlave);
        }
    }

    [HarmonyPatch(typeof(Hediff_ChainOfSexSlave), nameof(Hediff_ChainOfSexSlave.AddToPawn))]
    internal static class SSCPnaChainAdditionPatch
    {
        private static void Postfix(Hediff_ChainOfSexSlave __result)
            => SSCPnaExposureUtility.RefreshBodyState(__result?.pawn, true);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class SSCPnaChainRemovalPatch
    {
        private static void Postfix(Hediff hediff)
        {
            if (hediff is Hediff_ChainOfSexSlave) SSCPnaExposureUtility.RefreshBodyState(hediff.pawn, true);
        }
    }

    [HarmonyPatch(typeof(CompDrug), nameof(CompDrug.PrePostIngested))]
    internal static class SSCPnaRootedDrugPatch
    {
        private static bool Prefix(CompDrug __instance, Pawn ingester)
        {
            // P1 只关闭锁链对象的旧 PNA 成瘾/补充；普通对象的随机成瘾替换留在 P2。
            if (!SSCPnaExposureUtility.IsAddictionDef(__instance.Props.chemical?.addictionHediff) ||
                !SSCPnaExposureUtility.IsRooted(ingester)) return true;
            SSCPnaExposureUtility.RefreshBodyState(ingester);
            return false;
        }
    }
}
