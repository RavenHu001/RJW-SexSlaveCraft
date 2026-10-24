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
            // 从采集开始暂停能力并使旧请求失效；作用域仅隔离人格写入，不承诺原流程的完整回滚。
            if (!SSCPersonalityRuntimeUtility.TryBeginMutation(victim, out var mutation)) return;
            using (mutation)
            {
                CompSexSlaveTraining originalComp = victim.TryGetComp<CompSexSlaveTraining>();
                // 步骤 1：读取锁链严重度，决定本次产出哪一档人格凝胶。
                ThingDef targetSlimeDef = PersonalityGelUtility.GetPersonalityGelDefForPawn(victim);
                // 步骤 2：生成人格凝胶物品，并把受害者的人格载荷写入其中。
                Thing slime = ThingMaker.MakeThing(targetSlimeDef);
                var comp = slime.TryGetComp<CompPersonalityStore>();
                if (comp == null)
                {
                    Log.Error("[SSC] Personality extraction requires a personality store before stripping the source.");
                    slime.Destroy(DestroyMode.Vanish);
                    return;
                }
                if (comp != null)
                {
                    comp.StorePawnData(victim);
                    if (!mutation.IsCurrent || victim.TryGetComp<CompSexSlaveTraining>() != originalComp) return;
                    PetCatAbilityUtility.DetachAfterExtraction(victim);
                    PetDogAbilityUtility.DetachAfterExtraction(victim);
                    CombatantSpecializationGelUtility.DetachAfterExtraction(victim);
                }

                GenSpawn.Spawn(slime, victim.Position, victim.Map);
                // 资格与成长已进入成功生成的凝胶，再清原身体并写当前成长版本。
                // 空壳即使保留主人身份，也不能在以后读档时补回转走的记录。
                if (comp != null)
                {
                    SSCAbilityToggleUtility.ClearPersonalityState(victim);
                    SSCMasterQualificationUtility.ClearPersonalityQualification(victim);
                    SSCMasterProgressionUtility.ClearPersonalityRecord(victim);
                }
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
        }

        /// <summary>在植入前清理接收身体残留的恶堕、性奴身份、锁链及专精状态。</summary>
        public static void CleanConsumerData(Pawn consumer)
        {
            if (consumer == null) return;
            if (!SSCPersonalityRuntimeUtility.TryBeginMutation(consumer, out var mutation)) return;
            using (mutation) CleanConsumerDataCore(consumer);
        }

        // 独立清理入口建立作用域；植入入口已持有外层作用域时直接复用此核心，避免嵌套被拒绝。
        /// <summary>复用已持有的人格作用域清理宿主；身体开发与外观按原过滤保留，不把它们当人格字典重置。</summary>
        private static void CleanConsumerDataCore(Pawn consumer)
        {
            SSCAbilityToggleUtility.ClearPersonalityState(consumer);
            // 清宿主时同时封住旧档补建，且必须早于下面的普通身份清理；
            // 后续若来源没有成长，空记录就是植入的最终结果。
            SSCMasterQualificationUtility.ClearPersonalityQualification(consumer);
            SSCMasterProgressionUtility.ClearPersonalityRecord(consumer);
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
            // 同时清除无终极标记却残留在宿主能力追踪器中的猫、狗技能。
            PetCatAbilityUtility.RemoveAbility(consumer);
            PetDogAbilityUtility.RemoveAbility(consumer);

            // 清除接收身体的普通、有效终极和禁用终极标签。必须在源人格
            // 的方向和进度恢复前完成，避免宿主完成记录或普通严重度混入。
            TrainerSpecializationGelUtility.RemoveAllTrainerStates(consumer);
            CombatantSpecializationGelUtility.RemoveAllStates(consumer);
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
            if (data.sscIdentity != PawnIdentity.Unset && data.sscIdentity != PawnIdentity.Slave &&
                data.sscIdentity != PawnIdentity.Master)
            {
                Log.Error("[SSC] Personality insertion has an invalid saved SSC identity.");
                return false;
            }
            data.NormalizeMasterQualification();
            // 植入和旧凝胶加工副本都可能尚未经过读档，修改宿主前先解释独立成长版本。
            data.NormalizeMasterProgressionSnapshot();
            if (data.masterQualificationUnlocked && consumer.TryGetComp<CompSexSlaveTraining>() == null)
            {
                Log.Error("[SSC] Personality insertion cannot preserve Master qualification without an SSC training component.");
                return false;
            }

            // 手动主人可能没有永久资格但有成长，必须在清空身体前独立预检。
            if (data.masterGrowthPoints >= 0f && !SSCMasterProgressionUtility.CanCarryRecord(consumer))
            {
                Log.Error("[SSC] Personality insertion cannot preserve Master progression in this body.");
                return false;
            }

            SSCAbilityToggleState toggleSnapshot = data.abilityToggles?.Copy() ?? new SSCAbilityToggleState();
            CompSexSlaveTraining originalComp = consumer.TryGetComp<CompSexSlaveTraining>();
            if (toggleSnapshot.HasEntries && originalComp == null)
            {
                Log.Error("[SSC] Personality insertion cannot preserve personal ability preferences without an SSC training component.");
                return false;
            }
            if (!SSCPersonalityRuntimeUtility.TryBeginMutation(consumer, out var mutation)) return false;
            using (mutation)
            {
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
                    CleanConsumerDataCore(consumer);
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
                        // 原泌乳字段按来源精确覆盖，包括 false；不与人格字典合并或套用身体开关默认。
                        trainingComp.milkProductionEnabled = data.milkProductionEnabled;
                        if (!SSCIdentityUtility.TryRestorePersonalityIdentity(consumer, data.sscIdentity,
                            data.masterQualificationUnlocked))
                        {
                            Log.Error("[SSC] Personality insertion could not restore the saved SSC identity.");
                            return false;
                        }
                    }
                    // 身份恢复成功后再重建来源成长；同为主人也不能跳过此步骤。
                    // 无记录来源保持清空，退出主人后的来源则恢复暂停记录；通用标签不能取最大值覆盖它。
                    if (!SSCMasterProgressionUtility.RestoreRecord(consumer, data.masterGrowthPoints))
                    {
                        Log.Error("[SSC] Personality insertion could not restore the saved Master progression.");
                        return false;
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
                                // 旧凝胶可能存有第三方或旧主人关系；实际锁链恢复失败时也不能回放奖励关系。
                                if (relData.def == SSCDefOf.SSC_FlawedLovers &&
                                    !SSCBondRelationUtility.IsValidPair(consumer, relData.otherPawn)) continue;
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
#if SSC_RW17
                            // 拒绝历史／外部凝胶中的身体开发标签，既不补建，也不合并覆盖宿主原进度和开关。
                            if (SSCBodyDevelopmentUtility.IsBodyStateDef(hediffDef)) continue;
#endif
                            // 全部 PNA 身体状态留在宿主，历史／外部凝胶中的同类标签也不恢复。
                            // 宿主原有记录及 Need_PNA 值由身体保留，不参与下方最大值合并。
                            // 能力标记也不得按标签还原：偏好由专用容器精确替换，标记在流转结束后派生。
                            if (SSCPnaExposureUtility.IsBodyStateDef(hediffDef) || SSCAbilityToggleUtility.IsMarkerDef(hediffDef)) continue;

                            // 主人经验已由专用标量恢复；跳过历史或外部加入的同名标签，
                            // 防止下方通用最大值合并把无记录或低经验再次替换为宿主旧高经验。
                            if (SSCMasterProgressionUtility.IsProgressionDef(hediffDef)) continue;

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

                            // 训导官与战斗员标签由下方互斥恢复统一处理。通用路径
                            // 对严重度采取取较大值，会把宿主旧进度并入源人格。
                            if (TrainerSpecializationGelUtility.IsTrainerTag(hediffDef) ||
                                CombatantSpecializationGelUtility.IsCombatantTag(hediffDef))
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
                    CombatantSpecializationGelUtility.ApplyExclusiveTags(consumer, data);

                    // 同步组件与刚恢复的健康状态，使凝胶携带的专精立即被识别并可持续训练。
                    CompSexSlaveTraining.ReconcileSpecialization(consumer);

                    // 终极标记与全部人格字段就位后立即恢复技能的剩余冷却。
                    // 只携带绝对结束时间，不复制原身体的 Ability 实例或临时激励。
                    PetCatAbilityUtility.RestoreCooldown(consumer, data.petCatComfortCooldownEndTick);
                    PetDogAbilityUtility.RestoreCooldown(consumer, data.petDogTameCooldownEndTick);

                    // 身份、锁链、专精及其健康回调全部完成后，按当前身体的最终有效链核对 PNA。
                    // 不从凝胶恢复负荷／成瘾或补充需求；仍合法存在的需求保留宿主自己的满足度。
                    SSCPnaExposureUtility.RefreshBodyState(consumer);
                    consumer.needs?.AddOrRemoveNeedsAsAppropriate();

                    if (!mutation.IsCurrent || consumer.TryGetComp<CompSexSlaveTraining>() != originalComp) return false;
                    // 完整恢复成功后精确替换，明确关闭及未知能力键均保留；作用域结束才重新启用能力。
                    SSCAbilityToggleUtility.RestorePreference(consumer, toggleSnapshot);

                    // 健康、专精及能力恢复可能触发后续回调，成功出口再按最终锁链核对一次。
                    // 此处只清理失效关系，不把凝胶里的旧社会历史解释为一次新的仪式奖励。
                    SSCBondRelationUtility.RemoveInvalidRelations(consumer);
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
}
