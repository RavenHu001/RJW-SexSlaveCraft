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

        internal bool CanInvite(Pawn partner)
        {
            // RJW 已处理可达性与候选类型。SSC 在此额外核对范围、工作占用、拒绝冷却和双方的实际方向。
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
            return (area == null || area.TrueCount == 0 || area[partner.Position]) &&
                SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(pawn, partner, SSCInteractionKind.Consensual, true)).Allowed;
        }

        /// <summary>搜索失败不计时。实际询问时先登记冷却，接受后才建立一场有精确任务归属的邀请。</summary>
        internal bool TryRecordInvitation(Pawn partner, object personality, bool accepted, out Job job)
        {
            job = null;
            int now = SSCSlaveBreakdown.Now;
            if ((accepted && xxx.quick_sex == null) || now < nextInvitationAt || !CanSeekInvitation || !CanInvite(partner) ||
                !SSCPersonalityRuntimeUtility.IsCurrent(partner, personality) || !CanSeekInvitation) return false;

            nextInvitationAt = (long)now + InvitationInterval;
            if (!accepted)
            {
                invitationRefusals[partner] = (long)now + RefusalInterval;
                return true;
            }

            // 先登记发起者和对象的原工作，再把任务交给原版预约。即使预约失败，邀约也已发生。
            job = JobMaker.MakeJob(xxx.quick_sex, partner);
            invitation = new SSCBreakdownInvitation(partner, personality);
            SetBehaviorJob(job);
            return true;
        }

        // 以编号认领，然后由调用方复查对象与驱动。目标被替换时，仍由旧记录负责撤销这份 Job。
        internal SSCBreakdownInvitation InvitationFor(Job job) => OwnsBehaviorJob(job) ? invitation : null;

        // 只有原 RJW Start 确实执行，才允许既有互动先收尾。行走、预约和接收器安装都还在准备期。
        private bool InvitationStarted => invitation != null && OwnsBehaviorJob(pawn.CurJob) &&
            pawn.jobs.curDriver is JobDriver_SexQuick driver &&
            SSCRestrictionJobGuard.HasStartedScene(driver, SSCInteractionKind.Consensual);

        internal void CancelUnstartedInvitation()
        {
            if (invitation != null && !InvitationStarted) CancelInvitation();
        }

        private void TickInvitation()
        {
            if (invitation == null) return;
            if (!HasBehaviorJob)
            {
                CompleteBehaviorJob();
                return;
            }
            if (!invitation.PartnerIsCurrent(pawn) || (HasApprovedTraining && !InvitationStarted))
            {
                CancelInvitation();
                return;
            }
            // 读档只承接存档指向的任务。安装失败或被替换的编号沿用行为任务的缺失清理。
            RetireMissingBehaviorJob();
            if (invitation != null && OwnsBehaviorJob(pawn.CurJob) &&
                (pawn.jobs.curDriver is not JobDriver_SexQuick driver ||
                driver.Partner != invitation.Partner ||
                (InvitationStarted ? !invitation.HasReceiver(pawn) : !invitation.HoldsOriginalPartnerWork(pawn))))
                CancelInvitation();
        }

        private void CancelInvitation()
        {
            int id = behaviorJobId;
            try
            {
                if (OwnsBehaviorJob(pawn.CurJob) && pawn.jobs.curDriver is { ended: false })
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
            }
            finally
            {
                // 正常情况下原版结束钩子已经清理。没有安装过任务、异常或退场回调缺失时仍由本场兜底。
                if (OwnsBehaviorJobId(id)) CompleteBehaviorJob();
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
        private Pawn partner;
        // 原工作只用来确认对方没有在等候时被其他请求接管，永远不是这一场的清理对象。
        private int originalJobId = -1;
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
            originalJobId = partner.CurJob?.loadID ?? -1;
        }

        internal Pawn Partner => partner;

        internal bool PartnerIsCurrent(Pawn actor) => actor.Spawned && partner?.Spawned == true &&
            !partner.Dead && !partner.Downed && !partner.Drafted && !partner.InMentalState &&
            partner.Map == actor.Map && !personalityInvalidated && SSCPersonalityRuntimeUtility.IsCurrent(partner, personality) &&
            !SSCPersonalityRuntimeUtility.IsMutating(partner);

        private JobDriver_SexBaseReciever Receiver(Pawn actor)
            => xxx.getting_quickie != null && partner?.CurJob?.def == xxx.getting_quickie &&
                partner?.jobs?.curDriver is JobDriver_SexBaseReciever receiver && receiver.Partner == actor
                ? receiver : null;

        internal bool HasReceiver(Pawn actor) => receiverJobId >= 0 && Receiver(actor)?.job?.loadID == receiverJobId;
        internal bool OccupiesPartner(Pawn actor) => HasReceiver(actor) || preparation.Owns(partner, partner?.CurJob);

        internal bool HoldsOriginalPartnerWork(Pawn actor)
        {
            if (partner?.jobs == null) return false;
            // RJW 走位准备可能 StopAll。发现任何新排队指令就撤销邀约，避免清掉后来取得的工作。
            foreach (QueuedJob queued in partner.jobs.jobQueue)
                if (!preparation.Owns(partner, queued.job)) return false;
            Job current = partner.CurJob;
            return (current?.loadID ?? -1) == originalJobId || preparation.Owns(partner, current) ||
                HasReceiver(actor);
        }

        private bool MayPrepare(Pawn actor) => !partner.IsFighting() && partner.Awake() &&
            SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(actor, partner,
                SSCInteractionKind.Consensual, true)).Allowed && CasualSex_Helper.CanTargetHookup(actor, partner) &&
            PartnerIsCurrent(actor) && HoldsOriginalPartnerWork(actor) &&
            (!HasReceiver(actor) || actor.Position.DistanceToSquared(partner.Position) <= 1f);

        internal bool CanStartScene(MentalState_SSCSlaveBreakdown state, JobDriver_SexQuick driver)
            => state.CanIssueBehavior && !state.HasApprovedTraining && driver.Partner == partner &&
                PartnerIsCurrent(driver.pawn) && HasReceiver(driver.pawn) &&
                driver.pawn.Position.DistanceToSquared(partner.Position) <= 1f;

        internal void RunNativeStep(MentalState_SSCSlaveBreakdown state, JobDriver_SexQuick driver, Action original,
            bool requiresReceiver)
        {
            Pawn actor = driver.pawn;
            bool Current() => state.InvitationFor(driver.job) == this && actor.jobs?.curDriver == driver &&
                driver.Partner == partner && state.CanIssueBehavior;

            bool started = SSCRestrictionJobGuard.HasStartedScene(driver, SSCInteractionKind.Consensual);
            if (!Current() || !PartnerIsCurrent(actor) ||
                (!started && (state.HasApprovedTraining || !MayPrepare(actor) ||
                    (requiresReceiver && !CanStartScene(state, driver)))) || !Current())
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

            // 原 Quickie 会同步创建走位、等待和接收工作。只认领相对于调用前新增、且指向当前发起者的任务。
            var before = SSCRestrictionJobPreparation.Snapshot(partner);
            int receiverBefore = Receiver(actor)?.job?.loadID ?? -1;
            try { original?.Invoke(); }
            finally
            {
                preparation.RecordNew(partner, before);
                int after = Receiver(actor)?.job?.loadID ?? -1;
                if (after != receiverBefore && after >= 0) receiverJobId = after;
                // 原生回调可能在同步撤销后才返回。迟到的任务仍由原邀约负责清理。
                if (!Current()) Release(actor);
            }
        }

        internal void Release(Pawn actor, bool succeeded = false)
        {
            int id = receiverJobId;
            receiverJobId = -1;
            try { preparation.Cleanup(); }
            finally
            {
                if (id >= 0 && Receiver(actor) is { } receiver && receiver.job.loadID == id)
                {
                    // 精确释放自己的关联，不能替同一个接收器里的其他参与者结束工作。
                    receiver.parteners?.Remove(actor);
                    if (receiver.parteners == null || receiver.parteners.Count == 0)
                        partner.jobs.EndCurrentJob(succeeded ? JobCondition.Succeeded : JobCondition.Incompletable,
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
            Scribe_Values.Look(ref originalJobId, "originalJobId", -1);
            Scribe_Values.Look(ref receiverJobId, "receiverJobId", -1);
            Scribe_Values.Look(ref personalityInvalidated, "personalityInvalidated", false);
            preparation.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !personalityInvalidated)
                personality = SSCPersonalityRuntimeUtility.Capture(partner);
        }
    }
}
