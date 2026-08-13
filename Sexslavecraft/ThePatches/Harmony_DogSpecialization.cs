using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(JobDriver_Train), "MakeNewToils")]
    public static class Patch_DogSpecialization_AnimalTraining
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> __result, JobDriver_Train __instance)
        {
            foreach (Toil toil in __result)
            {
                yield return toil;
            }

            yield return DogAnimalHandlingToil(__instance);
        }

        private static Toil DogAnimalHandlingToil(JobDriver __instance)
        {
            return new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Instant,
                initAction = delegate
                {
                    Pawn handler = __instance.pawn;
                    Pawn animal = __instance.job.GetTarget(TargetIndex.A).Pawn;
                    DogSpecializationUtility.NotifyAnimalTrainingCompleted(handler, animal);
                }
            };
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Patch_DogSpecialization_TameAttempt
    {
        public static void Postfix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, bool __result)
        {
            if (!__result || intDef != InteractionDefOf.TameAttempt) return;

            Pawn handler = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            DogSpecializationUtility.NotifyAnimalTamingAttempted(handler, recipient);
        }
    }

    [HarmonyPatch(typeof(SexUtility), nameof(SexUtility.ProcessSex))]
    public static class Patch_DogSpecialization_AnimalInteractionProgress
    {
        public static void Postfix(SexProps props)
        {
            DogSpecializationUtility.NotifyAnimalInteractionProcessed(props);
        }
    }
}
