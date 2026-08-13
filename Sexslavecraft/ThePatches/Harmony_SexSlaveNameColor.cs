using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

// EN: Sex-slave stages 1-2 use pink names, while stages 3-4 use light purple.
// CN: 性奴前两个阶段使用粉红色名字，后两个阶段使用淡紫色名字。
namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(PawnNameColorUtility), nameof(PawnNameColorUtility.PawnNameColorOf))]
    public static class Patch_PawnNameColorUtility_SexSlaveStages
    {
        private static readonly Color EarlyStagePink = new Color32(255, 120, 183, 255);
        private static readonly Color LateStagePurple = new Color32(197, 139, 255, 255);

        public static void Postfix(Pawn pawn, ref Color __result)
        {
            if (pawn?.Faction != Faction.OfPlayer) return;

            int stage = SSCIdentityUtility.GetSexSlaveStage(pawn);
            if (stage <= 0) return;
            __result = stage >= 3 ? LateStagePurple : EarlyStagePink;
        }
    }
}
