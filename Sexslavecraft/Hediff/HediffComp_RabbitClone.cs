using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public class HediffCompProperties_RabbitCloneSource : HediffCompProperties
    {
        public HediffCompProperties_RabbitCloneSource()
        {
            compClass = typeof(HediffComp_RabbitCloneSource);
        }
    }

    // EN: Stored on the rabbit specialization hediff. This is the source-side list requested by design.
    // CN: 挂在兔专精 Hediff 上，用来保存“本体 -> 分身 Pawn”引用列表。
    public class HediffComp_RabbitCloneSource : HediffComp
    {
        private List<Pawn> clones = new List<Pawn>();

        public IReadOnlyList<Pawn> Clones => clones;

        public int LivingCloneCount
        {
            get
            {
                CleanupClones();
                return clones.Count;
            }
        }

        public bool HasClone(Pawn clone)
        {
            CleanupClones();
            return clone != null && clones.Contains(clone);
        }

        public bool CanRegisterMoreClones => LivingCloneCount < RabbitCloneUtility.MaxRabbitClones;

        public void RegisterClone(Pawn clone)
        {
            if (clone == null) return;
            CleanupClones();
            if (!clones.Contains(clone))
            {
                clones.Add(clone);
            }
        }

        public void ClearClones()
        {
            clones.Clear();
        }

        public void CleanupClones()
        {
            clones.RemoveAll(x => x == null || x.DestroyedOrNull() || x.Dead || !RabbitCloneUtility.IsRabbitCloneOf(x, Pawn));
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn source = Pawn;
            if (source == null || source.DestroyedOrNull() || source.Dead) return;
            if (RabbitCloneUtility.IsRabbitClone(source)) return;
            if (!source.IsHashIntervalTick(RabbitCloneUtility.RabbitCloneSyncIntervalTicks)) return;

            CleanupClones();
            if (clones.Count == 0) return;

            RabbitCloneUtility.SyncRabbitCloneNeeds(source, clones);
            RabbitCloneUtility.PropagateSourceMentalState(source, clones);
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (Pawn == null || RabbitCloneUtility.IsRabbitClone(Pawn)) return base.CompLabelInBracketsExtra;
                CleanupClones();
                return $"分身 {clones.Count}/{RabbitCloneUtility.MaxRabbitClones}";
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look(ref clones, "rabbitClones", LookMode.Reference);
            if (clones == null) clones = new List<Pawn>();
        }
    }

    public class HediffCompProperties_RabbitCloneLink : HediffCompProperties
    {
        public HediffCompProperties_RabbitCloneLink()
        {
            compClass = typeof(HediffComp_RabbitCloneLink);
        }
    }

    // EN: Stored on every clone. It records the source pawn and drives the three-day maturation.
    // CN: 挂在每个分身身上，保存本体引用，并推动三天成体。
    public class HediffComp_RabbitCloneLink : HediffComp
    {
        public Pawn source;
        public int bornTick = -1;
        public long startAgeTicks = -1;
        public bool mature;

        public void Initialize(Pawn sourcePawn, long initialAgeTicks)
        {
            source = sourcePawn;
            bornTick = Find.TickManager.TicksGame;
            startAgeTicks = Math.Max(1L, initialAgeTicks);
            mature = false;
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);

            if (bornTick < 0)
            {
                bornTick = Find.TickManager.TicksGame;
            }

            if (startAgeTicks < 0 && Pawn?.ageTracker != null)
            {
                startAgeTicks = Math.Max(1L, Pawn.ageTracker.AgeBiologicalTicks);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn clone = Pawn;
            if (clone == null || clone.DestroyedOrNull() || clone.Dead) return;
            if (!clone.IsHashIntervalTick(RabbitCloneUtility.RabbitCloneGrowthIntervalTicks)) return;

            HediffComp_RabbitCloneSource sourceComp = RabbitCloneUtility.GetRabbitCloneSourceComp(source);
            sourceComp?.RegisterClone(clone);

            if (!mature)
            {
                RabbitCloneUtility.TickRabbitCloneGrowth(clone, source, this);
            }
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (mature) return "成熟";
                float progress = RabbitCloneUtility.GetCloneGrowthProgress(this);
                return $"成型 {progress.ToStringPercent()}";
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref source, "rabbitCloneSource");
            Scribe_Values.Look(ref bornTick, "rabbitCloneBornTick", -1);
            Scribe_Values.Look(ref startAgeTicks, "rabbitCloneStartAgeTicks", -1L);
            Scribe_Values.Look(ref mature, "rabbitCloneMature", false);
        }
    }

    public class HediffCompProperties_RabbitClonePnaProduct : HediffCompProperties
    {
        public int spawnCount = 3;
        public float severityPerDay = 0.08f;
        public float nutritionPerDay = 0.08f;

        public HediffCompProperties_RabbitClonePnaProduct()
        {
            compClass = typeof(HediffComp_RabbitClonePnaProduct);
        }
    }

    // EN: Low-efficiency PNA output for mature rabbit clones. It reuses the product ThingDef from SSC_Purple_HyperLactation.
    // CN: 成熟兔分身的低效 PNA 产出。产物 ThingDef 从原有 SSC_Purple_HyperLactation 读取，避免重复硬编码物品名。
    public class HediffComp_RabbitClonePnaProduct : HediffComp
    {
        public HediffCompProperties_RabbitClonePnaProduct Props => (HediffCompProperties_RabbitClonePnaProduct)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !RabbitCloneUtility.IsMatureRabbitClone(pawn)) return;
            if (!pawn.IsHashIntervalTick(60)) return;

            if (parent.Severity < 1f)
            {
                float severityGain = Props.severityPerDay / GenDate.TicksPerDay * 60f;
                float nutritionCost = Props.nutritionPerDay / GenDate.TicksPerDay * 60f;

                if (pawn.needs?.food != null)
                {
                    if (pawn.needs.food.CurLevel >= nutritionCost)
                    {
                        pawn.needs.food.CurLevel -= nutritionCost;
                        parent.Severity += severityGain;
                    }
                }
                else
                {
                    parent.Severity += severityGain;
                }
            }

            if (parent.Severity >= 1f)
            {
                parent.Severity = 0f;
                SpawnProduct(pawn);
            }
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                string progress = (parent.Severity * 100f).ToString("F0") + "%";
                if (Pawn?.needs?.food != null && Pawn.needs.food.CurLevel < Props.nutritionPerDay / GenDate.TicksPerDay * 60f)
                {
                    return Strings.Produce_ProgressStalled(progress);
                }

                return Strings.Produce_Progress(progress);
            }
        }

        private void SpawnProduct(Pawn pawn)
        {
            ThingDef productDef = RabbitCloneUtility.GetPnaProductThingDef();
            if (productDef == null) return;

            Thing thing = ThingMaker.MakeThing(productDef);
            thing.stackCount = Props.spawnCount;

            if (pawn.Map != null)
            {
                GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                return;
            }

            Caravan caravan = pawn.GetCaravan();
            if (caravan != null)
            {
                CaravanInventoryUtility.GiveThing(caravan, thing);
            }
        }
    }
}
