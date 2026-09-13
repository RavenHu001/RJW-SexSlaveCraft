using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>迁移普通人格特质，同时保留接收身体的基因及其授予特质。</summary>
    public static class PersonalityTraitUtility
    {
        private const string SlaveTraitName = "SexSlaveCraft_SexSlave";
        private const string ExtractedTraitName = "SSC_Trait_PE";
        private static readonly MethodInfo RecacheTraitsMethod =
            AccessTools.Method(typeof(TraitSet), "RecacheTraits");

        /// <summary>复制角色的普通人格特质，包括受抑制条目；排除基因来源及模组派生状态，角色数据缺失时返回空列表。</summary>
        public static List<Trait> CaptureTraits(Pawn pawn)
        {
            return pawn?.story?.traits?.allTraits?.Where(trait => IsPersonalityTrait(trait) && trait.sourceGene == null)
                .Select(CloneTrait)
                .ToList() ?? new List<Trait>();
        }

        /// <summary>以快照替换普通特质，保留宿主基因及派生状态，并刷新抑制关系、共享能力和缓存；空列表表示清除，缺失快照不作修改。</summary>
        public static void RestoreTraits(Pawn pawn, IEnumerable<Trait> storedTraits)
        {
            TraitSet traits = pawn?.story?.traits;
            if (traits == null || storedTraits == null) return;

            // 修改角色前先复制快照，避免角色与凝胶共享可变的特质实例。
            // 旧快照无法可靠辨认基因来源，按已存条目作为人格特质处理。
            List<Trait> incoming = storedTraits.Where(IsPersonalityTrait).Select(CloneTrait).ToList();
            List<Trait> retained = traits.allTraits.Where(IsRetainedTrait).ToList();

            foreach (Trait oldTrait in traits.allTraits.Where(trait => trait != null && !IsRetainedTrait(trait)).ToList())
            {
                // 原版 RemoveTrait 使用忽略受抑制特质的 HasTrait 检查，因此先解除待移除条目的抑制。
                // 此处仅处理非基因来源特质，不会触发删除来源基因的路径。
                oldTrait.suppressedByTrait = false;
                oldTrait.suppressedByGene = null;
                traits.RemoveTrait(oldTrait, true);
            }

            traits.RecalculateSuppression();
            foreach (Trait newTrait in incoming)
            {
                AddPersonalityTrait(traits, newTrait, retained);
            }

            // 移除普通特质可能一并移除保留特质授予的同一能力，需要补回共享能力。
            // 原版 GainTrait 即使遇到受抑制特质也会授予能力，此处沿用该行为。
            if (pawn.abilities != null)
            {
                foreach (Trait retainedTrait in retained)
                {
                    if (retainedTrait.CurrentData.abilities == null) continue;
                    foreach (AbilityDef ability in retainedTrait.CurrentData.abilities)
                    {
                        pawn.abilities.GainAbility(ability);
                    }
                }

                // 活跃基因也可能直接授予同一能力，而不通过基因特质授予。
                if (pawn.genes != null)
                {
                    foreach (Gene gene in pawn.genes.GenesListForReading)
                    {
                        if (gene == null || !gene.Active || gene.def.abilities == null) continue;
                        foreach (AbilityDef ability in gene.def.abilities)
                        {
                            pawn.abilities.GainAbility(ability);
                        }
                    }
                }
            }

            RefreshTraitEffects(pawn, traits);
        }

        /// <summary>通过原版接口添加普通特质，在发生同类或冲突特质时保留宿主基因的作用，并保存受抑制的人格条目。</summary>
        private static void AddPersonalityTrait(TraitSet traits, Trait newTrait, List<Trait> retained)
        {
            List<TraitSuppression> savedSuppression = retained.Select(trait => new TraitSuppression(trait)).ToList();
            bool suppressedByBodyTrait = retained.Any(trait =>
                !trait.Suppressed && trait.def.CanSuppress(newTrait));

            try
            {
                // 即使启用冲突抑制，GainTrait 仍会拒绝与活跃基因特质定义、等级均相同的条目。
                // 暂时隐藏该基因特质，让两种来源的条目都能保存在 allTraits 中。
                // 随后恢复宿主的抑制标记，避免导入的人格特质压制宿主基因特质。
                foreach (Trait bodyTrait in retained)
                {
                    if (bodyTrait.def == newTrait.def && bodyTrait.Degree == newTrait.Degree)
                    {
                        bodyTrait.suppressedByTrait = true;
                    }
                }

                traits.GainTrait(newTrait, true);
            }
            finally
            {
                foreach (TraitSuppression suppression in savedSuppression)
                {
                    suppression.Restore();
                }
            }

            if (suppressedByBodyTrait && traits.allTraits.Contains(newTrait))
            {
                // 沿用原版基因强制特质对普通特质的抑制方式。
                // 后续移除基因时，RemoveTrait(..., true) 可让该普通特质重新生效。
                newTrait.suppressedByTrait = true;
            }
        }

        /// <summary>重算基因抑制并刷新特质、工作、技能、需求、图像、攻击目标和冥想缓存，使替换结果立即反映到角色状态。</summary>
        private static void RefreshTraitEffects(Pawn pawn, TraitSet traits)
        {
            traits.RecalculateSuppression();
            RecacheTraitsMethod.Invoke(traits, null);
            pawn.Notify_DisabledWorkTypesChanged();
            pawn.skills?.Notify_SkillDisablesChanged();
            pawn.skills?.DirtyAptitudes();
            if (!pawn.Dead)
            {
                pawn.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
            }
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            pawn.Map?.attackTargetsCache?.UpdateTarget(pawn);
            MeditationFocusTypeAvailabilityCache.ClearFor(pawn);
        }

        /// <summary>检查条目是否具有有效定义且不属于性奴或空壳派生状态；基因来源由调用方另行筛选。</summary>
        private static bool IsPersonalityTrait(Trait trait)
        {
            return trait?.def != null && trait.def.defName != SlaveTraitName && trait.def.defName != ExtractedTraitName;
        }

        /// <summary>判断条目是否为需要保留的宿主基因特质或性奴派生状态。</summary>
        private static bool IsRetainedTrait(Trait trait)
        {
            return trait?.sourceGene != null || trait?.def?.defName == SlaveTraitName;
        }

        /// <summary>按定义、等级和剧本强制标记创建独立特质，不复制来源基因及身体相关的临时抑制状态。</summary>
        private static Trait CloneTrait(Trait trait)
        {
            return new Trait(trait.def, trait.Degree, trait.ScenForced);
        }

        private sealed class TraitSuppression
        {
            private readonly Trait trait;
            private readonly bool byTrait;
            private readonly Gene byGene;

            /// <summary>记录保留特质的两种抑制标记，以便导入普通特质后恢复宿主原有状态。</summary>
            public TraitSuppression(Trait trait)
            {
                this.trait = trait;
                byTrait = trait.suppressedByTrait;
                byGene = trait.suppressedByGene;
            }

            /// <summary>恢复记录的特质抑制和基因抑制标记，撤销导入过程对宿主条目的临时影响。</summary>
            public void Restore()
            {
                trait.suppressedByTrait = byTrait;
                trait.suppressedByGene = byGene;
            }
        }
    }
}
