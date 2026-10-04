using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public static class PetSpecializationUtility
    {
        private const float ThresholdProgress = 0.20f;
        private const float InitialHediffSeverity = 0.01f;
        private const float AutoAffectionMaxDistance = 2.9f;

        private static readonly SexSlaveSpecializationType[] PetTypes =
        {
            SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog,
            SexSlaveSpecializationType.PetRabbit
        };

        public static bool IsPetSpecialization(SexSlaveSpecializationType type)
        {
            return PetSpecializationRules.IsPetSpecialization(type);
        }

        public static HediffDef GetBaseHediffDef(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetCat");
                case SexSlaveSpecializationType.PetDog:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetDog");
                case SexSlaveSpecializationType.PetRabbit:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetRabbit");
                default:
                    return null;
            }
        }

        public static HediffDef GetFinalHediffDef(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetCat_Final");
                case SexSlaveSpecializationType.PetDog:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetDog_Final");
                case SexSlaveSpecializationType.PetRabbit:
                    return DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_PetRabbit_Final");
                default:
                    return null;
            }
        }

        public static string GetResearchDefName(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return "SSC_RES_PetCat";
                case SexSlaveSpecializationType.PetDog:
                    return "SSC_RES_PetDog";
                case SexSlaveSpecializationType.PetRabbit:
                    return "SSC_RES_PetRabbit";
                default:
                    return null;
            }
        }

        public static bool HasAnyPetState(Pawn pawn, SexSlaveSpecializationType type)
        {
            if (pawn?.health?.hediffSet == null || !IsPetSpecialization(type)) return false;

            HediffDef baseDef = GetBaseHediffDef(type);
            HediffDef finalDef = GetFinalHediffDef(type);
            return (baseDef != null && pawn.health.hediffSet.HasHediff(baseDef)) ||
                   (finalDef != null && pawn.health.hediffSet.HasHediff(finalDef));
        }

        public static bool HasFinalPetState(Pawn pawn, SexSlaveSpecializationType type)
        {
            if (pawn?.health?.hediffSet == null || !IsPetSpecialization(type)) return false;

            HediffDef finalDef = GetFinalHediffDef(type);
            return finalDef != null && pawn.health.hediffSet.HasHediff(finalDef);
        }

        public static bool HasAnyFinalPetState(Pawn pawn)
        {
            foreach (SexSlaveSpecializationType type in PetTypes)
                if (HasFinalPetState(pawn, type)) return true;
            return false;
        }

        /// <summary>只读查询生效资格；历史和残留普通标签不授予行为资格，也不改变存档。</summary>
        public static bool HasActivePetEffects(Pawn pawn, SexSlaveSpecializationType type)
        {
            if (pawn?.health?.hediffSet == null || pawn.Destroyed || pawn.Dead) return false;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            return PetSpecializationRules.HasEffects(type, comp?.specializationType ?? SexSlaveSpecializationType.None,
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetCat),
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetDog),
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit));
        }

        /// <summary>终极成果使用完成进度；普通效果只读取本种当前进度，绝不借用其他方向或历史。</summary>
        public static float GetEffectivePetProgress(Pawn pawn, SexSlaveSpecializationType type)
        {
            if (!HasActivePetEffects(pawn, type)) return 0f;
            if (HasFinalPetState(pawn, type)) return 1f;
            return CompSexSlaveTraining.NormalizeSpecializationProgress(
                pawn.TryGetComp<CompSexSlaveTraining>().specializationProgress);
        }

        /// <summary>亲昵需要真实训练组件，以及任一当前普通宠物或保留的终极宠物成果。</summary>
        public static bool HasPetAffectionQualification(Pawn pawn)
        {
            if (pawn?.TryGetComp<CompSexSlaveTraining>() == null) return false;
            foreach (SexSlaveSpecializationType type in PetTypes)
                if (HasActivePetEffects(pawn, type)) return true;
            return false;
        }

        /// <summary>持续培养不重查研究或玩家开放状态，合法旧档猫兔仍可成长。</summary>
        public static bool CanTrainPetSpecialization(Pawn pawn, SexSlaveSpecializationType type)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && PetSpecializationRules.CanTrain(type, comp.specializationType,
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetCat),
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetDog),
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit));
        }

        public static bool IsPetTag(HediffDef def)
        {
            if (def == null) return false;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                if (def == GetBaseHediffDef(type) || def == GetFinalHediffDef(type))
                {
                    return true;
                }
            }

            return false;
        }

        public static HediffDef GetBaseHediffForFinal(HediffDef finalDef)
        {
            if (finalDef == null) return null;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                if (finalDef == GetFinalHediffDef(type))
                {
                    return GetBaseHediffDef(type);
                }
            }

            return null;
        }

        public static void EnsurePetHediffFromSpecialization(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !CanTrainPetSpecialization(pawn, comp.specializationType)) return;

            HediffDef baseDef = GetBaseHediffDef(comp.specializationType);
            if (baseDef == null) return;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(baseDef);
            bool wasMissing = hediff == null;
            if (wasMissing)
            {
                hediff = pawn.health.AddHediff(baseDef);
            }

            if (hediff != null)
            {
                if (wasMissing)
                {
                    hediff.Severity = Mathf.Max(InitialHediffSeverity, comp.specializationProgress);
                }
                else
                {
                    // 初始 0.01 仅用于显示，不能在下次对账时反写成经验。
                    // 更高的外部 Hediff 进度仍导入组件，供旧档和注入内容使用。
                    float markerProgress = hediff.Severity > InitialHediffSeverity
                        ? CompSexSlaveTraining.NormalizeSpecializationProgress(hediff.Severity) : 0f;
                    comp.specializationProgress = Mathf.Max(
                        CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress), markerProgress);
                    hediff.Severity = Mathf.Max(InitialHediffSeverity, comp.specializationProgress);
                }
            }
        }

        /// <summary>清理不生效的普通状态后同步当前普通进度；不删除终极成果或选择冲突成果。</summary>
        public static void SyncPetStates(Pawn pawn)
        {
            RemoveInactiveOrdinaryPetStates(pawn);
            EnsurePetHediffFromSpecialization(pawn);
        }

        /// <summary>仅清理普通状态；切换通知及人格恢复期间不导入宿主进度，也不提前创建普通状态。</summary>
        internal static void RemoveInactiveOrdinaryPetStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.TryGetComp<CompSexSlaveTraining>() == null) return;
            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                // 组内任意终极成果都结束普通宠物培养，避免同种普通与终极属性重复叠加。
                if (!CanTrainPetSpecialization(pawn, type)) RemoveIfPresent(pawn, GetBaseHediffDef(type));
            }
        }

        public static bool CanUsePetSpecialization(Pawn pawn, SexSlaveSpecializationType type, out string reason)
        {
            bool allowed = CanSelectPetSpecialization(pawn, type, out PetSpecializationFailure failure);
            reason = GetSelectionFailureReason(failure);
            return allowed;
        }

        /// <summary>玩家选择的完整资格；与底层方向恢复和持续培养分开。</summary>
        public static bool CanSelectPetSpecialization(Pawn pawn, SexSlaveSpecializationType type,
            out PetSpecializationFailure failure)
        {
            failure = PetSpecializationFailure.MissingRequirements;
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !IsPetSpecialization(type)) return false;
            if (type == SexSlaveSpecializationType.PetCat || type == SexSlaveSpecializationType.PetRabbit)
            {
                failure = PetSpecializationFailure.NotImplemented;
                return false;
            }
            if (comp.pawnIdentity != PawnIdentity.Slave) return false;
            if (!PetSpecializationRules.CanChangeDirection(type,
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetCat),
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetDog),
                HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit), out failure)) return false;
            if (HasFinalPetState(pawn, type))
            {
                failure = PetSpecializationFailure.AlreadyFinalized;
                return false;
            }

            ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(GetResearchDefName(type));
            if (SSCDefOf.SSC_BasicTraining == null || research == null)
            {
                failure = PetSpecializationFailure.MissingRequirements;
                return false;
            }
            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining) ||
                !ResearchUtils.IsResearchFinished(research))
            {
                failure = PetSpecializationFailure.ResearchRequired;
                return false;
            }

            failure = PetSpecializationFailure.None;
            return true;
        }

        /// <summary>菜单动作和其他玩家入口均在提交时复查，并确认仍使用同一个训练组件。</summary>
        public static bool TrySelectPetSpecialization(Pawn pawn, CompSexSlaveTraining comp,
            SexSlaveSpecializationType type, out PetSpecializationFailure failure)
        {
            failure = PetSpecializationFailure.MissingRequirements;
            if (comp == null || pawn?.TryGetComp<CompSexSlaveTraining>() != comp) return false;
            if (!CanSelectPetSpecialization(pawn, type, out failure) ||
                !comp.TrySetSpecialization(type, out failure)) return false;
            EnsurePetHediffFromSpecialization(pawn);
            return true;
        }

        public static string GetSelectionFailureReason(PetSpecializationFailure failure)
        {
            switch (failure)
            {
                case PetSpecializationFailure.None: return null;
                case PetSpecializationFailure.NotImplemented: return Strings.ITab_SpecializationUnfinishedSuffix;
                case PetSpecializationFailure.ResearchRequired: return Strings.ITab_SpecializationPetDisabledResearch;
                case PetSpecializationFailure.AlreadyFinalized: return Strings.ITab_SpecializationFinalizedSuffix;
                case PetSpecializationFailure.ConflictingFinal: return Strings.ITab_SpecializationPetDisabledConflictingFinal;
                default: return Strings.ITab_SpecializationPetDisabledMissingRequirements;
            }
        }

        public static bool TryGainPetProgress(Pawn pawn, SexSlaveSpecializationType type, float amount, bool showThresholdMessage = true)
        {
            if (pawn == null || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) ||
                !CanTrainPetSpecialization(pawn, type)) return false;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.specializationType != type) return false;
            EnsurePetHediffFromSpecialization(pawn);
            if (comp.specializationProgress >= CompSexSlaveTraining.SpecializationCompletionProgress) return false;

            float oldProgress = comp.specializationProgress;
            comp.AddSpecializationProgress(amount);
            EnsurePetHediffFromSpecialization(pawn);

            if (showThresholdMessage && oldProgress < ThresholdProgress && comp.specializationProgress >= ThresholdProgress)
            {
                Messages.Message(
                    Strings.Message_PetSpecializationUnlocked(pawn.LabelShort, GetSpecializationLabel(type)),
                    pawn,
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }

            return true;
        }

        public static bool TryStartAutomaticPetAffectionJob(Pawn pet, CompSexSlaveTraining comp = null)
        {
            if (pet == null) return false;
            comp = comp ?? pet.TryGetComp<CompSexSlaveTraining>();
            // 防止旧组件引用绕过当前冷却；资格由当前普通方向或独立终极成果提供。
            if (comp == null || pet.TryGetComp<CompSexSlaveTraining>() != comp ||
                !HasPetAffectionQualification(pet)) return false;

            Pawn master = SSCBondUtility.GetBoundMaster(pet);
            if (!CanDoPetAffectionNow(pet, master)) return false;
            if (!IsPetAffectionCooldownReady(comp)) return false;
            if (!CanStartPetAffectionJobWithoutDisruptingWork(pet)) return false;
            if (SSCDefOf.SSC_Job_PetAffection == null) return false;

            Job job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, master);
            job.expiryInterval = GenTicks.TickRareInterval;
            return pet.jobs != null && pet.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public static bool CompletePetAffection(Pawn pet, Pawn master)
        {
            if (pet == null) return false;
            CompSexSlaveTraining comp = pet.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !HasPetAffectionQualification(pet)) return false;
            if (!CanDoPetAffectionNow(pet, master)) return false;
            if (!IsPetAffectionCooldownReady(comp)) return false;

            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("SSC_PetAffection_Mood");
            if (thoughtDef != null && master?.needs?.mood?.thoughts?.memories != null)
            {
                master.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, pet);
            }

            comp.lastPetAffectionTick = Find.TickManager.TicksGame;

            // EN: Pet affection is deliberately not adding specialization experience yet.
            // CN: 亲昵动作暂时不增加宠物专精经验；等经验来源和数值敲定后，再接 TryGainPetProgress。
            return true;
        }

        public static bool CanDoPetAffectionNow(Pawn pet, Pawn master)
        {
            // 调度、执行中 FailOn 和完成结算共用同一检查，绑定或成果中途变化即失效。
            if (!HasPetAffectionQualification(pet) || master == null || master.DestroyedOrNull() || master.Dead ||
                SSCBondUtility.GetBoundMaster(pet) != master)
            {
                return false;
            }

            if (!pet.Spawned || !master.Spawned || pet.Map != master.Map)
            {
                return false;
            }

            if (pet.Dead || pet.Downed || master.Downed || pet.Drafted || master.Drafted)
            {
                return false;
            }

            return pet.Position.InHorDistOf(master.Position, AutoAffectionMaxDistance);
        }

        private static bool IsPetAffectionCooldownReady(CompSexSlaveTraining comp)
        {
            return comp != null &&
                   Find.TickManager.TicksGame - comp.lastPetAffectionTick >= CompSexSlaveTraining.PetAffectionCooldownTicks;
        }

        private static bool CanStartPetAffectionJobWithoutDisruptingWork(Pawn pet)
        {
            if (pet?.jobs == null) return false;

            JobDef curJobDef = pet.CurJobDef;
            if (curJobDef == null) return true;
            if (curJobDef == SSCDefOf.SSC_Job_PetAffection) return false;

            // EN: This affection job is only allowed to replace idle waiting jobs.
            // CN: 亲昵 job 只允许替换空闲等待类 job，避免打断搬运、建造、战斗等正常工作。
            if (curJobDef == JobDefOf.Wait || curJobDef == JobDefOf.Wait_Wander)
            {
                return true;
            }

            string defName = curJobDef.defName;
            return defName == "Wait_MaintainPosture" || defName == "GotoWander";
        }

        public static string GetSpecializationLabel(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return Strings.ITab_SpecializationPetCat;
                case SexSlaveSpecializationType.PetDog:
                    return Strings.ITab_SpecializationPetDog;
                case SexSlaveSpecializationType.PetRabbit:
                    return Strings.ITab_SpecializationPetRabbit;
                default:
                    return Strings.ITab_SpecializationNone;
            }
        }

        public static string GetSelectLabel(SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.PetCat:
                    return Strings.ITab_SelectSpecializationPetCat;
                case SexSlaveSpecializationType.PetDog:
                    return Strings.ITab_SelectSpecializationPetDog;
                case SexSlaveSpecializationType.PetRabbit:
                    return Strings.ITab_SelectSpecializationPetRabbit;
                default:
                    return Strings.ITab_SelectSpecializationNone;
            }
        }

        public static void StoreExclusivePetTags(CompPersonalityStore store, Pawn pawn)
        {
            if (store == null || pawn?.health?.hediffSet == null) return;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                HediffDef baseDef = GetBaseHediffDef(type);
                HediffDef finalDef = GetFinalHediffDef(type);

                store.RemoveTag(baseDef);
                store.RemoveTag(finalDef);

                Hediff finalHediff = finalDef != null ? pawn.health.hediffSet.GetFirstHediffOfDef(finalDef) : null;
                if (finalHediff != null)
                {
                    store.SetTag(finalDef, finalHediff.Severity);
                    continue;
                }

                Hediff baseHediff = baseDef != null ? pawn.health.hediffSet.GetFirstHediffOfDef(baseDef) : null;
                if (baseHediff != null)
                {
                    store.SetTag(baseDef, baseHediff.Severity);
                }
            }
        }

        public static void ApplyExclusivePetTags(Pawn pawn, CompPersonalityStore data)
        {
            if (pawn?.health?.hediffSet == null || data?.hediffTags == null) return;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                HediffDef baseDef = GetBaseHediffDef(type);
                HediffDef finalDef = GetFinalHediffDef(type);

                bool hasFinal = finalDef != null && data.HasTag(finalDef);
                bool hasBase = baseDef != null && data.HasTag(baseDef);

                RemoveIfPresent(pawn, baseDef);
                RemoveIfPresent(pawn, finalDef);

                if (hasFinal)
                {
                    ApplyTag(pawn, finalDef, data.GetTagSeverity(finalDef));
                    continue;
                }

                if (hasBase)
                {
                    ApplyTag(pawn, baseDef, data.GetTagSeverity(baseDef));
                }
            }
        }

        public static void RemoveAllPetStates(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            foreach (SexSlaveSpecializationType type in PetTypes)
            {
                RemoveIfPresent(pawn, GetBaseHediffDef(type));
                RemoveIfPresent(pawn, GetFinalHediffDef(type));
            }
        }

        private static void ApplyTag(Pawn pawn, HediffDef def, float severity)
        {
            if (pawn == null || def == null) return;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def) ?? pawn.health.AddHediff(def);
            if (hediff != null)
            {
                hediff.Severity = severity > 0f ? severity : 1f;
            }
        }

        private static void RemoveIfPresent(Pawn pawn, HediffDef def)
        {
            if (pawn?.health?.hediffSet == null || def == null) return;
            Hediff hediff;
            while ((hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def)) != null)
                pawn.health.RemoveHediff(hediff);
        }
    }
}
