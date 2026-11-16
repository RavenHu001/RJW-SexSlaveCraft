using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class HumanCattleCompatibility
    {
        private const string MilkedTrackerTypeName = "HumanCattle.HediffComp_MilkedTracker";
        private const string ChargeablePatchTypeName = "HumanCattle.HediffComp_Chargeable_Patches";
        private static Type milkedTrackerType;

        public static bool IsLoaded => MilkedTrackerType != null;
        /// <summary>三个外部钩子都安装后才接管 SSC 永久泌乳。安装失败后，残留前置仍放行原方法。</summary>
        internal static bool Installed { get; private set; }

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

        internal static void Register(Harmony harmony)
        {
            Installed = false;
            Installed = SSCOptionalPatchGroup.Install(harmony, "Human Cattle", () =>
            {
                var tick = DoopChargeablePatchType("CompPostTickInterval");
                var before = DoopChargeablePatchType("CompPostMake_Pre");
                var after = DoopChargeablePatchType("CompPostMake_Post");
                // 跟踪器可作为依赖仍在的信号。外部改名导致钩子丢失时走失配处理。
                if (tick == null && before == null && after == null && !IsLoaded) return null;
                // Human Cattle 的入口本身是 Harmony 补丁方法，首参是 ref HediffComp_Chargeable。
                var comp = typeof(HediffComp_Chargeable).MakeByRefType();
                return new[]
                {
                    (SSCOptionalPatchGroup.RequireTarget(tick, "Prefix", typeof(bool), true, comp), typeof(Patch_HumanCattle_DoopsOwnTickPrefix), HarmonyPatchType.Prefix),
                    (SSCOptionalPatchGroup.RequireTarget(before, "Prefix", typeof(bool), true, comp), typeof(Patch_HumanCattle_DoopsOwnPostMakePrePrefix), HarmonyPatchType.Prefix),
                    (SSCOptionalPatchGroup.RequireTarget(after, "Postfix", typeof(void), true, comp), typeof(Patch_HumanCattle_DoopsOwnPostMakePostPrefix), HarmonyPatchType.Prefix)
                };
            });
        }

        public static bool IsSSCPermaLactation(HediffComp_Chargeable comp)
        {
            return comp?.parent?.def == SSCDefOf.SSC_Lactating_SubState;
        }
    }

    public static class Patch_HumanCattle_DoopsOwnTickPrefix
    {
        public static bool Prefix(
            [HarmonyArgument(0)] ref HediffComp_Chargeable comp,
            ref bool __result)
        {
            if (!HumanCattleCompatibility.Installed || !HumanCattleCompatibility.IsSSCPermaLactation(comp)) return true;

            __result = true;
            return false;
        }

    }

    public static class Patch_HumanCattle_DoopsOwnPostMakePrePrefix
    {
        public static bool Prefix(
            [HarmonyArgument(0)] ref HediffComp_Chargeable comp,
            ref bool __result)
        {
            if (!HumanCattleCompatibility.Installed || !HumanCattleCompatibility.IsSSCPermaLactation(comp)) return true;

            __result = true;
            return false;
        }
    }

    public static class Patch_HumanCattle_DoopsOwnPostMakePostPrefix
    {
        public static bool Prefix([HarmonyArgument(0)] ref HediffComp_Chargeable comp)
        {
            return !HumanCattleCompatibility.Installed || !HumanCattleCompatibility.IsSSCPermaLactation(comp);
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
