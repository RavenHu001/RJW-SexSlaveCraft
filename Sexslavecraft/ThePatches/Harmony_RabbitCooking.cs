using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(CompFoodPoisonable), nameof(CompFoodPoisonable.Notify_RecipeProduced))]
    internal static class Harmony_RabbitCooking
    {
        private static bool Prefix(CompFoodPoisonable __instance, Pawn pawn)
        {
            if (pawn?.jobs?.curDriver is not JobDriver_DoBill) return true;
            RecipeDef recipe = pawn.CurJob?.RecipeDef;
            if (recipe == null || recipe.workSkill != SkillDefOf.Cooking) return true;

            HediffDef effectDef = HediffComp_PetRabbitCookingAbility.EffectDef;
            if (effectDef == null) return true;
            Hediff effect = pawn.health?.hediffSet?.GetFirstHediffOfDef(effectDef);
            if (effect?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear is not > 0 ||
                pawn.Dead || !PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit))
                return true;

            // 原版餐食记录的产物范围。只跳过本配方合格产物的制作期染毒。
            ThingDef product = __instance.parent.def;
            return !product.IsNutritionGivingIngestible || product.ingestible.preferability < FoodPreferability.MealAwful ||
                   recipe.products?.Any(entry => entry.thingDef == product) != true;
        }
    }
}
