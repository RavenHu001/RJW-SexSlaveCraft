// AI 辅助生成：一场发作持有唯一的信件决定、任务授权和结局。
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed partial class MentalState_SSCSlaveBreakdown
    {
        // Waiting 只持有信件。Approved 已消费决定，等游戏 tick 派发。
        // Dispatched 只授权一个已记录 loadID 的发起 Job，训练预约仍由驱动取得。
        private enum RequestPhase { None, Waiting, Approved, Dispatched }
        private enum EpisodeResult { None, Success, Failure }
        // 拒绝、失效和任务失败共用的重申请间隔，与训练组件的日常冷却独立。
        private const int RequestInterval = 3750;
        private const float RequestRadius = 15f;
        private RequestPhase requestPhase;
        private EpisodeResult episodeResult;
        private Pawn requestTrainer;
        private object trainerPersonality; // 仅当前进程有效，用于识别同一身体中的人格替换。
        private int requestId = -1;
        private int trainingJobId = -1;
        private long nextRequestAt;
        // 延时只给已经开场的这份批准。原期限用于决定窗口和撤销恢复，运行期限始终由 Hediff 提供。
        private long originalDeadline = -1;
        private bool trainingStarted;
        private bool outcomeIssued;

        internal const string TrainingDefName = "SSC_BreakdownTraining";
        /// <summary>JobDef 划定特批路径。实际权限还需向当前发作核对受方、调教员和任务编号。</summary>
        internal static bool IsBreakdownTraining(Job job) => job?.def?.defName == TrainingDefName;

        private long RequestDeadline => originalDeadline >= 0 ? originalDeadline : Deadline;
        private bool TrainerIdentityCurrent => requestTrainer != null &&
            SSCPersonalityRuntimeUtility.IsCurrent(requestTrainer, trainerPersonality) &&
            !SSCPersonalityRuntimeUtility.IsMutating(requestTrainer);

        private void ExposeTrainingRequest()
        {
            // 精神状态保存授权，原版信堆和任务跟踪器分别保存各自对象。存档只用 Pawn 引用和 ID 把三者接回。
            // 第三步旧档缺少这些字段时按尚无申请处理，沿用原开场阶段。加载期间只恢复数据。
            Scribe_Values.Look(ref requestPhase, "sscBreakdownRequestPhase", RequestPhase.None);
            Scribe_Values.Look(ref episodeResult, "sscBreakdownResult", EpisodeResult.None);
            Scribe_References.Look(ref requestTrainer, "sscBreakdownTrainer");
            Scribe_Values.Look(ref requestId, "sscBreakdownRequestId", -1);
            Scribe_Values.Look(ref trainingJobId, "sscBreakdownTrainingJobId", -1);
            Scribe_Values.Look(ref nextRequestAt, "sscBreakdownNextRequestAt", 0L);
            Scribe_Values.Look(ref originalDeadline, "sscBreakdownOriginalDeadline", -1L);
            Scribe_Values.Look(ref trainingStarted, "sscBreakdownTrainingStarted", false);
            Scribe_Values.Look(ref outcomeIssued, "sscBreakdownOutcomeIssued", false);
            // 交叉引用解析后才能取得当前进程的人格凭据。首次使用时仍须核对原信件或原任务。
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                trainerPersonality = SSCPersonalityRuntimeUtility.Capture(requestTrainer);
        }

        // 延长的时间只给已经开场的任务，拒绝在原到期时间后批准或生成另一封请求。
        private bool CanRequestTraining => openingPhase == OpeningPhase.Wander &&
            episodeResult == EpisodeResult.None && CanIssueBehavior && SSCSlaveBreakdown.Now < RequestDeadline;

        // 从玩家决定起让出候选槽。等待选择的信件本身不占用发作小人或调教员。
        internal bool HasApprovedTraining => requestPhase is RequestPhase.Approved or RequestPhase.Dispatched;

        /// <summary>从扫描到执行都需要的基础资格。路径距离和可打断的旧工作仅在派发前检查。</summary>
        private bool TrainerEligible(Pawn trainer)
            => trainer?.Spawned == true && trainer != pawn && trainer.Map == pawn.Map && !trainer.Drafted &&
                !trainer.InMentalState && !trainer.IsFighting() && trainer.Awake() &&
                !SSCPersonalityRuntimeUtility.IsMutating(trainer) &&
                SSCRestrictionTrainingUtility.EvaluateBreakdown(trainer, pawn).Allowed;

        /// <summary>预检同图距离、可达性、预约竞争、训练失败冷却和身体准入。查询期间不占用双方。</summary>
        private bool CanApproach(Pawn trainer)
            => !pawn.Downed && !pawn.Drafted && !pawn.IsFighting() && TrainerEligible(trainer) &&
                trainer.Position.InHorDistOf(pawn.Position, RequestRadius) &&
                !pawn.IsForbidden(trainer) && trainer.CanReach(pawn, PathEndMode.Touch, Danger.Some) &&
                TrainerAssignmentUtility.CanReserveDailyTraining(trainer, pawn) &&
                !SSCDailyTrainingOccupancy.IsBusy(pawn) && !SSCDailyTrainingOccupancy.HasOtherReceiver(pawn, trainer) &&
                !BindingRitualStateUtility.HasActiveTargetRitual(pawn) &&
                !training.IsWaitingAfterFailedValidation &&
                !ProgressionEducationCompatibility.ShouldDeferAutomaticTraining(pawn, false) &&
                Trainjudge.TryCanBeFuckedWithReason(pawn, out _, skipReason: true);

        // 保留玩家指令、排队工作及不可随意打断的任务，允许接管空闲和可打断的原版工作。
        private static bool AvailableToTeach(Pawn trainer)
            => trainer.jobs?.jobQueue.Count == 0 &&
                (trainer.CurJob == null || (!trainer.CurJob.playerForced && trainer.CurJob.def.casualInterruptible));

        // 进食、睡觉和既有互动仍可完成。特批只接管本场游荡或空闲受方。
        internal bool CanReceiveBreakdownTraining => !HasBehaviorJob && !pawn.Downed && !pawn.Drafted &&
            !pawn.IsFighting() && pawn.Awake() &&
            (pawn.CurJob == null || pawn.CurJob.jobGiver is JobGiver_SSCSlaveBreakdownWander);

        private ChoiceLetter_SSCBreakdownTraining ActiveLetter
            => Find.LetterStack?.LettersListForReading.OfType<ChoiceLetter_SSCBreakdownTraining>()
                .FirstOrDefault(letter => letter.ID == requestId && letter.Subject == pawn);

        /// <summary>只接受原版信堆仍持有的那封活信。归档、旧对话回调和相同角色后来的申请均独立。</summary>
        internal bool IsPendingDecision(ChoiceLetter_SSCBreakdownTraining letter)
            => requestPhase == RequestPhase.Waiting && letter != null && requestId == letter.ID &&
                requestTrainer == letter.Trainer && letter.Subject == pawn && CanRequestTraining && ActiveLetter == letter;

        /// <summary>界面的接受按钮和真正的提交共用同一准入，界面状态随点击时的现场重新检查。</summary>
        internal bool CanApprove(ChoiceLetter_SSCBreakdownTraining letter)
            => IsPendingDecision(letter) && TrainerIdentityCurrent &&
                (CanWaitForInvitedTrainer() || (CanApproach(requestTrainer) && AvailableToTeach(requestTrainer)));

        // 如果调教员也是本场邀约的对象，可以先批准。邀约收尾后的完整可达性和预约检查仍在派发前执行。
        private bool CanWaitForInvitedTrainer() => invitation?.Partner == requestTrainer &&
            invitation?.OccupiesPartner(pawn) == true && TrainerEligible(requestTrainer) &&
            requestTrainer.Position.InHorDistOf(pawn.Position, RequestRadius);

        internal void Decide(ChoiceLetter_SSCBreakdownTraining letter, bool accept)
        {
            if (!IsPendingDecision(letter)) return;
            if (!accept || !CanApprove(letter))
            {
                if (IsPendingDecision(letter)) CancelTrainingRequest();
                return;
            }
            if (!IsPendingDecision(letter) || !TrainerIdentityCurrent) return;
            // 先消费这次按钮权限。关闭信件会触发原版回调，重复点击也不会启动第二个 Job。
            // 等下一个状态 tick 再派发，允许在游戏暂停时稳妥保存已批准的决定。
            requestPhase = RequestPhase.Approved;
            try { Find.LetterStack.RemoveLetter(letter); }
            finally
            {
                // 信件回调可能同步取消批准。只在该状态仍处于获批阶段时抢占准备中的邀约。
                if (requestPhase == RequestPhase.Approved) CancelUnstartedInvitation();
            }
        }

        private void TickTrainingRequest(int now, int delta)
        {
            // 极少数存档落在认领完成与恢复之间。读档后走统一结束入口，避免重派或重新发奖。
            if (episodeResult != EpisodeResult.None) { EndCurrentState(); return; }
            if (requestPhase == RequestPhase.Dispatched)
            {
                // 当前驱动不再属于批准时，撤销原任务。原期限内仍可游荡并在冷却后重提。
                if (!OwnsTrainingJob(requestTrainer, requestTrainer?.CurJob) ||
                    requestTrainer.jobs.curDriver is not JobDriver_Training)
                    CancelTrainingRequest();
                else ExtendStartedTraining(now);
                return;
            }

            if (!CanRequestTraining) return;
            if (requestPhase == RequestPhase.Waiting)
            {
                // 调教员暂时接到其他工作时接受选项可恢复。离图、失格、离开范围或换人格会使旧信失效。
                if (ActiveLetter == null || !TrainerIdentityCurrent || !TrainerEligible(requestTrainer) ||
                    !requestTrainer.Position.InHorDistOf(pawn.Position, RequestRadius)) CancelTrainingRequest();
                return;
            }
            if (requestPhase == RequestPhase.Approved)
            {
                // 发作小人或调教员可能仍在已开场的互动中。先等精确的原互动结束，再检查预约及任务占用。
                if (!TrainerIdentityCurrent || !TrainerEligible(requestTrainer) ||
                    !requestTrainer.Position.InHorDistOf(pawn.Position, RequestRadius))
                {
                    CancelTrainingRequest();
                    return;
                }
                if (!CanReceiveBreakdownTraining) return;
                if (!CanApproach(requestTrainer) || !AvailableToTeach(requestTrainer))
                {
                    CancelTrainingRequest();
                    return;
                }
                DispatchApprovedTraining();
                return;
            }
            // 发信期间不申请预约。通过信件批准后，才由现有训练驱动认领占用。
            if (!CanReceiveBreakdownTraining || now < nextRequestAt || !pawn.IsHashIntervalTick(150, delta) ||
                !SSCSceneHookAvailability.CanStartDaily(false)) return;
            var def = DefDatabase<LetterDef>.GetNamedSilentFail("SSC_BreakdownTrainingRequest");
            if (def == null || Find.LetterStack == null) return;
            Pawn trainer = pawn.Map.mapPawns.FreeColonistsSpawned.OrderBy(other =>
                other.Position.DistanceToSquared(pawn.Position)).FirstOrDefault(other => AvailableToTeach(other) && CanApproach(other));
            if (trainer == null || !CanRequestTraining || requestPhase != RequestPhase.None) return;
            var letter = (ChoiceLetter_SSCBreakdownTraining)LetterMaker.MakeLetter(
                "SSC_BreakdownRequest_Label".Translate(), "SSC_BreakdownRequest_Text".Translate(pawn.LabelShort, trainer.LabelShort),
                def, new LookTargets(pawn, trainer));
            letter.Subject = pawn;
            letter.Trainer = trainer;
            letter.disappearAtTick = (int)RequestDeadline;
            // 先登记所属，再交信堆。收信侧的日志、UI 或兼容补丁可以同步查看当前申请。
            requestTrainer = trainer;
            trainerPersonality = SSCPersonalityRuntimeUtility.Capture(trainer);
            requestId = letter.ID;
            requestPhase = RequestPhase.Waiting;
            Find.LetterStack.ReceiveLetter(letter);
        }

        private void DispatchApprovedTraining()
        {
            // 启动前才执行原日常训练中带副作用的身体兼容准备，之后再次核对原批准是否仍有效。
            Pawn trainer = requestTrainer;
            var def = DefDatabase<JobDef>.GetNamedSilentFail(TrainingDefName);
            if (def == null || !TrainingJobUtility.TryValidateTarget(pawn, false, "SSC_BREAKDOWN") ||
                requestPhase != RequestPhase.Approved || !CanRequestTraining || !TrainerIdentityCurrent ||
                !CanApproach(trainer) || !AvailableToTeach(trainer) || !CanReceiveBreakdownTraining)
            {
                if (requestPhase == RequestPhase.Approved) CancelTrainingRequest();
                return;
            }
            // 路径和预约查询可能调用外部补丁。仍须由原批准启动任务。
            if (requestPhase != RequestPhase.Approved || requestTrainer != trainer ||
                !CanRequestTraining || !TrainerIdentityCurrent) return;
            Job job = JobMaker.MakeJob(def, pawn);
            // 原版 StartJob 同步预约并可能立即运行首个 Toil，必须提前登记 loadID。
            trainingJobId = job.loadID;
            requestPhase = RequestPhase.Dispatched;
            trainer.jobs.StartJob(job, JobCondition.InterruptOptional, preToilReservationsCanFail: true);
            if (requestPhase == RequestPhase.Dispatched && !OwnsTrainingJob(trainer, trainer.CurJob))
                CancelTrainingRequest();
        }

        /// <summary>核对当前发作、双方人格和实际任务归属。Job 对象可重用，因此以本次 loadID 为凭据。</summary>
        internal bool OwnsTrainingJob(Pawn trainer, Job job)
            => requestPhase == RequestPhase.Dispatched && IsBreakdownTraining(job) && !job.playerForced && job.loadID == trainingJobId &&
                job.targetA.Pawn == pawn && trainer == requestTrainer && TrainerIdentityCurrent && CanIssueBehavior &&
                !pawn.Downed && !pawn.Drafted && TrainerEligible(trainer) && !BindingRitualStateUtility.HasActiveTargetRitual(pawn);

        /// <summary>游荡行为树保留由本场训练驱动实际安装的当前接收 Job。</summary>
        internal bool HoldsTrainingReceiver(Job job)
            => job != null && job == pawn.CurJob && requestTrainer?.jobs?.curDriver is JobDriver_Training driver &&
                OwnsTrainingJob(requestTrainer, driver.job) && driver.HasDailyReceiver(pawn);

        /// <summary>训练驱动确认 RJW Start 已执行后登记。信件批准、行走和接收准备均不消耗额外时限。</summary>
        internal void MarkApprovedTrainingStarted(JobDriver_Training driver)
        {
            if (!OwnsTrainingJob(driver.pawn, driver.job)) return;
            trainingStarted = true;
            ExtendStartedTraining(SSCSlaveBreakdown.Now);
        }

        private void ExtendStartedTraining(int now)
        {
            long deadline = Deadline;
            if (!trainingStarted || originalDeadline >= 0 || now >= deadline ||
                deadline - now > GenDate.TicksPerHour || !HasEffect()) return;
            // 保留原决定期限。延长只用于完成已开场的任务，中断后恢复原到期点。
            originalDeadline = deadline;
            effect.ExpiresAt = deadline + GenDate.TicksPerHour;
        }

        /// <summary>真实开场且当前仍是原获批任务，才可进入完成 Toil。</summary>
        internal bool CanSettleApprovedTraining(JobDriver_Training driver)
            => episodeResult == EpisodeResult.None && trainingStarted && OwnsTrainingJob(driver.pawn, driver.job);

        internal bool ClaimTrainingSuccess(JobDriver_Training driver)
        {
            if (!CanSettleApprovedTraining(driver)) return false;
            // 日常日期和冷却已经记账。先固定发作结果，再移交原版恢复；随后的清理保留正式身份。
            episodeResult = EpisodeResult.Success;
            temporaryIdentity = false;
            return true;
        }

        internal void FinishSuccessfulTraining()
        {
            if (episodeResult == EpisodeResult.Success && !ending) EndCurrentState();
        }

        private void CancelTrainingRequest()
        {
            ChoiceLetter_SSCBreakdownTraining letter = requestPhase == RequestPhase.Waiting ? ActiveLetter : null;
            Pawn trainer = requestTrainer;
            int jobId = trainingJobId;
            // 先撤销自己的引用，原版删信和清理 Job 的同步回调只能看到失效的授权。
            requestPhase = RequestPhase.None;
            requestId = trainingJobId = -1;
            if (!ending)
            {
                nextRequestAt = (long)SSCSlaveBreakdown.Now + RequestInterval;
                if (originalDeadline >= 0)
                {
                    // 在原版任务清理的回调之前恢复截止点。状态刷新会在本轮重新检查是否已经到期。
                    if (HasEffect()) effect.ExpiresAt = originalDeadline;
                    originalDeadline = -1;
                }
            }
            trainingStarted = false;
            requestTrainer = null;
            trainerPersonality = null;
            if (letter != null) Find.LetterStack.RemoveLetter(letter);
            // 成功 Job 自己完成最后一个 Toil。失败只删除精确的原任务，保护已接手的新任务和其他队列条目。
            if (jobId < 0 || trainer?.jobs == null || episodeResult == EpisodeResult.Success) return;
            trainer.jobs.jobQueue.RemoveAll(trainer, job => IsBreakdownTraining(job) &&
                job.loadID == jobId && job != trainer.CurJob);
            if (IsBreakdownTraining(trainer.CurJob) && trainer.CurJob.loadID == jobId &&
                trainer.jobs.curDriver is { ended: false })
                trainer.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
        }

        private void PublishEpisodeOutcome()
        {
            if (episodeResult == EpisodeResult.None || outcomeIssued) return;
            // 心情与信件回调都可能重入结束入口。先认领发布，再把记忆写到原人格。
            outcomeIssued = true;
            bool success = episodeResult == EpisodeResult.Success;
            var memories = SameCarrier() && !pawn.Dead ? pawn.needs?.mood?.thoughts?.memories : null;
            var mood = DefDatabase<ThoughtDef>.GetNamedSilentFail(success ? "SSC_BreakdownSuccess" : "SSC_BreakdownFailure");
            if (mood != null) memories?.TryGainMemory(mood);
            if (success)
            {
                var opinion = DefDatabase<ThoughtDef>.GetNamedSilentFail("SSC_BreakdownTrainerOpinion");
                if (opinion != null && TrainerIdentityCurrent && SameCarrier())
                    memories?.TryGainMemory(opinion, requestTrainer);
            }
            else if (LetterDefOf.NegativeEvent != null && Find.LetterStack != null)
            {
                Find.LetterStack.ReceiveLetter("SSC_BreakdownFailure_Label".Translate(),
                    "SSC_BreakdownFailure_Text".Translate(pawn.LabelShort), LetterDefOf.NegativeEvent, pawn);
            }
        }
    }

    /// <summary>原版选择信保存描述和时限，SSC 保存参与者引用，实际决定交给受方的现行精神状态。</summary>
    public sealed class ChoiceLetter_SSCBreakdownTraining : ChoiceLetter
    {
        internal Pawn Subject;
        internal Pawn Trainer;
        // 右键静默移除会跳过拒绝语义。稍后决定沿用原版，只关闭对话并保留信件。
        public override bool CanDismissWithRightClick => false;
        public override bool ShouldAutomaticallyOpenLetter => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var state = MentalState_SSCSlaveBreakdown.CurrentFor(Subject);
                if (ArchivedOnly || state?.IsPendingDecision(this) != true)
                {
                    yield return Option_Close;
                    yield break;
                }
                // 点击时重新查当前状态，并用这封信的原 ID 复核。旧对话无法借用同 Pawn 的新发作。
                var accept = new DiaOption("AcceptButton".Translate()) { resolveTree = true,
                    action = () => MentalState_SSCSlaveBreakdown.CurrentFor(Subject)?.Decide(this, true) };
                if (!state.CanApprove(this)) accept.Disable("SSC_BreakdownRequest_Unavailable".Translate());
                yield return accept;
                yield return new DiaOption("RejectLetter".Translate()) { resolveTree = true,
                    action = () => MentalState_SSCSlaveBreakdown.CurrentFor(Subject)?.Decide(this, false) };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref Subject, "sscBreakdownSubject");
            Scribe_References.Look(ref Trainer, "sscBreakdownTrainer");
        }
    }
}
