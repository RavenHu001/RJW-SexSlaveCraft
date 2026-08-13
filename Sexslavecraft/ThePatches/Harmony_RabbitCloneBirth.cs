using HarmonyLib;
using rjw;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Hediff_HumanlikePregnancy), nameof(Hediff_HumanlikePregnancy.GiveBirth))]
    public static class Patch_RabbitCloneBirth_HumanlikePregnancy
    {
        public static bool Prefix(Hediff_HumanlikePregnancy __instance)
        {
            return !RabbitCloneUtility.TryReplacePregnancyBirthWithClone(__instance);
        }
    }

    [HarmonyPatch(typeof(Hediff_BestialPregnancy), nameof(Hediff_BestialPregnancy.GiveBirth))]
    public static class Patch_RabbitCloneBirth_BestialPregnancy
    {
        public static bool Prefix(Hediff_BestialPregnancy __instance)
        {
            return !RabbitCloneUtility.TryReplacePregnancyBirthWithClone(__instance);
        }
    }
}
