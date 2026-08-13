using System;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;

namespace SexSlaveCraft
{
    public static class HumanCattleCompatibility
    {
        private const string MilkedTrackerTypeName = "HumanCattle.HediffComp_MilkedTracker";
        private const string ChargeablePatchTypeName = "HumanCattle.HediffComp_Chargeable_Patches";
        private static Type milkedTrackerType;

        public static bool IsLoaded => MilkedTrackerType != null;

        private static Type MilkedTrackerType
        {
            get
            {
                if (milkedTrackerType == null)
                {
                    milkedTrackerType = SoftDependencyUtility.FindOptionalType(MilkedTrackerTypeName);
                }

                return milkedTrackerType;
            }
        }

        public static Type DoopChargeablePatchType(string nestedTypeName)
        {
            return SoftDependencyUtility.FindOptionalType(ChargeablePatchTypeName + "+" + nestedTypeName);
        }

        public static bool IsSSCPermaLactation(HediffComp_Chargeable comp)
        {
            return comp?.parent?.def == SSCDefOf.SSC_Lactating_SubState;
        }
    }

    [HarmonyPatch]
    public static class Patch_HumanCattle_DoopsOwnTickPrefix
    {
        public static bool Prepare() => HumanCattleCompatibility.DoopChargeablePatchType("CompPostTickInterval") != null;

        public static MethodBase TargetMethod()
        {
            Type targetType = HumanCattleCompatibility.DoopChargeablePatchType("CompPostTickInterval");
            return AccessTools.Method(targetType, "Prefix");
        }

        public static bool Prefix(
            [HarmonyArgument(0)] ref HediffComp_Chargeable comp,
            ref bool __result)
        {
            if (!HumanCattleCompatibility.IsSSCPermaLactation(comp)) return true;

            __result = true;
            return false;
        }

    }

    [HarmonyPatch]
    public static class Patch_HumanCattle_DoopsOwnPostMakePrePrefix
    {
        public static bool Prepare() => HumanCattleCompatibility.DoopChargeablePatchType("CompPostMake_Pre") != null;

        public static MethodBase TargetMethod()
        {
            Type targetType = HumanCattleCompatibility.DoopChargeablePatchType("CompPostMake_Pre");
            return AccessTools.Method(targetType, "Prefix");
        }

        public static bool Prefix(
            [HarmonyArgument(0)] ref HediffComp_Chargeable comp,
            ref bool __result)
        {
            if (!HumanCattleCompatibility.IsSSCPermaLactation(comp)) return true;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    public static class Patch_HumanCattle_DoopsOwnPostMakePostPrefix
    {
        public static bool Prepare() => HumanCattleCompatibility.DoopChargeablePatchType("CompPostMake_Post") != null;

        public static MethodBase TargetMethod()
        {
            Type targetType = HumanCattleCompatibility.DoopChargeablePatchType("CompPostMake_Post");
            return AccessTools.Method(targetType, "Postfix");
        }

        public static bool Prefix([HarmonyArgument(0)] ref HediffComp_Chargeable comp)
        {
            return !HumanCattleCompatibility.IsSSCPermaLactation(comp);
        }
    }

    [HarmonyPatch(typeof(HediffComp_Chargeable), nameof(HediffComp_Chargeable.GreedyConsume))]
    public static class Patch_HumanCattle_TrackCowMilkConsumption
    {
        public static void Postfix(HediffComp_Chargeable __instance, float __result)
        {
            if (!HumanCattleBridgeUtility.IsLoaded || __result <= 0f) return;
            if (__instance?.parent?.def != HediffDefOf.Lactating) return;

            BusSpecializationUtility.TryGainCowProgressFromMilkAmount(__instance.Pawn, __result);
        }
    }
}
