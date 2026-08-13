using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    // 👇 重点在这里：加入 new Type[] { typeof(Thing) } 明确指定参数类型
    [HarmonyPatch(typeof(Bill), "IsFixedOrAllowedIngredient", new Type[] { typeof(Thing) })]
    public static class Patch_IsFixedOrAllowedIngredient
    {
        public static void Postfix(Bill __instance, Thing thing, ref bool __result)
        {
            if (!(__instance.recipe is RecipeDef_PSTag smartRecipe)) return;
            if (!smartRecipe.filterIfTagExists || smartRecipe.hediffToAdd == null) return;

            CompPersonalityStore comp = thing.TryGetComp<CompPersonalityStore>();
            if (comp == null || !PersonalityGelUtility.IsSupportedPersonalityGel(thing.def)) return;

            __result = true;

            if (smartRecipe.requireBusSpecializationComplete &&
                (comp.specializationType != SexSlaveSpecializationType.Bus || comp.specializationProgress < 0.999f))
            {
                __result = false;
                return;
            }

            if (smartRecipe.requireSpecializationType != SexSlaveSpecializationType.None && comp.specializationType != smartRecipe.requireSpecializationType)
            {
                __result = false;
                return;
            }

            if (smartRecipe.requireSpecializationComplete && comp.specializationProgress < 0.999f)
            {
                __result = false;
                return;
            }

            if (smartRecipe.hediffToAdd == SSCDefOf.SSC_Hediff_Bus_Final && !comp.HasTag(SSCDefOf.SSC_Hediff_Bus))
            {
                __result = false;
                return;
            }

            if (smartRecipe.hediffToAdd == SSCDefOf.SSC_Hediff_Cow_Final && !comp.HasTag(SSCDefOf.SSC_Hediff_Cow))
            {
                __result = false;
                return;
            }

            HediffDef petBaseTag = PetSpecializationUtility.GetBaseHediffForFinal(smartRecipe.hediffToAdd);
            if (petBaseTag != null && !comp.HasTag(petBaseTag))
            {
                __result = false;
                return;
            }

            if (smartRecipe.exclusiveTags != null && smartRecipe.exclusiveTags.Any(tag => tag != null && comp.HasTag(tag)))
            {
                __result = false;
                return;
            }

            // 5. 【核心逻辑】如果物品已经含有该 Tag，禁止使用
            if (comp.HasTag(smartRecipe.hediffToAdd))
            {
                __result = false; // 强行设置为不可用
            }
        }
    }
}
