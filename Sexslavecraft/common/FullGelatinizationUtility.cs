using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

// EN: This file defines the rules for full gelatinization.
// EN: It checks whether a hollow pawn is ready for full-gel surgery, computes the success race, and resolves the final result.
// CN: 这个文件定义完全胶化的规则。
// CN: 它负责判断空壳 Pawn 是否准备好接受全凝胶化手术、计算成功竞赛，并结算最终结果。
namespace SexSlaveCraft
{
    public static class FullGelatinizationUtility
    {
        private const float RequirementCompleteThreshold = 0.999f;

        private static readonly List<TrainingRequirement> Requirements = new List<TrainingRequirement>
        {
            new TrainingRequirement("SSC_Exp_Hand", 0.15f, "SSC_FullGel_Requirement_Hand"),
            new TrainingRequirement("SSC_Exp_Foot", 0.15f, "SSC_FullGel_Requirement_Foot"),
            new TrainingRequirement("SSC_Exp_Oral", 0.15f, "SSC_FullGel_Requirement_Oral"),
            new TrainingRequirement("SSC_Exp_Breast", 0.15f, "SSC_FullGel_Requirement_Breast"),
            new TrainingRequirement("SSC_Exp_Genitals", 0.20f, "SSC_FullGel_Requirement_Genitals"),
            new TrainingRequirement("SSC_Exp_Anus", 0.20f, "SSC_FullGel_Requirement_Anus")
        };

        private static readonly HediffDef PersonalityExcretedDoneDef = SSCDefOf.SSC_PersonalityExcreted_Done;
        private static readonly HediffDef FullGelatinizedBodyDef = SSCDefOf.SSC_FullGelatinizedBody;

        public static bool HasPersonalityExcretionDone(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(PersonalityExcretedDoneDef) != null;
        }

        public static float GetCorruption(Pawn pawn)
        {
            return pawn?.needs?.TryGetNeed<Need_Corruption>()?.CurLevelPercentage ?? 0f;
        }

        public static float GetRequirementProgress(Pawn pawn, string hediffDefName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            if (pawn?.health?.hediffSet == null || def == null) return 0f;

            List<Hediff> matches = pawn.health.hediffSet.hediffs.Where(h => h.def == def).ToList();
            return CalculateAverageSeverity(matches);
        }

        public static float GetWeightedPartProgress(Pawn pawn)
        {
            float total = 0f;
            foreach (TrainingRequirement requirement in Requirements)
            {
                total += GetRequirementProgress(pawn, requirement.HediffDefName) * requirement.Weight;
            }
            return Math.Min(1f, total);
        }

        public static float GetSmoothedSuccessWeight(Pawn pawn)
        {
            // EN: Full gelatinization is a race between adaptation and collapse, so use one smoothed score to feed both sides.
            // CN: 完全胶化本质上是“适应”与“崩坏”的竞赛，所以这里先算一个平滑后的总成功权重，供两边共用。
            float raw = 0.45f * GetCorruption(pawn) + 0.55f * GetWeightedPartProgress(pawn);
            raw = Math.Min(1f, Math.Max(0f, raw));
            return raw * raw * (3f - 2f * raw);
        }

        public static bool AreAllRequirementsMaxed(Pawn pawn, out string reason)
        {
            List<string> missing = new List<string>();
            foreach (TrainingRequirement requirement in Requirements)
            {
                float progress = GetRequirementProgress(pawn, requirement.HediffDefName);
                if (progress < RequirementCompleteThreshold)
                {
                    missing.Add(requirement.LabelKey.Translate() + " " + progress.ToStringPercent());
                }
            }

            reason = missing.Count == 0 ? null : string.Join("; ", missing);
            return missing.Count == 0;
        }

        public static bool IsGuaranteedSuccess(Pawn pawn, out string missingReason)
        {
            // EN: Full gelatinization only becomes guaranteed when corruption and every required body-part training track are maxed.
            // CN: 只有恶堕和所有必需部位训练条目都拉满时，完全胶化才会变成必定成功。
            List<string> reasons = new List<string>();
            if (GetCorruption(pawn) < RequirementCompleteThreshold)
            {
                reasons.Add("SSC_FullGel_Requirement_Corruption".Translate(GetCorruption(pawn).ToStringPercent()).ToString());
            }

            if (!AreAllRequirementsMaxed(pawn, out string partReason))
            {
                reasons.Add(partReason);
            }

            missingReason = string.Join(" | ", reasons.Where(r => !string.IsNullOrEmpty(r)));
            return reasons.Count == 0;
        }

        public static string BuildRiskReport(Pawn pawn)
        {
            // EN: Build the surgery warning text from corruption, body-part training progress, and the final success race weight.
            // CN: 这里把恶堕、部位训练进度和最终成功竞赛权重拼成手术警告文本。
            FullGelatinizationSnapshot snapshot = BuildSnapshot(pawn);
            IsGuaranteedSuccess(pawn, out string missingReason);
            return "SSC_FullGel_RiskReport".Translate(snapshot.Corruption.ToStringPercent(), snapshot.PartProgress.ToStringPercent(), snapshot.SuccessWeight.ToStringPercent(), string.IsNullOrEmpty(missingReason) ? "-" : missingReason).ToString();
        }

        public static FullGelatinizationSnapshot BuildSnapshot(Pawn pawn)
        {
            float corruption = GetCorruption(pawn);
            float partProgress = GetWeightedPartProgress(pawn);
            float successWeight = GetSmoothedSuccessWeight(pawn);
            return new FullGelatinizationSnapshot(corruption, partProgress, successWeight);
        }

        public static void CompleteSuccess(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            // EN: Step 1: strip temporary training / transition hediffs from the hollow pawn's body.
            // CN: 步骤 1：先清理空壳 Pawn 身上所有临时训练态和过渡态 Hediff。
            List<Hediff> toRemove = GetRemovableHediffs(pawn);
            for (int i = 0; i < toRemove.Count; i++)
            {
                pawn.health.RemoveHediff(toRemove[i]);
            }

            // EN: Step 2: stamp the permanent SSC_FullGelatinizedBody state and refresh the full-gel visuals.
            // CN: 步骤 2：写入永久的 SSC_FullGelatinizedBody 状态，并刷新完全胶化外观。
            EnsureFullGelBodyHediff(pawn);
            RefreshPawnVisuals(pawn);
            Messages.Message("SSC_FullGel_Success".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent, false);
        }

        public static FloatRange GetAdaptationGainRange(Pawn pawn, FloatRange guaranteedRange, FloatRange baseRange, out bool guaranteed)
        {
            // EN: If the hollow pawn already meets all requirements, adaptation uses the guaranteed-success gain range instead of the risky range.
            // CN: 如果空壳 Pawn 已经满足全部条件，适应增长就直接走“必定成功”的区间，而不是风险区间。
            guaranteed = IsGuaranteedSuccess(pawn, out _);
            if (guaranteed) return guaranteedRange;

            float smooth = GetSmoothedSuccessWeight(pawn);
            float gain = Mathf.Lerp(baseRange.min, baseRange.max, smooth);
            return new FloatRange(gain, gain);
        }

        public static FloatRange GetFailureSeverityGainRange(Pawn pawn, FloatRange baseRange)
        {
            // EN: Failure gain shrinks when success weight rises, because stronger preparation slows down the collapse side of full-gel surgery.
            // CN: 成功权重越高，失败严重度增长就越慢，因为准备越充分，完全胶化手术的崩坏侧就越不容易占上风。
            float smooth = GetSmoothedSuccessWeight(pawn);
            float min = Mathf.Lerp(baseRange.max, baseRange.min, smooth);
            float max = Mathf.Lerp(baseRange.max + 0.02f, baseRange.max * 0.6f, smooth);

            if (IsGuaranteedSuccess(pawn, out _))
            {
                min *= 0.35f;
                max *= 0.35f;
            }

            return new FloatRange(min, max);
        }

        public static void DestroyPawnCompletely(Pawn pawn)
        {
            if (pawn == null) return;

            // EN: A failed full-gel race destroys the hollow pawn entirely instead of leaving a partial body behind.
            // CN: 完全胶化竞赛失败后，这个空壳 Pawn 会被彻底毁掉，而不是留下半成品身体。
            string pawnName = pawn.LabelShort;
            Map map = pawn.MapHeld;
            if (pawn.Spawned)
            {
                pawn.DeSpawn();
            }

            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);

            if (map != null)
            {
                Messages.Message("SSC_FullGel_Failure".Translate(pawnName), new TargetInfo(IntVec3.Invalid, map), MessageTypeDefOf.NegativeEvent, false);
            }
            else
            {
                Messages.Message("SSC_FullGel_Failure".Translate(pawnName), MessageTypeDefOf.NegativeEvent, false);
            }
        }

        public static bool ShouldTintFullGelGraphic(Pawn pawn)
        {
            return pawn != null
                && SSCMod.settings != null
                && SSCMod.settings.enableFullGelatinizedBodyTint
                && FullGelatinizedBodyDef != null
                && pawn.health?.hediffSet?.GetFirstHediffOfDef(FullGelatinizedBodyDef) != null;
        }

        public static bool ShouldTintBody(Pawn pawn)
        {
            return ShouldTintFullGelGraphic(pawn);
        }

        private static float CalculateAverageSeverity(List<Hediff> matches)
        {
            if (matches == null || matches.Count == 0) return 0f;

            float total = 0f;
            for (int i = 0; i < matches.Count; i++)
            {
                total += matches[i].Severity;
            }

            return Math.Min(1f, total / matches.Count);
        }

        private static List<Hediff> GetRemovableHediffs(Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs.Where(h => !ShouldPreserveHediff(h)).ToList();
        }

        private static void EnsureFullGelBodyHediff(Pawn pawn)
        {
            if (FullGelatinizedBodyDef == null) return;
            if (pawn.health.hediffSet.GetFirstHediffOfDef(FullGelatinizedBodyDef) != null) return;

            pawn.health.AddHediff(FullGelatinizedBodyDef);
        }

        // EN: Full-body conversion rewrites most visible body state, so graphics and portraits must refresh immediately.
        // CN: 完全胶化会重写绝大部分可见身体状态，所以图像和头像缓存必须立刻刷新。
        private static void RefreshPawnVisuals(Pawn pawn)
        {
            pawn.health.Notify_HediffChanged(null);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
        }

        private static bool ShouldPreserveHediff(Hediff hediff)
        {
            if (hediff == null) return false;
            if (hediff.def == SSCDefOf.SSC_FullGelatinizationTemporary) return false;
            if (hediff.def == SSCDefOf.SSC_FullGelatinizedBody) return true;

            string packageId = hediff.def?.modContentPack?.PackageId?.ToLowerInvariant() ?? string.Empty;
            if (packageId.Contains("sexslavecraft"))
            {
                return true;
            }

            // EN: Preserve every RJW sex-part hediff so full gelatinization never wipes genitals, anus, or breasts.
            // CN: 保留所有 RJW 性器官 Hediff，避免完全胶化把阴茎、阴道、肛门或乳房状态一起清掉。
            if (hediff.def.GetType().FullName == "rjw.HediffDef_SexPart")
            {
                return true;
            }

            return packageId.Contains("rjw") && hediff.Part != null;
        }

        private sealed class TrainingRequirement
        {
            public readonly string HediffDefName;
            public readonly float Weight;
            public readonly string LabelKey;

            public TrainingRequirement(string hediffDefName, float weight, string labelKey)
            {
                HediffDefName = hediffDefName;
                Weight = weight;
                LabelKey = labelKey;
            }
        }

        public readonly struct FullGelatinizationSnapshot
        {
            public readonly float Corruption;
            public readonly float PartProgress;
            public readonly float SuccessWeight;

            public FullGelatinizationSnapshot(float corruption, float partProgress, float successWeight)
            {
                Corruption = corruption;
                PartProgress = partProgress;
                SuccessWeight = successWeight;
            }
        }
    }
}
