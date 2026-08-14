using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;

// EN: This comp stores a pawn's daily training / Binding Ritual state.
// EN: It remembers whether the pawn is acting as master or sex slave, which training mode is locked, who the trainer is, and whether cooldown or ritual state is active.
// CN: 这个组件负责保存 Pawn 的“日常调教 / 绑定仪式”状态。
// CN: 它会记录当前是主人还是性奴、锁定的调教姿势、指定 trainer，以及冷却和仪式中的状态。
namespace SexSlaveCraft
{
    // EN: PawnIdentity decides whether this pawn uses the training tab as a master or as a sex slave.
    // CN: PawnIdentity 用来决定这个 Pawn 在调教面板里以“主人”还是“性奴”身份运作。
    public enum PawnIdentity
    {
        Unset,
        Slave,
        Master
    }

    public enum SexSlaveSpecializationType
    {
        None,
        Bus,
        Cow,
        PetCat,
        PetDog,
        PetRabbit
    }

    public enum RabbitReproductionMode
    {
        Offspring,
        Clone
    }

    public enum TrainingMode { Disabled, Enabled }

    public enum TrainingActType
    {
        Auto, Vaginal, Anal, Oral, Boobjob, Handjob, Footjob, Fingering, MutualMasturbation, Fisting, Rimming, Sixtynine
    }

    public class CompSexSlaveTraining : ThingComp
    {
        private static Pawn debugSelectedMaster;

        // EN: Identity state: master pawns do not behave like trainable sex slaves.
        // CN: 身份状态：主人不会按可被调教的性奴方式运作。
        public PawnIdentity pawnIdentity = PawnIdentity.Unset;

        // EN: Training configuration chosen by the player from gizmos / ITab.
        // CN: 玩家通过 gizmo / ITab 选定的调教配置。
        public TrainingMode mode = TrainingMode.Disabled;
        public TrainingActType selectedMode = TrainingActType.Auto;
        public Pawn selectedTrainer;
        public bool allowOthersForTrainingOrSex = false;
        public bool scheduledTrainingEnabled = false;
        public int scheduledTrainingHour = 20;
        public int scheduledTrainingIntervalDays = 1;
        public int lastTrainingLocalDay = -999999;
        public SexSlaveSpecializationType specializationType = SexSlaveSpecializationType.None;
        public float specializationProgress = 0f;
        public float savedCowReservoirCharge = 0f;
        private Dictionary<string, float> perTypeProgress;
        public bool milkProductionEnabled = true;
        public RabbitReproductionMode rabbitReproductionMode = RabbitReproductionMode.Offspring;

        // EN: Fine training fields (reserved)
        // CN: 精细调教字段（预留）
        public bool fineTrainingEnabled = false;
        public Dictionary<string, FinePartData> finePartDataMap;

        // EN: Last training score for decay calculation
        // CN: 上次训练评分，用于衰减计算
        public float lastTrainingScore = 0f;

        // EN: Runtime state used by daily training and the Binding Ritual.
        // CN: 日常调教和绑定仪式运行时会用到的状态字段。
        public int lastTrainingTick = -999999;
        public int lastFailedTrainingValidationTick = -999999;
        public int lastPetAffectionTick = -999999;
        public int lastDogAnimalInteractionTick = -999999;
        public bool isBeingTrained = false;
        public bool isRitualTraining = false;

        // EN: Binding Ritual phase counter. Phase 0-5 are active steps, phase 6 means completion.
        // CN: 绑定仪式阶段计数。0-5 是有效阶段，推进到 6 就代表仪式完成。
        public int ritualPhase = 0;

        // EN: Daily training cooldown. Ritual training does not use this cooldown gate.
        // CN: 日常调教冷却时间。绑定仪式不会使用这道冷却门槛。
        public const int CooldownTicks = 22500;
        public const int FailedValidationRetryTicks = 300;
        public const int ScheduledTrainingWindowHours = 2;
        public const int PetAffectionCooldownTicks = 60000;

        public bool IsEnabled => mode == TrainingMode.Enabled;

        public bool IsBusSpecialized => specializationType == SexSlaveSpecializationType.Bus;

        public bool HasReachedBusThreshold => specializationProgress >= 0.20f;

        public bool HasCompletedBusSpecialization => specializationProgress >= 0.999f;

        public bool IsCowSpecialized => specializationType == SexSlaveSpecializationType.Cow;

        public bool HasReachedCowThreshold => specializationProgress >= 0.20f;

        public bool HasCompletedCowSpecialization => specializationProgress >= 0.999f;

        public bool IsPetCatSpecialized => specializationType == SexSlaveSpecializationType.PetCat;

        public bool IsPetDogSpecialized => specializationType == SexSlaveSpecializationType.PetDog;

        public bool IsPetRabbitSpecialized => specializationType == SexSlaveSpecializationType.PetRabbit;

        public bool IsPetSpecialized => PetSpecializationUtility.IsPetSpecialization(specializationType);

        public bool HasReachedPetThreshold => specializationProgress >= 0.20f && IsPetSpecialized;

        public bool HasCompletedPetSpecialization => specializationProgress >= 0.999f && IsPetSpecialized;

        public bool AllowsOthersForTrainingOrSex => allowOthersForTrainingOrSex;

        public bool IsWaitingAfterFailedValidation => (Find.TickManager.TicksGame - lastFailedTrainingValidationTick) < FailedValidationRetryTicks;

        public int CurrentLocalDay
        {
            get
            {
                if (!(parent is Thing thing)) return -999999;
                return GenLocalDate.Year(thing) * 60 + GenLocalDate.DayOfYear(thing);
            }
        }

        public bool IsScheduledTrainingDayDue =>
            !scheduledTrainingEnabled ||
            lastTrainingLocalDay < 0 ||
            CurrentLocalDay - lastTrainingLocalDay >= Mathf.Max(1, scheduledTrainingIntervalDays);

        public bool IsWithinScheduledTrainingWindow
        {
            get
            {
                if (!scheduledTrainingEnabled) return true;
                if (!(parent is Thing thing)) return false;

                int currentHour = GenLocalDate.HourOfDay(thing);
                int startHour = Mathf.Clamp(scheduledTrainingHour, 0, 23);
                int elapsedHours = (currentHour - startHour + 24) % 24;
                return elapsedHours < ScheduledTrainingWindowHours;
            }
        }

        public bool IsScheduledTrainingAvailableNow =>
            IsScheduledTrainingDayDue && IsWithinScheduledTrainingWindow;

        public int ScheduledTrainingEndHour =>
            (Mathf.Clamp(scheduledTrainingHour, 0, 23) + ScheduledTrainingWindowHours) % 24;

        public bool IsOnCooldown
        {
            get
            {
                // EN: Binding Ritual ignores the daily training cooldown because ritual progression is tracked separately.
                // CN: 绑定仪式会忽略日常调教冷却，因为仪式推进有自己独立的阶段状态。
                if (isRitualTraining) return false;
                return (Find.TickManager.TicksGame - lastTrainingTick) < CooldownTicks;
            }
        }

        public void Notify_TrainingCompleted()
        {
            isBeingTrained = false;
            
            // EN: Daily training starts cooldown. Binding Ritual only clears its ritual flag here.
            // CN: 日常调教会开始冷却；绑定仪式这里只需要清掉仪式标记。
            if (!isRitualTraining)
            {
                lastTrainingTick = Find.TickManager.TicksGame;
                lastTrainingLocalDay = CurrentLocalDay;
            }
            else
                isRitualTraining = false;
        }

        public void Notify_TrainingAborted() => isBeingTrained = false;

        public override void CompTickRare()
        {
            base.CompTickRare();

            if (!(parent is Pawn pawn)) return;
            PetSpecializationUtility.TryStartAutomaticPetAffectionJob(pawn, this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            // EN: Save identity first so loading logic knows whether this pawn should act as master or sex slave.
            // CN: 先保存身份数据，这样读档逻辑才能知道这个 Pawn 应该按主人还是性奴处理。
            Scribe_Values.Look(ref pawnIdentity, "pawnIdentity", PawnIdentity.Unset);

            Scribe_Values.Look(ref mode, "mode", TrainingMode.Disabled);
            Scribe_Values.Look(ref selectedMode, "selectedMode", TrainingActType.Auto);
            Scribe_Values.Look(ref allowOthersForTrainingOrSex, "allowOthersForTrainingOrSex", false);
            Scribe_Values.Look(ref scheduledTrainingEnabled, "scheduledTrainingEnabled", false);
            Scribe_Values.Look(ref scheduledTrainingHour, "scheduledTrainingHour", 20);
            Scribe_Values.Look(ref scheduledTrainingIntervalDays, "scheduledTrainingIntervalDays", 1);
            Scribe_Values.Look(ref lastTrainingLocalDay, "lastTrainingLocalDay", -999999);
            Scribe_Values.Look(ref specializationType, "specializationType", SexSlaveSpecializationType.None);
            Scribe_Values.Look(ref specializationProgress, "specializationProgress", 0f);
            Scribe_Values.Look(ref savedCowReservoirCharge, "savedCowReservoirCharge", 0f);
            Scribe_Collections.Look(ref perTypeProgress, "perTypeProgress", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref milkProductionEnabled, "milkProductionEnabled", true);
            Scribe_Values.Look(ref rabbitReproductionMode, "rabbitReproductionMode", RabbitReproductionMode.Offspring);
            Scribe_Values.Look(ref lastTrainingTick, "lastTrainingTick", -999999);
            Scribe_Values.Look(ref lastFailedTrainingValidationTick, "lastFailedTrainingValidationTick", -999999);
            Scribe_Values.Look(ref lastPetAffectionTick, "lastPetAffectionTick", -999999);
            Scribe_Values.Look(ref lastDogAnimalInteractionTick, "lastDogAnimalInteractionTick", -999999);
            Scribe_Values.Look(ref isBeingTrained, "isBeingTrained", false);
            Scribe_Values.Look(ref ritualPhase, "ritualPhase", 0);
            Scribe_Values.Look(ref isRitualTraining, "isRitualTraining", false);

            // EN: selectedTrainer is a Pawn reference and must be restored after the base scalar fields.
            // CN: selectedTrainer 是 Pawn 引用，所以要作为引用类型单独保存和恢复。
            Scribe_Values.Look(ref fineTrainingEnabled, "fineTrainingEnabled", false);
            Scribe_Collections.Look(ref finePartDataMap, "finePartDataMap", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref lastTrainingScore, "lastTrainingScore", 0f);

            Scribe_References.Look(ref selectedTrainer, "selectedTrainer");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                scheduledTrainingHour = Mathf.Clamp(scheduledTrainingHour, 0, 23);
                scheduledTrainingIntervalDays = Mathf.Clamp(scheduledTrainingIntervalDays, 1, 7);

                if (parent is Pawn loadedPawn)
                {
                    // EN: Identity/bond refactors must repair old saves at the data boundary.
                    // Keep the slave-side chain, master-side bridle, and exclusive trainer
                    // assignment consistent before any WorkGiver scan can observe them.
                    // CN: 身份/绑定重构必须在读档边界修复旧存档。WorkGiver 开始扫描前，
                    // 先统一性奴锁链、主人缰绳与独占调教师，避免三者不一致锁死训练。
                    SSCBondUtility.RepairReciprocalLink(loadedPawn);
                    Pawn boundMaster = SSCBondUtility.GetBoundMaster(loadedPawn);
                    if (boundMaster == null &&
                        selectedTrainer != null &&
                        (selectedTrainer.DestroyedOrNull() || selectedTrainer.Dead))
                    {
                        selectedTrainer = null;
                    }

                    if (boundMaster != null &&
                        !AllowsOthersForTrainingOrSex &&
                        !IsBusSpecialized &&
                        !BusSpecializationUtility.HasAnyBusState(loadedPawn))
                    {
                        selectedTrainer = boundMaster;
                    }

                    bool inActiveTrainingJob =
                        loadedPawn.CurJobDef == SSCDefOf.TrainingSexSlave ||
                        loadedPawn.CurJobDef == SSCDefOf.Training_Ritual ||
                        loadedPawn.CurJobDef == SSCDefOf.SSC_TrainingReceiver;

                    if (isBeingTrained && !inActiveTrainingJob)
                    {
                        isBeingTrained = false;
                    }
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && isRitualTraining)
            {
                // EN: Post-load repair: if the pawn is no longer inside the Binding Ritual job chain, clear stale ritual flags.
                // CN: 读档修复：如果这个 Pawn 已经不在绑定仪式的 Job 链里，就把残留的仪式状态清掉。
                if (parent is Pawn p)
                {
                    bool inRitualJob =
                        p.CurJobDef == SSCDefOf.Training_Ritual ||
                        p.CurJobDef == SSCDefOf.SSC_TrainingReceiver;

                    if (!inRitualJob)
                    {
                        isRitualTraining = false;
                        isBeingTrained = false;
                        ritualPhase = 0;
                    }
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            // EN: Step 1: only pawns show the training inspect string.
            // CN: 步骤 1：只有 Pawn 才显示调教状态文本。
            if (!(parent is Pawn p)) return null;

            if (pawnIdentity == PawnIdentity.Unset)
            {
                return Strings.Train_IdentityUnset;
            }

            // EN: Step 2: masters show the master identity line instead of the sex-slave cooldown/status text.
            // CN: 步骤 2：主人显示“主人身份”文本，而不是性奴那套冷却 / 状态说明。
            if (pawnIdentity == PawnIdentity.Master)
            {
                return Strings.Train_IdentityMaster;
            }

            // EN: Step 3: disabled sex slaves do not show active training status.
            // CN: 步骤 3：未启用调教的性奴，不显示进行中的调教状态。
            if (!IsEnabled) return null;

            // EN: Step 4: show either the daily training cooldown or the waiting-for-training state.
            // CN: 步骤 4：显示“日常调教冷却中”或“等待训练”状态。
            string status = IsOnCooldown
                ? Strings.Train_Cooldown((CooldownTicks - (Find.TickManager.TicksGame - lastTrainingTick)).ToStringTicksToPeriod())
                : Strings.Train_Waiting;

            if (scheduledTrainingEnabled)
            {
                status += " / " + "SSC_Schedule_Inspect".Translate(
                    scheduledTrainingHour.ToString("00"),
                    ScheduledTrainingEndHour.ToString("00"),
                    scheduledTrainingIntervalDays);
            }

            if (IsWaitingAfterFailedValidation)
            {
                status += " / 验证失败冷却中";
            }

            return Strings.Train_Status(status);
        }

        public void SetSpecialization(SexSlaveSpecializationType type)
        {
            SexSlaveSpecializationType previousType = specializationType;
            bool changedType = previousType != type;

            if (changedType)
            {
                // EN: Progress is tracked per specialization type, so switching never destroys invested progress.
                // CN: 特化进度按类型独立保存，切换特化不会清空已投入的进度。
                if (previousType != SexSlaveSpecializationType.None)
                {
                    SetSavedProgress(previousType, specializationProgress);
                }

                specializationProgress = type == SexSlaveSpecializationType.None ? 0f : GetSavedProgress(type);

                if (parent is Pawn pawn)
                {
                    RemoveInactiveSpecializationStates(pawn, this, type);
                }
            }

            specializationType = type;
            if (type == SexSlaveSpecializationType.None)
            {
                allowOthersForTrainingOrSex = false;
                specializationProgress = 0f;
                rabbitReproductionMode = RabbitReproductionMode.Offspring;
                return;
            }

            if (type == SexSlaveSpecializationType.Bus)
            {
                allowOthersForTrainingOrSex = true;
            }
            else
            {
                allowOthersForTrainingOrSex = false;
            }

            if (type != SexSlaveSpecializationType.PetRabbit)
            {
                rabbitReproductionMode = RabbitReproductionMode.Offspring;
            }
        }

        private float GetSavedProgress(SexSlaveSpecializationType type)
        {
            if (perTypeProgress == null) return 0f;
            return perTypeProgress.TryGetValue(type.ToString(), out float value) ? value : 0f;
        }

        private void SetSavedProgress(SexSlaveSpecializationType type, float value)
        {
            perTypeProgress = perTypeProgress ?? new Dictionary<string, float>();
            perTypeProgress[type.ToString()] = value;
        }

        private static void RemoveInactiveSpecializationStates(Pawn pawn, CompSexSlaveTraining comp, SexSlaveSpecializationType typeToKeep)
        {
            if (pawn?.health?.hediffSet == null) return;

            // EN: Only in-progress (base) hediffs are removed when switching. Finalized specializations are permanent.
            // CN: 切换时只移除未完成的“基础”hediff；终极化状态是永久的，绝不被删除。
            if (typeToKeep != SexSlaveSpecializationType.Bus)
            {
                RemoveSpecializationHediff(pawn, SSCDefOf.SSC_Hediff_Bus);
            }

            if (typeToKeep != SexSlaveSpecializationType.Cow)
            {
                if (comp != null)
                {
                    Hediff cowHediff = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow);
                    if (cowHediff != null)
                    {
                        comp.savedCowReservoirCharge = cowHediff.TryGetComp<HediffComp_CowMilkReservoir>()?.CurrentCharge ?? comp.savedCowReservoirCharge;
                    }
                }

                RemoveSpecializationHediff(pawn, SSCDefOf.SSC_Hediff_Cow);
            }

            RemovePetStateIfNotKept(pawn, typeToKeep, SexSlaveSpecializationType.PetCat);
            RemovePetStateIfNotKept(pawn, typeToKeep, SexSlaveSpecializationType.PetDog);
            RemovePetStateIfNotKept(pawn, typeToKeep, SexSlaveSpecializationType.PetRabbit);
        }

        private static void RemovePetStateIfNotKept(Pawn pawn, SexSlaveSpecializationType typeToKeep, SexSlaveSpecializationType petType)
        {
            if (typeToKeep == petType) return;

            RemoveSpecializationHediff(pawn, PetSpecializationUtility.GetBaseHediffDef(petType));
        }

        private static void RemoveSpecializationHediff(Pawn pawn, HediffDef def)
        {
            if (pawn?.health?.hediffSet == null || def == null) return;

            Hediff hediff;
            while ((hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def)) != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        public float AddSpecializationProgress(float amount)
        {
            if (amount <= 0f || specializationType == SexSlaveSpecializationType.None) return specializationProgress;
            specializationProgress = Mathf.Clamp01(specializationProgress + amount);
            return specializationProgress;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!(parent is Pawn pawn))
            {
                yield break;
            }

            if (!Prefs.DevMode || SSCMod.settings?.enableDebugGizmos != true || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Mark Master",
                defaultDesc = "Mark this pawn as the debug master for slave-chain binding tests.",
                action = delegate
                {
                    debugSelectedMaster = pawn;
                    Messages.Message($"[SSC DEV] Debug master -> {pawn.LabelShort}", pawn, MessageTypeDefOf.NeutralEvent, false);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Bind To Master",
                defaultDesc = "Bind this pawn to the marked master, add bridle/chain, and raise corruption to 10% for chain-rule testing.",
                action = delegate
                {
                    if (debugSelectedMaster == null || debugSelectedMaster.DestroyedOrNull())
                    {
                        Messages.Message("[SSC DEV] No debug master selected.", pawn, MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    SSCBondUtility.Bind(debugSelectedMaster, pawn, true);

                    Need_Corruption corruptionNeed = pawn.needs?.TryGetNeed<Need_Corruption>();
                    if (corruptionNeed != null && corruptionNeed.CurLevel < 0.10f)
                    {
                        corruptionNeed.SetCorruption(0.10f);
                    }

                    Messages.Message($"[SSC DEV] {pawn.LabelShort} bound to {debugSelectedMaster.LabelShort} (10% corruption floor)", pawn, MessageTypeDefOf.PositiveEvent, false);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Corruption 10%",
                defaultDesc = "Raise corruption to at least 10% so chain / bridle and shared-bed rules can be tested faster.",
                action = delegate
                {
                    Need_Corruption corruptionNeed = pawn.needs?.TryGetNeed<Need_Corruption>();
                    if (corruptionNeed != null)
                    {
                        corruptionNeed.SetCorruption(Mathf.Max(corruptionNeed.CurLevel, 0.10f));
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Corruption +10%",
                defaultDesc = "Increase corruption by 10% for faster training / gel testing.",
                action = delegate
                {
                    Need_Corruption corruptionNeed = pawn.needs?.TryGetNeed<Need_Corruption>();
                    if (corruptionNeed != null)
                    {
                        corruptionNeed.SetCorruption(corruptionNeed.CurLevel + 0.10f);
                        Messages.Message($"[SSC DEV] {pawn.LabelShort} corruption -> {corruptionNeed.CurLevel:P0}", pawn, MessageTypeDefOf.NeutralEvent, false);
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Corruption 100%",
                defaultDesc = "Set corruption to 100% for ritual conversion and full-gel testing.",
                action = delegate
                {
                    Need_Corruption corruptionNeed = pawn.needs?.TryGetNeed<Need_Corruption>();
                    if (corruptionNeed != null)
                    {
                        corruptionNeed.SetCorruption(1f);
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Partial Adapt +10%",
                defaultDesc = "Increase adaptation by 10% on every partial gelatinization hediff on this pawn.",
                action = delegate
                {
                    AdjustPartialGelAdaptation(pawn, 0.10f);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: FullGel Adapt +10%",
                defaultDesc = "Increase adaptation by 10% on SSC_FullGelatinizationTemporary.",
                action = delegate
                {
                    AdjustFullGelAdaptation(pawn, 0.10f);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Add PE 100%",
                defaultDesc = "Add the personality-excretion hediff at 100% so PE jobs can be tested immediately.",
                action = delegate
                {
                    if (RabbitCloneUtility.IsRabbitClone(pawn))
                    {
                        Messages.Message("[SSC DEV] Rabbit clones cannot receive personality excretion.", pawn, MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Hediff pe = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreting);
                    if (pe == null)
                    {
                        pe = pawn.health.AddHediff(SSCDefOf.SSC_PersonalityExcreting);
                    }

                    if (pe != null)
                    {
                        pe.Severity = 1f;
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Add Hollow",
                defaultDesc = "Add the hollow-pawn state directly for personality insertion tests.",
                action = delegate
                {
                    if (!pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done))
                    {
                        pawn.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
                    }
                }
            };

            if (IsPetRabbitSpecialized || PetSpecializationUtility.HasAnyPetState(pawn, SexSlaveSpecializationType.PetRabbit))
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Create Rabbit Clone",
                    defaultDesc = "Create one pet-rabbit clone near this pawn. Rabbit clones mature in 3 days and share rest/mood with the source.",
                    action = delegate
                    {
                        RabbitCloneUtility.TryCreateRabbitClone(pawn, out _);
                    }
                };
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Add Partial Gel Arm",
                defaultDesc = "Add Hediff_GelatinizationTemporary to the first valid manipulation limb.",
                action = delegate
                {
                    AddPartialGelatinizationDebug(pawn, manipulationLimb: true);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Add Partial Gel Leg",
                defaultDesc = "Add Hediff_GelatinizationTemporary to the first valid moving limb.",
                action = delegate
                {
                    AddPartialGelatinizationDebug(pawn, manipulationLimb: false);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Add Full Gel",
                defaultDesc = "Add SSC_FullGelatinizationTemporary for full-gel testing.",
                action = delegate
                {
                    if (!pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_FullGelatinizationTemporary))
                    {
                        pawn.health.AddHediff(SSCDefOf.SSC_FullGelatinizationTemporary);
                    }
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Clear Train Flags",
                defaultDesc = "Clear active training / ritual runtime flags without touching saved trainer or act.",
                action = delegate
                {
                    isBeingTrained = false;
                    isRitualTraining = false;
                    ritualPhase = 0;
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Status Log",
                defaultDesc = "Print current training debug state for this pawn.",
                action = delegate
                {
                    SSCLog.Important($"[SSC DEV] Training status: pawn={pawn.LabelShort}, identity={pawnIdentity}, mode={mode}, act={selectedMode}, trainer={selectedTrainer?.LabelShort ?? "none"}, cooldown={IsOnCooldown}, lastTick={lastTrainingTick}, beingTrained={isBeingTrained}, ritual={isRitualTraining}, ritualPhase={ritualPhase}, curJob={pawn.CurJobDef?.defName ?? "null"}");
                }
            };
        }

        private static void AddPartialGelatinizationDebug(Pawn pawn, bool manipulationLimb)
        {
            if (pawn?.health?.hediffSet == null) return;

            BodyPartRecord part = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(x =>
                manipulationLimb
                    ? x.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)
                    : x.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore));

            if (part == null)
            {
                Messages.Message($"[SSC DEV] No valid {(manipulationLimb ? "arm" : "leg")} part found for {pawn.LabelShort}", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            HediffDef gelDef = DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_GelatinizationTemporary");
            if (gelDef == null)
            {
                Messages.Message("[SSC DEV] Hediff_GelatinizationTemporary not found.", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (pawn.health.hediffSet.hediffs.Any(h => h.def == gelDef && h.Part == part))
            {
                Messages.Message($"[SSC DEV] {pawn.LabelShort} already has partial gel on {part.LabelCap}", pawn, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(gelDef, pawn, part);
            pawn.health.AddHediff(hediff, part, null);
            Messages.Message($"[SSC DEV] Added partial gel to {pawn.LabelShort}: {part.LabelCap}", pawn, MessageTypeDefOf.PositiveEvent, false);
        }

        private static void AdjustPartialGelAdaptation(Pawn pawn, float delta)
        {
            if (pawn?.health?.hediffSet == null) return;

            List<Hediff_GelatinizationTemporary> partialGels = pawn.health.hediffSet.hediffs.OfType<Hediff_GelatinizationTemporary>().ToList();
            if (partialGels.Count == 0)
            {
                Messages.Message($"[SSC DEV] {pawn.LabelShort} has no partial gelatinization hediff.", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            foreach (Hediff_GelatinizationTemporary gel in partialGels)
            {
                gel.adaptation = Mathf.Clamp01(gel.adaptation + delta);
            }

            Messages.Message($"[SSC DEV] {pawn.LabelShort} partial adaptation +{delta:P0} ({partialGels.Count} hediffs)", pawn, MessageTypeDefOf.NeutralEvent, false);
        }

        private static void AdjustFullGelAdaptation(Pawn pawn, float delta)
        {
            if (pawn?.health?.hediffSet == null) return;

            Hediff_FullGelatinizationTemporary fullGel = pawn.health.hediffSet.hediffs.OfType<Hediff_FullGelatinizationTemporary>().FirstOrDefault();
            if (fullGel == null)
            {
                Messages.Message($"[SSC DEV] {pawn.LabelShort} has no full gelatinization hediff.", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            fullGel.adaptation = Mathf.Clamp01(fullGel.adaptation + delta);
            Messages.Message($"[SSC DEV] {pawn.LabelShort} full-gel adaptation -> {fullGel.adaptation:P0}", pawn, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    // EN: Standard properties wrapper so XML comps can instantiate CompSexSlaveTraining.
    // CN: 标准 CompProperties 包装类，供 XML 里的 compClass 正常实例化 CompSexSlaveTraining。
    public class CompProperties_SexSlaveTraining : CompProperties
    {
        public CompProperties_SexSlaveTraining()
        {
            this.compClass = typeof(CompSexSlaveTraining);
        }
    }
}
