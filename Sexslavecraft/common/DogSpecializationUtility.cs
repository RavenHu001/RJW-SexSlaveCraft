using RimWorld;
using rjw;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public static class DogSpecializationUtility
    {
        public const int AnimalInteractionRollCooldownTicks = GenDate.TicksPerDay;

        private const float AnimalTrainingProgress = 0.004f;
        private const float AnimalTamingAttemptProgress = 0.006f;
        private const float AnimalInteractionProgress = 0.020f;
        private const float MinAnimalInteractionChance = 0.06f;
        private const float ProgressAnimalInteractionChanceBonus = 0.08f;
        private const float FinalDogAnimalInteractionChanceBonus = 0.04f;
        private const float MaxAnimalInteractionChance = 0.18f;
        private const float ProgressGainMultiplierBonus = 0.50f;
        private const float FinalDogProgressGainBonus = 0.25f;

        public static bool IsDogSpecialized(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp?.IsPetDogSpecialized == true ||
                   PetSpecializationUtility.HasAnyPetState(pawn, SexSlaveSpecializationType.PetDog);
        }

        public static void NotifyAnimalTrainingCompleted(Pawn handler, Pawn animal)
        {
            if (!IsValidDogAnimalHandlingPair(handler, animal)) return;

            PetSpecializationUtility.TryGainPetProgress(
                handler,
                SexSlaveSpecializationType.PetDog,
                GetScaledProgressGain(handler, AnimalTrainingProgress),
                showThresholdMessage: true);

            TryRollAnimalInteractionAfterHandling(handler, animal);
        }

        public static void NotifyAnimalTamingAttempted(Pawn handler, Pawn animal)
        {
            if (!IsValidDogAnimalHandlingPair(handler, animal)) return;

            PetSpecializationUtility.TryGainPetProgress(
                handler,
                SexSlaveSpecializationType.PetDog,
                GetScaledProgressGain(handler, AnimalTamingAttemptProgress),
                showThresholdMessage: true);

            TryRollAnimalInteractionAfterHandling(handler, animal);
        }

        public static void NotifyAnimalInteractionProcessed(SexProps props)
        {
            if (props?.pawn == null || props.partner == null) return;
            if (!IsHumanAnimalPair(props.pawn, props.partner)) return;

            Pawn dog = IsDogSpecialized(props.pawn) ? props.pawn : IsDogSpecialized(props.partner) ? props.partner : null;
            if (dog == null) return;

            PetSpecializationUtility.TryGainPetProgress(
                dog,
                SexSlaveSpecializationType.PetDog,
                GetScaledProgressGain(dog, AnimalInteractionProgress),
                showThresholdMessage: true);
        }

        private static void TryRollAnimalInteractionAfterHandling(Pawn handler, Pawn animal)
        {
            CompSexSlaveTraining comp = handler?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            int now = Find.TickManager.TicksGame;
            if (now - comp.lastDogAnimalInteractionTick < AnimalInteractionRollCooldownTicks)
            {
                return;
            }

            // Cooldown is consumed by the roll itself, not only by success. This keeps dog events rare even with many animals.
            comp.lastDogAnimalInteractionTick = now;

            if (!Rand.Chance(GetAnimalInteractionChance(handler))) return;
            TryStartAnimalInteractionJob(handler, animal);
        }

        private static bool TryStartAnimalInteractionJob(Pawn handler, Pawn animal)
        {
            if (!CanStartAnimalInteraction(handler, animal)) return false;

            Job job = TryMakeBedAnimalInteractionJob(handler, animal);
            if (job == null && xxx.can_rape(handler))
            {
                job = JobMaker.MakeJob(xxx.bestiality, animal);
            }

            if (job == null) return false;

            job.expiryInterval = GenDate.TicksPerHour;
            bool started = handler.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            if (started)
            {
                Messages.Message(
                    Strings.Message_DogAnimalInteractionTriggered(handler.LabelShort, animal.LabelShort),
                    new LookTargets(handler, animal),
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }

            return started;
        }

        private static bool CanStartAnimalInteraction(Pawn handler, Pawn animal)
        {
            if (handler == null || animal == null) return false;
            if (handler.Dead || animal.Dead || handler.Downed || handler.Drafted) return false;
            if (!handler.Spawned || !animal.Spawned || handler.Map != animal.Map) return false;
            if (handler.InMentalState || (animal.InMentalState && !animal.Downed)) return false;
            if (handler.CurJobDef == SSCDefOf.SSC_Job_PetAffection) return false;
            if (IsInSexJob(handler) || IsInSexJob(animal)) return false;
            if (!animal.RaceProps.Animal || animal.IsFighting() || handler.IsFighting()) return false;
            if (animal.HostileTo(handler) && !animal.Downed) return false;
            if (!xxx.can_do_animalsex(handler, animal)) return false;
            if (!handler.CanReserveAndReach(animal, PathEndMode.Touch, Danger.Deadly)) return false;
            if (!xxx.can_fuck(handler) && !xxx.can_be_fucked(handler) && !xxx.can_rape(handler)) return false;
            if (!xxx.can_fuck(animal) && !xxx.can_be_fucked(animal)) return false;
            return true;
        }

        private static Job TryMakeBedAnimalInteractionJob(Pawn handler, Pawn animal)
        {
            if (animal.Downed || (!xxx.can_fuck(handler) && !xxx.can_be_fucked(handler)))
            {
                return null;
            }

            Building_Bed handlerBed = handler.ownership?.OwnedBed;
            Building_Bed animalBed = animal.ownership?.OwnedBed;

            if (handlerBed != null && animalBed != null &&
                animal.CanReach(handlerBed, PathEndMode.OnCell, Danger.Some) &&
                handler.CanReach(animalBed, PathEndMode.OnCell, Danger.Some))
            {
                Building_Bed chosenBed = handler.Position.DistanceToSquared(animalBed.Position) >= handler.Position.DistanceToSquared(handlerBed.Position)
                    ? handlerBed
                    : animalBed;
                return JobMaker.MakeJob(xxx.bestialityForFemale, animal, chosenBed);
            }

            if (handlerBed != null && animal.CanReach(handlerBed, PathEndMode.OnCell, Danger.Some))
            {
                return JobMaker.MakeJob(xxx.bestialityForFemale, animal, handlerBed);
            }

            if (animalBed != null && handler.CanReach(animalBed, PathEndMode.OnCell, Danger.Some))
            {
                return JobMaker.MakeJob(xxx.bestialityForFemale, animal, animalBed);
            }

            return null;
        }

        private static float GetScaledProgressGain(Pawn dog, float baseAmount)
        {
            if (baseAmount <= 0f) return 0f;

            CompSexSlaveTraining comp = dog?.TryGetComp<CompSexSlaveTraining>();
            float progress = comp?.specializationProgress ?? 0f;
            float multiplier = 1f + progress * ProgressGainMultiplierBonus;
            if (PetSpecializationUtility.HasFinalPetState(dog, SexSlaveSpecializationType.PetDog))
            {
                multiplier += FinalDogProgressGainBonus;
            }

            return baseAmount * multiplier;
        }

        private static float GetAnimalInteractionChance(Pawn dog)
        {
            CompSexSlaveTraining comp = dog?.TryGetComp<CompSexSlaveTraining>();
            float progress = comp?.specializationProgress ?? 0f;
            float chance = MinAnimalInteractionChance + progress * ProgressAnimalInteractionChanceBonus;
            if (PetSpecializationUtility.HasFinalPetState(dog, SexSlaveSpecializationType.PetDog))
            {
                chance += FinalDogAnimalInteractionChanceBonus;
            }

            return Mathf.Min(chance, MaxAnimalInteractionChance);
        }

        private static bool IsInSexJob(Pawn pawn)
        {
            return pawn?.jobs?.curDriver is JobDriver_Sex;
        }

        private static bool IsValidDogAnimalHandlingPair(Pawn handler, Pawn animal)
        {
            return handler != null &&
                   animal != null &&
                   animal.RaceProps.Animal &&
                   !handler.Dead &&
                   !animal.Dead &&
                   IsDogSpecialized(handler);
        }

        private static bool IsHumanAnimalPair(Pawn a, Pawn b)
        {
            return (xxx.is_human(a) && xxx.is_animal(b)) ||
                   (xxx.is_animal(a) && xxx.is_human(b));
        }
    }
}
