using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
// 这个文件负责人格排泄和人格植入时的数据流转。
// 它会把受害者转换成人格凝胶产物，再把储存的人格数据恢复到空壳角色体内。
namespace SexSlaveCraft
{
    public static class ExcretionUtility
    {
        /// <summary>生成人格凝胶并保存角色快照，随后清理社交身份、重置技能并将身体标记为空壳。</summary>
        public static void DoExcretion(Pawn victim)
        {
            if (victim == null || victim.Map == null) return;
            if (RabbitCloneUtility.IsRabbitClone(victim)) return;
            // 步骤 1：读取锁链严重度，决定本次产出哪一档人格凝胶。
            ThingDef targetSlimeDef = PersonalityGelUtility.GetPersonalityGelDefForPawn(victim);
            // 步骤 2：生成人格凝胶物品，并把受害者的人格载荷写入其中。
            Thing slime = ThingMaker.MakeThing(targetSlimeDef);
            var comp = slime.TryGetComp<CompPersonalityStore>();
            if (comp != null) comp.StorePawnData(victim);

            GenSpawn.Spawn(slime, victim.Position, victim.Map);
            // 步骤 3：剥离社交身份，让身体真正进入空壳状态。
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
            // 步骤 4：移除进行中的排泄健康状态，并换成“已排泄完成”的空壳状态。
            var old = victim.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreting);
            if (old != null) victim.health.RemoveHediff(old);

            victim.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            Messages.Message(Strings.Message_PersonalityExcretedComplete(victim.LabelShort), victim, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>在植入前清理接收身体残留的恶堕、性奴身份、锁链及专精状态。</summary>
        public static void CleanConsumerData(Pawn consumer)
        {
            if (consumer == null) return;
            // 清理准备接收人格的身体，移除残留恶堕、性奴特质和锁链状态。
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

            // 清除接收身体的普通、有效终极和禁用终极标签。必须在源人格
            // 的方向和进度恢复前完成，避免宿主完成记录或普通严重度混入。
            TrainerSpecializationGelUtility.RemoveAllTrainerStates(consumer);
        }

        /// <summary>校验凝胶快照，替换人格与全部特化历史并恢复记忆实例；成功后消耗凝胶，校验或恢复失败时返回失败。</summary>
        public static bool InheritEverything(Pawn consumer, CompPersonalityStore data)
        {
            if (consumer == null || data == null) return false;
            // 缺失快照不等于合法的空特质人格。
            // 必须在修改身体或消耗凝胶前拒绝此类植入。
            if (consumer.story?.traits?.allTraits == null || data.storedTraits == null)
            {
                Log.Error("[SSC] Personality insertion requires a valid trait snapshot and a humanlike trait tracker.");
                return false;
            }

            try
            {
                // 先移除已排泄完成的空壳状态，开始恢复人格。
                if (SSCDefOf.SSC_PersonalityExcreted_Done != null)
                {
                    Hediff doneHediff = consumer.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreted_Done);
                    if (doneHediff != null)
                    {
                        consumer.health.RemoveHediff(doneHediff);
                    }
                }
                // 步骤 1：在恢复新人格前，先清空旧空壳残留数据。
                CleanConsumerData(consumer);
                // 步骤 2：开始恢复人格身份卡，首先恢复空壳角色的名字。
                if (!string.IsNullOrEmpty(data.nickName))
                {
                    consumer.Name = new NameTriple(data.firstName ?? string.Empty, data.nickName, data.lastName ?? string.Empty);
                }
                // 步骤 3：恢复腐化度，让 SSC 的成长系统从原进度继续。
                if (data.corruptionLevel >= 0 && consumer.needs != null)
                {
                    var corNeed = consumer.needs.TryGetNeed<Need_Corruption>();
                    if (corNeed != null) corNeed.RestoreCorruption(data.corruptionLevel, data.highestCorruptionLevel);
                }
                // 步骤 4：恢复童年/成年背景，并刷新工作限制。
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

                // 步骤 5：旧快照（版本 0）无法可靠辨认基因来源，按已存条目恢复人格特质。
                // 与版本 1 一样保留宿主基因，不猜测旧数据的来源。
                PersonalityTraitUtility.RestoreTraits(consumer, data.storedTraits);

                // 性奴特质仅由恢复后的历史最高恶堕值重建。
                SSCIdentityUtility.SyncSexSlaveTraitFromHighestCorruption(consumer);

                CompSexSlaveTraining trainingComp = consumer.TryGetComp<CompSexSlaveTraining>();
                if (trainingComp != null)
                {
                    // 整体替换历史，避免普通方向切换将宿主的旧进度混入新人格。
                    trainingComp.RestoreSpecializationProgress(data.specializationType,
                        data.specializationProgress, data.specializationProgressByType);
                    trainingComp.milkProductionEnabled = data.milkProductionEnabled;
                    SSCIdentityUtility.TrySetIdentity(consumer, data.sscIdentity);
                }
                // 步骤 6：重建锁链，让主从系统继续知道当前绑定的主人。
                if (data.chainHediffData?.masterPawn != null && consumer.health != null &&
                    SSCBondUtility.Bind(data.chainHediffData.masterPawn, consumer, true))
                {
                    Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(consumer);
                    if (chain != null)
                    {
                        chain.Severity = data.chainHediffData.severity;
                    }
                }
                // 步骤 7：根据存储的人格数据恢复技能等级、热情和经验。
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
                // 步骤 8：恢复记忆和直接关系，让角色重新拿回原本的社会历史。
                // 先恢复旧记忆堆，再把直接社交关系重新搭回去。
                PersonalityMemoryUtility.Restore(consumer, data.storedMemories);
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
                // 步骤 9：重新应用自定义健康状态标签，恢复凝胶保存的模组状态。
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

                        // 训导官三种标签由下方的互斥恢复统一处理。通用路径
                        // 对严重度采取取较大值，会把宿主旧进度并入源人格。
                        if (TrainerSpecializationGelUtility.IsTrainerTag(hediffDef))
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

                // 即使旧凝胶没有任何标签，仍须保证接收身体的训导官标签
                // 已被清空；有标签时只恢复源人格所持的一个权威状态。
                // 阶段 2 的外层恢复作用域会在身份、绑定和标签全部就位后
                // 决定终极标记是否应转为禁用，不能在此处提前维护。
                TrainerSpecializationGelUtility.ApplyExclusiveTrainerTags(consumer, data);

                // 同步组件与刚恢复的健康状态，使凝胶携带的专精立即被识别并可持续训练。
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

    }
}
