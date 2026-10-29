// AI 辅助生成：补充 SSC 身体与生育特化 Hediff 的原生生育因子展示，不参与属性计算或状态保存。
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(HediffStatsUtility), nameof(HediffStatsUtility.SpecialDisplayStats))]
    public static class Harmony_HediffFertilityDisplay
    {
        /// <summary>仅扩展子宫开发、永久泌乳与生育特化实例的展示，其他 Hediff 和无实例的定义预览保持原结果。</summary>
        public static void Postfix(Hediff instance, ref IEnumerable<StatDrawEntry> __result)
        {
            string name = instance?.def?.defName;
            if (name != SSCUterusDevelopmentUtility.DefName && name != "SSC_Lactating_SubState" && !FertilitySpecializationUtility.IsTag(instance?.def)) return;
            __result = WithReproductionEffects(WithFertilityFactor(__result, instance), instance);
        }

        /// <summary>保留全部原版属性行，补齐非原生Stat的孕期、教育及陪玩影响；无保存或状态修改。</summary>
        private static IEnumerable<StatDrawEntry> WithReproductionEffects(IEnumerable<StatDrawEntry> original, Hediff instance)
        {
            foreach (var entry in original) yield return entry;
            bool uterus = instance is Hediff_SSCUterusDevelopment;
            bool specialization = FertilitySpecializationUtility.IsTag(instance.def);
            if (!uterus && !specialization) yield break;
            bool enabled = SSCUterusDevelopmentUtility.Find(instance.pawn)?.FertilityEnabled == true;
            int stage = specialization ? FertilitySpecializationUtility.Stage(instance.pawn) : -1;
            // 分项只展示本Hediff的贡献：子宫0.8与特化额外系数相乘，不重复列总倍率。
            float duration = !enabled ? 1f : uterus ? SSCUterusPregnancyUtility.DurationFactor
                : stage < 0 ? 1f : HediffComp_SSCUterusPregnancy.DurationForStage(stage) / SSCUterusPregnancyUtility.DurationFactor;
            yield return EffectEntry("Duration", duration, 4069);
            if (specialization)
            {
                float care = FertilitySpecializationUtility.CareFactor(instance.pawn);
                yield return EffectEntry("Education", care, 4068);
                yield return EffectEntry("BabyPlay", care, 4067);
            }
        }

        /// <summary>普通倍率行使用本地化名称与说明，健康悬停也由原版读取这些同一条目。</summary>
        private static StatDrawEntry EffectEntry(string effect, float factor, int order)
            => new StatDrawEntry(StatCategoryDefOf.CapacityEffects, ("SSC_HediffFertility_" + effect).Translate(),
                "SSC_HediffFertility_Factor".Translate(factor.ToString("0.##")),
                ("SSC_HediffFertility_" + effect + "Description").Translate(), order);

        /// <summary>保留原有属性条目，再读取实例当前阶段；开关和兼容修正后的值随本次查询实时反映。</summary>
        private static IEnumerable<StatDrawEntry> WithFertilityFactor(IEnumerable<StatDrawEntry> original, Hediff instance)
        {
            foreach (StatDrawEntry entry in original) yield return entry;
            HediffStage stage = instance.CurStage;
            if (stage == null || !float.IsFinite(stage.fertilityFactor) || stage.fertilityFactor < 0f) yield break;
            // fertilityFactor 由原版单独相乘，不应改写成 statFactors 或显示为角色的最终受孕概率。
            // 连倍率为 1 的中性状态也明确展示，避免 HumanCattle 接管后仍误报永久泌乳抑制。
            string description = "SSC_HediffFertility_Description".Translate();
            if (instance.def.defName == Harmony_LactationFertilityControl.LactationDefName)
            {
                description += "\n\n" + "SSC_HediffFertility_LactationNote".Translate();
                // 与阶段计算共用当前身体的判据；中和时说明 ×1 的来源，不假装修改了共享配置。
                if (stage.fertilityFactor == 1f && Harmony_LactationFertilityControl.IsOverrideActive(instance))
                    description += "\n\n" + "SSC_HediffFertility_LactationOverrideNote".Translate();
            }
            // Fertility 定义来自 Biotech；未加载该定义时仍给出可读标签，不额外增加 DLC 准入或空引用。
            string label = StatDefOf.Fertility?.LabelCap.ToString() ?? "SSC_HediffFertility_Label".Translate().ToString();
            yield return new StatDrawEntry(StatCategoryDefOf.CapacityEffects, label,
                "SSC_HediffFertility_Factor".Translate(stage.fertilityFactor.ToString("0.##")),
                description, 4070);
        }
    }

    [HarmonyPatch(typeof(Hediff), nameof(Hediff.TipStringExtra), MethodType.Getter)]
    public static class Harmony_LactationFertilityTip
    {
        /// <summary>原生悬停只输出属性名和数值，另附本次 ×1 的原因；不重复添加数值行。</summary>
        public static void Postfix(Hediff __instance, ref string __result)
        {
            // 基础 getter 也由 HediffWithComps 调用，永久泌乳的组件提示会继续正常追加。
            if (!Harmony_LactationFertilityControl.IsOverrideActive(__instance) ||
                __instance.CurStage?.fertilityFactor != 1f) return;
            __result += "\n" + "SSC_HediffFertility_LactationOverrideNote".Translate();
        }
    }
}
