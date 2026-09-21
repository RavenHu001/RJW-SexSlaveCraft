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
            public Job Job;
            public int JobId;
            public bool HasRecord = true;
            public bool HasTrainingRecord = true;
            public bool Started;
            public bool Rejected;
            public Pawn Initiator;
            public Pawn Receiver;
            public SSCInteractionKind Kind;
            public bool LegacyProgress;
            public Pawn PreparedTarget;
            public List<int> PreparedJobs = new List<int>();
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

        /// <summary>预约阶段只返回许可，不结束正在被任务跟踪器安装的驱动，也不创建配置或玩家命令豁免。</summary>
        public static bool TryReserve(JobDriver_Sex driver, out bool allowed, bool ordered = false)
        {
            allowed = true;
            if (!SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request)) return false;
            if (HasStarted(driver, request)) return true;
            allowed = Evaluate(driver, request, out SSCRestrictionDecision decision, out string reason, ordered);
            LogDecision(driver, request, decision, "Reserve", allowed, reason);
            State(driver).Rejected = !allowed;
            return true;
        }

        /// <summary>在步骤或 Start 前重新查询；已开始的原场景保留收尾，迟到回调直接停止且不碰新任务。</summary>
        public static bool TryCheck(JobDriver_Sex driver, string phase, out bool allowed)
        {
            allowed = true;
            if (!SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request)) return false;
            if (driver.pawn?.jobs?.curDriver != driver) { allowed = false; return true; }
            if (HasStarted(driver, request)) return true;
            allowed = Evaluate(driver, request, out SSCRestrictionDecision decision, out string reason);
            LogDecision(driver, request, decision, phase, allowed, reason);
            if (!allowed) Reject(driver, request, reason);
            return true;
        }

        /// <summary>接收端沿用发起任务的调度方式，避免 playerForced 的接收任务替自动日常工作解除唯一指派。</summary>
        private static bool Evaluate(JobDriver_Sex driver, SSCRestrictionRequest request,
            out SSCRestrictionDecision decision, out string reason, bool ordered = false)
        {
            JobDriver_Sex actor = driver is JobDriver_SexBaseReciever receiver
                ? SSCRestrictionJobContext.FindInitiator(receiver) : driver;
            bool automaticDaily = actor is JobDriver_Training && !ordered && actor.job?.playerForced != true;
            return SSCRestrictionTrainingUtility.TryEvaluate(request, automaticDaily, out decision, out reason);
        }

        /// <summary>只在原 Start 确实执行且驱动仍为当前任务时记下凭据；准备阶段不会得到开始标记。</summary>
        public static bool TryMarkStarted(JobDriver_SexBaseInitiator driver, bool ranOriginal)
        {
            if (!SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request)) return false;
            if (!ranOriginal || driver.pawn?.jobs?.curDriver != driver) return true;
            SceneState state = State(driver);
            if (state.Rejected) return true;
            state.Started = true;
            state.Initiator = request.Initiator;
            state.Receiver = request.Receiver;
            state.Kind = SceneKind(driver, request);
            var receiver = request.Receiver?.jobs?.curDriver as JobDriver_SexBaseReciever;
            if (receiver != null && receiver.Partner == request.Initiator)
            {
                SceneState targetState = State(receiver);
                targetState.Started = true;
                targetState.Initiator = request.Initiator;
                targetState.Receiver = request.Receiver;
                targetState.Kind = state.Kind;
            }
            return true;
        }

        /// <summary>阻止未开始而遭拒绝的原生收尾，避免访问空数据或替无关参与者生成结算任务。</summary>
        public static bool TryEnd(JobDriver_SexBaseInitiator driver, out bool allowed)
        {
            allowed = true;
            if (!SSCRestrictionJobContext.TryCreate(driver, out _)) return false;
            SceneState state = State(driver);
            allowed = !state.Rejected || (state.Started && state.Initiator == driver.pawn && state.Receiver == driver.Partner);
            return true;
        }

        /// <summary>只撤销当前请求准备的接收关联，再结束本驱动；保留其他参与者和后来替换的新任务。</summary>
        private static void Reject(JobDriver_Sex driver, SSCRestrictionRequest request, string reason)
        {
            SceneState state = State(driver);
            if (state.Rejected && driver.pawn?.jobs?.curDriver != driver) return;
            state.Rejected = true;
            CleanupPreparation(state);
            if (driver is JobDriver_SexBaseInitiator initiator)
            {
                Pawn target = request.Receiver;
                var receiver = target?.jobs?.curDriver as JobDriver_SexBaseReciever;
                if (receiver != null)
                {
                    receiver.parteners?.Remove(initiator.pawn);
                    // 家具兼容的常驻任务不属于本请求；本批只主动结束标准或 SSC 专用接收任务。
                    bool owned = receiver.Partner == initiator.pawn &&
                        (receiver.GetType().Assembly == typeof(JobDriver_Sex).Assembly || receiver.job?.def == SSCDefOf.SSC_TrainingReceiver);
                    if (owned && receiver.parteners?.Count == 0 && target.jobs.curDriver == receiver)
                        target.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
                }
                if (driver is JobDriver_PE)
                {
                    OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(target, initiator.pawn);
                    TrainingJobUtility.MarkValidationFailure(target, "SSC_Restrictions_PE");
                }
            }
            if (driver is JobDriver_RitualTraining ritualDriver)
                ritualDriver.AbortForRestriction(reason);
            if (driver.pawn?.jobs?.curDriver != driver) return;
            if (driver.job?.playerForced == true && !(driver is JobDriver_RitualTraining))
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
            Scribe_Values.Look(ref state.Started, "sscRestrictionSceneStarted", false);
            Scribe_Values.Look(ref state.Kind, "sscRestrictionSceneKind", SSCInteractionKind.Unknown);
            Scribe_References.Look(ref state.Initiator, "sscRestrictionSceneInitiator");
            Scribe_References.Look(ref state.Receiver, "sscRestrictionSceneReceiver");
            Scribe_References.Look(ref state.PreparedTarget, "sscRestrictionPreparedTarget");
            Scribe_Collections.Look(ref state.PreparedJobs, "sscRestrictionPreparedJobs", LookMode.Value);
            // 原版 PostLoadInit 的 SetupToils 会重建 RJW 计时；必须先在 LoadingVars 捕获旧存档进度。
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                state.LegacyProgress = driver.Sexprops != null &&
                    (driver.orgasms > 0 || (driver.duration > 0 && driver.ticks_left < driver.duration));
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (state.LegacyProgress && driver is JobDriver_SexBaseInitiator &&
                SSCRestrictionJobContext.TryCreate(driver, out SSCRestrictionRequest request) && request.DirectionKnown &&
                (!state.HasRecord || (!state.HasTrainingRecord && SSCRestrictionTrainingUtility.IsTraining(request))))
            {
                state.Started = true;
                state.Initiator = request.Initiator;
                state.Receiver = request.Receiver;
                state.Kind = SceneKind(driver, request);
            }
            state.HasRecord = true;
            state.HasTrainingRecord = true;
            if (state.PreparedJobs == null) state.PreparedJobs = new List<int>();
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
        public static IEnumerable<Toil> TrackPreparation(JobDriver_SexQuick driver, IEnumerable<Toil> toils)
        {
            foreach (Toil toil in toils)
            {
                Action original = toil.initAction;
                // 包装初始化回调只用于记录归属；许可检查仍在统一的步骤和开始入口执行。
                toil.initAction = () =>
                {
                    Pawn target = driver.Partner;
                    HashSet<int> before = PreparationJobs(target);
                    original?.Invoke();
                    if (driver.pawn?.jobs?.curDriver != driver || State(driver).Started) return;
                    SceneState state = State(driver);
                    state.PreparedTarget = target;
                    foreach (int id in PreparationJobs(target))
                        if (!before.Contains(id) && !state.PreparedJobs.Contains(id)) state.PreparedJobs.Add(id);
                };
                yield return toil;
            }
        }

        /// <summary>快照目标当前及排队中的普通移动、等待任务编号，供前后差异确定本请求创建的任务。</summary>
        private static HashSet<int> PreparationJobs(Pawn target)
        {
            var result = new HashSet<int>();
            if (target?.jobs == null) return result;
            Job current = target.CurJob;
            if (IsPreparationJob(current)) result.Add(current.loadID);
            foreach (QueuedJob queued in target.jobs.jobQueue)
                if (IsPreparationJob(queued.job)) result.Add(queued.job.loadID);
            return result;
        }

        /// <summary>仅识别已核对的快速任务准备类型，不把任意接收任务或玩家工作作为等待任务处理。</summary>
        private static bool IsPreparationJob(Job job) => job != null && (job.def == JobDefOf.Goto || job.def == JobDefOf.Wait);

        /// <summary>规则拒绝时撤销本请求留下的排队和当前准备任务；编号不匹配的新任务保持不变。</summary>
        private static void CleanupPreparation(SceneState state)
        {
            Pawn target = state.PreparedTarget;
            if (target?.jobs == null || state.PreparedJobs == null || state.PreparedJobs.Count == 0) return;
            target.jobs.jobQueue.RemoveAll(target, job => IsPreparationJob(job) && state.PreparedJobs.Contains(job.loadID));
            if (IsPreparationJob(target.CurJob) && state.PreparedJobs.Contains(target.CurJob.loadID))
                target.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
            state.PreparedJobs.Clear();
        }

        /// <summary>记录统一判定的用途、方向、条目与来源；不再输出易误导的新任务旧开关值。</summary>
        private static void LogDecision(JobDriver_Sex driver, SSCRestrictionRequest request, SSCRestrictionDecision decision,
            string phase, bool allowed, string reason)
        {
            SSCLog.Verbose($"[SSC Restrictions] phase={phase} job={driver.job?.def?.defName} kind={request.Kind} " +
                $"actor={request.Initiator?.LabelShort} receiver={request.Receiver?.LabelShort} direction={request.DirectionKnown} " +
                $"allowed={allowed} permission={decision.Allowed} reason={decision.Reason} failure={reason} rule={decision.Entry?.Rule} " +
                $"source={decision.Entry?.Source} def={decision.Entry?.SourceDef}");
        }
    }
}
