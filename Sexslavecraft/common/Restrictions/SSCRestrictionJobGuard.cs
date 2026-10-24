// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>管理本批任务的检查时机、可保存开始凭据和拒绝清理；所有许可均交给 SSCRestrictionPolicy。</summary>
    internal static class SSCRestrictionJobGuard
    {
        private sealed class SceneState
        {
            // 状态必须同时属于驱动、Job实例和loadID，避免复用后继承旧请求的开始凭据。
            public Job Job;
            public int JobId;
            public bool HasRecord = true;
            public bool HasTrainingRecord = true;
            public bool HasCompatibilityRecord = true;
            public bool Started;
            public bool Rejected;
            public Pawn Initiator;
            public Pawn Receiver;
            public SSCInteractionKind Kind;
            public bool LegacyProgress;
            public bool TrainerProgressClaimed;
            // 仅本次 ProcessSex 调用栈拥有；异常/跳过原方法后释放，不写入存档。
            public bool OutcomeInFlight;
            // 只由发起任务在首次真实 Start 建立；空值同时表示旧档或无可靠解析，不在收尾补造。
            public SSCOrdinarySexPartsSnapshot Parts;
            // 准备归属和事件交接各自封装；场景只决定何时可登记、通知及清理。
            public readonly SSCRestrictionJobPreparation Preparation = new SSCRestrictionJobPreparation();
            public readonly SSCRestrictionJobEvents Events = new SSCRestrictionJobEvents();
        }

        private static readonly ConditionalWeakTable<JobDriver_Sex, SceneState> States =
            new ConditionalWeakTable<JobDriver_Sex, SceneState>();

        /// <summary>按驱动和当前 Job 隔离状态；即使外部复用驱动，也不能继承上一个任务的开始许可。</summary>
        private static SceneState State(JobDriver_Sex driver)
        {
            SceneState state = States.GetOrCreateValue(driver);
            // LoadingVars 中 JobDriver 尚未接回 Pawn/Job；加载期间绑定引用但不能丢掉刚读入的凭据。
            if (Scribe.mode != LoadSaveMode.Inactive && Scribe.mode != LoadSaveMode.Saving)
            {
                state.Job = driver.job;
                state.JobId = driver.job?.loadID ?? 0;
                return state;
            }
            if (state.Job != driver.job || state.JobId != (driver.job?.loadID ?? 0))
            {
                States.Remove(driver);
                state = new SceneState { Job = driver.job, JobId = driver.job?.loadID ?? 0 };
                States.Add(driver, state);
            }
            state.Events.ClaimPending(driver, state.Preparation);
            return state;
        }

        /// <summary>仅为同一任务、用途及有向参与者保留已开始状态；新加入者不能借用其他发起者的凭据。</summary>
        private static bool MatchesStarted(JobDriver_Sex driver, SSCRestrictionRequest request)
        {
            SceneState state = State(driver);
            return state.Started && state.Initiator == request.Initiator && state.Receiver == request.Receiver &&
                state.Kind == SceneKind(driver, request) && request.DirectionKnown;
        }

        /// <summary>首次绑定准备仍属于原日常场景或仪式阶段；途中绑定变化不把已开始场景误当成新用途。</summary>
        private static SSCInteractionKind SceneKind(JobDriver_Sex driver, SSCRestrictionRequest request)
        {
            if (driver is JobDriver_Training) return SSCInteractionKind.DailyTraining;
            if (driver is JobDriver_RitualTraining) return SSCInteractionKind.RitualTraining;
            return request.Kind;
        }

        /// <summary>接收方只承接仍指向自己的发起任务凭据，不以参与者名单或已分配 SexProps 判定开始。</summary>
        private static bool HasStarted(JobDriver_Sex driver, SSCRestrictionRequest request)
        {
            if (driver is JobDriver_SexBaseReciever receiver)
            {
                SceneState state = State(receiver);
                if (state.Started && state.Initiator == receiver.Partner && state.Receiver == receiver.pawn) return true;
                JobDriver_SexBaseInitiator initiator = SSCRestrictionJobContext.FindInitiator(receiver);
                return initiator != null && MatchesStarted(initiator, request);
            }
            return MatchesStarted(driver, request);
        }

        /// <summary>由专用单人驱动核对统一守卫确实记录了本次 Start，而非仅有旧 SexProps。</summary>
        internal static bool HasStartedScene(JobDriver_Sex driver, SSCInteractionKind kind)
        {
            return SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request) &&
                request.Kind == kind && (!SSCRestrictionTrainingUtility.IsTraining(request) ||
                    SSCIdentityUtility.IsSexSlave(request.Receiver)) && HasStarted(driver, request);
        }

        /// <summary>预约阶段只返回许可，不结束正在被任务跟踪器安装的驱动，也不创建配置或玩家命令豁免。</summary>
        public static bool TryReserve(JobDriver_Sex driver, out bool allowed, bool ordered = false)
        {
            allowed = true;
            if (!SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request)) return false;
            if (HasStarted(driver, request) && (!SSCRestrictionTrainingUtility.IsTraining(request) ||
                SSCIdentityUtility.IsSexSlave(request.Receiver))) return true;
            allowed = Evaluate(driver, request, out SSCRestrictionDecision decision, out string reason, ordered);
            LogDecision(driver, request, decision, "Reserve", allowed, reason);
            State(driver).Rejected = !allowed;
            return true;
        }

        /// <summary>步骤与 Start 前重查；调教身份持续有效才保留开始凭据，普通场景沿用原收尾，迟到回调不碰新任务。</summary>
        public static bool TryCheck(JobDriver_Sex driver, string phase, out bool allowed)
        {
            allowed = true;
            if (!SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request)) return false;
            if (driver.pawn?.jobs?.curDriver != driver) { allowed = false; return true; }
            if (HasStarted(driver, request) && (!SSCRestrictionTrainingUtility.IsTraining(request) ||
                SSCIdentityUtility.IsSexSlave(request.Receiver))) return true;
            allowed = Evaluate(driver, request, out SSCRestrictionDecision decision, out string reason);
            LogDecision(driver, request, decision, phase, allowed, reason);
            if (!allowed && driver is JobDriver_SexBaseReciever persistent && OnaholeCompatibilityUtility.IsBeOnaholeDriver(persistent))
            {
                // 家具常驻任务不归本次互动所有。拒绝真正的发起任务后仍保留家具占用，不将拒绝解释为解绑。
                JobDriver_SexBaseInitiator actor = SSCRestrictionJobContext.FindInitiator(persistent);
                if (actor != null) Reject(actor, request, reason);
                allowed = true;
            }
            else if (!allowed) Reject(driver, request, reason);
            return true;
        }

        /// <summary>接收端沿用发起任务的调度方式，避免 playerForced 的接收任务替自动日常工作解除唯一指派。</summary>
        private static bool Evaluate(JobDriver_Sex driver, SSCRestrictionRequest request,
            out SSCRestrictionDecision decision, out string reason, bool ordered = false)
        {
            JobDriver_Sex actor = driver is JobDriver_SexBaseReciever receiver
                ? SSCRestrictionJobContext.FindInitiator(receiver) : driver;
            bool automaticDaily = actor is JobDriver_Training && !ordered && actor.job?.playerForced != true;
            SSCTrainingAdmission admission = SSCRestrictionTrainingUtility.Evaluate(request, automaticDaily);
            decision = admission.Permission;
            reason = admission.Reason;
            return admission.Allowed;
        }

        /// <summary>只在原 Start 确实执行且驱动仍为当前任务时记下凭据；准备阶段不会得到开始标记。</summary>
        public static bool TryMarkStarted(JobDriver_SexBaseInitiator driver, bool ranOriginal)
        {
            if (!SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request)) return false;
            if (!ranOriginal || driver.pawn?.jobs?.curDriver != driver) return true;
            SceneState state = State(driver);
            if (state.Rejected) return true;
            bool firstStart = !state.Started;
            state.Started = true;
            state.Initiator = request.Initiator;
            state.Receiver = request.Receiver;
            state.Kind = SceneKind(driver, request);
            if (firstStart && (state.Kind == SSCInteractionKind.Consensual || state.Kind == SSCInteractionKind.Forced
#if SSC_RW17
                || state.Kind == SSCInteractionKind.DailyTraining || state.Kind == SSCInteractionKind.RitualTraining
#endif
                ))
                state.Parts = SSCOrdinarySexPartsUtility.Capture(driver.Sexprops);
            var receiver = request.Receiver?.jobs?.curDriver as JobDriver_SexBaseReciever;
            if (receiver != null && receiver.Partner == request.Initiator)
            {
                SceneState targetState = State(receiver);
                targetState.Started = true;
                targetState.Initiator = request.Initiator;
                targetState.Receiver = request.Receiver;
                targetState.Kind = state.Kind;
            }
            state.Events.NotifyStarted(state.Initiator, state.Receiver);
            return true;
        }

#if SSC_RW17
        /// <summary>只读取得当前调教 Job 首次 Start 保存的部位；不为旧档、替换任务或新 SexProps 补造。</summary>
        internal static SSCOrdinarySexPartsSnapshot TrainingParts(JobDriver_SexBaseInitiator driver,
            SSCInteractionKind kind)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || driver == null || driver.pawn?.jobs?.curDriver != driver ||
                driver.job == null || (kind != SSCInteractionKind.DailyTraining && kind != SSCInteractionKind.RitualTraining)) return null;
            SceneState state = State(driver);
            if (!state.Started || state.Rejected || state.Kind != kind || state.Initiator != driver.pawn ||
                state.Receiver != driver.Partner || state.Parts?.Matches(driver.Sexprops) != true) return null;
            return state.Parts;
        }
#endif

        /// <summary>阻止未开始而遭拒绝的原生收尾，避免访问空数据或替无关参与者生成结算任务。</summary>
        public static bool TryEnd(JobDriver_SexBaseInitiator driver, out bool allowed)
        {
            allowed = true;
            if (!SSCRestrictionJobContext.TryCreate(driver, out _)) return false;
            SceneState state = State(driver);
            allowed = !state.Rejected || (state.Started && state.Initiator == driver.pawn && state.Receiver == driver.Partner);
            return true;
        }

        /// <summary>只读验证已开始的普通 RJW 完成候选，供原结算前快照与返回后的认领共用。</summary>
        private static bool TryReadOrdinarySexOutcome(SexProps props, out Pawn initiator, out Pawn recipient)
        {
            initiator = null;
            recipient = null;

            // ProcessSex 的 pawn 会受姿势反转影响。实际发起者只能从其当前
            // Job 与统一请求的方向读取，且传入的 SexProps 必须属于此任务。
            JobDriver_SexBaseInitiator driver = props?.initiator?.jobs?.curDriver as JobDriver_SexBaseInitiator;
            if (driver == null || driver.pawn != props.initiator || driver.Sexprops != props ||
                driver is JobDriver_Training || driver is JobDriver_RitualTraining || driver is JobDriver_PE ||
                !SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request) ||
                !request.DirectionKnown || request.Initiator != driver.pawn ||
                request.Receiver == null || request.Receiver != props.recipient ||
                (request.Kind != SSCInteractionKind.Consensual && request.Kind != SSCInteractionKind.Forced)) return false;

            // RJW 普通双人 Job 只在发起方计时结束后进入 ProcessSex 的即时
            // 结算步骤。额外检查倒计时，阻止其他补丁提前调用该方法领奖；
            // 接收方驱动有独立计时，不参与本次完成判断。
            if (driver.ticks_left > 0) return false;

            // Start 成功后才有凭据；准备、预约、拒绝及接收任务的调用都不能领。
            // 沿用原经验的持久化字段与存档键，先消费整次完成，再由同一后缀分发经验及 PNA。
            // 即使无经验资格或无产出者，也不能在以后重放旧场景补领；旧档已领取者不回填。
            SceneState state = State(driver);
            if (!state.Started || state.Rejected || state.TrainerProgressClaimed ||
                state.Initiator != request.Initiator || state.Receiver != request.Receiver ||
                state.Kind != request.Kind) return false;
            initiator = request.Initiator;
            recipient = request.Receiver;
            return true;
        }

        public static bool TryClaimOrdinarySexOutcome(SexProps props, out Pawn initiator, out Pawn recipient)
        {
            if (!TryReadOrdinarySexOutcome(props, out initiator, out recipient)) return false;
            State((JobDriver_SexBaseInitiator)initiator.jobs.curDriver).TrainerProgressClaimed = true;
            return true;
        }

        /// <summary>原结算前只占用临时凭据；持久化认领等原方法正常返回后再消费。</summary>
        public static SSCOrdinarySexOutcomeUtility.Context PrepareOrdinaryOutcome(SexProps props)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || props?.isCoreLovin == true ||
                !TryReadOrdinarySexOutcome(props, out Pawn initiator, out Pawn recipient)) return null;
            var driver = (JobDriver_SexBaseInitiator)initiator.jobs.curDriver;
            SceneState state = State(driver);
            if (state.OutcomeInFlight) return null;
            Job job = driver.job;
            int jobId = job.loadID;
            SSCOrdinarySexPartsSnapshot parts = state.Parts;
            bool Current() => initiator.jobs?.curDriver == driver && ReferenceEquals(driver.job, job) &&
                ReferenceEquals(initiator.CurJob, job) && job.loadID == jobId && driver.Partner == recipient &&
                driver.Sexprops == props && props.initiator == initiator && props.recipient == recipient;
            state.OutcomeInFlight = true;
            try
            {
                return SSCOrdinarySexOutcomeUtility.Capture(initiator, recipient, true, Current, () =>
                {
                    bool valid = Current() && TryReadOrdinarySexOutcome(props, out Pawn a, out Pawn b) &&
                        a == initiator && b == recipient && State(driver) == state;
                    // 只写捕获的旧场状态；人格或参与者被回调改变后也不能让旧场补领。
                    state.TrainerProgressClaimed = true;
                    return valid;
                }, () => state.OutcomeInFlight = false, parts,
                    () => Current() && state.Parts == parts && parts?.Matches(props) == true);
            }
            catch { state.OutcomeInFlight = false; throw; }
        }

        /// <summary>只撤销当前请求准备的接收关联，再结束本驱动；保留其他参与者和后来替换的新任务。</summary>
        private static void Reject(JobDriver_Sex driver, SSCRestrictionRequest request, string reason)
        {
            SceneState state = State(driver);
            if (state.Rejected && driver.pawn?.jobs?.curDriver != driver) return;
            state.Rejected = true;
            state.Preparation.Cleanup();
            if (driver is JobDriver_SexBaseInitiator initiator)
            {
                Pawn target = request.Receiver;
                var receiver = target?.jobs?.curDriver as JobDriver_SexBaseReciever;
                if (receiver != null)
                {
                    receiver.parteners?.Remove(initiator.pawn);
                    // 家具兼容的常驻任务不属于本请求；本批只主动结束标准或 SSC 专用接收任务。
                    bool owned = !OnaholeCompatibilityUtility.IsBeOnaholeDriver(receiver) && receiver.Partner == initiator.pawn &&
                        (receiver.GetType().Assembly == typeof(JobDriver_Sex).Assembly || receiver.job?.def == SSCDefOf.SSC_TrainingReceiver ||
                         receiver.GetType().FullName == SSCRestrictionExternalJobs.SpotReceiverType);
                    if (owned && receiver.parteners?.Count == 0 && target.jobs.curDriver == receiver)
                        target.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
                }
                if (driver is JobDriver_PE || OnaholeCompatibilityUtility.IsBeOnaholeDriver(receiver))
                    OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(target, initiator.pawn);
                if (driver is JobDriver_PE)
                {
                    TrainingJobUtility.MarkValidationFailure(target, "SSC_Restrictions_PE");
                }
            }
            if (driver is JobDriver_RitualTraining ritualDriver)
                ritualDriver.AbortForRestriction(reason);
            if (driver.pawn?.jobs?.curDriver != driver) return;
            if (driver.job?.playerForced == true && !(driver is JobDriver_RitualTraining) && !state.Events.IsEvent)
                Messages.Message(reason,
                    new LookTargets(request.Initiator, request.Receiver), MessageTypeDefOf.RejectInput);
            driver.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
        }

        /// <summary>保存任务级开始凭据；升级旧存档只依据实际计时进度恢复，单有准备数据不算开始。</summary>
        public static void ExposeData(JobDriver_Sex driver)
        {
            SceneState state = State(driver);
            Scribe_Values.Look(ref state.HasRecord, "sscRestrictionSceneRecord", false);
            Scribe_Values.Look(ref state.HasTrainingRecord, "sscRestrictionTrainingRecord", false);
            Scribe_Values.Look(ref state.HasCompatibilityRecord, "sscRestrictionCompatibilityRecord", false);
            Scribe_Values.Look(ref state.Started, "sscRestrictionSceneStarted", false);
            Scribe_Values.Look(ref state.Kind, "sscRestrictionSceneKind", SSCInteractionKind.Unknown);
            // 和开始凭据存在同一 Job 的存档中；老档默认尚未领过这项新经验。
            Scribe_Values.Look(ref state.TrainerProgressClaimed, "sscTrainerSexProgressClaimed", false);
            Scribe_References.Look(ref state.Initiator, "sscRestrictionSceneInitiator");
            Scribe_References.Look(ref state.Receiver, "sscRestrictionSceneReceiver");
            Scribe_Deep.Look(ref state.Parts, "sscOrdinarySexParts");
            state.Preparation.ExposeData();
            state.Events.ExposeData();
            // 原版 PostLoadInit 的 SetupToils 会重建 RJW 计时；必须先在 LoadingVars 捕获旧存档进度。
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                state.LegacyProgress = driver.Sexprops != null &&
                    (driver.orgasms > 0 || (driver.duration > 0 && driver.ticks_left < driver.duration));
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            // 引用解析后仅绑定已加载的 SexProps；不读取 resolved getter，不为旧档创建凭据。
            state.Parts?.Bind(driver.Sexprops);
            if (state.LegacyProgress && driver is JobDriver_SexBaseInitiator &&
                SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request) && request.DirectionKnown &&
                (!state.HasRecord || (!state.HasTrainingRecord && SSCRestrictionTrainingUtility.IsTraining(request)) ||
                 (!state.HasCompatibilityRecord && driver.job?.def?.defName == "rjw_genes_lifeforce_randomrape")))
            {
                state.Started = true;
                state.Initiator = request.Initiator;
                state.Receiver = request.Receiver;
                state.Kind = SceneKind(driver, request);
            }
            state.HasRecord = true;
            state.HasTrainingRecord = true;
            state.HasCompatibilityRecord = true;
        }

        /// <summary>仪式自身已保存精确的 Start 标记；在派生驱动恢复完成后补回升级前场景的统一凭据。</summary>
        public static void RestoreRitualScene(JobDriver_RitualTraining driver, bool sceneStarted)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || !sceneStarted ||
                !SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request) || !request.DirectionKnown) return;
            SceneState state = State(driver);
            state.Started = true;
            state.Initiator = request.Initiator;
            state.Receiver = request.Receiver;
            state.Kind = SceneKind(driver, request);
        }

        /// <summary>记录快速双人任务本次准备回调新建的移动和等待任务；按实例编号清理，不删除其他玩家排队命令。</summary>
        public static IEnumerable<Toil> TrackPreparation(JobDriver_SexBaseInitiator driver, IEnumerable<Toil> toils)
        {
            foreach (Toil toil in toils)
            {
                Action original = toil.initAction;
                // 包装初始化回调只用于记录归属；许可检查仍在统一的步骤和开始入口执行。
                toil.initAction = () =>
                {
                    Pawn target = driver.Partner;
                    SceneState state = State(driver);
                    HashSet<int> before = SSCRestrictionJobPreparation.Snapshot(target);
                    try
                    {
                        original?.Invoke();
                    }
                    finally
                    {
                        // 原回调可能在同步取消后才安装准备，或安装后抛异常；仍认领其新增非玩家任务。
                        // 使用回调前的场景状态，避免 Job 已归池／复用后把归属挂到新的请求上。
                        if (!state.Started)
                        {
                            state.Preparation.RecordNew(target, before);
                            if (driver.pawn?.jobs?.curDriver != driver || driver.job != state.Job ||
                                driver.job?.loadID != state.JobId) state.Preparation.Cleanup();
                        }
                    }
                };
                yield return toil;
            }
        }

        /// <summary>事件调度前检查实际Job驱动并登记来源；拒绝时尚未打断任何工作，也不以事件名推测许可。</summary>
        public static bool PrepareEvent(Pawn actor, Job job, SSCRestrictionEvent source)
        {
            JobDriver_Sex driver = SSCRestrictionJobContext.GetDriver(job, actor);
            if (driver == null || !TryReserve(driver, out bool allowed) || !allowed) return false;
            SSCRestrictionJobEvents.Prepare(actor, job, source);
            return true;
        }

        /// <summary>手动命令失败时返回本次具体许可或资格原因；只读重查不修改配置、队列或场景状态。</summary>
        public static string RejectionReason(JobDriver_Sex driver)
        {
            if (!SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request))
                return "SSC_Restrictions_CommandRejected".Translate();
            Evaluate(driver, request, out _, out string reason, ordered: true);
            return reason ?? "SSC_Restrictions_CommandRejected".Translate();
        }

        /// <summary>在启动行为任务前登记事件新建的等待任务，确保预约失败或立即取消时也可精确回收。</summary>
        public static void RegisterEventWait(Pawn actor, Job job, Pawn target, Job wait)
            => SSCRestrictionJobEvents.RegisterWait(actor, job, target, wait);

        /// <summary>后续事件只读检查本场景拥有的准备任务；此入口不改变许可、登记和清理流程。</summary>
        internal static bool OwnsPreparedJob(JobDriver_Sex driver, Pawn target, Job job)
            => driver != null && State(driver).Preparation.Owns(target, job);

        /// <summary>调度未被接收时移除Job上的待领取事件，防止对象池继续持有旧参与者引用。</summary>
        public static void CancelPendingEvent(Job job) => SSCRestrictionJobEvents.CancelPending(job);

        /// <summary>场景未开始便结束时清理本请求准备任务，已开始或属于新任务的状态不受影响。</summary>
        public static void CleanupAbortedPreparation(JobDriver_Sex driver)
        {
            SceneState state = State(driver);
            if (!state.Started) state.Preparation.Cleanup();
        }

        /// <summary>记录统一判定的用途、方向、条目与来源；不再输出易误导的新任务旧开关值。</summary>
        private static void LogDecision(JobDriver_Sex driver, SSCRestrictionRequest request, SSCRestrictionDecision decision,
            string phase, bool allowed, string reason)
        {
            // 调用方先检查日志闸门，避免关闭详细日志时仍格式化参与者、规则来源和完整诊断字符串。
            if (!SSCLog.VerboseEnabled) return;
            SSCLog.Verbose($"[SSC Restrictions] phase={phase} job={driver.job?.def?.defName} kind={request.Kind} " +
                $"actor={request.Initiator?.LabelShort} receiver={request.Receiver?.LabelShort} direction={request.DirectionKnown} " +
                $"allowed={allowed} permission={decision.Allowed} reason={decision.Reason} failure={reason} rule={decision.Entry?.Rule} " +
                $"source={decision.Entry?.Source} def={decision.Entry?.SourceDef}");
        }
    }
}
