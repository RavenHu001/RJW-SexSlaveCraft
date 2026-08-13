using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public static class PetSpecializationUtility
    {
        private const float ThresholdProgress = 0.20f;
        private const float InitialHediffSeverity = 0.01f;
        private const float AutoAffectionMaxDistance = 2.9f;

        private static readonly SexSlaveSpecializationType[] PetTypes =
        {
            SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog,
            SexSlaveSpecializationType.PetRabbit
        };

        public static bool IsPetSpecialization(SexSlaveSpecializationType type)
        {
            return type == SexSlaveSpecializationType.PetCat ||
                   type == SexSlaveSpecializationType.PetDog ||
                   type == SexSlaveSpecializationType.PetRabbit;
        }

        public static HediffDef GetBaseHediffDef(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetCat");
                case SexSlaveSpecializationType.PetDog:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetDog");
                case SexSlaveSpecializationType.PetRabbit:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetRabbit");
                default:
                    return null;
            }
        }

        public static HediffDef GetFinalHediffDef(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetCat_Final");
                case SexSlaveSpecializationType.PetDog:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetDog_Final");
                case SexSlaveSpecializationType.PetRabbit:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetRabbit_Final");
                default:
                    return null;
            }
        }

        public static string GetResearchDefName(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return "SSC_RES_PetCat";
                case SexSlaveSpecializationType.PetDog:
                    return "SSC_RES_PetDog";
                case SexSlaveSpecializationType.PetRabbit:
                    return "SSC_RES_PetRabbit";
                default:
                    return null;
            }
        }

        public static bool HasAnyPetState(Pawn pawn, SexSlaveSpecializationType type)
        {
            if (pawn?.health?.hediffSet == null || !IsPetSpecialization(type)) return false;

            HediffDef baseDef = GetBaseHediffDef(type);
            HediffDef finalDef = GetFinalHediffDef(type);
            return (baseDef != null && pawn.health.hediffSet.HasHediff(baseDef)) ||
                   (finalDef != null && pawn.health.hediffSet.HasHediff(finalDef));
        }

        public static bool HasFinalPetState(Pawn pawn, SexSlaveSpecializationType type)
        {
            if (pawn?.health?.hediffSet == null || !IsPetSpecialization(type)) return false;

            HediffDef finalDef = GetFinalHediffDef(type);
            return finalDef != null && pawn.health.hediffSet.HasHediff(finalDef);
        }

        public static bool IsPetTag(HediffDef def)
        {
            if (def == null) return false;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                if (def == GetBaseHediffDef(type) || def == GetFinalHediffDef(type))
                {
                    return true;
                }
            }

            return false;
        }

        public static HediffDef GetBaseHediffForFinal(HediffDef finalDef)
        {
            if (finalDef == null) return null;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                if (finalDef == GetFinalHediffDef(type))
                {
                    return GetBaseHediffDef(type);
                }
            }

            return null;
        }

        public static void EnsurePetHediffFromSpecialization(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !IsPetSpecialization(comp.specializationType)) return;
            if (HasFinalPetState(pawn, comp.specializationType)) return;

            HediffDef baseDef = GetBaseHediffDef(comp.specializationType);
            if (baseDef == null) return;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(baseDef) ?? pawn.health.AddHediff(baseDef);
            if (hediff != null)
            {
                hediff.Severity = Mathf.Max(InitialHediffSeverity, comp.specializationProgress);
            }
        }

        public static void SyncPetStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                HediffDef finalDef = GetFinalHediffDef(type);
                HediffDef baseDef = GetBaseHediffDef(type);
                if (finalDef == null || baseDef == null) continue;

                Hediff finalHediff = pawn.health.hediffSet.GetFirstHediffOfDef(finalDef);
                Hediff baseHediff = pawn.health.hediffSet.GetFirstHediffOfDef(baseDef);
                if (finalHediff != null && baseHediff != null)
                {
                    pawn.health.RemoveHediff(baseHediff);
                }
            }

            EnsurePetHediffFromSpecialization(pawn);
        }

        public static bool CanUsePetSpecialization(Pawn pawn, SexSlaveSpecializationType type, out string reason)
        {
            reason = null;
            if (pawn == null || !IsPetSpecialization(type))
            {
                reason = Strings.ITab_SpecializationPetDisabledMissingRequirements;
                return false;
            }

            string researchDefName = GetResearchDefName(type);
            ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(researchDefName);
            if (research != null && !ResearchUtils.IsResearchFinished(research))
            {
                reason = Strings.ITab_SpecializationPetDisabledResearch;
                return false;
            }

            return true;
        }

        public static bool TryGainPetProgress(Pawn pawn, SexSlaveSpecializationType type, float amount, bool showThresholdMessage = true)
        {
            if (pawn == null || amount <= 0f || !IsPetSpecialization(type)) return false;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.specializationType != type) return false;

            float oldProgress = comp.specializationProgress;
            comp.AddSpecializationProgress(amount);
            EnsurePetHediffFromSpecialization(pawn);

            if (showThresholdMessage && oldProgress < ThresholdProgress && comp.specializationProgress >= ThresholdProgress)
            {
                Messages.Message(
                    Strings.Message_PetSpecializationUnlocked(pawn.LabelShort, GetSpecializationLabel(type)),
                    pawn,
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }

            return true;
        }

        public static bool TryStartAutomaticPetAffectionJob(Pawn pet, CompSexSlaveTraining comp = null)
        {
            if (pet == null) return false;
            comp = comp ?? pet.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !IsPetSpecialization(comp.specializationType)) return false;

            Pawn master = SSCBondUtility.GetResolvedMaster(pet);
            if (!CanDoPetAffectionNow(pet, master)) return false;
            if (!IsPetAffectionCooldownReady(comp)) return false;
            if (!CanStartPetAffectionJobWithoutDisruptingWork(pet)) return false;
            if (SSCDefOf.SSC_Job_PetAffection == null) return false;

            Job job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, master);
            job.expiryInterval = GenTicks.TickRareInterval;
            return pet.jobs != null && pet.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public static bool CompletePetAffection(Pawn pet, Pawn master)
        {
            if (pet == null) return false;
            CompSexSlaveTraining comp = pet.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !IsPetSpecialization(comp.specializationType)) return false;
            if (!CanDoPetAffectionNow(pet, master)) return false;
            if (!IsPetAffectionCooldownReady(comp)) return false;

            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("SSC_PetAffection_Mood");
            if (thoughtDef != null && master?.needs?.mood?.thoughts?.memories != null)
            {
                master.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, pet);
            }

            comp.lastPetAffectionTick = Find.TickManager.TicksGame;

            // EN: Pet affection is deliberately not adding specialization experience yet.
            // CN: 亲昵动作暂时不增加宠物专精经验；等经验来源和数值敲定后，再接 TryGainPetProgress。
            return true;
        }

        public static bool CanDoPetAffectionNow(Pawn pet, Pawn master)
        {
            if (pet == null || master == null || master.DestroyedOrNull() || master.Dead)
            {
                return false;
            }

            if (!pet.Spawned || !master.Spawned || pet.Map != master.Map)
            {
                return false;
            }

            if (pet.Dead || pet.Downed || master.Downed || pet.Drafted || master.Drafted)
            {
                return false;
            }

            return pet.Position.InHorDistOf(master.Position, AutoAffectionMaxDistance);
        }

        private static bool IsPetAffectionCooldownReady(CompSexSlaveTraining comp)
        {
            return comp != null &&
                   Find.TickManager.TicksGame - comp.lastPetAffectionTick >= CompSexSlaveTraining.PetAffectionCooldownTicks;
        }

        private static bool CanStartPetAffectionJobWithoutDisruptingWork(Pawn pet)
        {
            if (pet?.jobs == null) return false;

            JobDef curJobDef = pet.CurJobDef;
            if (curJobDef == null) return true;
            if (curJobDef == SSCDefOf.SSC_Job_PetAffection) return false;

            // EN: This affection job is only allowed to replace idle waiting jobs.
            // CN: 亲昵 job 只允许替换空闲等待类 job，避免打断搬运、建造、战斗等正常工作。
            if (curJobDef == JobDefOf.Wait || curJobDef == JobDefOf.Wait_Wander)
            {
                return true;
            }

            string defName = curJobDef.defName;
            return defName == "Wait_MaintainPosture" || defName == "GotoWander";
        }

        public static string GetSpecializationLabel(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return Strings.ITab_SpecializationPetCat;
                case SexSlaveSpecializationType.PetDog:
                    return Strings.ITab_SpecializationPetDog;
                case SexSlaveSpecializationType.PetRabbit:
                    return Strings.ITab_SpecializationPetRabbit;
                default:
                    return Strings.ITab_SpecializationNone;
            }
        }

        public static string GetSelectLabel(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return Strings.ITab_SelectSpecializationPetCat;
                case SexSlaveSpecializationType.PetDog:
                    return Strings.ITab_SelectSpecializationPetDog;
                case SexSlaveSpecializationType.PetRabbit:
                    return Strings.ITab_SelectSpecializationPetRabbit;
                default:
                    return Strings.ITab_SelectSpecializationNone;
            }
        }

        public static void StoreExclusivePetTags(CompPersonalityStore store, Pawn pawn)
        {
            if (store == null || pawn?.health?.hediffSet == null) return;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                HediffDef baseDef = GetBaseHediffDef(type);
                HediffDef finalDef = GetFinalHediffDef(type);

                store.RemoveTag(baseDef);
                store.RemoveTag(finalDef);

                Hediff finalHediff = finalDef != null ? pawn.health.hediffSet.GetFirstHediffOfDef(finalDef) : null;
                if (finalHediff != null)
                {
                    store.SetTag(finalDef, finalHediff.Severity);
                    continue;
                }

                Hediff baseHediff = baseDef != null ? pawn.health.hediffSet.GetFirstHediffOfDef(baseDef) : null;
                if (baseHediff != null)
                {
                    store.SetTag(baseDef, baseHediff.Severity);
                }
            }
        }

        public static void ApplyExclusivePetTags(Pawn pawn, CompPersonalityStore data)
        {
            if (pawn?.health?.hediffSet == null || data?.hediffTags == null) return;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                HediffDef baseDef = GetBaseHediffDef(type);
                HediffDef finalDef = GetFinalHediffDef(type);

                bool hasFinal = finalDef != null && data.HasTag(finalDef);
                bool hasBase = baseDef != null && data.HasTag(baseDef);

                RemoveIfPresent(pawn, baseDef);
                RemoveIfPresent(pawn, finalDef);

                if (hasFinal)
                {
                    ApplyTag(pawn, finalDef, data.GetTagSeverity(finalDef));
                    continue;
                }

                if (hasBase)
                {
                    ApplyTag(pawn, baseDef, data.GetTagSeverity(baseDef));
                }
            }
        }

        public static void RemoveAllPetStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                RemoveIfPresent(pawn, GetBaseHediffDef(type));
                RemoveIfPresent(pawn, GetFinalHediffDef(type));
            }
        }

        private static void ApplyTag(Pawn pawn, HediffDef def, float severity)
        {
            if (pawn == null || def == null) return;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def) ?? pawn.health.AddHediff(def);
            if (hediff != null)
            {
                hediff.Severity = severity > 0f ? severity : 1f;
            }
        }

        private static void RemoveIfPresent(Pawn pawn, HediffDef def)
        {
            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(def);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
