// AI 辅助生成
using System;

namespace SexSlaveCraft
{
    /// <summary>生育终极化的只读契约；筛料、消耗前守卫和工作者共用。</summary>
    internal static class FertilityFinalizationRecipeUtility
    {
        /// <summary>已知配方缺目标时仍受保护；同目标的配方也不能绕过普通完成要求。</summary>
        internal static bool IsFinalizationRecipe(RecipeDef_PSTag recipe) =>
            recipe?.defName == "SSC_PSEdit_Fertility_Final" || recipe?.hediffToAdd?.defName == FertilitySpecializationUtility.FinalName;

        /// <summary>先验证输出契约，再验证人格的当前方向、有限完成进度与普通标签。</summary>
        internal static bool IsEligibleGel(RecipeDef_PSTag recipe, CompPersonalityStore gel)
        {
            // 分类不等于授权。缺目标、错误方向或非永久严重度不能生成错误成果。
            if (recipe == null || FertilitySpecializationUtility.FinalDef == null ||
                recipe.hediffToAdd != FertilitySpecializationUtility.FinalDef ||
                recipe.requireSpecializationType != SexSlaveSpecializationType.Fertility ||
                !recipe.requireSpecializationComplete || recipe.severity != 1f) return false;
            // 不检查parent.Destroyed：原版正常消耗后，工作者仍须读取原人格组件。
            return gel != null && FertilitySpecializationUtility.OrdinaryDef != null &&
                gel.specializationType == SexSlaveSpecializationType.Fertility && float.IsFinite(gel.specializationProgress) &&
                gel.specializationProgress >= CompSexSlaveTraining.SpecializationCompletionProgress && gel.specializationProgress <= 1f &&
                gel.HasTag(FertilitySpecializationUtility.OrdinaryDef) && !gel.HasTag(FertilitySpecializationUtility.FinalDef);
        }

        /// <summary>复用现有八种凝胶的等级映射与人格组件预检；不依赖宠物互斥规则。</summary>
        internal static bool CanCreateProduct(Verse.ThingDef sourceDef) => PetFinalizationRecipeUtility.CanCreateProduct(sourceDef);
    }
}
