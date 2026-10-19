// AI 辅助生成：仅为显式标记的 PNA 近战武器取消品质伤害系数。
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>XML 自愿接入标记；不修改武器品质、基础伤害或其他属性。</summary>
    public sealed class SSCPnaFixedMeleeDamageExtension : DefModExtension { }

    internal static class SSCPnaMeleeQualityUtility
    {
        /// <summary>同一条件用于数值与说明，兼容实物及制作／信息卡的抽象 Def 请求。</summary>
        internal static bool SkipQuality(StatPart part, StatRequest request)
            => part?.parentStat != null &&
               part.parentStat == StatDefOf.MeleeWeapon_DamageMultiplier &&
               request.Def is ThingDef weapon && weapon.IsMeleeWeapon &&
               weapon.GetModExtension<SSCPnaFixedMeleeDamageExtension>() != null;
    }

    [HarmonyPatch(typeof(StatPart_Quality), nameof(StatPart_Quality.TransformValue))]
    internal static class Harmony_SSCPnaMeleeQualityValue
    {
        // 只跳过原版品质这一项，不把最终倍率重置为 1：其他模组／场景修正仍生效。
        // 显式近战穿甲复用此倍率，因此与伤害及信息卡采用一致的品质例外。
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
}
