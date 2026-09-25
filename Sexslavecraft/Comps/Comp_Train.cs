using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI.Group;
using RimWorld;
using UnityEngine;

// 这个组件负责保存 Pawn 的“日常调教 / 绑定仪式”状态。
// 它会记录当前是主人还是性奴、锁定的调教姿势、指定 trainer，以及冷却和仪式中的状态。
namespace SexSlaveCraft
{
    // PawnIdentity 用来决定这个 Pawn 在调教面板里以“主人”还是“性奴”身份运作。
    public enum PawnIdentity
    {
        Unset,
        Slave,
        Master
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

    public partial class CompSexSlaveTraining : ThingComp
    {
        private static Pawn debugSelectedMaster;

        // 身份状态：主人不会按可被调教的性奴方式运作。
        public PawnIdentity pawnIdentity = PawnIdentity.Unset;

        // 玩家通过 gizmo / ITab 选定的调教配置。
        public TrainingMode mode = TrainingMode.Disabled;
        public TrainingActType selectedMode = TrainingActType.Auto;
        public Pawn selectedTrainer;
        public SSCSharedSleepRecord sharedSleep;
        public bool scheduledTrainingEnabled = false;
        public int scheduledTrainingHour = 20;
        public int scheduledTrainingIntervalDays = 1;
        public int lastTrainingLocalDay = -999999;
        public float savedCowReservoirCharge = 0f;
        // 训导官生命周期的事务/重入状态只存在于运行期；失格后禁止自动认领
        // 则须随存档保存，否则读档会从其他残留终极状态改写刚退出的方向。
        public int trainerMutationDepth;
        public bool trainerMaintenanceInProgress;
        public bool trainerInvalidExitBlocksAdoption;
        // 玩家明确选择“未选择”后仍保留终极 Hediff，但低频对账不能把它重新
        // 认领为当前方向。该选择要随存档保存；旧档缺字段时仍可按原规则恢复。
        public bool specializationExplicitlyUnset;
        public bool milkProductionEnabled = true;
        public RabbitReproductionMode rabbitReproductionMode = RabbitReproductionMode.Offspring;

        // 精细调教字段（预留）
        public bool fineTrainingEnabled = false;
        public Dictionary<string, FinePartData> finePartDataMap;

        // 上次训练评分，用于衰减计算
        public float lastTrainingScore = 0f;

        // 日常调教和绑定仪式运行时会用到的状态字段。
        public int lastTrainingTick = -999999;
        public int lastFailedTrainingValidationTick = -999999;
        public int lastPetAffectionTick = -999999;
        public int lastDogAnimalInteractionTick = -999999;
        public bool isBeingTrained = false;
        public bool isRitualTraining = false;

        // 绑定仪式阶段计数。0-5 是有效阶段，推进到 6 就代表仪式完成。
        public int ritualPhase = 0;

        // 同一场仪式的各阶段共享 Lord；新场次必须重新计数。引用也会随存档保存，
        // 避免读档把有效仪式当成残留，或把上一次仪式的阶段带到下一场。
        public Lord bindingRitualLord;
        public bool bindingRitualOutcomeClaimed;

        // 日常调教冷却时间。绑定仪式不会使用这道冷却门槛。
        public const int CooldownTicks = 22500;
        public const int FailedValidationRetryTicks = 300;
        public const int ScheduledTrainingWindowHours = 2;
        public const int PetAffectionCooldownTicks = 60000;

        /// <summary>读取玩家是否启用了日常训练。</summary>
        public bool IsEnabled => mode == TrainingMode.Enabled;

        /// <summary>判断当前选择的特化方向是否为巴士。</summary>
        public bool IsBusSpecialized => specializationType == SexSlaveSpecializationType.Bus;

        /// <summary>检查进度是否达到巴士基础阈值；调用方另行确认当前特化方向。</summary>
        public bool HasReachedBusThreshold => specializationProgress >= 0.20f;

        /// <summary>检查进度是否达到巴士完成阈值；调用方另行确认当前特化方向。</summary>
        public bool HasCompletedBusSpecialization => specializationProgress >= SpecializationCompletionProgress;

        /// <summary>判断当前选择的特化方向是否为奶牛。</summary>
        public bool IsCowSpecialized => specializationType == SexSlaveSpecializationType.Cow;

        /// <summary>检查进度是否达到奶牛基础阈值；调用方另行确认当前特化方向。</summary>
        public bool HasReachedCowThreshold => specializationProgress >= 0.20f;

        /// <summary>检查进度是否达到奶牛完成阈值；调用方另行确认当前特化方向。</summary>
        public bool HasCompletedCowSpecialization => specializationProgress >= SpecializationCompletionProgress;

        /// <summary>判断当前选择的特化方向是否为宠物猫。</summary>
        public bool IsPetCatSpecialized => specializationType == SexSlaveSpecializationType.PetCat;

        /// <summary>判断当前选择的特化方向是否为宠物狗。</summary>
        public bool IsPetDogSpecialized => specializationType == SexSlaveSpecializationType.PetDog;

        /// <summary>判断当前选择的特化方向是否为宠物兔。</summary>
        public bool IsPetRabbitSpecialized => specializationType == SexSlaveSpecializationType.PetRabbit;

        /// <summary>判断当前特化是否属于任一种宠物方向。</summary>
        public bool IsPetSpecialized => PetSpecializationUtility.IsPetSpecialization(specializationType);

        /// <summary>同时检查宠物特化身份及其基础进度阈值。</summary>
        public bool HasReachedPetThreshold => specializationProgress >= 0.20f && IsPetSpecialized;

        /// <summary>同时检查宠物特化身份及其完成进度阈值。</summary>
        public bool HasCompletedPetSpecialization => specializationProgress >= SpecializationCompletionProgress && IsPetSpecialized;

        /// <summary>判断上次训练资格校验失败后的重试间隔是否尚未结束。</summary>
        public bool IsWaitingAfterFailedValidation => (Find.TickManager.TicksGame - lastFailedTrainingValidationTick) < FailedValidationRetryTicks;

        /// <summary>将所在地年份及年内天数换算为连续日号；缺少有效父对象时返回哨兵值。</summary>
        public int CurrentLocalDay
        {
            get
            {
                if (!(parent is Thing thing)) return -999999;
                return GenLocalDate.Year(thing) * 60 + GenLocalDate.DayOfYear(thing);
            }
        }

        /// <summary>判断预约训练的间隔天数是否已满足；未启用预约或尚无训练记录时视为满足。</summary>
        public bool IsScheduledTrainingDayDue =>
            !scheduledTrainingEnabled ||
            lastTrainingLocalDay < 0 ||
            CurrentLocalDay - lastTrainingLocalDay >= Mathf.Max(1, scheduledTrainingIntervalDays);

        /// <summary>按当地小时检查两小时预约窗口，支持跨午夜；未启用预约时不限制时段。</summary>
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

        /// <summary>同时检查预约训练的日期间隔和当前时段是否满足。</summary>
        public bool IsScheduledTrainingAvailableNow =>
            IsScheduledTrainingDayDue && IsWithinScheduledTrainingWindow;

        /// <summary>计算预约窗口的结束小时，将跨午夜的结果折回 0 至 23 时。</summary>
        public int ScheduledTrainingEndHour =>
            (Mathf.Clamp(scheduledTrainingHour, 0, 23) + ScheduledTrainingWindowHours) % 24;

        /// <summary>判断日常训练冷却是否仍在持续；绑定仪式使用独立阶段状态，不受此冷却约束。</summary>
        public bool IsOnCooldown
        {
            get
            {
                // 绑定仪式会忽略日常调教冷却，因为仪式推进有自己独立的阶段状态。
                if (isRitualTraining) return false;
                return (Find.TickManager.TicksGame - lastTrainingTick) < CooldownTicks;
            }
        }

        /// <summary>结束日常训练并记录冷却与训练日期；仪式占用存在时交由仪式状态管理器处理。</summary>
        public void Notify_TrainingCompleted()
        {
            // 这里只处理日常训练；仪式的阶段推进和解锁统一交给仪式状态管理器。
            if (isRitualTraining || bindingRitualLord != null) return;
            isBeingTrained = false;
            lastTrainingTick = Find.TickManager.TicksGame;
            lastTrainingLocalDay = CurrentLocalDay;
        }

        /// <summary>中止日常训练时清除训练占用标志。</summary>
        public void Notify_TrainingAborted() => isBeingTrained = false;

        /// <summary>在稀疏更新中恢复失效仪式状态、核对特化状态，并尝试安排宠物互动。</summary>
        public override void CompTickRare()
        {
            base.CompTickRare();

            if (!(parent is Pawn pawn)) return;
            BindingRitualStateUtility.RecoverPawnState(pawn);
            ReconcileSpecialization(pawn);
            PetSpecializationUtility.TryStartAutomaticPetAffectionJob(pawn, this);
        }

        // 对账特化状态：凝胶注入/旧存档可能只有 hediff 而没有 comp 类型，
        // 这里自动认领并双向同步，让所有经验来源的门控条件恢复生效。
        /// <summary>根据已有健康状态认领特化方向，并同步当前方向的有效进度与基础状态。</summary>
        public static void ReconcileSpecialization(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            // 终极训导官记录可在其他当前方向上存在，必须在通用认领的
            // “无方向”提前返回之前维护；无关角色在该入口中快速退出。
            TrainerSpecializationLifecycle.Maintain(pawn);
            if (comp.pawnIdentity == PawnIdentity.Master) return;

            if (comp.specializationType == SexSlaveSpecializationType.None)
            {
                // 旧档缺少组件方向时仍可从健康状态恢复；玩家明确留空，或
                // 训导官失格退出后必须保持“无”，不能从终极标记重新选回。
                if (!comp.CanAdoptSpecializationFromHealth) return;
                SexSlaveSpecializationType adopted = DetectAdoptableType(pawn);
                if (adopted == SexSlaveSpecializationType.None) return;

                comp.SetSpecialization(adopted);
            }

            switch (comp.specializationType)
            {
                case SexSlaveSpecializationType.Bus:
                    BusSpecializationUtility.EnsureBusHediffFromSpecialization(pawn);
                    break;
                case SexSlaveSpecializationType.Cow:
                    BusSpecializationUtility.EnsureCowHediffFromSpecialization(pawn);
                    break;
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn);
                    break;
            }

            // 保持"进行中"特化的互斥性；终极化 hediff 永不删除。
            RemoveInactiveSpecializationStates(pawn, comp, comp.specializationType);
        }

        /// <summary>按既有优先级从健康状态中找出可恢复的特化方向。</summary>
        private static SexSlaveSpecializationType DetectAdoptableType(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return SexSlaveSpecializationType.None;

            if (BusSpecializationUtility.HasFinalBusState(pawn)) return SexSlaveSpecializationType.Bus;
            if (BusSpecializationUtility.HasFinalCowState(pawn)) return SexSlaveSpecializationType.Cow;
            if (PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetDog)) return SexSlaveSpecializationType.PetDog;
            if (PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetCat)) return SexSlaveSpecializationType.PetCat;
            if (PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit)) return SexSlaveSpecializationType.PetRabbit;

            if (SSCDefOf.SSC_Hediff_Bus != null && pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Bus)) return SexSlaveSpecializationType.Bus;
            if (SSCDefOf.SSC_Hediff_Cow != null && pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Cow)) return SexSlaveSpecializationType.Cow;

            HediffDef dogBase = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetDog);
            if (dogBase != null && pawn.health.hediffSet.HasHediff(dogBase)) return SexSlaveSpecializationType.PetDog;

            HediffDef catBase = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetCat);
            if (catBase != null && pawn.health.hediffSet.HasHediff(catBase)) return SexSlaveSpecializationType.PetCat;

            HediffDef rabbitBase = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetRabbit);
            if (rabbitBase != null && pawn.health.hediffSet.HasHediff(rabbitBase)) return SexSlaveSpecializationType.PetRabbit;

            return SexSlaveSpecializationType.None;
        }

        /// <summary>读写训练配置、新限制配置、成长进度、仪式归属及共同睡眠记录，兼容旧字段；仪式有效性核对延后到运行时。</summary>
        /// <remarks>sharedSleep 以深度序列化保存，避免读档后丢失尚未结算的同床经历；旧存档缺少该字段时保持空记录。</remarks>
        public override void PostExposeData()
        {
            base.PostExposeData();

            // 先保存身份数据，这样读档逻辑才能知道这个 Pawn 应该按主人还是性奴处理。
            Scribe_Values.Look(ref pawnIdentity, "pawnIdentity", PawnIdentity.Unset);
            ExposeTrainerIdentity();

            Scribe_Values.Look(ref mode, "mode", TrainingMode.Disabled);
            Scribe_Values.Look(ref selectedMode, "selectedMode", TrainingActType.Auto);
            Scribe_Values.Look(ref scheduledTrainingEnabled, "scheduledTrainingEnabled", false);
            Scribe_Values.Look(ref scheduledTrainingHour, "scheduledTrainingHour", 20);
            Scribe_Values.Look(ref scheduledTrainingIntervalDays, "scheduledTrainingIntervalDays", 1);
            Scribe_Values.Look(ref lastTrainingLocalDay, "lastTrainingLocalDay", -999999);
            Scribe_Values.Look(ref specializationType, "specializationType", SexSlaveSpecializationType.None);
            Scribe_Values.Look(ref specializationProgress, "specializationProgress", 0f);
            Scribe_Values.Look(ref savedCowReservoirCharge, "savedCowReservoirCharge", 0f);
            Scribe_Values.Look(ref trainerInvalidExitBlocksAdoption, "trainerInvalidExitBlocksAdoption", false);
            Scribe_Values.Look(ref specializationExplicitlyUnset, "specializationExplicitlyUnset", false);
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
            Scribe_References.Look(ref bindingRitualLord, "bindingRitualLord");
            Scribe_Values.Look(ref bindingRitualOutcomeClaimed, "bindingRitualOutcomeClaimed", false);

            // selectedTrainer 是 Pawn 引用，所以要作为引用类型单独保存和恢复。
            Scribe_Values.Look(ref fineTrainingEnabled, "fineTrainingEnabled", false);
            Scribe_Collections.Look(ref finePartDataMap, "finePartDataMap", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref lastTrainingScore, "lastTrainingScore", 0f);

            Scribe_References.Look(ref selectedTrainer, "selectedTrainer");
            Scribe_Deep.Look(ref sharedSleep, "sharedSleep");

            // 必须在旧输入全部读入后、关系和特化修复前捕获；实际迁移在 GameComponent 中执行。
            ExposeRestrictions();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                scheduledTrainingHour = Mathf.Clamp(scheduledTrainingHour, 0, 23);
                scheduledTrainingIntervalDays = Mathf.Clamp(scheduledTrainingIntervalDays, 1, 7);

                if (parent is Pawn loadedPawn)
                {
                    // 身份/绑定重构必须在读档边界修复旧存档。WorkGiver 开始扫描前，
                    // 先统一性奴锁链、主人缰绳与独占调教师，避免三者不一致锁死训练。
                    SSCBondUtility.RepairReciprocalLink(loadedPawn);
                    Pawn boundMaster = SSCBondUtility.GetBoundMaster(loadedPawn);
                    if (boundMaster == null &&
                        selectedTrainer != null &&
                        (selectedTrainer.DestroyedOrNull() || selectedTrainer.Dead))
                    {
                        selectedTrainer = null;
                    }

                    // 指派限制统一等 GameComponent 完成迁移后协调。
                    // 此处不能再用旧开放字段提前改回主人，否则会破坏迁移前的真实指定关系。

                    bool inActiveTrainingJob =
                        loadedPawn.CurJobDef == SSCDefOf.TrainingSexSlave ||
                        loadedPawn.CurJobDef == SSCDefOf.Training_Ritual ||
                        loadedPawn.CurJobDef == SSCDefOf.SSC_TrainingReceiver;

                    if (isBeingTrained && !isRitualTraining && bindingRitualLord == null && !inActiveTrainingJob)
                    {
                        isBeingTrained = false;
                    }
                }
            }

            // 仪式修复延后到 CompTickRare / 工作检查，此时 Lord 和角色引用已恢复。
            // 接收 Job 也用于日常调教，不能仅凭 JobDef 判断是否仍属于有效仪式。
        }

        /// <summary>按角色身份、训练开关和冷却时间生成检查面板状态文本。</summary>
        public override string CompInspectStringExtra()
        {
            // 步骤 1：只有 Pawn 才显示调教状态文本。
            if (!(parent is Pawn p)) return null;

            if (pawnIdentity == PawnIdentity.Unset)
            {
                return Strings.Train_IdentityUnset;
            }

            // 步骤 2：主人显示“主人身份”文本，而不是性奴那套冷却 / 状态说明。
            if (pawnIdentity == PawnIdentity.Master)
            {
                return Strings.Train_IdentityMaster;
            }

            // 步骤 3：未启用调教的性奴，不显示进行中的调教状态。
            if (!IsEnabled) return null;

            // 步骤 4：显示“日常调教冷却中”或“等待训练”状态。
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

        /// <summary>移除非当前方向的基础健康状态，保留终极状态与身体已有奶量。</summary>
        private static void RemoveInactiveSpecializationStates(Pawn pawn, CompSexSlaveTraining comp, SexSlaveSpecializationType typeToKeep)
        {
            if (pawn?.health?.hediffSet == null) return;

            // 切换时只移除未完成的“基础”hediff；终极化状态是永久的，绝不被删除。
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

            // 训导官普通 Hediff 只代表正在培养。方向退出时必须同时清理，
            // 否则下一轮对账会把失格角色从残留标记重新认领回来。
            if (typeToKeep != SexSlaveSpecializationType.TrainerOfficer)
                RemoveSpecializationHediff(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer);
        }

        /// <summary>移除未被保留的宠物方向基础状态。</summary>
        private static void RemovePetStateIfNotKept(Pawn pawn, SexSlaveSpecializationType typeToKeep, SexSlaveSpecializationType petType)
        {
            if (typeToKeep == petType) return;

            RemoveSpecializationHediff(pawn, PetSpecializationUtility.GetBaseHediffDef(petType));
        }

        /// <summary>安全移除指定定义的全部基础健康状态。</summary>
        private static void RemoveSpecializationHediff(Pawn pawn, HediffDef def)
        {
            if (pawn?.health?.hediffSet == null || def == null) return;

            Hediff hediff;
            while ((hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def)) != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        /// <summary>为当前方向累计正向训练进度，并把结果限制在完成范围内。</summary>
        public float AddSpecializationProgress(float amount)
        {
            if (amount <= 0f || specializationType == SexSlaveSpecializationType.None) return specializationProgress;
            specializationProgress = Mathf.Clamp01(specializationProgress + amount);
            return specializationProgress;
        }

        /// <summary>生成训练配置与开发调试按钮；开发清理操作也通过统一入口复位仪式临时状态。</summary>
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
                    BindingRitualStateUtility.ClearRitualState(this);
                    isBeingTrained = false;
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

        /// <summary>通过调试按钮为首个有效操作肢体或移动肢体添加局部凝胶化状态。</summary>
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

        /// <summary>通过调试按钮调整角色所有局部凝胶化状态的适应度。</summary>
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

        /// <summary>通过调试按钮调整角色完全凝胶化状态的适应度。</summary>
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

    // 标准 CompProperties 包装类，供 XML 里的 compClass 正常实例化 CompSexSlaveTraining。
    public class CompProperties_SexSlaveTraining : CompProperties
    {
        /// <summary>指定由此配置创建的训练组件类型。</summary>
        public CompProperties_SexSlaveTraining()
        {
            this.compClass = typeof(CompSexSlaveTraining);
        }
    }
}
