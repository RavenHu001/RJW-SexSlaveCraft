// 废案归档（2026-10-19）：固定 PNA 近战伤害、单独恢复品质穿甲的历史适配。
// 原用途：通过 ThingDef 品质标记跳过 MeleeWeapon_DamageMultiplier 的品质数值和说明，
// 再于两种穿甲重载补回品质倍率，以保持近战物理伤害固定而穿甲随品质变化。
// 废弃原因：维护者决定全部武器恢复原版品质规则，伤害与穿甲一同随品质变化。
// 本文件不在生产或测试编译清单中，XML 标记和所有生效入口均已撤下；仅作实现参考。
// 不可直接重新加入 PatchAll：丝鞭工作端现已独立识别，当前测试也按原版品质规则验证。
// AI 辅助生成：显式标记的 PNA 近战固定品质伤害，穿甲单独恢复原版品质系数。
using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>XML 自愿接入标记；不修改武器品质、基础伤害或其他属性。</summary>
    public sealed class SSCPnaFixedMeleeDamageExtension : DefModExtension { }

    internal static class SSCPnaMeleeQualityUtility
    {
        // 从当前 StatDef 中的实际品质组件读取曲线，不维护第二份七档常量。
        // 委托只访问原版的品质系数读取器，不改变共享属性缓存或临时切换全局品质规则。
        private static readonly Func<StatPart_Quality, QualityCategory, float> QualityMultiplier =
            AccessTools.MethodDelegate<Func<StatPart_Quality, QualityCategory, float>>(
                AccessTools.Method(typeof(StatPart_Quality), "QualityMultiplier"));

        internal static bool IsFixedWeapon(ThingDef weapon)
            => weapon != null && weapon.IsMeleeWeapon &&
               weapon.GetModExtension<SSCPnaFixedMeleeDamageExtension>() != null;

        internal static float ArmorQualityFactor(QualityCategory quality)
        {
            float factor = 1f;
            var parts = StatDefOf.MeleeWeapon_DamageMultiplier?.parts;
            if (parts == null) return factor;
            // 只补回被本补丁跳过的品质项；其余倍率已包含在原版穿甲结果里，不能再乘一遍。
            foreach (var part in parts)
                if (part is StatPart_Quality qualityPart)
                    factor *= QualityMultiplier(qualityPart, quality);
            return float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0f ? 1f : factor;
        }

        /// <summary>同一条件用于数值与说明，兼容实物及制作／信息卡的抽象 Def 请求。</summary>
        internal static bool SkipQuality(StatPart part, StatRequest request)
            => part?.parentStat != null &&
               part.parentStat == StatDefOf.MeleeWeapon_DamageMultiplier &&
               IsFixedWeapon(request.Def as ThingDef);
    }

    [HarmonyPatch(typeof(StatPart_Quality), nameof(StatPart_Quality.TransformValue))]
    internal static class Harmony_SSCPnaMeleeQualityValue
    {
        // 只跳过原版品质这一项，不把最终倍率重置为 1：其他模组／场景修正仍生效。
        // 原版穿甲也复用此属性；下方只在穿甲最终入口补回品质，伤害继续使用本例外。
        internal static bool Prefix(StatPart_Quality __instance, StatRequest req)
            => !SSCPnaMeleeQualityUtility.SkipQuality(__instance, req);
    }

    [HarmonyPatch(typeof(StatPart_Quality), nameof(StatPart_Quality.ExplanationPart))]
    internal static class Harmony_SSCPnaMeleeQualityExplanation
    {
        internal static bool Prefix(StatPart_Quality __instance, StatRequest req, ref string __result)
        {
            if (!SSCPnaMeleeQualityUtility.SkipQuality(__instance, req)) return true;
            // 隐去已经不参与计算的品质乘数，其他属性的品质说明仍由原版生成。
            __result = null;
            return false;
        }
    }

    // 只补两个底层重载。Verb 包装重载会调用实物重载，不能再在包装入口乘一次。
    // 原版实战、丝鞭专用 Verb 及实物信息卡都走同一入口，因此无需修改各伤害处理器。
    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration),
        new[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    internal static class Harmony_SSCPnaMeleeArmorQuality
    {
        internal static void Postfix(Thing equipment, ref float __result)
        {
            if (!SSCPnaMeleeQualityUtility.IsFixedWeapon(equipment?.def)) return;
            equipment.TryGetQuality(out QualityCategory quality);
            __result *= SSCPnaMeleeQualityUtility.ArmorQualityFactor(quality);
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration),
        new[] { typeof(Tool), typeof(Pawn), typeof(ThingDef), typeof(ThingDef), typeof(HediffComp_VerbGiver) })]
    internal static class Harmony_SSCPnaMeleeAbstractArmorQuality
    {
        internal static void Postfix(ThingDef equipment, ref float __result)
        {
            if (!SSCPnaMeleeQualityUtility.IsFixedWeapon(equipment)) return;
            // 原版这个重载没有品质参数，定义／配方预览按普通品质。实物卡使用上面的重载。
            __result *= SSCPnaMeleeQualityUtility.ArmorQualityFactor(QualityCategory.Normal);
        }
    }
}
