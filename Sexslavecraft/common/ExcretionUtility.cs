using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

// EN: This file handles personality excretion and personality insertion data transfer.
// EN: It turns a victim into personality gel output, then restores stored identity data into a hollow pawn.
// CN: 这个文件负责人格排泄和人格植入时的数据流转。
// CN: 它会把受害者转换成人格凝胶产物，再把储存的人格数据恢复到空壳 Pawn 体内。
namespace SexSlaveCraft
{
    public static class ExcretionUtility
    {
        public static void DoExcretion(Pawn victim)
        {
            if (victim == null || victim.Map == null) return;
            if (RabbitCloneUtility.IsRabbitClone(victim)) return;

            // EN: Step 1: read ChainOfSexSlave severity to decide which grade of personality gel should be produced.
            // CN: 步骤 1：读取 ChainOfSexSlave 严重度，决定本次产出哪一档人格凝胶。
            ThingDef targetSlimeDef = PersonalityGelUtility.GetPersonalityGelDefForPawn(victim);

            // EN: Step 2: create the personality gel item and copy the victim's stored personality payload into it.
            // CN: 步骤 2：生成人格凝胶物品，并把受害者的人格载荷写入其中。
            Thing slime = ThingMaker.MakeThing(targetSlimeDef);
            var comp = slime.TryGetComp<CompPersonalityStore>();
            if (comp != null) comp.StorePawnData(victim);

            GenSpawn.Spawn(slime, victim.Position, victim.Map);

            // EN: Step 3: strip social identity so the body becomes a real hollow pawn.
            // CN: 步骤 3：剥离社交身份，让身体真正进入空壳状态。
            if (victim.relations != null) victim.relations.ClearAllRelations();

            if (victim.story != null && !victim.story.traits.HasTrait(SSCDefOf.SSC_Trait_PE))
            {
                victim.story.traits.GainTrait(new Trait(SSCDefOf.SSC_Trait_PE));
            }

            if (victim.skills != null)
            {
                foreach (var skill in victim.skills.skills)
                {
                    skill.Level = (skill.def == SkillDefOf.Social) ? 0 : 6;
                    skill.xpSinceLastLevel = 0;
                }
            }

            // EN: Step 4: replace the running excretion hediff with the finished hollow-pawn state.
            // CN: 步骤 4：移除进行中的排泄 Hediff，并换成“已排泄完成”的空壳状态。
            var old = victim.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreting);
            if (old != null) victim.health.RemoveHediff(old);

            victim.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            Messages.Message(Strings.Message_PersonalityExcretedComplete(victim.LabelShort), victim, MessageTypeDefOf.NeutralEvent);
        }

        public static void CleanConsumerData(Pawn consumer)
        {
            if (consumer == null) return;

            // EN: This cleanup is for the receiving body before personality insertion, so it strips leftover corruption, SexSlave trait state, and slave-chain state.
            // CN: 这段清理是给“准备接收人格的身体”用的，所以会清掉残留恶堕、SexSlave 特质和锁链状态。
            if (consumer.needs != null)
            {
                var corNeed = consumer.needs.TryGetNeed<Need_Corruption>();
                if (corNeed != null) corNeed.SetCorruption(0f, true);
            }

            TraitDef ssDef = DefDatabase<TraitDef>.GetNamedSilentFail("SexSlaveCraft_SexSlave");
            if (ssDef != null && consumer.story?.traits?.allTraits != null)
            {
                consumer.story.traits.allTraits.RemoveAll(x => x.def == ssDef);
            }

            SSCBondUtility.Unbind(consumer);
            SSCIdentityUtility.TrySetIdentity(consumer, PawnIdentity.Unset);

            if (SSCDefOf.SSC_Hediff_Bus != null)
            {
                var targetHediff = consumer.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Bus);
                if (targetHediff != null) consumer.health.RemoveHediff(targetHediff);
            }

            if (SSCDefOf.SSC_Hediff_Bus_Final != null)
            {
                var targetHediff = consumer.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Bus_Final);
                if (targetHediff != null) consumer.health.RemoveHediff(targetHediff);
            }

            if (SSCDefOf.SSC_Hediff_Cow != null)
            {
                var targetHediff = consumer.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow);
                if (targetHediff != null) consumer.health.RemoveHediff(targetHediff);
            }

            if (SSCDefOf.SSC_Hediff_Cow_Final != null)
            {
                var targetHediff = consumer.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow_Final);
                if (targetHediff != null) consumer.health.RemoveHediff(targetHediff);
            }

            PetSpecializationUtility.RemoveAllPetStates(consumer);
        }

        public static bool InheritEverything(Pawn consumer, CompPersonalityStore data)
        {
            if (consumer == null || data == null) return false;

            try
            {
                // EN: Remove the empty-shell marker first so failed restores do not leave stale shell state behind.
                // CN: 先移除空壳状态，避免后续恢复异常时留下错误的空壳标记。
                if (SSCDefOf.SSC_PersonalityExcreted_Done != null)
                {
                    Hediff doneHediff = consumer.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreted_Done);
                    if (doneHediff != null)
                    {
                        consumer.health.RemoveHediff(doneHediff);
                    }
                }

                // EN: Step 1: clear all leftover shell data before the new personality is restored.
                // CN: 步骤 1：在恢复新人格前，先清空旧空壳残留数据。
                CleanConsumerData(consumer);

                if (consumer.story?.traits != null)
                {
                    Trait peTrait = consumer.story.traits.GetTrait(SSCDefOf.SSC_Trait_PE);
                    if (peTrait != null) consumer.story.traits.RemoveTrait(peTrait);
                }

                // EN: Step 2: restore the stored identity card, starting with the hollow pawn's name.
                // CN: 步骤 2：开始恢复人格身份卡，首先恢复空壳 Pawn 的名字。
                if (!string.IsNullOrEmpty(data.nickName))
                {
                    consumer.Name = new NameTriple(data.firstName ?? string.Empty, data.nickName, data.lastName ?? string.Empty);
                }

                // EN: Step 3: restore corruption so SSC progression systems continue from the saved state.
                // CN: 步骤 3：恢复腐化度，让 SSC 的成长系统从原进度继续。
                if (data.corruptionLevel >= 0 && consumer.needs != null)
                {
                    var corNeed = consumer.needs.TryGetNeed<Need_Corruption>();
                    if (corNeed != null) corNeed.RestoreCorruption(data.corruptionLevel, data.highestCorruptionLevel);
                }

                // EN: Step 4: restore childhood/adulthood and refresh work restrictions.
                // CN: 步骤 4：恢复童年/成年背景，并刷新工作限制。
                if (consumer.story != null)
                {
                if (data.childhood != null) consumer.story.Childhood = data.childhood;

                    if (data.adulthood != null)
                    {
                        consumer.story.Adulthood = data.adulthood;
                    }
                    else
                    {
                        if (consumer.ageTracker.AgeBiologicalYears >= 18)
                        {
                            BackstoryDef genericAdult = DefDatabase<BackstoryDef>.GetNamedSilentFail("Colonist");
                            consumer.story.Adulthood = genericAdult;
                        }
                        else
                        {
                            consumer.story.Adulthood = null;
                        }
                    }
                    consumer.Notify_DisabledWorkTypesChanged();
                }

                // SexSlaveTrait is derived exclusively from restored highest corruption.
                SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(consumer);

                CompSexSlaveTraining trainingComp = consumer.TryGetComp<CompSexSlaveTraining>();
                if (trainingComp != null)
                {
                    trainingComp.SetSpecialization(data.specializationType);
                    trainingComp.specializationProgress = data.specializationProgress;
                    trainingComp.milkProductionEnabled = data.milkProductionEnabled;
                    SSCIdentityUtility.TrySetIdentity(consumer, data.sscIdentity);
                }

                // EN: Step 6: rebuild ChainOfSexSlave so the master/slave systems still know the linked master.
                // CN: 步骤 6：重建 ChainOfSexSlave，让主从系统继续知道当前绑定的主人。
                if (data.chainHediffData?.masterPawn != null && consumer.health != null &&
                    SSCBondUtility.Bind(data.chainHediffData.masterPawn, consumer, true))
                {
                    Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(consumer);
                    if (chain != null)
                    {
                        chain.Severity = data.chainHediffData.severity;
                    }
                }

                // EN: Step 7: restore skill level, passion, and xp from the stored personality payload.
                // CN: 步骤 7：根据存储的人格数据恢复技能等级、热情和经验。
                if (consumer.skills != null && data.storedSkills != null)
                {
                    foreach (var sData in data.storedSkills)
                    {
                        var skill = consumer.skills.GetSkill(sData.def);
                        if (skill != null)
                        {
                            skill.Level = sData.level;
                            skill.xpSinceLastLevel = sData.xp;
                            skill.passion = sData.passion;
                        }
                    }
                }

                // EN: Step 8: restore memories and direct relations so the pawn regains its social history.
                // CN: 步骤 8：恢复记忆和直接关系，让 Pawn 重新拿回原本的社会历史。
                // EN: Restore the old memory stack first, then rebuild direct social relations on top of it.
                // CN: 先恢复旧记忆堆，再把直接社交关系重新搭回去。
                ImplMemories(consumer, data.storedMemories);
                if (consumer.relations != null && data.storedRelations != null && data.storedRelations.Count > 0)
                {
                    consumer.relations.ClearAllRelations();
                    foreach (var relData in data.storedRelations)
                    {
                        if (relData.otherPawn != null && !relData.otherPawn.Destroyed && relData.otherPawn != consumer)
                        {
                            consumer.relations.AddDirectRelation(relData.def, relData.otherPawn);
                        }
                    }
                }

                // EN: Step 9: re-apply custom hediff tags so modded state survives personality insertion.
                // CN: 步骤 9：重新注入自定义 Hediff 标签，保证模组状态在植入后仍然保留。
                if (data.hediffTags != null && data.hediffTags.Count > 0 && consumer.health != null)
                {
                    foreach (var tag in data.hediffTags)
                    {
                        HediffDef hediffDef = tag.Key;
                        float severity = tag.Value;

                        if (hediffDef == SSCDefOf.SSC_Hediff_Bus || hediffDef == SSCDefOf.SSC_Hediff_Bus_Final)
                        {
                            continue;
                        }

                        if (hediffDef == SSCDefOf.SSC_Hediff_Cow || hediffDef == SSCDefOf.SSC_Hediff_Cow_Final)
                        {
                            continue;
                        }

                        if (PetSpecializationUtility.IsPetTag(hediffDef))
                        {
                            continue;
                        }

                        if (hediffDef != null)
                        {
                            try
                            {
                                Log.Warning($"[SSC Tag Debug] 正在尝试将 Tag: {hediffDef.defName} (严重度: {severity}) 注入到 {consumer.LabelShort} 体内...");

                                Hediff existingHediff = consumer.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                                if (existingHediff != null)
                                {
                                    existingHediff.Severity = severity > existingHediff.Severity ? severity : existingHediff.Severity;
                                }
                                else
                                {
                                    Hediff newHediff = consumer.health.AddHediff(hediffDef);
                                    if (newHediff != null)
                                    {
                                        newHediff.Severity = severity > 0f ? severity : 1f;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[SSC Tag Debug] 注入 Tag 失败: {hediffDef.defName}, pawn={consumer.LabelShort}, err={ex}");
                            }
                        }
                    }

                    BusSpecializationUtility.ApplyExclusiveBusTags(consumer, data);
                    BusSpecializationUtility.ApplyExclusiveCowTags(consumer, data);
                    PetSpecializationUtility.ApplyExclusivePetTags(consumer, data);
                }
                else
                {
                    Log.Warning($"[SSC Tag Debug] ⚠️ 被塞入的这个雕像/凝胶体内，没有任何 Tag 数据！");
                }

                // EN: Reconcile comp state with the injected hediffs so gel-injected
                // specializations are immediately adoptable and trainable.
                // CN: 对账 comp 与刚注入的 hediff，确保凝胶注入的特化立即被认领并可持续训练。
                CompSexSlaveTraining.ReconcileSpecialization(consumer);

                Messages.Message(Strings.Message_PersonalityFusionComplete(consumer.LabelShort, data.nickName), consumer, MessageTypeDefOf.NeutralEvent);

                if (data.parent != null && !data.parent.Destroyed)
                {
                    data.parent.Destroy(DestroyMode.Vanish);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC] InheritEverything failed for {consumer?.LabelShort ?? "null"}: {ex}");
                return false;
            }
        }

        private static void ImplMemories(Pawn target, System.Collections.Generic.List<StoredMemoryData> memories)
        {
            if (target?.needs?.mood?.thoughts?.memories == null) return;
            if (memories == null) return;

            var memoryHandler = target.needs.mood.thoughts.memories;
            memoryHandler.Memories.Clear();

            foreach (var data in memories)
            {
                if (data == null) continue;
                if (data.def == null) continue;
                if (!ThoughtUtility.CanGetThought(target, data.def)) continue;

                Thought_Memory newThought = (Thought_Memory)ThoughtMaker.MakeThought(data.def);

                if (newThought != null)
                {
                    try
                    {
                        newThought.age = data.age;
                        newThought.moodPowerFactor = data.moodPowerFactor;
                        newThought.otherPawn = (data.otherPawn != null && !data.otherPawn.Destroyed) ? data.otherPawn : null;

                        memoryHandler.TryGainMemory(newThought);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[SSC] Skip invalid memory during inherit: def={data.def?.defName ?? "null"}, pawn={target.LabelShort}, err={ex.Message}");
                    }
                }
            }
        }
    }
}
