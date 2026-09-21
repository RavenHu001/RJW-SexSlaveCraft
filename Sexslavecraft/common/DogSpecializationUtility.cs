using RimWorld;
using rjw;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public static class DogSpecializationUtility
    {
        // 事件频率及成长数值保持旧版本约定，本阶段只接管调度许可。
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

        /// <summary>检查当前方向或有效宠物狗状态，历史进度本身不赋予事件资格。</summary>
        public static bool IsDogSpecialized(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp?.IsPetDogSpecialized == true ||
                   PetSpecializationUtility.HasAnyPetState(pawn, SexSlaveSpecializationType.PetDog);
        }

        /// <summary>完成动物训练后先计入工作成长，再尝试低频事件。</summary>
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

        /// <summary>完成一次驯服尝试后保留既有工作成长，再尝试事件。</summary>
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

        /// <summary>仅真实行为结算传入有效人兽参与者时计入行为成长，不由调度或预检发放。</summary>
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

        /// <summary>一天最多掷骰一次；冷却与规则许可分离，失败不会重掷。</summary>
        private static void TryRollAnimalInteractionAfterHandling(Pawn handler, Pawn animal)
        {
            CompSexSlaveTraining comp = handler?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            int now = Find.TickManager.TicksGame;
            if (now - comp.lastDogAnimalInteractionTick < AnimalInteractionRollCooldownTicks)
            {
                return;
            }

            // 冷却由掷骰消耗：许可拒绝和调度失败不退还，避免同一天连续训练动物反复重掷。
            comp.lastDogAnimalInteractionTick = now;

            if (!Rand.Chance(GetAnimalInteractionChance(handler))) return;
            TryStartAnimalInteractionJob(handler, animal);
        }

        /// <summary>先选择真实床上或地面驱动再检查许可，启动成功也不提前显示行为开始消息。</summary>
        private static bool TryStartAnimalInteractionJob(Pawn handler, Pawn animal)
        {
            if (!CanStartAnimalInteraction(handler, animal)) return false;

            Job job = TryMakeBedAnimalInteractionJob(handler, animal);
            if (job == null && xxx.can_rape(handler))
            {
                job = JobMaker.MakeJob(xxx.bestiality, animal);
            }

            if (job == null) return false;

            // 床上与地面任务使用不同RJW驱动，必须检查最终Job，不能一概按普通或强制请求预检。
            // 抽签冷却和已经完成的训练/驯服成长已在上层消耗，拒绝不会返还，也不尝试另一分支。
            job.expiryInterval = GenDate.TicksPerHour;
            job.playerForced = true;
            if (!SSCRestrictionJobGuard.PrepareEvent(handler, job, SSCRestrictionEvent.Dog))
            {
                JobMaker.ReturnToPool(job);
                return false;
            }
            // 使用直接启动，事件不受玩家Shift排队键影响；收到任务仍不代表场景已经开始。
            handler.jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.Misc, preToilReservationsCanFail: true);
            if (handler.CurJob == job) return true;
            SSCRestrictionJobGuard.CancelPendingEvent(job);
            handler.jobs.jobQueue.RemoveAll(handler, queued => queued == job);
            return false;
        }

        /// <summary>保留动物、身体、敌对、精神状态及可达性门槛，不用事件身份绕过正常任务条件。</summary>
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

        /// <summary>按双方床位及可达性选择原有普通驱动，不在这里修改床位或同床资格。</summary>
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

        /// <summary>沿用宠物进度及终极状态的成长倍率，不读取或改写行为许可。</summary>
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

        /// <summary>计算原有事件概率并应用上限，限制拒绝不会改变概率。</summary>
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

        /// <summary>识别正在进行的RJW任务，防止事件抢占既有场景。</summary>
        private static bool IsInSexJob(Pawn pawn)
        {
            return pawn?.jobs?.curDriver is JobDriver_Sex;
        }

        /// <summary>只为有效宠物狗与存活动物处理工作通知。</summary>
        private static bool IsValidDogAnimalHandlingPair(Pawn handler, Pawn animal)
        {
            return handler != null &&
                   animal != null &&
                   animal.RaceProps.Animal &&
                   !handler.Dead &&
                   !animal.Dead &&
                   IsDogSpecialized(handler);
        }

        /// <summary>确认结算对象是一人一动物，避免将其他互动计入宠物成长。</summary>
        private static bool IsHumanAnimalPair(Pawn a, Pawn b)
        {
            return (xxx.is_human(a) && xxx.is_animal(b)) ||
                   (xxx.is_animal(a) && xxx.is_human(b));
        }
    }
}
