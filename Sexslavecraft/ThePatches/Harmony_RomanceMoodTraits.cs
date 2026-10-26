// AI 辅助生成：特质与基因变化后的局部协调，不修改原版特质或抑制规则。
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>合并同一角色的嵌套特质／基因操作，只在外层完成后观察最终有效特质。</summary>
    [HarmonyPatch]
    public static class Harmony_SSCRomanceMoodTraits
    {
        public sealed class Scope
        {
            internal Pawn Pawn;
            internal object Token;
            internal TraitSet Traits;
            internal int Depth;
        }
        private static readonly ConditionalWeakTable<Pawn, Scope> scopes = new ConditionalWeakTable<Pawn, Scope>();

        /// <summary>覆盖原生添加、移除、基因抑制及直接覆盖；不通过每 Tick 或恶堕需求轮询。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TraitSet), nameof(TraitSet.GainTrait), new[] { typeof(Trait), typeof(bool) });
            yield return AccessTools.Method(typeof(TraitSet), nameof(TraitSet.RemoveTrait), new[] { typeof(Trait), typeof(bool) });
            yield return AccessTools.Method(typeof(TraitSet), nameof(TraitSet.RecalculateSuppression));
            yield return AccessTools.Method(typeof(Pawn_GeneTracker), "AddGene", new[] { typeof(Gene), typeof(bool) });
            yield return AccessTools.Method(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.RemoveGene));
            yield return AccessTools.Method(typeof(Pawn_GeneTracker), "CheckForOverrides");
            yield return AccessTools.Method(typeof(Gene), nameof(Gene.OverrideBy));
        }

        /// <summary>只捕获当前原 TraitSet 与人格，异常观察不能阻断原版操作。</summary>
        public static void Prefix(object __instance, out Scope __state)
        {
            __state = null;
            try
            {
                if (Scribe.mode != LoadSaveMode.Inactive) return;
                var pawn = AccessTools.Field(__instance.GetType(), "pawn")?.GetValue(__instance) as Pawn;
                if (pawn?.TryGetComp<CompSexSlaveTraining>() == null || SSCPersonalityRuntimeUtility.IsMutating(pawn)) return;
                if (__instance is TraitSet set && pawn.story?.traits != set || __instance is Pawn_GeneTracker genes && pawn.genes != genes) return;
                var state = scopes.GetOrCreateValue(pawn);
                if (state.Depth == 0) { state.Pawn = pawn; state.Token = SSCPersonalityRuntimeUtility.Capture(pawn); state.Traits = pawn.story?.traits; }
                state.Depth++; __state = state;
            }
            catch (Exception) { /* 原方法仍然执行；未建立的观察作用域不需要释放。 */ }
        }

        /// <summary>成功或异常均释放深度；原方法异常继续传播，不向已替换的人格提交旧通知。</summary>
        [HarmonyPriority(Priority.Last)]
        public static void Finalizer(Scope __state, Exception __exception)
        {
            if (__state == null || --__state.Depth != 0) return;
            try
            {
                if (__state.Pawn.story?.traits == __state.Traits && SSCPersonalityRuntimeUtility.IsCurrent(__state.Pawn, __state.Token))
                    SSCRomanceMoodRefresh.Notify(__state.Pawn, __exception == null ? "EffectiveTrait" : "TraitOperationFailed");
            }
            catch (Exception) { /* 诊断与缓存协调不替换原版异常。 */ }
            finally { __state.Pawn = null; __state.Traits = null; __state.Token = null; }
        }
    }
}
