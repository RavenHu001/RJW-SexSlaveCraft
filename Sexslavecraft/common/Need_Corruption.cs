using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using rjw;
using UnityEngine;
using Verse;
using static rjw.GenderHelper;

namespace SexSlaveCraft
{
    public class Need_Corruption : Need
    {
        private float highestCorruptionLevel;

        public Need_Corruption(Pawn pawn) : base(pawn)
        {
            SetInitialLevel();
            threshPercents = new List<float>();
            foreach (var stage in stages)
            {
                threshPercents.Add(stage.threshold);
            }
        }

        public override float MaxLevel => 1f;

        public float HighestCorruptionLevel => Mathf.Max(highestCorruptionLevel, CurLevel);
        public bool HasEverBeenCorrupted => HighestCorruptionLevel > 0f;

        private List<(float threshold, string label)> stages = new List<(float, string)>
        {
            (0.95f, Strings.Stage_Extreme),
            (0.75f, Strings.Stage_Severe),
            (0.5f,  Strings.Stage_Moderate),
            (0.25f, Strings.Stage_Minor),
            (0.05f, Strings.Stage_Slight)
        };

        private string GetStageLabel()
        {
            foreach (var stage in stages)
            {
                if (CurLevelPercentage >= stage.threshold)
                    return stage.label;
            }
            return Strings.Stage_Stable;
        }

        private float GetMinLevel()
        {
            return GetMinLevel(0f, MaxLevel);
        }

        private float GetMinLevel(float stageFloor, float stageCap)
        {
            float minLevel = 0f;
            foreach (var apparel in pawn.apparel?.WornApparel ?? new List<Apparel>())
            {
                var comp = apparel.TryGetComp<CompApparel_CorruptionModifier>();
                if (comp == null) continue;

                minLevel = Mathf.Max(minLevel, GetActiveMinLevel(comp.MinCorruptionLevel, stageFloor, stageCap));
            }

            foreach (var hediff in pawn.health?.hediffSet?.hediffs ?? new List<Hediff>())
            {
                if (hediff is HediffWithComps hwc)
                {
                    foreach (var hc in hwc.comps)
                    {
                        if (hc is ICorruptionModifierSource src)
                            minLevel = Mathf.Max(minLevel, GetActiveMinLevel(src.GetMinCorruption(), stageFloor, stageCap));
                    }
                }
            }

            return Mathf.Clamp(minLevel, 0f, MaxLevel);
        }

        private float GetActiveMinLevel(float requestedFloor, float stageFloor, float stageCap)
        {
            if (requestedFloor <= 0f) return 0f;
            if (HighestCorruptionLevel + 0.0001f < requestedFloor) return 0f;
            if (requestedFloor + 0.0001f < stageFloor) return stageFloor;
            if (requestedFloor > stageCap + 0.0001f) return 0f;
            return requestedFloor;
        }

        public void RecordCurrentCorruption()
        {
            highestCorruptionLevel = Mathf.Max(highestCorruptionLevel, CurLevel);
        }

        public void SetCorruption(float level, bool resetHistory = false)
        {
            CurLevel = Mathf.Clamp(level, 0f, MaxLevel);
            if (resetHistory)
                highestCorruptionLevel = CurLevel;
            else
                RecordCurrentCorruption();
            SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(pawn);
        }

        public void RestoreCorruption(float level, float historicalMaximum)
        {
            CurLevel = Mathf.Clamp(level, 0f, MaxLevel);
            float restoredMaximum = historicalMaximum >= 0f ? historicalMaximum : CurLevel;
            highestCorruptionLevel = Mathf.Clamp(Mathf.Max(CurLevel, restoredMaximum), 0f, MaxLevel);
            SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(pawn);
        }

        public override void NeedInterval()
        {
            if (IsFrozen) return;

            // EN: Apparel floors preserve milestones already reached; they never grant a new milestone.
            // CN: 装备地板只维持已经达到过的里程碑，不会凭空授予新的恶堕阶段。
            RecordCurrentCorruption();
            SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(pawn);

            if (SSCMod.settings?.useOldScoring ?? false)
            {
                float minLevel = GetMinLevel();
                CurLevel = Mathf.Clamp(CurLevel, minLevel, MaxLevel);
                return;
            }

            if (SSCMod.settings != null && !SSCMod.settings.enableCorruptionDecay)
            {
                float disabledDecayStageFloor = TrainingOutcomeUtility.GetCorruptionStageFloor(pawn);
                float disabledDecayCap = TrainingOutcomeUtility.GetChainCap(pawn);
                float disabledDecayMinLevel = GetMinLevel(disabledDecayStageFloor, disabledDecayCap);
                float disabledDecayFloor = Mathf.Max(disabledDecayStageFloor, disabledDecayMinLevel);
                CurLevel = Mathf.Clamp(CurLevel, disabledDecayFloor, disabledDecayCap);
                return;
            }

            float sexLevel = pawn.needs.TryGetNeed<Need_Sex>()?.CurLevel ?? 0.5f;
            float sexFactor = 1f - 1.5f * (sexLevel - 0.8f) * (sexLevel - 0.8f);
            sexFactor = Mathf.Clamp(sexFactor, 0.2f, 1.0f);

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            float lastScore = comp?.lastTrainingScore ?? 0f;
            float scoreFactor = Mathf.Clamp(1f - lastScore / 200f, 0.3f, 1.0f);

            float opinion = 0f;
            Hediff chainForOpinion = pawn.health?.hediffSet?.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_ChainOfSexSlave"));
            if (chainForOpinion is Hediff_ChainOfSexSlave chain && chain.LinkedPawn != null)
            {
                opinion = pawn.relations.OpinionOf(chain.LinkedPawn);
            }
            float opinionFactor = Mathf.Clamp(1f - opinion / 200f, 0.3f, 1.0f);

            float chainFactor = TrainingOutcomeUtility.GetChainDecayFactor(pawn);
            float partFactor = TrainingOutcomeUtility.GetPartDecayFactor(pawn);

            float modifier = 1f;
            foreach (var apparel in pawn.apparel?.WornApparel ?? new List<Apparel>())
            {
                var c = apparel.TryGetComp<CompApparel_CorruptionModifier>();
                if (c != null) modifier *= c.CorruptionModifier;
            }

            foreach (var hediff in pawn.health?.hediffSet?.hediffs ?? new List<Hediff>())
            {
                if (hediff is HediffWithComps hwc)
                {
                    foreach (var hc in hwc.comps)
                    {
                        if (hc is ICorruptionModifierSource src)
                            modifier *= src.GetDecayMultiplier();
                    }
                }
            }

            float baseDecayPerDay = SSCMod.settings?.corruptionDecayPerDay ?? 0.02f;
            float decayPerDay = baseDecayPerDay * sexFactor * scoreFactor * opinionFactor * chainFactor * partFactor * modifier;
            float decayPerTick = decayPerDay / 60000f;
            CurLevel -= decayPerTick * 150f;

            float chainSev = 0f;
            Hediff chainHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_ChainOfSexSlave"));
            if (chainHediff != null)
            {
                chainSev = chainHediff.Severity;
                float stageFloor = TrainingOutcomeUtility.GetCorruptionStageFloor(pawn);
                if (CurLevel < stageFloor && chainSev >= 0.1f)
                {
                    float newSev = chainSev - 0.1f;
                    if (newSev >= 0.3f && newSev < 0.5f) newSev = 0.3f;
                    else if (newSev >= 0.1f && newSev < 0.3f) newSev = 0.1f;
                    else newSev = 0f;
                    chainHediff.Severity = newSev;
                }
            }

            float stageMin = TrainingOutcomeUtility.GetCorruptionStageFloor(pawn);
            float brakeCap = TrainingOutcomeUtility.GetChainCap(pawn);
            float equipMin = GetMinLevel(stageMin, brakeCap);
            float floor = Mathf.Max(stageMin, equipMin);

            CurLevel = Mathf.Clamp(CurLevel, floor, brakeCap);
        }

        public override void SetInitialLevel()
        {
            CurLevel = 0f;
            highestCorruptionLevel = 0f;
        }

        public override string GetTipString()
        {
            string stage = GetStageLabel();
            return Strings.Need_TipFormat(def.LabelCap, CurLevelPercentage.ToStringPercent(), stage, def.description);
        }

        public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = int.MaxValue,
            float customMargin = -1f, bool drawArrows = true, bool doTooltip = true,
            Rect? rectForTooltip = null, bool drawLabel = true)
        {
            if (threshPercents == null)
                threshPercents = new List<float>();
            threshPercents.Clear();
            foreach (var stage in stages)
            {
                threshPercents.Add(stage.threshold);
            }

            base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows, doTooltip, rectForTooltip, drawLabel);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref highestCorruptionLevel, "highestCorruptionLevel", -1f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // EN: Existing saves had no milestone field, so migrate them from their current corruption.
                // CN: 旧存档没有最高恶堕字段，因此用读档时的当前恶堕值完成迁移。
                if (highestCorruptionLevel < 0f)
                    highestCorruptionLevel = CurLevel;
                else
                    RecordCurrentCorruption();
                SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(pawn);
            }
        }
    }

    public class CompApparel_CorruptionModifier : ThingComp
    {
        private Pawn Pawn
        {
            get
            {
                var apparel = parent as Apparel;
                if (apparel != null)
                {
                    return apparel.Wearer;
                }
                return null;
            }
        }

        public CompProperties_CorruptionModifier Props => (CompProperties_CorruptionModifier)props;

        public bool IsTraitValid()
        {
            if (Pawn == null)
                return false;
            return SSCIdentityUtility.GetSexSlaveStage(Pawn) > 0;
        }

        public float CorruptionModifier => Props.corruptionMultiplier;

        public float MinCorruptionLevel
        {
            get
            {
                if (IsTraitValid())
                {
                    return Props.minCorruptionLevel;
                }
                else
                {
                    return 0f;
                }
            }
        }
    }

    public class CompProperties_CorruptionModifier : CompProperties
    {
        public float corruptionMultiplier = 1f;
        public float minCorruptionLevel = -1f;

        public CompProperties_CorruptionModifier()
        {
            this.compClass = typeof(CompApparel_CorruptionModifier);
        }
    }
}
