// AI 辅助生成：1.7 兔特化只从原版已经记入的家务成果获得经验。
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment))]
    internal static class Patch_RabbitHouseworkProgress
    {
        internal static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
        {
            if (def != RecordDefOf.MessesCleaned && def != RecordDefOf.MealsCooked) return;

            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead || pawn.Destroyed || !SSCIdentityUtility.IsSexSlave(pawn)) return;

            JobDriver driver = pawn.jobs?.curDriver;
            if (def == RecordDefOf.MessesCleaned && driver is JobDriver_CleanFilth)
                PetSpecializationUtility.TryGainPetProgress(pawn, SexSlaveSpecializationType.PetRabbit, 0.002f);
            else if (def == RecordDefOf.MealsCooked && driver is JobDriver_DoBill &&
                pawn.CurJob?.RecipeDef?.workSkill == SkillDefOf.Cooking)
                PetSpecializationUtility.TryGainPetProgress(pawn, SexSlaveSpecializationType.PetRabbit, 0.001f);
        }
    }
}
