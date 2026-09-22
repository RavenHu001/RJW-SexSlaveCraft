using System;
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>身份、方向、绑定和周期对账完成后通知新配置；不接管行为许可或装备拦截。</summary>
    [HarmonyPatch(typeof(SSCIdentityUtility), nameof(SSCIdentityUtility.TrySetIdentity))]
    internal static class SSCRestrictionIdentityHook
    {
        /// <summary>身份变更后刷新已有绑定配置；单独分配性奴身份不再激活限制或创建配置。</summary>
        private static void Postfix(Pawn pawn, bool __result)
        {
            if (__result) SSCRestrictionGameComponent.Notify(pawn);
        }
    }

    [HarmonyPatch(typeof(CompSexSlaveTraining), nameof(CompSexSlaveTraining.SetSpecialization))]
    internal static class SSCRestrictionSpecializationHook
    {
        /// <summary>方向更新完成后处理公交车首次取得事件，重复选择不覆盖已保存选择。</summary>
        private static void Postfix(CompSexSlaveTraining __instance)
        {
            SSCRestrictionGameComponent.Notify(__instance.parent as Pawn);
        }
    }

    [HarmonyPatch(typeof(CompSexSlaveTraining), nameof(CompSexSlaveTraining.ReconcileSpecialization))]
    internal static class SSCRestrictionReconcileHook
    {
        /// <summary>健康状态整理完成后刷新，包括凝胶和兼容状态认领；无跨查询装备缓存需要清理。</summary>
        private static void Postfix(Pawn pawn)
        {
            SSCRestrictionGameComponent.Notify(pawn);
        }
    }

    [HarmonyPatch(typeof(ExcretionUtility), nameof(ExcretionUtility.InheritEverything))]
    internal static class SSCRestrictionPersonalityHook
    {
        /// <summary>在人格恢复前延迟其中的身份和特化事件。</summary>
        private static void Prefix(Pawn consumer, out IDisposable __state)
        {
            __state = SSCRestrictionLifecycle.BeginRestore(consumer);
        }

        /// <summary>成功、提前返回及异常均结束恢复作用域，按接收身体最终状态刷新配置。</summary>
        private static void Finalizer(IDisposable __state)
        {
            __state?.Dispose();
        }
    }

    [HarmonyPatch(typeof(RabbitCloneUtility), "PrepareFreshRabbitCloneBody")]
    internal static class SSCRestrictionCloneHook
    {
        /// <summary>复制体准备完成且尚未生成到地图时清除来源配置，保证新克隆拥有独立默认。</summary>
        private static void Postfix(Pawn clone)
        {
            SSCRestrictionLifecycle.ResetNewClone(clone);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class SSCRestrictionSpawnHook
    {
        /// <summary>补齐地图生成入口；读档生成仍等待全局引用恢复，不提前迁移。</summary>
        private static void Postfix(Pawn __instance)
        {
            SSCRestrictionGameComponent.Notify(__instance);
        }
    }

    [HarmonyPatch(typeof(Hediff_ChainOfSexSlave), nameof(Hediff_ChainOfSexSlave.AddToPawn))]
    internal static class SSCRestrictionChainHook
    {
        /// <summary>兼容直接建立旧锁链的入口，必须等目标引用写好后才判定首次适用。</summary>
        private static void Postfix(Hediff_ChainOfSexSlave __result)
        {
            SSCRestrictionGameComponent.Notify(__result?.pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    internal static class SSCRestrictionBusHealthHook
    {
        /// <summary>仅在公交车基础或终极状态加入后处理首次默认，覆盖离图角色；其他健康变化不扫描规则。</summary>
        private static void Postfix(Hediff hediff)
        {
            if (hediff != null && (hediff.def == SSCDefOf.SSC_Hediff_Bus || hediff.def == SSCDefOf.SSC_Hediff_Bus_Final))
                SSCRestrictionGameComponent.Notify(hediff.pawn);
        }
    }
}
