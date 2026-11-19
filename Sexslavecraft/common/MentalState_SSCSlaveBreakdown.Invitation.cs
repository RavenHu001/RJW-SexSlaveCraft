// AI 辅助生成：发作持有邀约的冷却、双方任务归属和中断后的清理责任。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed partial class MentalState_SSCSlaveBreakdown
    {
        // 实际提出时开始共用冷却。拒绝和成功接单另外按对象记各自的截止时间。
        private const int InvitationInterval = GenDate.TicksPerHour / 2;
        private const int TargetCooldown = GenDate.TicksPerHour;
        // 搜索节流供原版行为树多次重查使用。空搜索不写正式冷却，节流本身也不必随档恢复。
        private const int InvitationScanInterval = 150;
        internal const float InvitationRadius = 15f;
        private long nextInvitationAt;
        private long nextInvitationScanAt;
        private Dictionary<Pawn, long> invitationCooldowns = new Dictionary<Pawn, long>();
        private List<Pawn> cooldownPawns;
        private List<long> cooldownDeadlines;
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
            foreach (Pawn stale in invitationCooldowns.Where(entry => entry.Key.Destroyed || entry.Value <= now)
                .Select(entry => entry.Key).ToArray())
                invitationCooldowns.Remove(stale);
            return true;
        }

        internal bool CanInvite(Pawn partner)
        {
            // 求用的实际发起者是受邀小人，按该方向检查许可。
            if (partner?.Spawned != true || partner == pawn || !pawn.Spawned || partner.Map != pawn.Map ||
                !partner.RaceProps.Humanlike || partner.Dead || partner.Downed || partner.Drafted ||
                partner.InMentalState || partner.IsFighting() || !partner.Awake() || partner.jobs == null ||
                SSCPersonalityRuntimeUtility.IsMutating(partner) ||
                !pawn.Position.InHorDistOf(partner.Position, InvitationRadius) ||
                partner.Position.Fogged(pawn.Map) ||
                (invitationCooldowns.TryGetValue(partner, out long deadline) && SSCSlaveBreakdown.Now < deadline))
                return false;
            Area area = pawn.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            if (area != null && area.TrueCount != 0 && !area[partner.Position]) return false;
            return !partner.HostileTo(pawn) && !pawn.HostileTo(partner) &&
                (!partner.IsDesignatedHero() || RJWSettings.RPG_hero_control_AI_sex) &&
                SSCBreakdownInvitation.IsAllowed(pawn, partner);
        }

        /// <summary>确认双方能配对才登记本次询问的冷却，接受后才建立有精确任务归属的邀请。</summary>
        internal bool TryRecordInvitation(Pawn partner, object personality, bool accepted, out Job job)
        {
            job = null;
            int now = SSCSlaveBreakdown.Now;
            if (now < nextInvitationAt || !CanSeekInvitation || !CanInvite(partner)) return false;
            if ((accepted && (SSCBreakdownInvitation.WaitDef == null || JobDriver_SSCBreakdownInvitedUse.Def == null)) ||
                !SSCBreakdownInvitation.HasCompatibleInteraction(partner, pawn) ||
                !SSCPersonalityRuntimeUtility.IsCurrent(partner, personality) || !CanSeekInvitation) return false;

            nextInvitationAt = (long)now + InvitationInterval;
            if (!accepted)
            {
                invitationCooldowns[partner] = (long)now + TargetCooldown;
                return true;
            }

            // 行为树只返回当前小人的任务。求用先装本人的等待，追踪器完成安装后立刻在对方身上派发。
            invitation = new SSCBreakdownInvitation(partner, personality);
            job = invitation.CreateWait(pawn);
            behaviorIssuedAt = now;
            return true;
        }

        // 以编号认领，然后由调用方复查对象与驱动。目标被替换时，仍由旧记录负责撤销这份 Job。
        internal SSCBreakdownInvitation InvitationFor(Job job) => OwnsBehaviorJob(job) ? invitation : null;

        internal SSCBreakdownInvitation WaitingInvitationFor(Job job)
            => invitation is { } active && active.OwnsWait(pawn, job) &&
                !HasApprovedTraining && CanIssueBehavior && active.ParticipantsAreCurrent(pawn) ? active : null;

        internal void FinishInvitedUseWaitInstallation(SSCBreakdownInvitation accepted, int id)
        {
            if (invitation != accepted || behaviorJobId >= 0) return;
            if (pawn.CurJob?.loadID == id && accepted.OwnsWait(pawn, pawn.CurJob)) DispatchInvitedUse(accepted);
            else CancelInvitation();
        }

        // behaviorJobId 始终是发起任务。求用受方的 Job 只保存在邀请记录中，用于保持当前行为和精确清理。
        private Pawn BehaviorJobOwner => invitation?.Partner ?? pawn;
        internal SSCBreakdownInvitation InvitedUseReceiverFor(Job job) => job != null && pawn.CurJob == job &&
            invitation?.HasReceiver(pawn) == true ? invitation : null;

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
                // 安装回调是唯一派发入口。未能装上候选的场次在下一次刷新撤销。
                if (SSCSlaveBreakdown.Now > behaviorIssuedAt) CancelInvitation();
                return;
            }
            // 读档只承接存档指向的任务。安装失败或被替换的编号沿用行为任务的缺失清理。
            RetireMissingBehaviorJob();
            if (invitation != null && OwnsBehaviorJob(BehaviorJobOwner.CurJob) &&
                (BehaviorJobOwner.jobs.curDriver is not JobDriver_SexBaseInitiator driver ||
                !invitation.MatchesDirection(pawn, driver) ||
                (InvitationStarted ? !invitation.HasReceiver(pawn) : !invitation.CanPrepareRecipient(pawn))))
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
                if (def == null || !CanIssueBehavior || !accepted.CanDispatchInvitedUse(pawn))
                {
                    CancelIfCurrentInvitation(accepted);
                    return;
                }
                if (invitation != accepted || behaviorJobId >= 0) return;
                // 身体和许可查询可能触发同步回调，只复核参与者和这次的等待，不检查受邀方原任务。
                if (HasApprovedTraining || !CanIssueBehavior || !accepted.ParticipantsAreCurrent(pawn) ||
                    !accepted.OwnsWait(pawn, pawn.CurJob))
                {
                    CancelIfCurrentInvitation(accepted);
                    return;
                }

                // StartJob 会立即预约并可能进入 Toil。先持久化发起任务编号，回调才能识别这次授权。
                Job job = JobMaker.MakeJob(def, pawn);
                SetBehaviorJob(job);
                int id = behaviorJobId;
                bool installed = false;
                try
                {
                    actor.jobs.StartJob(job, JobCondition.InterruptForced, preToilReservationsCanFail: true);
                    installed = actor.CurJob?.loadID == id;
                }
                finally
                {
                    // 预约失败或兼容回调在安装中抛错时也释放本场，保留已经发生的邀约冷却。
                    if (!installed && invitation == accepted && OwnsBehaviorJobId(id)) CancelInvitation();
                }
                // 接受提示对应实际安装。预约、原生初始化失败或同场撤销时，等待由上面的清理释放。
                if (installed && invitation == accepted)
                {
                    invitationCooldowns[actor] = (long)SSCSlaveBreakdown.Now + TargetCooldown;
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
                        "SSC_BreakdownInvitedUse_Accepted".Translate(actor.LabelShort).ToString(), 3f);
                }
            }
            finally { dispatchingInvitedUse = false; }
        }

        internal static bool CanReachInvitedUse(Pawn actor, Pawn subject)
            => !subject.IsForbidden(actor) && actor.CanReach(subject, PathEndMode.OnCell, Danger.Some);

        private void CancelInvitation()
        {
            SSCBreakdownInvitation cancelled = invitation;
            if (cancelled == null) return;
            Pawn owner = BehaviorJobOwner;
            int id = behaviorJobId;
            if (SSCLog.VerboseEnabled)
                SSCLog.Verbose($"[SSC Breakdown] Invitation ended before completion. " +
                    $"Actor={owner.LabelShort} ({owner.gender}), subject={pawn.LabelShort}, " +
                    $"actorJob={owner.CurJob?.def?.defName}, subjectJob={pawn.CurJob?.def?.defName}.");
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
            // 拒绝与接受共享同一份对象截止时间。沿用原保存键，读档不重计时或补发提示。
            Scribe_Values.Look(ref nextInvitationAt, "sscBreakdownNextInvitationAt", 0L);
            Scribe_Collections.Look(ref invitationCooldowns, "sscBreakdownInvitationRefusals", LookMode.Reference,
                LookMode.Value, ref cooldownPawns, ref cooldownDeadlines, logNullErrors: false);
            Scribe_Deep.Look(ref invitation, "sscBreakdownInvitation");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                invitationCooldowns ??= new Dictionary<Pawn, long>();
        }
    }

    /// <summary>随发作保存接收、移动和等待的归属。即使发起 Job 在读档时已丢失，也能释放原任务。</summary>
    internal sealed class SSCBreakdownInvitation : IExposable
    {
        private const string WaitDefName = "SSC_BreakdownInvitationWait";
        internal static JobDef WaitDef => DefDatabase<JobDef>.GetNamedSilentFail(WaitDefName);
        internal static bool IsWait(Job job) => job?.def?.defName == WaitDefName;

        // partner 始终是受邀对象。求用时该角色在 RJW 中是发起者，实际接收者是发作小人。
        private Pawn partner;
        // 接收 Job 可以先于发起 Job 恢复或消失。编号和反向目标共同定位这次安装。
        private int receiverJobId = -1;
        private object personality;
        private bool personalityInvalidated;
        private readonly SSCRestrictionJobPreparation preparation = new SSCRestrictionJobPreparation();

        public SSCBreakdownInvitation() { }

        internal SSCBreakdownInvitation(Pawn partner, object personality)
        {
            this.partner = partner;
            this.personality = personality;
        }

        internal Pawn Partner => partner;
        internal Job CreateWait(Pawn subject)
        {
            var job = JobMaker.MakeJob(WaitDef, subject);
            job.expiryInterval = SSCSlaveBreakdown.DurationTicks;
            preparation.Register(subject, job.loadID);
            return job;
        }

        internal bool OwnsWait(Pawn subject, Job job)
            => IsWait(job) && job.targetA.Pawn == subject && preparation.Owns(subject, job);

        /// <summary>统一许可使用原生任务的实际方向。邀约同意不改变 SSC 的许可规则。</summary>
        internal static bool IsAllowed(Pawn subject, Pawn partner)
            => SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(partner, subject, SSCInteractionKind.Forced, true)).Allowed;

        internal static bool HasCompatibleInteraction(Pawn actor, Pawn subject)
        {
            // can_rape 只判断发起者自身。用 RJW 的配对查询确认原驱动确实有可选动作。
            if (SexInteractionFinder.FindInteractions(new SexProps(actor, subject) { isRape = true }).Count > 0)
                return true;
            if (SSCLog.VerboseEnabled)
                SSCLog.Verbose($"[SSC Breakdown] No compatible RJW interaction for {actor.LabelShort} " +
                    $"({actor.gender}) and {subject.LabelShort} ({subject.gender}).");
            return false;
        }

        internal bool ParticipantsAreCurrent(Pawn subject) => subject.Spawned && !subject.Downed && !subject.Drafted &&
            partner?.Spawned == true &&
            !partner.Dead && !partner.Downed && !partner.Drafted && !partner.InMentalState &&
            partner.Map == subject.Map && !personalityInvalidated && SSCPersonalityRuntimeUtility.IsCurrent(partner, personality) &&
            !SSCPersonalityRuntimeUtility.IsMutating(partner);

        internal bool MatchesDirection(Pawn subject, JobDriver_SexBaseInitiator driver)
            => driver is JobDriver_SSCBreakdownInvitedUse && driver.pawn == partner && driver.Partner == subject;

        internal bool HasStarted(JobDriver_SexBaseInitiator driver) => SSCRestrictionJobGuard.HasStartedScene(driver,
            SSCInteractionKind.Forced);

        private JobDriver_SexBaseReciever Receiver(Pawn subject)
        {
            return xxx.gettin_raped != null && subject.CurJob?.def == xxx.gettin_raped &&
                subject.jobs?.curDriver is JobDriver_SexBaseRecieverRaped receiver && receiver.Partner == partner
                ? receiver : null;
        }

        internal bool HasReceiver(Pawn subject) => receiverJobId >= 0 && Receiver(subject)?.job?.loadID == receiverJobId;
        internal bool OccupiesPartner(Pawn subject)
            => partner != null && MentalState_SSCSlaveBreakdown.CurrentFor(subject)?.InvitationFor(partner.CurJob) == this;

        internal bool CanDispatchInvitedUse(Pawn subject)
            => ParticipantsAreCurrent(subject) && subject.Awake() && partner.Awake() &&
                !partner.IsFighting() && !subject.HostileTo(partner) && !partner.HostileTo(subject) &&
                partner.jobs != null && OwnsWait(subject, subject.CurJob) && CanPerformInvitedUse(subject);

        // 排队任务没有取代当前等待或接收器，由原跟踪器保留。
        internal bool CanPrepareRecipient(Pawn subject) => OwnsWait(subject, subject.CurJob) || HasReceiver(subject);

        private bool CanPerformInvitedUse(Pawn subject)
            => xxx.can_rape(partner, forced: true) && CasualSex_Helper.CanHaveSex(subject) &&
                IsAllowed(subject, partner);

        private bool MayPrepare(Pawn subject)
            => !partner.IsFighting() && partner.Awake() &&
                CanPerformInvitedUse(subject) &&
                ParticipantsAreCurrent(subject) && CanPrepareRecipient(subject);

        internal bool CanStartScene(MentalState_SSCSlaveBreakdown state, Pawn subject, JobDriver_SexBaseInitiator driver)
            => state.CanIssueBehavior && !state.HasApprovedTraining && MatchesDirection(subject, driver) &&
                ParticipantsAreCurrent(subject) && HasReceiver(subject) && HasCompatibleInteraction(partner, subject);

        internal void RunNativeStep(MentalState_SSCSlaveBreakdown state, Pawn subject, JobDriver_SexBaseInitiator driver, Action original,
            bool requiresReceiver)
        {
            Pawn actor = driver.pawn;
            bool Current() => state.InvitationFor(driver.job) == this && actor.jobs?.curDriver == driver &&
                MatchesDirection(subject, driver) && state.CanIssueBehavior;

            bool started = HasStarted(driver);
            // 外部模组或读档可能替换原受方任务。发起方先进入后续 Toil 时，也不能把旧结果结算给后来接手的工作。
            if (!Current() || !ParticipantsAreCurrent(subject) ||
                (started && !HasReceiver(subject)) ||
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
            var before = SSCRestrictionJobPreparation.Snapshot(subject);
            int receiverBefore = Receiver(subject)?.job?.loadID ?? -1;
            try { original?.Invoke(); }
            finally
            {
                preparation.RecordNew(subject, before);
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
                    receiver.parteners?.Remove(partner);
                    if (receiver.parteners == null || receiver.parteners.Count == 0)
                        subject.jobs.EndCurrentJob(succeeded ? JobCondition.Succeeded : JobCondition.Incompletable,
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
            Scribe_Values.Look(ref receiverJobId, "receiverJobId", -1);
            Scribe_Values.Look(ref personalityInvalidated, "personalityInvalidated", false);
            preparation.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !personalityInvalidated)
                personality = SSCPersonalityRuntimeUtility.Capture(partner);
        }
    }
}
