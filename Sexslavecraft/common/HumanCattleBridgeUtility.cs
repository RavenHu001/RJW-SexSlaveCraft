using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class HumanCattleBridgeUtility
    {
        private const string MilkedTrackerTypeName = "HumanCattle.HediffComp_MilkedTracker";
        private const int LegacyCleanupInterval = 2500;
        private static Type milkedTrackerType;
        private static bool loadStateResolved;

        public static bool IsLoaded => MilkedTrackerType != null;

        private static Type MilkedTrackerType
        {
            get
            {
                if (!loadStateResolved)
                {
                    loadStateResolved = true;
                    milkedTrackerType = SoftDependencyUtility.FindOptionalType(MilkedTrackerTypeName);
                }

                return milkedTrackerType;
            }
        }

        public static void ApplyDoopMode(HediffComp_PermanentLactating sourceComp)
        {
            Pawn pawn = sourceComp?.Pawn;
            if (!IsLoaded || pawn?.health?.hediffSet == null) return;

            sourceComp.CurrentCharge = 0f;
            if (pawn.IsHashIntervalTick(LegacyCleanupInterval))
            {
                CleanupLegacyBridge(pawn);
            }
        }

        public static void CleanupBridge(Pawn pawn)
        {
            if (!IsLoaded || pawn?.health?.hediffSet == null) return;
            CleanupLegacyBridge(pawn);
        }

        private static void CleanupLegacyBridge(Pawn pawn)
        {
            HediffDef markerDef = SSCDefOf.SSC_HumanCattleLactationBridge;
            if (markerDef == null) return;

            Hediff marker = pawn.health.hediffSet.GetFirstHediffOfDef(markerDef);
            if (marker == null) return;

            pawn.health.RemoveHediff(marker);

            Hediff legacyShell = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Lactating);
            if (legacyShell != null)
            {
                pawn.health.RemoveHediff(legacyShell);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class HumanCattleCowStatBootstrap
    {
        static HumanCattleCowStatBootstrap()
        {
            if (!HumanCattleBridgeUtility.IsLoaded) return;

            StatDef lactationFactor = DefDatabase<StatDef>.GetNamedSilentFail("BaseLactationFactor");
            if (lactationFactor == null) return;

            MakeSSCSourceLactationNeutral();
            AddStageFactors(SSCDefOf.SSC_Hediff_Cow, lactationFactor, 1.10f, 1.25f, 1.40f);
            AddStageFactors(SSCDefOf.SSC_Hediff_Cow_Final, lactationFactor, 1.50f);
        }

        private static void MakeSSCSourceLactationNeutral()
        {
            if (SSCDefOf.SSC_Lactating_SubState?.stages == null) return;

            foreach (HediffStage stage in SSCDefOf.SSC_Lactating_SubState.stages)
            {
                if (stage != null)
                {
                    stage.fertilityFactor = 1f;
                }
            }
        }

        private static void AddStageFactors(HediffDef hediffDef, StatDef stat, params float[] factors)
        {
            if (hediffDef?.stages == null || factors == null || factors.Length == 0) return;

            for (int i = 0; i < hediffDef.stages.Count; i++)
            {
                HediffStage stage = hediffDef.stages[i];
                if (stage == null) continue;

                float factor = factors[Math.Min(i, factors.Length - 1)];
                stage.statFactors = stage.statFactors ?? new List<StatModifier>();

                StatModifier existing = stage.statFactors.Find(modifier => modifier.stat == stat);
                if (existing != null)
                {
                    existing.value *= factor;
                }
                else
                {
                    stage.statFactors.Add(new StatModifier
                    {
                        stat = stat,
                        value = factor
                    });
                }
            }
        }
    }
}
