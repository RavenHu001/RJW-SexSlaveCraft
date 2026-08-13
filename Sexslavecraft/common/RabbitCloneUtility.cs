using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using rjw;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public static class RabbitCloneUtility
    {
        public const int MaxRabbitClones = 2;
        public const int RabbitCloneMaturationTicks = GenDate.TicksPerDay * 3;
        public const int RabbitCloneSyncIntervalTicks = 80;
        public const int RabbitCloneGrowthIntervalTicks = 80;

        private const string RabbitCloneLinkDefName = "SSC_Hediff_RabbitCloneLink";
        private const string RabbitCloneLowPnaDefName = "SSC_Hediff_RabbitCloneLowPNA";
        private const string BasePnaProductionDefName = "SSC_Purple_HyperLactation";

        public static HediffDef RabbitCloneLinkDef => DefDatabase<HediffDef>.GetNamedSilentFail(RabbitCloneLinkDefName);

        public static HediffDef RabbitCloneLowPnaDef => DefDatabase<HediffDef>.GetNamedSilentFail(RabbitCloneLowPnaDefName);

        public static bool IsRabbitCloneBirthModeEnabled(Pawn source)
        {
            CompSexSlaveTraining comp = source?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.rabbitReproductionMode != RabbitReproductionMode.Clone) return false;

            return comp.IsPetRabbitSpecialized ||
                   PetSpecializationUtility.HasAnyPetState(source, SexSlaveSpecializationType.PetRabbit);
        }

        public static bool TryReplacePregnancyBirthWithClone(Hediff_BasePregnancy pregnancy)
        {
            Pawn source = pregnancy?.pawn;
            if (source == null || !IsRabbitCloneBirthModeEnabled(source)) return false;
            if (!CanCreateRabbitClone(source, out _)) return false;

            if (!TryCreateRabbitClone(source, out Pawn clone, showMessages: false))
            {
                return false;
            }

            DiscardPreparedPregnancyBabies(pregnancy);
            source.health?.RemoveHediff(pregnancy);

            Messages.Message(
                Strings.Message_RabbitCloneBirth(source.LabelShort, clone.LabelShort),
                clone,
                MessageTypeDefOf.PositiveEvent,
                false);

            return true;
        }

        public static bool TryCreateRabbitClone(Pawn source, out Pawn clone, bool showMessages = true)
        {
            clone = null;

            if (!CanCreateRabbitClone(source, out string reason))
            {
                if (showMessages && !string.IsNullOrEmpty(reason))
                {
                    Messages.Message(reason, source, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            HediffComp_RabbitCloneSource sourceComp = GetRabbitCloneSourceComp(source);
            if (sourceComp == null)
            {
                if (showMessages)
                {
                    Messages.Message("[SSC] 兔专精状态缺少分身组件。", source, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            try
            {
                clone = Find.PawnDuplicator.Duplicate(source);
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC] Rabbit clone duplication failed: source={source?.LabelShort ?? "null"}, error={ex}");
                clone = null;
            }

            if (clone == null)
            {
                if (showMessages)
                {
                    Messages.Message("[SSC] 兔分身生成失败。", source, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            PrepareFreshRabbitCloneBody(source, clone);
            SpawnRabbitCloneNearSource(source, clone);

            HediffComp_RabbitCloneLink linkComp = AddRabbitCloneLink(source, clone);
            if (linkComp == null)
            {
                if (!clone.Destroyed)
                {
                    clone.Destroy(DestroyMode.Vanish);
                }
                if (showMessages)
                {
                    Messages.Message("[SSC] 兔分身链接状态添加失败。", source, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            sourceComp.RegisterClone(clone);
            sourceComp.CleanupClones();

            clone.Drawer?.renderer?.SetAllGraphicsDirty();

            if (showMessages)
            {
                Messages.Message($"[SSC] {source.LabelShort} 生成了兔分身（{sourceComp.LivingCloneCount}/{MaxRabbitClones}）。", clone, MessageTypeDefOf.PositiveEvent, false);
            }

            return true;
        }

        public static bool CanCreateRabbitClone(Pawn source, out string reason)
        {
            reason = null;
            if (source == null || source.DestroyedOrNull() || source.Dead)
            {
                reason = "[SSC] 无效的兔分身本体。";
                return false;
            }

            if (IsRabbitClone(source))
            {
                reason = "[SSC] 兔分身不能继续生成分身。";
                return false;
            }

            if (!source.Spawned || source.Map == null)
            {
                reason = "[SSC] 兔分身本体必须在地图上。";
                return false;
            }

            if (!source.RaceProps.Humanlike)
            {
                reason = "[SSC] 只有人形 Pawn 可以生成兔分身。";
                return false;
            }

            CompSexSlaveTraining comp = source.TryGetComp<CompSexSlaveTraining>();
            bool isRabbit = comp?.IsPetRabbitSpecialized == true ||
                            PetSpecializationUtility.HasAnyPetState(source, SexSlaveSpecializationType.PetRabbit);
            if (!isRabbit)
            {
                reason = "[SSC] 只有宠物兔专精可以生成兔分身。";
                return false;
            }

            HediffComp_RabbitCloneSource sourceComp = GetRabbitCloneSourceComp(source);
            if (sourceComp == null)
            {
                reason = "[SSC] 兔专精健康状态还没有分身组件。";
                return false;
            }

            if (!sourceComp.CanRegisterMoreClones)
            {
                reason = $"[SSC] 兔分身最多只能同时存在 {MaxRabbitClones} 个。";
                return false;
            }

            return true;
        }

        public static HediffComp_RabbitCloneSource GetRabbitCloneSourceComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            Hediff rabbit = pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetRabbit));
            HediffComp_RabbitCloneSource comp = (rabbit as HediffWithComps)?.TryGetComp<HediffComp_RabbitCloneSource>();
            if (comp != null) return comp;

            Hediff finalRabbit = pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetRabbit));
            return (finalRabbit as HediffWithComps)?.TryGetComp<HediffComp_RabbitCloneSource>();
        }

        public static bool IsRabbitClone(Pawn pawn)
        {
            return GetRabbitCloneLinkComp(pawn) != null;
        }

        public static bool IsRabbitCloneOf(Pawn clone, Pawn source)
        {
            HediffComp_RabbitCloneLink link = GetRabbitCloneLinkComp(clone);
            return link != null && link.source == source;
        }

        public static bool IsMatureRabbitClone(Pawn pawn)
        {
            HediffComp_RabbitCloneLink link = GetRabbitCloneLinkComp(pawn);
            return link != null && link.mature;
        }

        public static HediffComp_RabbitCloneLink GetRabbitCloneLinkComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            HediffDef linkDef = RabbitCloneLinkDef;
            if (linkDef == null) return null;

            Hediff linkHediff = pawn.health.hediffSet.GetFirstHediffOfDef(linkDef);
            return (linkHediff as HediffWithComps)?.TryGetComp<HediffComp_RabbitCloneLink>();
        }

        public static void TickRabbitCloneGrowth(Pawn clone, Pawn source, HediffComp_RabbitCloneLink link)
        {
            if (clone?.ageTracker == null || link == null) return;

            float progress = GetCloneGrowthProgress(link);
            if (source?.ageTracker != null)
            {
                long targetAge = Math.Max(source.ageTracker.AgeBiologicalTicks, link.startAgeTicks);
                long desiredAge = link.startAgeTicks + (long)((targetAge - link.startAgeTicks) * progress);
                long delta = desiredAge - clone.ageTracker.AgeBiologicalTicks;
                if (delta > 0)
                {
                    clone.ageTracker.AgeTickMothballed((int)Math.Min(delta, int.MaxValue));
                }
            }

            if (link.parent != null)
            {
                link.parent.Severity = Mathf.Clamp01(Mathf.Max(0.01f, progress));
            }

            if (progress >= 1f)
            {
                MatureRabbitClone(clone, source, link);
            }
        }

        public static float GetCloneGrowthProgress(HediffComp_RabbitCloneLink link)
        {
            if (link == null || link.bornTick < 0) return 0f;
            return Mathf.Clamp01((Find.TickManager.TicksGame - link.bornTick) / (float)RabbitCloneMaturationTicks);
        }

        public static void MatureRabbitClone(Pawn clone, Pawn source, HediffComp_RabbitCloneLink link)
        {
            if (clone == null || link == null || link.mature) return;

            if (source?.ageTracker != null && clone.ageTracker != null)
            {
                clone.ageTracker.AgeBiologicalTicks = source.ageTracker.AgeBiologicalTicks;
            }

            AddLowPnaProduction(clone);
            CopySscHediffsFromSource(source, clone);

            link.mature = true;
            if (link.parent != null)
            {
                link.parent.Severity = 1f;
            }

            clone.Drawer?.renderer?.SetAllGraphicsDirty();
            Messages.Message($"[SSC] {clone.LabelShort} 的兔分身已经成熟。", clone, MessageTypeDefOf.PositiveEvent, false);
        }

        public static void SyncRabbitCloneNeeds(Pawn source, List<Pawn> clones)
        {
            if (source == null || clones == null) return;

            List<Pawn> group = new List<Pawn> { source };
            group.AddRange(clones.Where(x => x != null && !x.DestroyedOrNull() && !x.Dead));

            SyncNeed(group, p => p.needs?.rest, (need, value) => need.CurLevelPercentage = value);
            SyncNeed(group, p => p.needs?.mood, (need, value) => need.CurLevelPercentage = value);
        }

        public static void PropagateSourceMentalState(Pawn source, List<Pawn> clones)
        {
            if (source?.mindState?.mentalStateHandler == null || !source.mindState.mentalStateHandler.InMentalState) return;
            if (clones == null) return;

            foreach (Pawn clone in clones)
            {
                if (clone?.mindState?.mentalStateHandler == null || clone.Dead || clone.DestroyedOrNull()) continue;
                if (clone.mindState.mentalStateHandler.InMentalState) continue;
                clone.mindState.mentalStateHandler.TryStartMentalState(
                    MentalStateDefOf.Wander_Psychotic,
                    "rabbit clone shared consciousness",
                    forced: true,
                    forceWake: true,
                    causedByMood: false,
                    otherPawn: source,
                    transitionSilently: true);
            }
        }

        public static ThingDef GetPnaProductThingDef()
        {
            HediffDef basePnaDef = DefDatabase<HediffDef>.GetNamedSilentFail(BasePnaProductionDefName);
            HediffCompProperties_SeverityProduct productProps =
                basePnaDef?.comps?.OfType<HediffCompProperties_SeverityProduct>().FirstOrDefault();
            if (productProps?.thingToSpawn != null) return productProps.thingToSpawn;

            return DefDatabase<ThingDef>.AllDefs.FirstOrDefault(def =>
                def != null &&
                !def.defName.ToLowerInvariant().Contains("plus") &&
                (def.defName.ToLowerInvariant().Contains("pna") ||
                 def.label.ToLowerInvariant().Contains("pna") ||
                 def.label.Contains("纳米")));
        }

        private static void PrepareFreshRabbitCloneBody(Pawn source, Pawn clone)
        {
            if (source == null || clone == null) return;

            clone.SetFaction(source.Faction);

            long initialAgeTicks = GetInitialCloneAgeTicks(source);
            if (clone.ageTracker != null)
            {
                clone.ageTracker.AgeBiologicalTicks = initialAgeTicks;
            }

            RemoveNonSscHediffs(clone);
            ClearCopiedCloneSourceLists(clone);
            RemoveExistingCloneLinks(clone);
            RemovePersonalityExcretionStates(clone);

            CompSexSlaveTraining sourceTraining = source.TryGetComp<CompSexSlaveTraining>();
            CompSexSlaveTraining cloneTraining = clone.TryGetComp<CompSexSlaveTraining>();
            if (sourceTraining != null && cloneTraining != null)
            {
                cloneTraining.specializationType = sourceTraining.specializationType;
                cloneTraining.specializationProgress = sourceTraining.specializationProgress;
                cloneTraining.selectedTrainer = sourceTraining.selectedTrainer;
                cloneTraining.pawnIdentity = sourceTraining.pawnIdentity;
                cloneTraining.allowOthersForTrainingOrSex = sourceTraining.allowOthersForTrainingOrSex;
                cloneTraining.rabbitReproductionMode = sourceTraining.rabbitReproductionMode;
            }

            SyncTraits(source, clone);
        }

        private static long GetInitialCloneAgeTicks(Pawn source)
        {
            long sourceAge = source?.ageTracker?.AgeBiologicalTicks ?? GenDate.TicksPerYear;
            long earlyAge = GenDate.TicksPerYear / 100L;
            return Math.Max(1L, Math.Min(sourceAge, earlyAge));
        }

        private static void SpawnRabbitCloneNearSource(Pawn source, Pawn clone)
        {
            if (clone.Spawned || source?.Map == null) return;

            IntVec3 cell;
            if (!CellFinder.TryFindRandomCellNear(source.Position, source.Map, 3,
                    c => c.Standable(source.Map) && !c.Fogged(source.Map), out cell))
            {
                cell = source.Position;
            }

            GenSpawn.Spawn(clone, cell, source.Map);
        }

        private static HediffComp_RabbitCloneLink AddRabbitCloneLink(Pawn source, Pawn clone)
        {
            HediffDef linkDef = RabbitCloneLinkDef;
            if (clone?.health == null || linkDef == null) return null;

            Hediff linkHediff = clone.health.AddHediff(linkDef);
            linkHediff.Severity = 0.01f;

            HediffComp_RabbitCloneLink linkComp = (linkHediff as HediffWithComps)?.TryGetComp<HediffComp_RabbitCloneLink>();
            linkComp?.Initialize(source, clone.ageTracker?.AgeBiologicalTicks ?? 1L);
            return linkComp;
        }

        private static void AddLowPnaProduction(Pawn clone)
        {
            if (clone?.health?.hediffSet == null) return;

            HediffDef pnaDef = RabbitCloneLowPnaDef;
            if (pnaDef == null) return;
            if (clone.health.hediffSet.HasHediff(pnaDef)) return;

            Hediff pna = clone.health.AddHediff(pnaDef);
            if (pna != null)
            {
                pna.Severity = 0.01f;
            }
        }

        private static void RemoveNonSscHediffs(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null) return;

            List<Hediff> toRemove = pawn.health.hediffSet.hediffs
                .Where(hediff => hediff != null && !ShouldKeepSscHediff(hediff.def))
                .ToList();

            foreach (Hediff hediff in toRemove)
            {
                if (hediff?.pawn?.health != null && !hediff.pawn.Dead)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        private static bool ShouldKeepSscHediff(HediffDef def)
        {
            if (def == null) return false;

            string defName = def.defName ?? string.Empty;
            if (defName.StartsWith("SSC_", StringComparison.Ordinal)) return true;
            if (defName.StartsWith("Hediff_SSC_", StringComparison.Ordinal)) return true;
            if (defName == "Hediff_BridleOfSexSlave" || defName == "Hediff_ChainOfSexSlave") return true;

            string packageId = def.modContentPack?.PackageId ?? string.Empty;
            string packageIdFacing = def.modContentPack?.PackageIdPlayerFacing ?? string.Empty;
            string modName = def.modContentPack?.Name ?? string.Empty;
            return packageId.IndexOf("sexslavecraft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   packageIdFacing.IndexOf("sexslavecraft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   modName.IndexOf("sexslavecraft", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CopySscHediffsFromSource(Pawn source, Pawn clone)
        {
            if (source?.health?.hediffSet?.hediffs == null || clone?.health == null) return;

            foreach (Hediff sourceHediff in source.health.hediffSet.hediffs.ToList())
            {
                if (sourceHediff == null || !ShouldKeepSscHediff(sourceHediff.def)) continue;
                if (sourceHediff.def == SSCDefOf.SSC_PersonalityExcreting ||
                    sourceHediff.def == SSCDefOf.SSC_PersonalityExcreted_Done ||
                    sourceHediff.def == RabbitCloneLinkDef ||
                    sourceHediff.def == RabbitCloneLowPnaDef)
                {
                    continue;
                }

                Hediff existing = clone.health.hediffSet.GetFirstHediffOfDef(sourceHediff.def);
                if (existing == null)
                {
                    existing = clone.health.AddHediff(sourceHediff.def, sourceHediff.Part);
                }

                if (existing != null)
                {
                    existing.Severity = sourceHediff.Severity;
                }
            }

            ClearCopiedCloneSourceLists(clone);
            RemovePersonalityExcretionStates(clone);
        }

        private static void ClearCopiedCloneSourceLists(Pawn pawn)
        {
            HediffComp_RabbitCloneSource sourceComp = GetRabbitCloneSourceComp(pawn);
            sourceComp?.ClearClones();
        }

        private static void RemoveExistingCloneLinks(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            HediffDef linkDef = RabbitCloneLinkDef;
            if (linkDef == null) return;

            List<Hediff> links = pawn.health.hediffSet.hediffs
                .Where(h => h?.def == linkDef)
                .ToList();
            foreach (Hediff link in links)
            {
                pawn.health.RemoveHediff(link);
            }
        }

        public static void RemovePersonalityExcretionStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            RemoveHediffIfPresent(pawn, SSCDefOf.SSC_PersonalityExcreting);
            RemoveHediffIfPresent(pawn, SSCDefOf.SSC_PersonalityExcreted_Done);
        }

        private static void RemoveHediffIfPresent(Pawn pawn, HediffDef def)
        {
            if (pawn?.health?.hediffSet == null || def == null) return;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static void SyncTraits(Pawn source, Pawn clone)
        {
            if (source?.story?.traits?.allTraits == null || clone?.story?.traits?.allTraits == null) return;

            clone.story.traits.allTraits.Clear();
            foreach (Trait trait in source.story.traits.allTraits)
            {
                if (trait?.def == null) continue;
                if (trait.def == SSCDefOf.SexSlaveTrait) continue;
                clone.story.traits.GainTrait(new Trait(trait.def, trait.Degree, trait.ScenForced));
            }

            SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(clone);
        }

        private static void DiscardPreparedPregnancyBabies(Hediff_BasePregnancy pregnancy)
        {
            if (pregnancy?.babies == null) return;

            foreach (Pawn baby in pregnancy.babies.ToList())
            {
                if (baby == null) continue;
                if (!baby.Destroyed)
                {
                    baby.Destroy();
                }
                baby.Discard(true);
            }

            pregnancy.babies.Clear();
        }

        private static void SyncNeed<TNeed>(List<Pawn> group, Func<Pawn, TNeed> getter, Action<TNeed, float> setter)
            where TNeed : Need
        {
            List<TNeed> needs = group.Select(getter).Where(x => x != null).ToList();
            if (needs.Count == 0) return;

            float average = Mathf.Clamp01(needs.Average(x => x.CurLevelPercentage));
            foreach (TNeed need in needs)
            {
                setter(need, average);
            }
        }
    }
}
