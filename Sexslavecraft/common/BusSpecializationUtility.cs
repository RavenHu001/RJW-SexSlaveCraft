using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public static class BusSpecializationUtility
    {
        private const float ProgressPerQualifiedSex = 0.05f;
        private const float ThresholdProgress = 0.20f;
        private const float CowProgressPerMilk = 0.35f;
        private const float InitialHediffSeverity = 0.01f;

        public static bool IsBusRelated(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            return pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Bus)
                || pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Bus_Final);
        }

        public static bool HasAnyBusState(Pawn pawn)
        {
            return IsBusRelated(pawn);
        }

        public static bool HasFinalBusState(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.HasHediff(SSCDefOf.SSC_Hediff_Bus_Final) ?? false;
        }

        public static bool IsCowRelated(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            return pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Cow)
                || pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Cow_Final);
        }

        public static bool HasAnyCowState(Pawn pawn)
        {
            return IsCowRelated(pawn);
        }

        public static bool HasFinalCowState(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.HasHediff(SSCDefOf.SSC_Hediff_Cow_Final) ?? false;
        }

        public static bool ShouldProcessBusGrowth(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && comp.IsBusSpecialized;
        }

        public static bool ShouldProcessCowGrowth(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && comp.IsCowSpecialized;
        }

        public static void EnsureBusHediffFromSpecialization(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            if (HasFinalBusState(pawn)) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !comp.IsBusSpecialized) return;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Bus) ?? pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Bus);
            if (hediff != null)
            {
                hediff.Severity = Mathf.Max(InitialHediffSeverity, comp.specializationProgress);
            }
        }

        public static void SyncBusStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            Hediff finalBus = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Bus_Final);
            Hediff baseBus = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Bus);
            if (finalBus != null && baseBus != null)
            {
                pawn.health.RemoveHediff(baseBus);
                return;
            }

            EnsureBusHediffFromSpecialization(pawn);
        }

        public static void EnsureCowHediffFromSpecialization(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            if (HasFinalCowState(pawn)) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !comp.IsCowSpecialized) return;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow) ?? pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Cow);
            if (hediff != null)
            {
                hediff.Severity = Mathf.Max(InitialHediffSeverity, comp.specializationProgress);
            }
        }

        public static void SyncCowStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            Hediff finalCow = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow_Final);
            Hediff baseCow = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow);
            if (finalCow != null && baseCow != null)
            {
                pawn.health.RemoveHediff(baseCow);
                return;
            }

            EnsureCowHediffFromSpecialization(pawn);
        }

        public static void TryGainBusProgressFromSex(Pawn pawn, Pawn otherPawn)
        {
            if (pawn == null || otherPawn == null || pawn == otherPawn) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !comp.IsBusSpecialized) return;

            Pawn owner = GetOwner(pawn, comp);
            if (owner != null && owner == otherPawn) return;

            float oldProgress = comp.specializationProgress;
            comp.AddSpecializationProgress(ProgressPerQualifiedSex);
            EnsureBusHediffFromSpecialization(pawn);
            if (oldProgress < ThresholdProgress && comp.specializationProgress >= ThresholdProgress)
            {
                Messages.Message(Strings.Message_BusSpecializationUnlocked(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        public static void TryGainCowProgressFromMilkAmount(Pawn pawn, float producedMilk)
        {
            if (pawn == null || producedMilk <= 0f) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !comp.IsCowSpecialized) return;
            if (!pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState)) return;

            float oldProgress = comp.specializationProgress;
            comp.AddSpecializationProgress(producedMilk * CowProgressPerMilk);
            EnsureCowHediffFromSpecialization(pawn);
            if (oldProgress < ThresholdProgress && comp.specializationProgress >= ThresholdProgress)
            {
                Messages.Message(Strings.Message_CowSpecializationUnlocked(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        public static bool CanUseBusSpecialization(Pawn pawn, out string reason)
        {
            reason = null;
            ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("SSC_RES_PublicUse");
            if (research != null && !ResearchUtils.IsResearchFinished(research))
            {
                reason = Strings.ITab_SpecializationBusDisabledResearch;
                return false;
            }

            return true;
        }

        public static bool CanUseCowSpecialization(Pawn pawn, out string reason)
        {
            reason = null;
            if (pawn == null)
            {
                reason = Strings.ITab_SpecializationCowDisabledMissingRequirements;
                return false;
            }

            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_RES_CowTraining))
            {
                reason = Strings.ITab_SpecializationCowDisabledResearch;
                return false;
            }

            if (SSCIdentityUtility.GetSexSlaveStage(pawn) < 2)
            {
                reason = Strings.ITab_SpecializationCowDisabledDegree;
                return false;
            }

            if (!pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState))
            {
                reason = Strings.ITab_SpecializationCowDisabledLactation;
                return false;
            }

            return true;
        }

        public static float GetCowLactationBoostMultiplier(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !comp.IsCowSpecialized || !comp.milkProductionEnabled) return 0f;
            if (comp.specializationProgress >= 0.999f) return 1.60f;
            if (comp.specializationProgress >= 0.50f) return 1.30f;
            if (comp.specializationProgress >= 0.20f) return 1.00f;
            return 0.60f;
        }

        public static bool CanAccelerateMilkProduction(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && comp.IsCowSpecialized && comp.milkProductionEnabled;
        }

        public static HediffComp_CowMilkReservoir GetCowMilkReservoirComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow_Final)
                ?? pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow);
            return hediff?.TryGetComp<HediffComp_CowMilkReservoir>();
        }

        public static Pawn GetOwner(Pawn pawn, CompSexSlaveTraining comp = null)
        {
            return SSCBondUtility.GetResolvedMaster(pawn);
        }

        public static void StoreExclusiveBusTags(CompPersonalityStore store, Pawn pawn)
        {
            if (store == null || pawn?.health?.hediffSet == null) return;

            store.RemoveTag(SSCDefOf.SSC_Hediff_Bus);
            store.RemoveTag(SSCDefOf.SSC_Hediff_Bus_Final);

            Hediff finalBus = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Bus_Final);
            if (finalBus != null)
            {
                store.SetTag(SSCDefOf.SSC_Hediff_Bus_Final, finalBus.Severity);
                return;
            }

            Hediff baseBus = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Bus);
            if (baseBus != null)
            {
                store.SetTag(SSCDefOf.SSC_Hediff_Bus, baseBus.Severity);
            }
        }

        public static void StoreExclusiveCowTags(CompPersonalityStore store, Pawn pawn)
        {
            if (store == null || pawn?.health?.hediffSet == null) return;

            store.RemoveTag(SSCDefOf.SSC_Hediff_Cow);
            store.RemoveTag(SSCDefOf.SSC_Hediff_Cow_Final);

            Hediff finalCow = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow_Final);
            if (finalCow != null)
            {
                store.SetTag(SSCDefOf.SSC_Hediff_Cow_Final, finalCow.Severity);
                return;
            }

            Hediff baseCow = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow);
            if (baseCow != null)
            {
                store.SetTag(SSCDefOf.SSC_Hediff_Cow, baseCow.Severity);
            }
        }

        public static void ApplyExclusiveBusTags(Pawn pawn, CompPersonalityStore data)
        {
            if (pawn?.health?.hediffSet == null || data?.hediffTags == null) return;

            bool hasFinal = data.HasTag(SSCDefOf.SSC_Hediff_Bus_Final);
            bool hasBase = data.HasTag(SSCDefOf.SSC_Hediff_Bus);

            RemoveIfPresent(pawn, SSCDefOf.SSC_Hediff_Bus);
            RemoveIfPresent(pawn, SSCDefOf.SSC_Hediff_Bus_Final);

            if (hasFinal)
            {
                ApplyTag(pawn, SSCDefOf.SSC_Hediff_Bus_Final, data.GetTagSeverity(SSCDefOf.SSC_Hediff_Bus_Final));
                return;
            }

            if (hasBase)
            {
                ApplyTag(pawn, SSCDefOf.SSC_Hediff_Bus, data.GetTagSeverity(SSCDefOf.SSC_Hediff_Bus));
            }
        }

        public static void ApplyExclusiveCowTags(Pawn pawn, CompPersonalityStore data)
        {
            if (pawn?.health?.hediffSet == null || data?.hediffTags == null) return;

            bool hasFinal = data.HasTag(SSCDefOf.SSC_Hediff_Cow_Final);
            bool hasBase = data.HasTag(SSCDefOf.SSC_Hediff_Cow);

            RemoveIfPresent(pawn, SSCDefOf.SSC_Hediff_Cow);
            RemoveIfPresent(pawn, SSCDefOf.SSC_Hediff_Cow_Final);

            if (hasFinal)
            {
                ApplyTag(pawn, SSCDefOf.SSC_Hediff_Cow_Final, data.GetTagSeverity(SSCDefOf.SSC_Hediff_Cow_Final));
                return;
            }

            if (hasBase)
            {
                ApplyTag(pawn, SSCDefOf.SSC_Hediff_Cow, data.GetTagSeverity(SSCDefOf.SSC_Hediff_Cow));
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
