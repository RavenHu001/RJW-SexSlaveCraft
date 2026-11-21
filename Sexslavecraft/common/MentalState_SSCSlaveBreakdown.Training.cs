// AI 辅助生成：一场发作持有唯一的信件决定、任务授权和结局。
using System;
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
        // 存档记录本次批准的业务结果。信件占位文字、调教员后来的任职变化均不能重选结果。
        internal enum TrainingDecision { None, FormOwnerless, FormBound, BindOwnerless, AdvanceOwnerless, AdvanceBound }
        // Success 先表示原任务已通过普通收益的完成判定，正式阶段和结果反馈由精神状态退出时提交。
        private enum EpisodeResult { None, Success, Failure }
        // 拒绝、失效和任务失败共用的重申请间隔，与训练组件的日常冷却独立。
        private const int RequestInterval = 3750;
        private const float RequestRadius = 15f;
        private RequestPhase requestPhase;
        private TrainingDecision approvedDecision;
        private float completedTrainingScore; // 复用原任务的基础分，退出时仅供符合资格的原版转奴使用。
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
            // 精神状态持有批准结果，原版信堆和任务跟踪器各自保存对象。读档按双方引用、信件 ID 和 Job loadID 恢复关联。
            // 缺请求阶段的旧档视为尚未发信。若已有批准但缺结果字段，只有原主继续调教可以从既有绑定推断。
            Scribe_Values.Look(ref requestPhase, "sscBreakdownRequestPhase", RequestPhase.None);
            Scribe_Values.Look(ref approvedDecision, "sscBreakdownTrainingDecision", TrainingDecision.None);
            Scribe_Values.Look(ref completedTrainingScore, "sscBreakdownCompletedTrainingScore", 0f);
            Scribe_Values.Look(ref episodeResult, "sscBreakdownResult", EpisodeResult.None);
            Scribe_References.Look(ref requestTrainer, "sscBreakdownTrainer");
            Scribe_Values.Look(ref requestId, "sscBreakdownRequestId", -1);
            Scribe_Values.Look(ref trainingJobId, "sscBreakdownTrainingJobId", -1);
            Scribe_Values.Look(ref nextRequestAt, "sscBreakdownNextRequestAt", 0L);
            Scribe_Values.Look(ref originalDeadline, "sscBreakdownOriginalDeadline", -1L);
            Scribe_Values.Look(ref trainingStarted, "sscBreakdownTrainingStarted", false);
            Scribe_Values.Look(ref outcomeIssued, "sscBreakdownOutcomeIssued", false);
            // 交叉引用解析后才能取得当前进程的人格凭据。首次使用时仍须核对原信件或原任务，
            // 有链时还要核对精神状态保存的原链和实际主人。读档本身没有新的批准或派发。
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                trainerPersonality = SSCPersonalityRuntimeUtility.Capture(requestTrainer);
                // 已有绑定的旧批准只有原主继续调教一种结果。无链旧批准交由派发守卫撤销。
                if (boundEpisode && HasApprovedTraining && approvedDecision == TrainingDecision.None)
                    approvedDecision = TrainingDecision.AdvanceBound;
            }
        }

        // 延长的时间只给已经开场的任务，拒绝在原到期时间后批准或生成另一封请求。
        private bool CanRequestTraining => openingPhase == OpeningPhase.Wander &&
            episodeResult == EpisodeResult.None && CanIssueBehavior && SSCSlaveBreakdown.Now < RequestDeadline;

        // 从玩家决定起让出候选槽。等待选择的信件本身不占用发作小人或调教员。
        internal bool HasApprovedTraining => requestPhase is RequestPhase.Approved or RequestPhase.Dispatched;

        /// <summary>从扫描到执行都需要的基础资格。路径距离和可打断的旧工作仅在派发前检查。</summary>
        /// <remarks>绑定主人决定有链场次的调教员范围，现有行为许可与训练身份资格仍在此检查。</remarks>
        private bool TrainerEligible(Pawn trainer)
            => trainer?.Spawned == true && trainer != pawn && trainer.Map == pawn.Map && !trainer.Drafted &&
                !trainer.InMentalState && !trainer.IsFighting() && trainer.Awake() &&
                (!boundEpisode || trainer == BoundOwner) &&
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
        /// <remarks>此处核对选择权，与当前能否接受分开。有效的有链请求可在主人暂时不适合接单时等待。</remarks>
        internal bool IsPendingDecision(ChoiceLetter_SSCBreakdownTraining letter)
            => requestPhase == RequestPhase.Waiting && letter != null && requestId == letter.ID &&
                requestTrainer == letter.Trainer && (!boundEpisode || requestTrainer == BoundOwner) &&
                letter.Subject == pawn && CanRequestTraining && ActiveLetter == letter;

        /// <summary>界面绘制和实际提交共用一套接受准入，点击时重新查询可达性、预约和调教员占用。</summary>
        internal bool CanApprove(ChoiceLetter_SSCBreakdownTraining letter, TrainingDecision decision)
            => IsPendingDecision(letter) && TrainerIdentityCurrent && CanUseDecision(decision) &&
                (CanWaitForInvitedTrainer() || (CanApproach(requestTrainer) && AvailableToTeach(requestTrainer)));

        /// <summary>按入场阶段及该封信调教员的当前身份提供选项。按钮可用性另由批准条件决定。</summary>
        internal IEnumerable<TrainingDecision> TrainingChoices()
        {
            if (boundEpisode) { yield return TrainingDecision.AdvanceBound; yield break; }
            if (SSCIdentityUtility.IsMaster(requestTrainer))
                yield return startingOwnerlessStage > 0 ? TrainingDecision.BindOwnerless : TrainingDecision.FormBound;
            yield return startingOwnerlessStage > 0 ? TrainingDecision.AdvanceOwnerless : TrainingDecision.FormOwnerless;
        }

        internal static bool IsBindingDecision(TrainingDecision decision)
            => decision is TrainingDecision.FormBound or TrainingDecision.BindOwnerless;

        // 每个阶段都重新核对当初选定的结果。零阶已成形时撤销首次选项，达到四阶时撤销发作中的绑定选项。
        // 同一记录已被外部推进的成长任务仍可完成普通收益，额外进度由最终结算核对起始阶段。
        private bool CanUseDecision(TrainingDecision decision)
        {
            // 再绑定冷却沿用统一建绑准入，只限制有主成形和等进度绑定。保持无主仍可照常批准及结算。
            if (boundEpisode) return decision == TrainingDecision.AdvanceBound;
            int stage = Hediff_SSCOwnerlessSlave.GetActiveStage(pawn);
            return decision switch
            {
                TrainingDecision.FormOwnerless => startingOwnerlessStage == 0 && stage == 0,
                TrainingDecision.FormBound => startingOwnerlessStage == 0 && stage == 0 &&
                    SSCBondUtility.CanBindNewWithProgress(requestTrainer, pawn),
                TrainingDecision.BindOwnerless => startingOwnerlessStage > 0 && stage is > 0 and < 4 &&
                    SSCBondUtility.CanBindNewWithProgress(requestTrainer, pawn),
                TrainingDecision.AdvanceOwnerless => startingOwnerlessStage > 0 && stage > 0,
                _ => false
            };
        }

        // 如果调教员也是本场邀约的对象，可以先批准。邀约收尾后的完整可达性和预约检查仍在派发前执行。
        private bool CanWaitForInvitedTrainer() => invitation?.Partner == requestTrainer &&
            invitation?.OccupiesPartner(pawn) == true && TrainerEligible(requestTrainer) &&
            requestTrainer.Position.InHorDistOf(pawn.Position, RequestRadius);

        internal void Decide(ChoiceLetter_SSCBreakdownTraining letter, TrainingDecision decision)
        {
            if (!IsPendingDecision(letter)) return;
            if (decision == TrainingDecision.None)
            {
                CancelTrainingRequest();
                return;
            }
            if (!CanUseDecision(decision)) return;
            if (!CanApprove(letter, decision))
            {
                // 主人暂时不可用时，已打开的旧对话也不能消费仍然有效的原请求。
                // 无链分支沿用原有撤销规则，在申请间隔后重新寻找可用调教员。
                if (!boundEpisode && IsPendingDecision(letter)) CancelTrainingRequest();
                return;
            }
            if (!IsPendingDecision(letter) || !TrainerIdentityCurrent || !CanUseDecision(decision)) return;
            // 先消费这次按钮权限。关闭信件会触发原版回调，重复点击也不会启动第二个 Job。
            // 等下一个状态 tick 再派发，允许在游戏暂停时稳妥保存已批准的决定。
            approvedDecision = decision;
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
                // 有链请求随同一个主人保留到期限。征召、离图、距离或忙碌只影响 CanApprove，
                // 旧人格或旧绑定则使信件失效。批准一旦提交，下一段逻辑仍按现有规则处理派发失败。
                bool trainerStillApplies = boundEpisode ? requestTrainer == BoundOwner :
                    TrainerEligible(requestTrainer) && requestTrainer.Position.InHorDistOf(pawn.Position, RequestRadius);
                if (ActiveLetter == null || !TrainerIdentityCurrent || !trainerStillApplies) CancelTrainingRequest();
                return;
            }
            if (requestPhase == RequestPhase.Approved)
            {
                // 发作小人或调教员可能仍在已开场的互动中。先等精确的原互动结束，再检查预约及任务占用。
                if (!TrainerIdentityCurrent || !CanUseDecision(approvedDecision) || !TrainerEligible(requestTrainer) ||
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
            // 有链只询问实际主人。主人尚不可用时继续游荡，等待下一轮检查。
            // 无链角色沿用按距离扫描调教员，两个分支共用同一种选择信。
            Pawn trainer = boundEpisode ? BoundOwner : pawn.Map.mapPawns.FreeColonistsSpawned.OrderBy(other =>
                other.Position.DistanceToSquared(pawn.Position)).FirstOrDefault(other => AvailableToTeach(other) && CanApproach(other));
            if (trainer == null || !CanRequestTraining || requestPhase != RequestPhase.None) return;
            // 路径、预约和身体兼容查询可能触发回调。选定主人后再检查当前发作及请求槽。
            if (boundEpisode && (!AvailableToTeach(trainer) || !CanApproach(trainer) ||
                !CanRequestTraining || requestPhase != RequestPhase.None)) return;
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
                requestPhase != RequestPhase.Approved || !CanRequestTraining || !TrainerIdentityCurrent || !CanUseDecision(approvedDecision) ||
                !CanApproach(trainer) || !AvailableToTeach(trainer) || !CanReceiveBreakdownTraining)
            {
                if (requestPhase == RequestPhase.Approved) CancelTrainingRequest();
                return;
            }
            // 路径和预约查询可能调用外部补丁。仍须由原批准启动任务。
            if (requestPhase != RequestPhase.Approved || requestTrainer != trainer ||
                !CanRequestTraining || !TrainerIdentityCurrent || !CanUseDecision(approvedDecision)) return;
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
                CanUseDecision(approvedDecision) &&
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

        internal bool ClaimTrainingSuccess(JobDriver_Training driver, float score)
        {
            if (!CanSettleApprovedTraining(driver)) return false;
            // 普通收益、日期、日常冷却和经验已入账。记录原任务的完成事实和基础分，留待退出时提交正式结果。
            episodeResult = EpisodeResult.Success;
            completedTrainingScore = score;
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
            approvedDecision = TrainingDecision.None;
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

        private const float InitialTrainingProgress = .2f;

        // 共享于写入前后，允许本场按批准结果交接原归属。来源 Hediff 与新链的核对由各个写入入口负责。
        private bool SettlementParticipantsCurrent(Need_Corruption need)
            => SameCarrier() && SSCSlaveBreakdown.IsEligibleSubject(pawn) && SSCIdentityUtility.IsSexSlave(pawn) &&
                TrainerIdentityCurrent && TrainerEligible(requestTrainer) && SSCSlaveBreakdown.GetNeed(pawn) == need;

        /// <summary>只提交玩家批准的结果。此时普通收益已经入账，退出流程仍持有原任务的凭据。</summary>
        private Hediff ApplyTrainingDecision(Need_Corruption need, out int beforeStage, out float before)
        {
            beforeStage = 0;
            before = 0f;
            // 待派发的批准无法直接结算。先校验原进度载体，再核对参与者，最后复查可能被同步通知改动的归属。
            if (need == null || requestPhase != RequestPhase.Dispatched || trainingJobId < 0 || !trainingStarted || !HasCurrentBinding() ||
                !CanUseDecision(approvedDecision) || !SettlementParticipantsCurrent(need) || !HasCurrentBinding())
                return null;

            Hediff original = boundEpisode ? boundChain : startingOwnerless;
            var stage = TrainingOutcomeUtility.GetSexSlaveProgressStage(original);
            beforeStage = stage.Stage;
            before = original?.Severity ?? 0f;
            if (IsBindingDecision(approvedDecision))
            {
                float severity = approvedDecision == TrainingDecision.BindOwnerless ? before : InitialTrainingProgress;
                // 新归属在提交中是预期变化。回调复核双方和原需求，链及来源由绑定服务另行核对。
                return SSCBondUtility.BindNewWithProgress(requestTrainer, pawn, startingOwnerless, severity,
                    () => SettlementParticipantsCurrent(need), out var linked) ? linked : null;
            }
            if (approvedDecision == TrainingDecision.FormOwnerless)
            {
                // 首次成形以获批任务的完成为准。零阶记录可以复用，新记录在挂载前写好起始严重度。
                if (stage.Stage != 0) return null;
                var ownerless = startingOwnerless;
                bool adding = ownerless == null;
                if (adding)
                {
                    var def = DefDatabase<HediffDef>.GetNamedSilentFail(Hediff_SSCOwnerlessSlave.DefName);
                    if (def?.hediffClass != typeof(Hediff_SSCOwnerlessSlave)) return null;
                    ownerless = (Hediff_SSCOwnerlessSlave)HediffMaker.MakeHediff(def, pawn);
                }
                ownerless.Severity = InitialTrainingProgress;
                if (adding && SettlementParticipantsCurrent(need) && HasCurrentBinding()) health.AddHediff(ownerless);
                return SameCarrier() && TrainingOutcomeUtility.GetSexSlaveProgress(pawn) == ownerless ? ownerless : null;
            }

            // 同一场的普通收益发生后才读取当前恶堕。若原载体已被外部推进跨阶，本场额外进度为零。
            int startingStage = boundEpisode ? startingChainStage : startingOwnerlessStage;
            if (original != null && stage.Stage == startingStage && stage.Next.HasValue)
            {
                float next = CorruptionProgressionUtility.CalculateChainProgress(before, need.CurLevel, stage.Next);
                if (next > before)
                {
                    if (original is Hediff_ChainOfSexSlave chain) chain.SetProgressSeverity(pawn, next);
                    else original.Severity = next;
                }
            }
            return SameCarrier() && HasCurrentBinding() && SSCSlaveBreakdown.GetNeed(pawn) == need ? original : null;
        }

        /// <summary>首次成形和等进度绑定可在 SSC 结果提交后尝试原版转奴。纯成长不消耗这次判定。</summary>
        /// <remarks>共用仪式开关，爆发使用普通调教评分及非仪式概率。转奴结果只用于反馈。</remarks>
        private string TryVanillaEnslavement(Need_Corruption need, Hediff written)
        {
            if (approvedDecision is TrainingDecision.AdvanceBound or TrainingDecision.AdvanceOwnerless ||
                SSCMod.settings?.enableRitualEnslavement == false || !ModsConfig.IdeologyActive ||
                !SettlementParticipantsCurrent(need) || TrainingOutcomeUtility.GetSexSlaveProgress(pawn) != written ||
                pawn.IsSlave || pawn.guest == null || pawn.apparel == null || !pawn.IsColonist || need.CurLevel < .2f)
                return null;
            float chance = WillReductionUtility.CalculateEnslaveChance(pawn, completedTrainingScore, need.CurLevel,
                isRitual: false, out _);
            if (!SettlementParticipantsCurrent(need) || TrainingOutcomeUtility.GetSexSlaveProgress(pawn) != written ||
                pawn.IsSlave || !pawn.IsColonist) return null;
            bool converted = Rand.Chance(chance) && GenGuest.TryEnslavePrisoner(requestTrainer, pawn);
            return converted && pawn.IsSlave ? "SSC_BreakdownSuccess_Enslaved" : "SSC_BreakdownSuccess_EnslaveFailed";
        }

        private void PublishEpisodeOutcome()
        {
            if (episodeResult == EpisodeResult.None || outcomeIssued) return;
            // 普通调教已认领成功。在写链、心情与信件之前先认领本次发布，防止同步回调重复发奖。
            // 整个退出流程还持有本场请求的原调教员，本方法返回后才释放相关字段。
            outcomeIssued = true;
            Need_Corruption need = SSCSlaveBreakdown.GetNeed(pawn);
            int beforeStage = 0;
            float before = 0f;
            var progress = episodeResult == EpisodeResult.Success ? ApplyTrainingDecision(need, out beforeStage, out before) : null;
            bool success = progress != null;
            if (success) temporaryIdentity = false;
            else episodeResult = EpisodeResult.Failure;
            // 在原版转奴通知前记录实际完成的归属与阶段。原版身份变化不会改写本场 SSC 结果。
            TaggedString progressText = success ? FormatProgress(progress, beforeStage, before) : default;
            string ownerName = success && IsBindingDecision(approvedDecision) ? SSCBondUtility.GetBoundMaster(pawn)?.LabelShort : null;
            string enslavement = success ? TryVanillaEnslavement(need, progress) : null;
            // 原版转奴可能触发换人格。任何记忆仍只由原人格领取。
            var memories = SameCarrier() && !pawn.Dead ? pawn.needs?.mood?.thoughts?.memories : null;
            var mood = DefDatabase<ThoughtDef>.GetNamedSilentFail(success ? "SSC_BreakdownSuccess" : "SSC_BreakdownFailure");
            if (mood != null)
            {
                // 原版刷新同一 ThoughtDef 的单条成功记忆，并切换到本场分支的描述。
                if (success) memories?.TryGainMemoryFast(mood, startingOwnerlessStage > 0 || boundEpisode ? 1 : 0);
                else memories?.TryGainMemory(mood);
            }
            if (success)
            {
                var opinion = DefDatabase<ThoughtDef>.GetNamedSilentFail("SSC_BreakdownTrainerOpinion");
                if (opinion != null && TrainerIdentityCurrent && SameCarrier())
                    memories?.TryGainMemory(opinion, requestTrainer);
                if (LetterDefOf.PositiveEvent != null && Find.LetterStack != null)
                {
                    string trainerName = requestTrainer?.LabelShort ?? "Unknown".Translate().ToString();
                    bool formed = approvedDecision is TrainingDecision.FormBound or TrainingDecision.FormOwnerless;
                    TaggedString text = (formed ? "SSC_BreakdownSuccess_Text" : "SSC_BreakdownSuccess_BoundText")
                        .Translate(pawn.LabelShort, trainerName) + "\n\n" + progressText;
                    if (ownerName != null) text += "\n" + "SSC_BreakdownSuccess_NewOwner".Translate(ownerName);
                    if (enslavement != null) text += "\n" + enslavement.Translate();
                    Find.LetterStack.ReceiveLetter("SSC_BreakdownSuccess_Label".Translate(), text, LetterDefOf.PositiveEvent, pawn);
                }
            }
            else if (LetterDefOf.NegativeEvent != null && Find.LetterStack != null)
            {
                Find.LetterStack.ReceiveLetter("SSC_BreakdownFailure_Label".Translate(),
                    "SSC_BreakdownFailure_Text".Translate(pawn.LabelShort), LetterDefOf.NegativeEvent, pawn);
            }
        }

        private TaggedString FormatProgress(Hediff progress, int beforeStage, float before)
        {
            int stage = TrainingOutcomeUtility.GetSexSlaveProgressStage(progress).Stage;
            float after = progress.Severity;
            if (approvedDecision is TrainingDecision.FormBound or TrainingDecision.FormOwnerless or TrainingDecision.BindOwnerless)
                return "SSC_BreakdownSuccess_CurrentProgress".Translate(stage, after.ToStringPercent());
            if (stage > beforeStage) return "SSC_BreakdownSuccess_StageAdvanced".Translate(beforeStage, stage);
            if (after > before) return "SSC_BreakdownSuccess_ProgressAdvanced".Translate(stage, before.ToStringPercent(), after.ToStringPercent());
            return "SSC_BreakdownSuccess_StageUnchanged".Translate();
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
                foreach (var decision in state.TrainingChoices())
                {
                    // 开发环境占位，定稿前替换资源文案。实际授权只保存枚举。
                    var accept = new DiaOption(("SSC_BreakdownRequest_Dev_" + decision).Translate()) { resolveTree = true,
                        action = () => MentalState_SSCSlaveBreakdown.CurrentFor(Subject)?.Decide(this, decision) };
                    if (!state.CanApprove(this, decision))
                    {
                        var cooldown = Hediff_SSCBondReleaseAftereffect.Active(Subject);
                        accept.Disable(MentalState_SSCSlaveBreakdown.IsBindingDecision(decision) && cooldown != null
                            ? "SSC_BondRelease_BindingCooldown".Translate(cooldown.LabelInBrackets)
                            : "SSC_BreakdownRequest_Unavailable".Translate());
                    }
                    yield return accept;
                }
                yield return new DiaOption("RejectLetter".Translate()) { resolveTree = true,
                    action = () => MentalState_SSCSlaveBreakdown.CurrentFor(Subject)?.Decide(this,
                        MentalState_SSCSlaveBreakdown.TrainingDecision.None) };
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
