using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class LifeForceConflictUtility
    {
        private static readonly string[] ConflictHediffDefNames =
        {
            "Hediff_ChainOfSexSlave",
            "Hediff_BridleOfSexSlave",
            "Hediff_SSC_PersonalityExcreting",
            "SSC_PersonalityExcreted_Done",
            "SSC_PostExcretionComa",
            "Hediff_GelatinizationTemporary",
            "Hediff_GelatinizedArm",
            "Hediff_GelatinizedLeg",
            "SSC_FullGelatinizationTemporary",
            "SSC_FullGelatinizedBody"
        };

        private static readonly List<HediffDef> CachedConflictHediffs = new List<HediffDef>();
        private static bool initialized;

        private static GeneDef LifeForceGene => DefDatabase<GeneDef>.GetNamedSilentFail("rjw_genes_lifeforce");

        public static bool TryRemoveLifeForceGeneIfConflicting(Pawn pawn, bool notify = true)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null || pawn.health?.hediffSet == null)
            {
                return false;
            }

            GeneDef lifeForceGene = LifeForceGene;
            if (lifeForceGene == null || !pawn.genes.HasActiveGene(lifeForceGene))
            {
                return false;
            }

            if (!HasConflictingSSCHediffs(pawn))
            {
                return false;
            }

            Gene gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == lifeForceGene);
            if (gene == null)
            {
                return false;
            }

            SpawnLifeForceEmbryo(pawn, lifeForceGene);
            pawn.genes.RemoveGene(gene);

            if (notify)
            {
                Messages.Message("SSC_LifeForce_Removed".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NeutralEvent, false);
            }

            return true;
        }

        private static bool HasConflictingSSCHediffs(Pawn pawn)
        {
            EnsureHediffCache();
            for (int i = 0; i < CachedConflictHediffs.Count; i++)
            {
                if (pawn.health.hediffSet.HasHediff(CachedConflictHediffs[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static void SpawnLifeForceEmbryo(Pawn pawn, GeneDef geneDef)
        {
            if (pawn.MapHeld == null || geneDef == null) return;

            Genepack genepack = ThingMaker.MakeThing(ThingDefOf.Genepack) as Genepack;
            if (genepack == null) return;

            genepack.Initialize(new List<GeneDef> { geneDef });
            GenPlace.TryPlaceThing(genepack, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
        }

        private static void EnsureHediffCache()
        {
            if (initialized) return;
            initialized = true;

            for (int i = 0; i < ConflictHediffDefNames.Length; i++)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(ConflictHediffDefNames[i]);
                if (def != null)
                {
                    CachedConflictHediffs.Add(def);
                }
            }
        }
    }
}
