using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

// EN: This file contains the master-bond and damage-share helpers.
// EN: It calculates the master's resonance field, selects bonded sex slaves, and builds the damage-share plan.
// CN: 这个文件负责主从绑定和伤害分摊工具。
// CN: 它会计算主人身上的“灵欲共振场”、挑选绑定性奴，并生成伤害分摊方案。
namespace SexSlaveCraft
{
    public static class SSCMasterBondUtility
    {
        public const float DamageShareRatio = 0.7f;
        private static int redirectDepth;
        private static readonly float[] StageMinSeverity = { 0.01f, 0.25f, 0.50f, 0.75f };
        private static readonly float[] StageMaxSeverity = { 0.24f, 0.49f, 0.74f, 1.00f };

        public readonly struct DamageRedirectPlan
        {
            public readonly float OriginalAmount;
            public readonly float RedirectAmount;
            public readonly float RemainingAmount;
            public readonly List<Pawn> Candidates;
            public readonly float TotalWeight;

            public DamageRedirectPlan(float originalAmount, float redirectAmount, List<Pawn> candidates, float totalWeight)
            {
                OriginalAmount = originalAmount;
                RedirectAmount = redirectAmount;
                RemainingAmount = originalAmount - redirectAmount;
                Candidates = candidates;
                TotalWeight = totalWeight;
            }
        }

        public static bool IsRedirectingDamage => redirectDepth > 0;

        public static IEnumerable<Pawn> GetBoundSlaves(Pawn master)
        {
            if (master == null) yield break;

            Hediff_BridleOfSexSlave bridle = SSCBondUtility.GetBridle(master);
            if (bridle == null) yield break;

            foreach (Pawn slave in bridle.ValidTargets)
            {
                if (slave == null || slave.DestroyedOrNull() || slave.Dead || slave == master) continue;
                if (SSCBondUtility.IsBoundTo(slave, master))
                {
                    yield return slave;
                }
            }
        }

        public static float GetCorruption(Pawn pawn)
        {
            return pawn?.needs?.TryGetNeed<Need_Corruption>()?.CurLevelPercentage ?? 0f;
        }

        public static float CalculateMasterEmpowermentSeverity(Pawn master)
        {
            // EN: The master's resonance field grows from both sex-slave count and average corruption.
            // CN: 主人身上的“灵欲共振场”强度同时取决于性奴数量和平均恶堕。
            int slaveCount = GetBoundSlaves(master).Count();
            int stage = GetEmpowermentStageFromSlaveCount(slaveCount);
            if (stage < 0)
            {
                return 0f;
            }

            float avgCorruption = slaveCount > 0 ? GetBoundSlaves(master).Average(GetCorruption) : 0f;
            float scaledCorruption = GetStageScaledCorruption(avgCorruption);
            return Mathf.Lerp(StageMinSeverity[stage], StageMaxSeverity[stage], scaledCorruption);
        }

        private static float GetStageScaledCorruption(float avgCorruption)
        {
            float clamped = Mathf.Clamp01(avgCorruption);
            float smooth = clamped * clamped * (3f - 2f * clamped);
            return Mathf.Pow(smooth, 1.35f);
        }

        public static int GetEmpowermentStage(float severity)
        {
            if (severity >= 0.75f) return 3;
            if (severity >= 0.50f) return 2;
            if (severity >= 0.25f) return 1;
            if (severity > 0f) return 0;
            return -1;
        }

        public static int GetEmpowermentStageFromSlaveCount(int slaveCount)
        {
            if (slaveCount >= 8) return 3;
            if (slaveCount >= 5) return 2;
            if (slaveCount >= 3) return 1;
            if (slaveCount >= 1) return 0;
            return -1;
        }

        public static float GetTrainingCorruptionMultiplier(Pawn master)
        {
            int slaveCount = GetBoundSlaves(master).Count();
            int stage = GetEmpowermentStageFromSlaveCount(slaveCount);
            switch (stage)
            {
                case 0: return 1.1f;
                case 1: return 1.3f;
                case 2: return 1.5f;
                case 3: return 1.8f;
                default: return 1.0f;
            }
        }

        public static string GetEmpowermentSummary(Pawn master)
        {
            // EN: Build the inspect-string text for the current resonance-field stage.
            // CN: 这里负责生成当前“灵欲共振场”等级的检查栏文本。
            float severity = CalculateMasterEmpowermentSeverity(master);
            int stage = GetEmpowermentStage(severity);
            if (stage < 0)
            {
                return "SSC_MasterEmpowerment_None".Translate();
            }

            float manipulation = 0.08f;
            float hit = 0.04f;
            float dodge = 0.04f;
            float move = 0.08f;
            float pain = 0.08f;

            if (stage == 1)
            {
                manipulation = 0.15f;
                hit = 0.08f;
                dodge = 0.08f;
                move = 0.15f;
                pain = 0.15f;
            }
            else if (stage == 2)
            {
                manipulation = 0.22f;
                hit = 0.12f;
                dodge = 0.12f;
                move = 0.22f;
                pain = 0.25f;
            }
            else if (stage == 3)
            {
                manipulation = 0.30f;
                hit = 0.16f;
                dodge = 0.16f;
                move = 0.30f;
                pain = 0.35f;
            }

            int slaveCount = GetBoundSlaves(master).Count();
            float avgCorruption = slaveCount > 0 ? GetBoundSlaves(master).Average(GetCorruption) : 0f;

            return "SSC_MasterEmpowerment_Summary".Translate(
                slaveCount.ToString(),
                (stage + 1).ToString(),
                avgCorruption.ToStringPercent(),
                severity.ToStringPercent(),
                manipulation.ToStringPercent(),
                hit.ToStringPercent(),
                dodge.ToStringPercent(),
                move.ToStringPercent(),
                pain.ToStringPercent());
        }

        public static void RefreshMasterEmpowerment(Pawn master)
        {
            if (master?.health == null || SSCDefOf.SSC_MasterBondEmpowerment == null) return;

            float severity = CalculateMasterEmpowermentSeverity(master);
            Hediff existing = master.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_MasterBondEmpowerment);

            if (severity <= 0f)
            {
                if (existing != null)
                {
                    master.health.RemoveHediff(existing);
                }
                return;
            }

            if (existing == null)
            {
                existing = HediffMaker.MakeHediff(SSCDefOf.SSC_MasterBondEmpowerment, master);
                master.health.AddHediff(existing);
            }

            existing.Severity = severity;
        }

        public static List<Pawn> GetPreferredDamageShareSlaves(Pawn master)
        {
            // EN: Prefer sex slaves with temporary gelatinization parts, then sort by deeper corruption.
            // CN: 优先挑选带“半凝胶化处理中”部位的性奴，再按更深的恶堕排序。
            return GetBoundSlaves(master)
                .Where(slave => CanSlaveShareDamage(master, slave))
                .OrderByDescending(HasTemporaryGelatinizationPart)
                .ThenByDescending(GetCorruption)
                .ToList();
        }

        public static List<Pawn> PrioritizeDamageShareSlaves(IEnumerable<Pawn> candidates)
        {
            List<Pawn> candidateList = candidates?.ToList() ?? new List<Pawn>();
            List<Pawn> prioritized = candidateList.Where(HasTemporaryGelatinizationPart).ToList();
            return prioritized.Count > 0 ? prioritized : candidateList;
        }

        public static bool CanSlaveShareDamage(Pawn master, Pawn slave)
        {
            if (master == null || slave == null) return false;
            if (slave.DestroyedOrNull() || slave.Dead || slave.Downed) return false;
            if (!slave.Spawned || slave.MapHeld == null || master.MapHeld == null) return false;
            if (slave.MapHeld != master.MapHeld) return false;
            if (slave.health == null) return false;
            return true;
        }

        public static bool HasTemporaryGelatinizationPart(Pawn pawn)
        {
            return GetTemporaryGelatinizationParts(pawn).Count > 0;
        }

        public static List<BodyPartRecord> GetTemporaryGelatinizationParts(Pawn pawn)
        {
            List<BodyPartRecord> parts = new List<BodyPartRecord>();
            if (pawn?.health?.hediffSet == null) return parts;

            foreach (Hediff_GelatinizationTemporary gel in pawn.health.hediffSet.hediffs.OfType<Hediff_GelatinizationTemporary>())
            {
                if (gel.Part != null && !pawn.health.hediffSet.PartIsMissing(gel.Part) && !parts.Contains(gel.Part))
                {
                    parts.Add(gel.Part);
                }
            }
            return parts;
        }

        public static BodyPartRecord GetPreferredDamagePart(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            // EN: Temporary gelatinization parts are the first-choice landing spots for shared damage.
            // CN: 临时胶化中的部位，是伤害分摊时优先落点的第一选择。
            List<BodyPartRecord> tempParts = GetTemporaryGelatinizationParts(pawn);
            if (tempParts.Count > 0)
            {
                return tempParts[0];
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if ((hediff.def == DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_GelatinizedArm") || hediff.def == DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_GelatinizedLeg"))
                    && hediff.Part != null
                    && !pawn.health.hediffSet.PartIsMissing(hediff.Part))
                {
                    return hediff.Part;
                }
            }

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore) || part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore))
                {
                    return part;
                }
            }

            return pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(part => part.depth == BodyPartDepth.Outside && part.parent != null)
                ?? pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault();
        }

        public static bool CanRedirectDamageFrom(Pawn master, DamageInfo dinfo)
        {
            if (master == null || master.Dead || IsRedirectingDamage) return false;
            if (dinfo.Def == null || dinfo.Amount <= 0f || !dinfo.Def.harmsHealth) return false;
            if (master.MapHeld == null) return false;
            if (ShouldBlockDamageDef(dinfo.Def)) return false;

            Hediff_BridleOfSexSlave bridle = SSCBondUtility.GetBridle(master);
            return bridle != null && bridle.shareDamageEnabled;
        }

        public static bool ShouldBlockDamageDef(DamageDef damageDef)
        {
            if (damageDef == null) return true;
            DamageDef toxicBurn = DefDatabase<DamageDef>.GetNamedSilentFail("ToxicBurn");
            return damageDef == DamageDefOf.ExecutionCut
                || damageDef == DamageDefOf.SurgicalCut
                || damageDef == DamageDefOf.Extinguish
                || damageDef == DamageDefOf.EMP
                || damageDef == toxicBurn
                || (damageDef.defName != null && damageDef.defName.IndexOf("Toxic", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static float GetDamageWeight(Pawn slave)
        {
            return 0.25f + GetCorruption(slave);
        }

        public static bool TryCreateDamageRedirectPlan(Pawn master, DamageInfo dinfo, out DamageRedirectPlan plan)
        {
            plan = default;
            if (!CanRedirectDamageFrom(master, dinfo)) return false;

            // EN: Step 1: gather every bonded sex slave who can still stand in and share this hit.
            // CN: 步骤 1：收集所有还能替主人承伤的绑定性奴。
            float originalAmount = dinfo.Amount;
            List<Pawn> candidates = PrioritizeDamageShareSlaves(GetPreferredDamageShareSlaves(master));
            if (candidates.Count == 0) return false;

            // EN: Step 2: convert the fixed damage-share ratio into a weighted plan based on corruption.
            // CN: 步骤 2：按固定分摊比例和恶堕权重，生成这次伤害分摊方案。
            float redirectAmount = originalAmount * DamageShareRatio;
            if (redirectAmount <= 0f) return false;

            float totalWeight = candidates.Sum(GetDamageWeight);
            if (totalWeight <= 0f) return false;

            plan = new DamageRedirectPlan(originalAmount, redirectAmount, candidates, totalWeight);
            return true;
        }

        public static float CalculateSlaveDamageShare(Pawn slave, DamageRedirectPlan plan)
        {
            if (slave == null || plan.TotalWeight <= 0f) return 0f;
            return plan.RedirectAmount * (GetDamageWeight(slave) / plan.TotalWeight);
        }

        public static DamageInfo CreateRedirectedDamageInfo(DamageInfo source, Pawn slave, float share, out BodyPartRecord part)
        {
            DamageInfo redirected = new DamageInfo(source);
            redirected.SetAmount(share);

            part = GetPreferredDamagePart(slave);
            if (part != null)
            {
                redirected.SetHitPart(part);
            }

            return redirected;
        }

        public static int CountTemporaryGelatinizationCandidates(IEnumerable<Pawn> slaves)
        {
            return slaves?.Count(HasTemporaryGelatinizationPart) ?? 0;
        }

        public static IDisposable BeginDamageRedirectScope()
        {
            redirectDepth++;
            return new RedirectScope();
        }

        private sealed class RedirectScope : IDisposable
        {
            public void Dispose()
            {
                redirectDepth = Math.Max(0, redirectDepth - 1);
            }
        }
    }
}
