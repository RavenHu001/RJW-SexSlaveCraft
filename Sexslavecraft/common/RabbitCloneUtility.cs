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

        /// <summary>检查角色是否处于兔特化克隆繁殖模式，不在查询中改变身份或配置。</summary>
        public static bool IsRabbitCloneBirthModeEnabled(Pawn source)
        {
            CompSexSlaveTraining comp = source?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.rabbitReproductionMode != RabbitReproductionMode.Clone) return false;

            return comp.IsPetRabbitSpecialized ||
                   PetSpecializationUtility.HasAnyPetState(source, SexSlaveSpecializationType.PetRabbit);
        }

        /// <summary>成功创建分身后才移除待产幼体和妊娠状态；失败时保留原生分娩流程。</summary>
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

        /// <summary>校验分身资格、复制并准备身体，再建立来源链接；链接失败时销毁本次生成的分身。</summary>
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

        /// <summary>检查本体状态、种族、兔特化及分身上限，返回首个不能创建的原因。</summary>
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

        /// <summary>依次从基础和终极兔特化状态取得分身来源组件，不补建缺失状态。</summary>
        public static HediffComp_RabbitCloneSource GetRabbitCloneSourceComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            Hediff rabbit = pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetRabbit));
            HediffComp_RabbitCloneSource comp = (rabbit as HediffWithComps)?.TryGetComp<HediffComp_RabbitCloneSource>();
            if (comp != null) return comp;

            Hediff finalRabbit = pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetRabbit));
            return (finalRabbit as HediffWithComps)?.TryGetComp<HediffComp_RabbitCloneSource>();
        }

        /// <summary>根据分身链接组件识别复制体，不把普通兔特化角色视为分身。</summary>
        public static bool IsRabbitClone(Pawn pawn)
        {
            return GetRabbitCloneLinkComp(pawn) != null;
        }

        /// <summary>核对分身链接的实际来源是否为指定本体。</summary>
        public static bool IsRabbitCloneOf(Pawn clone, Pawn source)
        {
            HediffComp_RabbitCloneLink link = GetRabbitCloneLinkComp(clone);
            return link != null && link.source == source;
        }

        /// <summary>读取分身链接中的成熟标记；没有链接时返回否。</summary>
        public static bool IsMatureRabbitClone(Pawn pawn)
        {
            HediffComp_RabbitCloneLink link = GetRabbitCloneLinkComp(pawn);
            return link != null && link.mature;
        }

        /// <summary>从分身健康状态取得链接组件，缺少角色或定义时安全返回空。</summary>
        public static HediffComp_RabbitCloneLink GetRabbitCloneLinkComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            HediffDef linkDef = RabbitCloneLinkDef;
            if (linkDef == null) return null;

            Hediff linkHediff = pawn.health.hediffSet.GetFirstHediffOfDef(linkDef);
            return (linkHediff as HediffWithComps)?.TryGetComp<HediffComp_RabbitCloneLink>();
        }

        /// <summary>按成长进度推进生理年龄及链接严重度，达到终点时执行一次成熟处理。</summary>
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

        /// <summary>将出生后经过的游戏刻数换算为零到一的成长比例，未记录出生时视为零。</summary>
        public static float GetCloneGrowthProgress(HediffComp_RabbitCloneLink link)
        {
            if (link == null || link.bornTick < 0) return 0f;
            return Mathf.Clamp01((Find.TickManager.TicksGame - link.bornTick) / (float)RabbitCloneMaturationTicks);
        }

        /// <summary>将分身年龄同步到本体，补充低产量状态并复制允许继承的健康状态，只结算一次成熟。</summary>
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

        /// <summary>收集存活分身及本体，将休息和心情按组内平均值同步。</summary>
        public static void SyncRabbitCloneNeeds(Pawn source, List<Pawn> clones)
        {
            if (source == null || clones == null) return;

            List<Pawn> group = new List<Pawn> { source };
            group.AddRange(clones.Where(x => x != null && !x.DestroyedOrNull() && !x.Dead));

            SyncNeed(group, p => p.needs?.rest, (need, value) => need.CurLevelPercentage = value);
            SyncNeed(group, p => p.needs?.mood, (need, value) => need.CurLevelPercentage = value);
        }

        /// <summary>本体处于精神状态时让仍正常的分身进入游荡状态，不覆盖分身已有的精神状态。</summary>
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

        /// <summary>优先读取基础产物定义，缺失时按现有名称规则查找普通 PNA 产物。</summary>
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

        /// <summary>准备新克隆体并复制原有特化配置；新个体的可选调教员资格从关闭开始。</summary>
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
                cloneTraining.slaveTrainerEnabled = false;
                cloneTraining.trainerIdentityInitialized = true;
                // 克隆是新角色，不继承本体可能尚未完成的旧档迁移版本。
                cloneTraining.trainerOfficerMigrationVersion = CompSexSlaveTraining.CurrentTrainerOfficerMigrationVersion;
                // 新克隆的限制由准备完成后的 ResetNewClone 独立初始化，不复制旧例外或新配置。
                cloneTraining.rabbitReproductionMode = sourceTraining.rabbitReproductionMode;
            }

            SyncTraits(source, clone);
        }

        /// <summary>根据本体年龄计算分身初始年龄，保证至少一刻且不超过既定幼体上限。</summary>
        private static long GetInitialCloneAgeTicks(Pawn source)
        {
            long sourceAge = source?.ageTracker?.AgeBiologicalTicks ?? GenDate.TicksPerYear;
            long earlyAge = GenDate.TicksPerYear / 100L;
            return Math.Max(1L, Math.Min(sourceAge, earlyAge));
        }

        /// <summary>在本体附近寻找可站立且未遮蔽的位置生成分身，找不到时使用本体所在格。</summary>
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

        /// <summary>为新身体添加来源链接并记录初始年龄，缺少定义或健康组件时返回空。</summary>
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

        /// <summary>为成熟分身补充低产量状态，避免重复添加已有健康状态。</summary>
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

        /// <summary>从快照中移除不属于 SSC 保留范围的健康状态，避免遍历过程中修改原集合。</summary>
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

        /// <summary>根据定义名及模组标识判断健康状态是否属于现有 SSC 继承范围。</summary>
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

        /// <summary>复制允许继承的 SSC 状态及严重度，排除人格排泄和分身专用状态，再清理复制出的来源记录。</summary>
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

        /// <summary>清空复制身体上的分身列表，避免把本体的其他分身当作自己的后代。</summary>
        private static void ClearCopiedCloneSourceLists(Pawn pawn)
        {
            HediffComp_RabbitCloneSource sourceComp = GetRabbitCloneSourceComp(pawn);
            sourceComp?.ClearClones();
        }

        /// <summary>删除复制时带入的旧分身链接，供新身体随后建立独立来源关系。</summary>
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

        /// <summary>移除人格排泄中及已排泄状态，使新身体不会继承来源的排泄流程。</summary>
        public static void RemovePersonalityExcretionStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            RemoveHediffIfPresent(pawn, SSCDefOf.SSC_PersonalityExcreting);
            RemoveHediffIfPresent(pawn, SSCDefOf.SSC_PersonalityExcreted_Done);
        }

        /// <summary>删除指定的首个健康状态；角色、定义或状态不存在时不处理。</summary>
        private static void RemoveHediffIfPresent(Pawn pawn, HediffDef def)
        {
            if (pawn?.health?.hediffSet == null || def == null) return;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        /// <summary>复制来源特性并按接收者最高恶堕值重新协调性奴特性，不直接复制该派生特性。</summary>
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

        /// <summary>销毁并丢弃被克隆分娩替代的预生成幼体，随后清空妊娠组件列表。</summary>
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

        /// <summary>对组内实际存在的同类需求计算平均比例，再统一写回，缺失需求不参加平均。</summary>
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
