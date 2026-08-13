using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(PawnRenderNodeWorker), "CanDrawNow")]
    public static class Harmony_HideWeaponDuringAnimation
    {
        [HarmonyPrefix]
        public static bool Prefix(PawnRenderNode node, ref bool __result)
        {
            if (node.Props?.tag?.defName != "Equipment")
                return true;

            Pawn pawn = node.tree?.pawn;
            if (pawn != null && SoftDependencyUtility.IsAnimating(pawn))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
