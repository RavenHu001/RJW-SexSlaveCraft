// AI 辅助生成：独立真实事件与局部缓存通知，不借用牛特化门槛或改写原高潮处理。
using System;
using HarmonyLib;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    internal static class SSCCorruptionGelIngestedPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void Prefix(Thing __instance, Pawn ingester, out SSCCorruptionGelIngestionRuntime.Frame __state)
            => __state = SSCCorruptionGelIngestionRuntime.Begin(__instance, ingester);
        [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
        internal static void Finalizer(SSCCorruptionGelIngestionRuntime.Frame __state) => SSCCorruptionGelIngestionRuntime.End(__state);
    }

    [HarmonyPatch(typeof(JobDriver_Sex), nameof(JobDriver_Sex.Orgasm))]
    internal static class SSCCorruptionGelOrgasmPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void Prefix(JobDriver_Sex __instance, out SSCCorruptionGelSceneRuntime.Frame __state)
            => __state = SSCCorruptionGelSceneRuntime.Begin(__instance);
        [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
        internal static void Finalizer(JobDriver_Sex __instance, Exception __exception, SSCCorruptionGelSceneRuntime.Frame __state)
            => SSCCorruptionGelSceneRuntime.End(__instance, __state, __exception);
    }

    [HarmonyPatch(typeof(JobDriver_Sex), nameof(JobDriver_Sex.ExposeData))]
    internal static class SSCCorruptionGelSceneSavePatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void Prefix(JobDriver_Sex __instance) => SSCCorruptionGelSceneRuntime.RestoreJobBinding(__instance);
        [HarmonyPostfix] internal static void Postfix(JobDriver_Sex __instance) => SSCCorruptionGelSceneRuntime.Expose(__instance);
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    internal static class SSCCorruptionGelSceneEndPatch
    {
        [HarmonyPrefix] internal static void Prefix(JobDriver __instance) => SSCCorruptionGelSceneRuntime.Finish(__instance);
        [HarmonyFinalizer] internal static void Finalizer(JobDriver __instance) => SSCCorruptionGelSceneRuntime.Finish(__instance);
    }

    [HarmonyPatch(typeof(SSCAbilityToggleUtility), nameof(SSCAbilityToggleUtility.SyncMarkers))]
    internal static class SSCCorruptionGelAbilityLifecycle
    {
        [HarmonyPostfix] internal static void Postfix(Pawn pawn)
        {
            if (SSCCorruptionGelProductionUtility.Find(pawn) != null) SSCCorruptionGelProductionUtility.NotifyEffectsChanged(pawn);
            SSCCorruptionGelEffectUtility.RefreshBodyState(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged))]
    internal static class SSCCorruptionGelHealthLifecycle
    {
        [HarmonyPostfix] internal static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
        {
            // 器官／缺失部位与外部直接改健康列表都可能撤销资格；没有生产记录的普通角色不参与。
            Pawn pawn = __instance.hediffSet.pawn;
            if (SSCCorruptionGelProductionUtility.IsBodyStateDef(hediff?.def) || SSCCorruptionGelProductionUtility.Find(pawn) != null)
                SSCCorruptionGelProductionUtility.NotifyEffectsChanged(pawn);
            if (SSCCorruptionGelEffectUtility.IsEffectDef(hediff?.def) || SSCCorruptionGelEffectUtility.Find(pawn) != null)
                SSCCorruptionGelEffectUtility.RefreshBodyState(pawn);
        }
    }

    // 原版Add/RemoveHediff直接更新健康列表和DirtyCache，不保证经过Notify_HediffChanged。
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    internal static class SSCCorruptionGelHealthAdded
    {
        /// <summary>完整添加后复查最终身体，正常器官恢复也能立即清工作属性缓存。</summary>
        [HarmonyPostfix] internal static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
            => SSCCorruptionGelHealthLifecycle.Postfix(__instance, hediff);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class SSCCorruptionGelHealthRemoved
    {
        /// <summary>完整移除后撤销失效身体的派生代价；不依赖下一次健康间隔补查。</summary>
        [HarmonyPostfix] internal static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
            => SSCCorruptionGelHealthLifecycle.Postfix(__instance, hediff);
    }

    [HarmonyPatch(typeof(SSCPersonalityRuntimeUtility), nameof(SSCPersonalityRuntimeUtility.TryBeginMutation))]
    internal static class SSCCorruptionGelMutationLifecycle
    {
        [HarmonyPostfix] internal static void Postfix(Pawn pawn, bool __result)
        {
            if (__result)
            {
                SSCCorruptionGelProductionUtility.NotifyEffectsChanged(pawn);
                SSCCorruptionGelEffectUtility.RefreshBodyState(pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class SSCCorruptionGelSpawnLifecycle
    {
        [HarmonyPostfix] internal static void Postfix(Pawn __instance) => SSCCorruptionGelAbilityLifecycle.Postfix(__instance);
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    internal static class SSCCorruptionGelDespawnLifecycle
    {
        [HarmonyPostfix] internal static void Postfix(Pawn __instance) => SSCCorruptionGelAbilityLifecycle.Postfix(__instance);
    }
}
