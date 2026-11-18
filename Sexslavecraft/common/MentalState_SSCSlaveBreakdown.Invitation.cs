// AI 辅助生成：发作持有邀约的冷却、双方任务归属和中断后的清理责任。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed partial class MentalState_SSCSlaveBreakdown
    {
        // 实际提出时才启动本场共用冷却；对象明确拒绝才记录该身体的额外截止时间。
        private const int InvitationInterval = GenDate.TicksPerHour / 2;
        private const int RefusalInterval = GenDate.TicksPerHour;
        // 搜索节流供原版行为树多次重查使用。空搜索不写正式冷却，节流本身也不必随档恢复。
        private const int InvitationScanInterval = 150;
        internal const float InvitationRadius = 15f;
        private long nextInvitationAt;
        private long nextInvitationScanAt;
        private Dictionary<Pawn, long> invitationRefusals = new Dictionary<Pawn, long>();
        private List<Pawn> refusalPawns;
        private List<long> refusalDeadlines;
        private SSCBreakdownInvitation invitation;
        // 同步寻路或 StartJob 可重入精神状态。持有此标记的调用负责派发，存档仍以已写入的任务编号为准。
        private bool dispatchingInvitedUse;

        // 只在候选调用栈内设置。吸引力、权限和提示回调都可能触发原版再次询问行为树。
        internal bool SelectingInvitation { get; set; }

        // 等待选择信时仍可邀约；已接受的候选从返回给原版的那一刻起，占用与开场相同的任务槽。
        private bool CanSeekInvitation => openingPhase == OpeningPhase.Wander && !HasApprovedTraining &&
            !HasBehaviorJob && pawn.CurJob == null && CanIssueBehavior;

        internal bool TryBeginInvitationScan()
        {
            int now = SSCSlaveBreakdown.Now;
            if (SelectingInvitation || now < nextInvitationAt || now < nextInvitationScanAt || !CanSeekInvitation)
                return false;
            nextInvitationScanAt = (long)now + InvitationScanInterval;
            foreach (Pawn stale in invitationRefusals.Where(entry => entry.Key.Destroyed || entry.Value <= now)
                .Select(entry => entry.Key).ToArray())
                invitationRefusals.Remove(stale);
            return true;
        }

        internal bool CanInvite(Pawn partner, bool invitedUse = false)
        {
            // 两种邀约先共用范围、拒绝冷却和工作占用。普通邀约使用 RJW 的候选池，求用另按实际发起方向检查。
            if (partner?.Spawned != true || partner == pawn || !pawn.Spawned || partner.Map != pawn.Map ||
                !partner.RaceProps.Humanlike || partner.Dead || partner.Downed || partner.Drafted ||
                partner.InMentalState || partner.IsFighting() || !partner.Awake() || partner.jobs == null ||
                partner.jobs.jobQueue.Count != 0 || partner.CurJob?.playerForced == true ||
                SSCPersonalityRuntimeUtility.IsMutating(partner) ||
                !pawn.Position.InHorDistOf(partner.Position, InvitationRadius) ||
                partner.Position.Fogged(pawn.Map) ||
                (invitationRefusals.TryGetValue(partner, out long deadline) && SSCSlaveBreakdown.Now < deadline))
                return false;
            Area area = pawn.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            if (area != null && area.TrueCount != 0 && !area[partner.Position]) return false;
            return invitedUse
                ? (!partner.HostileTo(pawn) && !pawn.HostileTo(partner) &&
                   (!partner.IsDesignatedHero() || RJWSettings.RPG_hero_control_AI_sex) &&
                   (partner.CurJob == null || partner.CurJob.def.casualInterruptible) &&
                   SSCBreakdownInvitation.IsAllowed(pawn, partner, invitedUse: true))
                : SSCBreakdownInvitation.IsAllowed(pawn, partner, invitedUse: false);
        }

        /// <summary>搜索失败不计时。实际询问时先登记冷却，接受后才建立一场有精确任务归属的邀请。</summary>
        internal bool TryRecordInvitation(Pawn partner, object personality, bool accepted, out Job job,
            bool invitedUse = false)
        {
            job = null;
            int now = SSCSlaveBreakdown.Now;
            if ((accepted && (invitedUse ? JobDriver_SSCBreakdownInvitedUse.Def == null : xxx.quick_sex == null)) ||
                now < nextInvitationAt ||
                !CanSeekInvitation || !CanInvite(partner, invitedUse) ||
                !SSCPersonalityRuntimeUtility.IsCurrent(partner, personality) || !CanSeekInvitation) return false;

            nextInvitationAt = (long)now + InvitationInterval;
            if (!accepted)
            {
                invitationRefusals[partner] = (long)now + RefusalInterval;
                return true;
            }

            // 普通邀约由当前行为树安装。求用先占据邀约槽，下一次精神状态更新才向受邀方的任务追踪器派发。
            invitation = new SSCBreakdownInvitation(partner, personality, invitedUse);
            if (!invitedUse)
            {
                job = JobMaker.MakeJob(xxx.quick_sex, partner);
                SetBehaviorJob(job);
            }
            return true;
        }

        // 以编号认领，然后由调用方复查对象与驱动。目标被替换时，仍由旧记录负责撤销这份 Job。
        internal SSCBreakdownInvitation InvitationFor(Job job) => OwnsBehaviorJob(job) ? invitation : null;

        // behaviorJobId 始终是发起任务。求用受方的 Job 只保存在邀请记录中，用于保持当前行为和精确清理。
        private Pawn BehaviorJobOwner => invitation?.Initiator(pawn) ?? pawn;
        internal bool WaitingForInvitedUse => invitation?.InvitedUse == true && !HasApprovedTraining;
        internal SSCBreakdownInvitation InvitedUseReceiverFor(Job job) => job != null && pawn.CurJob == job &&
            invitation?.InvitedUse == true && invitation.HasReceiver(pawn) ? invitation : null;

        internal void CancelIfCurrentInvitation(SSCBreakdownInvitation expected)
        {
            if (invitation == expected) CancelInvitation();
        }

        // 只有原 RJW Start 确实执行，才允许既有互动先收尾。行走、预约和接收器安装都还在准备期。
        private bool InvitationStarted => invitation != null && OwnsBehaviorJob(BehaviorJobOwner.CurJob) &&
            BehaviorJobOwner.jobs?.curDriver is JobDriver_SexBaseInitiator driver && invitation.HasStarted(driver);

        internal void CancelUnstartedInvitation()
        {
            if (invitation != null && !InvitationStarted) CancelInvitation();
        }

        private void TickInvitation()
        {
            if (invitation == null) return;
            if (!invitation.ParticipantsAreCurrent(pawn) || (HasApprovedTraining && !InvitationStarted))
            {
                CancelInvitation();
                return;
            }
            if (behaviorJobId < 0)
            {
                // 只有求用有「已接受、未交给原版」的持久阶段。已有编号的档一律走恢复或撤销。
                if (invitation.InvitedUse) DispatchInvitedUse(invitation);
                else CompleteBehaviorJob();
                return;
            }
            // 读档只承接存档指向的任务。安装失败或被替换的编号沿用行为任务的缺失清理。
            RetireMissingBehaviorJob();
            if (invitation != null && OwnsBehaviorJob(BehaviorJobOwner.CurJob) &&
                (BehaviorJobOwner.jobs.curDriver is not JobDriver_SexBaseInitiator driver ||
                !invitation.MatchesDirection(pawn, driver) ||
                (InvitationStarted ? !invitation.HasReceiver(pawn) : !invitation.HoldsOriginalWork(pawn))))
                CancelInvitation();
        }

        private void DispatchInvitedUse(SSCBreakdownInvitation accepted)
        {
            if (dispatchingInvitedUse) return;
            dispatchingInvitedUse = true;
            try
            {
                Pawn actor = accepted.Partner;
                JobDef def = JobDriver_SSCBreakdownInvitedUse.Def;
                if (def == null || !CanIssueBehavior || !accepted.CanDispatchInvitedUse(pawn) ||
                    !CanReachInvitedUse(actor, pawn))
                {
                    if (invitation == accepted) CancelInvitation();
                    return;
                }
                // 身体、许可与寻路查询可同步改变任务或人格。实际打断外部工作前，重新匹配两端的原工作。
                if (!accepted.HoldsDispatchWork(pawn))
                {
                    CancelIfCurrentInvitation(accepted);
                    return;
                }
                if (invitation != accepted || behaviorJobId >= 0 || HasApprovedTraining || !CanIssueBehavior) return;

                // StartJob 会立即预约并可能进入 Toil。先持久化发起任务编号，回调才能识别这次授权。
                Job job = JobMaker.MakeJob(def, pawn);
                SetBehaviorJob(job);
                int id = behaviorJobId;
                bool installed = false;
                try
                {
                    actor.jobs.StartJob(job, JobCondition.InterruptOptional, preToilReservationsCanFail: true);
                    installed = actor.CurJob?.loadID == id;
                }
                finally
                {
                    // 预约失败或兼容回调在安装中抛错时也释放本场，保留已经发生的邀约冷却。
                    if (!installed && invitation == accepted && OwnsBehaviorJobId(id)) CancelInvitation();
                }
            }
            finally { dispatchingInvitedUse = false; }
        }

        internal static bool CanReachInvitedUse(Pawn actor, Pawn subject)
            => !subject.IsForbidden(actor) && actor.CanReserveAndReach(subject, PathEndMode.OnCell, Danger.Some, 1, 0);

        private void CancelInvitation()
        {
            SSCBreakdownInvitation cancelled = invitation;
            if (cancelled == null) return;
            Pawn owner = BehaviorJobOwner;
            int id = behaviorJobId;
            try
            {
                if (OwnsBehaviorJob(owner.CurJob) && owner.jobs?.curDriver is { ended: false })
                    owner.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
            }
            finally
            {
                // 正常情况下原版结束钩子已经清理。没有安装过任务、异常或退场回调缺失时仍由本场兜底。
                if (invitation == cancelled && behaviorJobId == id) CompleteBehaviorJob();
            }
        }

        private void ExposeInvitations()
        {
            // 持久化绝对截止时间和拒绝对象引用。加载只接回原邀请，不调用候选逻辑或补发提示。
            Scribe_Values.Look(ref nextInvitationAt, "sscBreakdownNextInvitationAt", 0L);
            Scribe_Collections.Look(ref invitationRefusals, "sscBreakdownInvitationRefusals", LookMode.Reference,
                LookMode.Value, ref refusalPawns, ref refusalDeadlines, logNullErrors: false);
            Scribe_Deep.Look(ref invitation, "sscBreakdownInvitation");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                invitationRefusals ??= new Dictionary<Pawn, long>();
        }
    }

    /// <summary>随发作保存接收、移动和等待的归属。即使发起 Job 在读档时已丢失，也能释放原任务。</summary>
    internal sealed class SSCBreakdownInvitation : IExposable
    {
        // partner 始终是受邀对象。求用时该角色在 RJW 中是发起者，实际接收者是发作小人。
        private Pawn partner;
        private bool invitedUse;
        // 原工作只用来确认对方没有在等候时被其他请求接管，永远不是这一场的清理对象。
        private int originalJobId = -1;
        // 接收 Job 可以先于发起 Job 恢复或消失。编号和反向目标共同定位这次安装。
        private int receiverJobId = -1;
        private object personality;
        private bool personalityInvalidated;
        private readonly SSCRestrictionJobPreparation preparation = new SSCRestrictionJobPreparation();

        public SSCBreakdownInvitation() { }

        internal SSCBreakdownInvitation(Pawn partner, object personality, bool invitedUse)
        {
            this.partner = partner;
            this.personality = personality;
            this.invitedUse = invitedUse;
            originalJobId = partner.CurJob?.loadID ?? -1;
        }

        internal Pawn Partner => partner;
        internal bool InvitedUse => invitedUse;
        internal Pawn Initiator(Pawn subject) => invitedUse ? partner : subject;
        internal Pawn Recipient(Pawn subject) => invitedUse ? subject : partner;

        /// <summary>统一许可使用原生任务的实际方向。邀约同意不改变 SSC 的许可规则。</summary>
        internal static bool IsAllowed(Pawn subject, Pawn partner, bool invitedUse)
            => SSCRestrictionPolicy.Evaluate(invitedUse
                ? new SSCRestrictionRequest(partner, subject, SSCInteractionKind.Forced, true)
                : new SSCRestrictionRequest(subject, partner, SSCInteractionKind.Consensual, true)).Allowed;

        internal bool ParticipantsAreCurrent(Pawn subject) => subject.Spawned && !subject.Downed && !subject.Drafted &&
            partner?.Spawned == true &&
            !partner.Dead && !partner.Downed && !partner.Drafted && !partner.InMentalState &&
            partner.Map == subject.Map && !personalityInvalidated && SSCPersonalityRuntimeUtility.IsCurrent(partner, personality) &&
            !SSCPersonalityRuntimeUtility.IsMutating(partner);

        internal bool MatchesDirection(Pawn subject, JobDriver_SexBaseInitiator driver)
            => driver.pawn == Initiator(subject) && driver.Partner == Recipient(subject) &&
                (invitedUse ? driver is JobDriver_SSCBreakdownInvitedUse : driver is JobDriver_SexQuick);

        internal bool HasStarted(JobDriver_SexBaseInitiator driver) => SSCRestrictionJobGuard.HasStartedScene(driver,
            invitedUse ? SSCInteractionKind.Forced : SSCInteractionKind.Consensual);

        private JobDriver_SexBaseReciever Receiver(Pawn subject)
        {
            Pawn recipient = Recipient(subject);
            JobDef def = invitedUse ? xxx.gettin_raped : xxx.getting_quickie;
            return def != null && recipient?.CurJob?.def == def &&
                recipient.jobs?.curDriver is JobDriver_SexBaseReciever receiver && receiver.Partner == Initiator(subject)
                ? receiver : null;
        }

        internal bool HasReceiver(Pawn subject) => receiverJobId >= 0 && Receiver(subject)?.job?.loadID == receiverJobId;
        internal bool OccupiesPartner(Pawn subject) => invitedUse
            ? partner != null && MentalState_SSCSlaveBreakdown.CurrentFor(subject)?.InvitationFor(partner.CurJob) == this
            : HasReceiver(subject) || preparation.Owns(partner, partner?.CurJob);

        internal bool CanDispatchInvitedUse(Pawn subject)
            => HoldsDispatchWork(subject) && CanPerformInvitedUse(subject);

        internal bool HoldsDispatchWork(Pawn subject)
            => invitedUse && ParticipantsAreCurrent(subject) && subject.Awake() && partner.Awake() &&
                !partner.IsFighting() && !subject.HostileTo(partner) && !partner.HostileTo(subject) &&
                partner.jobs?.jobQueue.Count == 0 && (partner.CurJob?.loadID ?? -1) == originalJobId &&
                (partner.CurJob == null || (!partner.CurJob.playerForced && partner.CurJob.def.casualInterruptible)) &&
                HoldsOriginalWork(subject);

        internal bool HoldsOriginalWork(Pawn subject)
        {
            Pawn recipient = Recipient(subject);
            if (recipient?.jobs == null) return false;
            // RJW 走位准备可能 StopAll。发现任何新排队指令就撤销邀约，避免清掉后来取得的工作。
            foreach (QueuedJob queued in recipient.jobs.jobQueue)
                if (!preparation.Owns(recipient, queued.job)) return false;
            Job current = recipient.CurJob;
            // 发作小人等对方走来的时候可以处于空闲或自己的等待工作。进食、休息及玩家新命令不由邀约接管。
            bool canReplace = invitedUse
                ? current == null || (!current.playerForced && current.jobGiver is JobGiver_SSCSlaveBreakdownWander)
                : (current?.loadID ?? -1) == originalJobId;
            return canReplace || preparation.Owns(recipient, current) || HasReceiver(subject);
        }

        private bool CanPerformInvitedUse(Pawn subject)
            => xxx.can_rape(partner, forced: true) && CasualSex_Helper.CanHaveSex(subject) &&
                IsAllowed(subject, partner, invitedUse: true);

        private bool MayPrepare(Pawn subject)
            => !partner.IsFighting() && partner.Awake() &&
                (invitedUse ? CanPerformInvitedUse(subject) :
                    IsAllowed(subject, partner, invitedUse: false) && CasualSex_Helper.CanTargetHookup(subject, partner)) &&
                ParticipantsAreCurrent(subject) && HoldsOriginalWork(subject) &&
                (!HasReceiver(subject) || subject.Position.DistanceToSquared(partner.Position) <= 1f);

        internal bool CanStartScene(MentalState_SSCSlaveBreakdown state, Pawn subject, JobDriver_SexBaseInitiator driver)
            => state.CanIssueBehavior && !state.HasApprovedTraining && MatchesDirection(subject, driver) &&
                ParticipantsAreCurrent(subject) && HasReceiver(subject) &&
                subject.Position.DistanceToSquared(partner.Position) <= 1f;

        internal void RunNativeStep(MentalState_SSCSlaveBreakdown state, Pawn subject, JobDriver_SexBaseInitiator driver, Action original,
            bool requiresReceiver)
        {
            Pawn actor = driver.pawn;
            bool Current() => state.InvitationFor(driver.job) == this && actor.jobs?.curDriver == driver &&
                MatchesDirection(subject, driver) && state.CanIssueBehavior;

            bool started = HasStarted(driver);
            // 外部模组或读档可能替换原受方任务。发起方先进入后续 Toil 时，也不能把旧结果结算给后来接手的工作。
            if (!Current() || !ParticipantsAreCurrent(subject) ||
                (started && invitedUse && !HasReceiver(subject)) ||
                (!started && (state.HasApprovedTraining || !MayPrepare(subject) ||
                    (requiresReceiver && !CanStartScene(state, subject, driver)))) || !Current())
            {
                SSCRestrictionJobGuard.AbortBreakdownInvitation(driver);
                return;
            }
            if (started)
            {
                // 原场景已经取得开始凭据，尾声由 RJW 处理。尾声创建的原生后续工作不归邀约准备所有。
                original?.Invoke();
                return;
            }

            // 原驱动会同步安装准备或接收工作。按照真实接收方取快照，只认领这次新增且指回原发起者的工作。
            Pawn recipient = Recipient(subject);
            var before = SSCRestrictionJobPreparation.Snapshot(recipient);
            int receiverBefore = Receiver(subject)?.job?.loadID ?? -1;
            try { original?.Invoke(); }
            finally
            {
                preparation.RecordNew(recipient, before);
                int after = Receiver(subject)?.job?.loadID ?? -1;
                if (after != receiverBefore && after >= 0) receiverJobId = after;
                // 原生回调可能在同步撤销后才返回。迟到的任务仍由原邀约负责清理。
                if (!Current()) Release(subject);
            }
        }

        internal void Release(Pawn subject, bool succeeded = false)
        {
            int id = receiverJobId;
            receiverJobId = -1;
            try { preparation.Cleanup(); }
            finally
            {
                if (id >= 0 && Receiver(subject) is { } receiver && receiver.job.loadID == id)
                {
                    // 精确释放自己的关联，不能替同一个接收器里的其他参与者结束工作。
                    receiver.parteners?.Remove(Initiator(subject));
                    if (receiver.parteners == null || receiver.parteners.Count == 0)
                        Recipient(subject).jobs.EndCurrentJob(succeeded ? JobCondition.Succeeded : JobCondition.Incompletable,
                            startNewJob: false);
                }
            }
        }

        public void ExposeData()
        {
            // 人格换入和取消可能相隔一帧。若在此期间存档，保留撤权事实及原任务编号，读档只能清理。
            if (Scribe.mode == LoadSaveMode.Saving && !SSCPersonalityRuntimeUtility.IsCurrent(partner, personality))
                personalityInvalidated = true;
            Scribe_References.Look(ref partner, "partner");
            Scribe_Values.Look(ref invitedUse, "invitedUse", false);
            Scribe_Values.Look(ref originalJobId, "originalJobId", -1);
            Scribe_Values.Look(ref receiverJobId, "receiverJobId", -1);
            Scribe_Values.Look(ref personalityInvalidated, "personalityInvalidated", false);
            preparation.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !personalityInvalidated)
                personality = SSCPersonalityRuntimeUtility.Capture(partner);
        }
    }
}
