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

            // 此处发生在本轮自然恶堕扣减之后。阶段来自锁链严重度，不来自当前恶堕或 Trait。
            // 仪式结算和需求刷新是两个入口：即使仪式只增加数值，后续刷新也可能触发退阶。
            Hediff chainHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_ChainOfSexSlave"));
            if (chainHediff != null)
            {
                float stageFloor = TrainingOutcomeUtility.GetCorruptionStageFloor(pawn);
                float stageCap = TrainingOutcomeUtility.GetChainCap(pawn);
                // EN: Apply earned equipment floors before checking decay, otherwise a
                // protected milestone can regress for one tick before its floor is restored.
                // CN: 先应用已解锁的装备底线，再判断衰减回退，避免底线恢复前误退阶。
                // GetMinLevel 仍沿用历史最高恶堕、装备/健康状态条件和当前阶段区间的校验；
                // 此修复改变的是应用顺序，不会使一个尚未解锁的高恶堕底线自动生效。
                // 例：已解锁 0.9 维持底线，当前恰好 0.9，衰减后略低于 0.9：
                // 必须先恢复到装备底线，再决定是否退阶，否则装备会在错误退阶后才生效。
                // 此时不能直接抬到 stageFloor：无装备保护时低于阶段底线应仍允许正常回退。
                CurLevel = Mathf.Max(CurLevel, GetMinLevel(stageFloor, stageCap));
                // 仅在实际每日衰减为正且保护后的恶堕仍不足时回退。
                // “关闭衰减”和“旧评分”已在前面的分支返回；这里补上“滑杆设为 0”
                // 或最终衰减倍率为 0 的情况，避免没有自然扣减时仍因已有数值不一致退阶。
                if (decayPerDay > 0f && CurLevel < stageFloor)
                {
                    // EN: Regress one actual stage, including the 90% -> 50% transition.
                    // CN: 按实际阶段退一级，包含 90% -> 50%，不能把高阶段落入兜底清零分支。
                    // 旧逻辑先减 0.1，再仅匹配 [0.1, 0.3) 和 [0.3, 0.5)；
                    // 例如严重度 1.1 得到 1.0（即使严重度为 1.0，也会得到 0.9），
                    // 两段都不匹配，最终错误赋值 0。
                    // 新逻辑按阶段表找前一级：0.9 档 -> 0.5，0.5 档 -> 0.3，
                    // 0.3 档 -> 0.1，0.1 档 -> 0；本轮只有这一次赋值，不循环连续退阶。
                    float previousSeverity = chainHediff.Severity;
                    chainHediff.Severity = TrainingOutcomeUtility.GetPreviousChainStageFloor(pawn);
                    // 自然退阶不经由 IncreaseChainSeverity。仅跌破第 3 阶段时
                    // 通知训导官退出或禁用终极记录，其余衰减不增加维护开销。
                    if (previousSeverity >= 0.5f && chainHediff.Severity < 0.5f)
                        TrainerSpecializationLifecycle.Notify(pawn);
                }
            }

            // 上面可能已经改变锁链阶段，必须重新读取底线和刹车上限。
            // 若复用退阶前的 stageFloor/stageCap，会用旧阶段的范围钳制恶堕，造成数据不一致。
            // 这一步沿用原有规则：最终恶堕仍被限制在退阶后的阶段/装备允许范围内。
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
