// AI 辅助生成：子宫开发开启生育时，仅中和当前身体永久泌乳的生育抑制。
using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.CurStage), MethodType.Getter)]
    public static class Harmony_LactationFertilityControl
    {
        public const string LactationDefName = "SSC_Lactating_SubState";

        // 缓存复制操作，不缓存阶段内容；兼容模组随后调整源阶段时，下次查询仍读取新配置。
        private static readonly Func<object, object> CopyStage = typeof(object)
            .GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)
            .CreateDelegate<Func<object, object>>();

        /// <summary>只认可当前身体实际持有的泌乳状态及有效、满级、已开启生育的开发记录。</summary>
        public static bool IsOverrideActive(Hediff instance)
        {
            if (instance?.def?.defName != LactationDefName ||
                instance.pawn?.health?.hediffSet?.hediffs.Contains(instance) != true) return false;
            return SSCUterusDevelopmentUtility.Find(instance.pawn)?.FertilityEnabled == true;
        }

        /// <summary>在原版阶段读取后提供只改生育倍率的副本，统一作用于属性运算和健康页展示。</summary>
        public static void Postfix(Hediff __instance, ref HediffStage __result)
        {
            // 普通 Hediff、中性／增强阶段及无效配置不处理，也不补建任何阶段或身体记录。
            if (__instance?.def?.defName != LactationDefName || __result == null ||
                !float.IsFinite(__result.fertilityFactor) || __result.fertilityFactor < 0f ||
                __result.fertilityFactor >= 1f || !IsOverrideActive(__instance)) return;

            // 原阶段来自共享 Def，绝不能就地写入。浅复制保留所有其他字段及只读列表，
            // 本次只写副本的 fertilityFactor；不触碰产奶组件、奶量、营养或保存数据。
            HediffStage effective = (HediffStage)CopyStage(__result);
            effective.fertilityFactor = 1f;
            __result = effective;
        }
    }
}
