// AI 辅助生成：开场和游荡任务的归属随当前精神状态保存。
using System;
using HarmonyLib;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed partial class MentalState_SSCSlaveBreakdown
    {
        // Pair 成功直接进入 Wander，失败进入 Solo。Solo 只有一次，无论结果都进入 Wander。
        internal enum OpeningPhase { Wander, Pair, Solo }

        // 第二步的旧档从 Wander 开始。只有新的 PostStart 才进入 Pair。
        private OpeningPhase openingPhase;
        // 引擎可把 Job 对象归池，因此存档和回调以 loadID 标识本次派发。
        private int behaviorJobId = -1;
        private int behaviorIssuedAt = -1;
        private Pawn openingPartner;
        // 在准备期间只接管邀请时的普通工作、本场准备工作，以及本场安装的接收任务。
        private int originalPartnerJobId = -1;
        private int receiverJobId = -1;
        private object partnerPersonality;
        // 只标记当前调用栈。候选查询触发重新思考时，仍由原查询负责消费这一轮。
        internal bool SelectingOpeningJob { get; set; }

        internal OpeningPhase Phase => openingPhase;
        internal bool HasBehaviorJob => behaviorJobId >= 0;
        /// <summary>派发与 RJW 准备都需要当前、已确认且尚未到期的发作。</summary>
        internal bool CanIssueBehavior
        {
            get
            {
                if (!initialized || Scribe.mode != LoadSaveMode.Inactive || SSCSlaveBreakdown.Now >= expiresAt) return false;
                CaptureCarrier();
                return CanInitializeOrContinue() && SSCIdentityUtility.IsSexSlave(pawn);
            }
        }

        internal bool OwnsBehaviorJob(Job job) => behaviorJobId >= 0 && job?.loadID == behaviorJobId;

        internal bool OwnsBehaviorJobId(int id) => behaviorJobId >= 0 && behaviorJobId == id;

        /// <summary>在返回候选时认领。预约失败或被覆写的候选同样消耗本轮尝试。</summary>
        internal void SetBehaviorJob(Job job)
        {
            behaviorJobId = job.loadID;
            behaviorIssuedAt = SSCSlaveBreakdown.Now;
            if (openingPhase != OpeningPhase.Pair) return;
            openingPartner = job.targetA.Pawn;
            originalPartnerJobId = openingPartner?.CurJob?.loadID ?? -1;
            partnerPersonality = SSCPersonalityRuntimeUtility.Capture(openingPartner);
        }

        internal void SkipOpeningAttempt()
        {
            openingPhase = openingPhase == OpeningPhase.Pair ? OpeningPhase.Solo : OpeningPhase.Wander;
        }

        internal void RetireMissingBehaviorJob()
        {
            // 候选可能在预约或原版覆写判定处被拒。给同步安装留出当轮，再推进一次状态。
            if (behaviorJobId >= 0 && !OwnsBehaviorJob(pawn.CurJob) && SSCSlaveBreakdown.Now > behaviorIssuedAt)
                CompleteBehaviorJob(false);
        }

        internal void CompleteBehaviorJob(bool succeeded)
        {
            int jobId = behaviorJobId;
            int receiver = receiverJobId;
            Pawn partner = openingPartner;
            if (openingPhase == OpeningPhase.Pair && succeeded) openingPhase = OpeningPhase.Wander;
            else SkipOpeningAttempt();
            behaviorJobId = behaviorIssuedAt = originalPartnerJobId = receiverJobId = -1;
            openingPartner = null;
            partnerPersonality = null;
            // 原版可先把当前工作挂回队列，再清理驱动。只取消本场任务，保留玩家和其他模块的队列。
            if (jobId >= 0)
                pawn.jobs?.jobQueue.RemoveAll(pawn, queued => queued?.loadID == jobId && queued != pawn.CurJob);
            if (!succeeded) ReleaseReceiver(pawn, partner, receiver);
        }

        /// <summary>在 RJW 可能接管对方前复查。场景已开始后的正常收尾由原驱动处理。</summary>
        internal bool CanContinuePair(JobDriver_SexBaseInitiator driver)
        {
            Pawn partner = driver.Partner;
            if (!CanIssueBehavior || openingPhase != OpeningPhase.Pair || !OwnsBehaviorJob(driver.job) ||
                pawn.jobs?.curDriver != driver || partner != openingPartner ||
                !ThinkNode_SSCSlaveBreakdownOpening.AvailablePartner(pawn, partner) ||
                !CasualSex_Helper.CanTargetHookup(pawn, partner)) return false;
            // RJW 意愿查询可能进入兼容回调。返回后确认任务和发作仍属于原来的双方。
            if (!CanIssueBehavior || pawn.jobs?.curDriver != driver || !OwnsBehaviorJob(driver.job) ||
                partner != driver.Partner || !ThinkNode_SSCSlaveBreakdownOpening.AvailablePartner(pawn, partner))
                return false;
            if (!SSCPersonalityRuntimeUtility.IsCurrent(partner, partnerPersonality) ||
                SSCPersonalityRuntimeUtility.IsMutating(partner)) return false;
            foreach (QueuedJob queued in partner.jobs.jobQueue)
                if (!SSCRestrictionJobGuard.OwnsPreparedJob(driver, partner, queued.job)) return false;
            Job current = partner.CurJob;
            return (current?.loadID ?? -1) == originalPartnerJobId ||
                SSCRestrictionJobGuard.OwnsPreparedJob(driver, partner, current) ||
                (current?.loadID == receiverJobId && IsReceiver(pawn, partner) &&
                 pawn.Position.DistanceToSquared(partner.Position) <= 1f);
        }

        internal void ObservePairReceiver(Pawn partner, int previousJobId, int actorJobId)
        {
            // 比较原生回调前后，区分本回调新建的接收任务和已存在的同类任务。
            if (!IsReceiver(pawn, partner) || partner.CurJob.loadID == previousJobId) return;
            int newId = partner.CurJob.loadID;
            if (!ending && openingPhase == OpeningPhase.Pair && behaviorJobId == actorJobId && partner == openingPartner)
                receiverJobId = newId;
            else ReleaseReceiver(pawn, partner, newId);
        }

        private static bool IsReceiver(Pawn actor, Pawn partner)
            => partner?.CurJob?.def == xxx.getting_quickie &&
                partner.jobs.curDriver is JobDriver_SexBaseReciever receiver && receiver.Partner == actor;

        private static void ReleaseReceiver(Pawn actor, Pawn partner, int id)
        {
            // 相同的 JobDef 可被其他场景重用，只有编号和指回的发起者都相符才允许释放。
            if (id < 0 || partner?.CurJob?.loadID != id || !IsReceiver(actor, partner)) return;
            var receiver = (JobDriver_SexBaseReciever)partner.jobs.curDriver;
            receiver.parteners?.Remove(actor);
            if (receiver.parteners == null || receiver.parteners.Count == 0)
                partner.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
        }

        private void StopBehaviorJob()
        {
            // 硬重置可以跳过原版 Recover。只结束本场互动或由专用游荡节点派发的任务。
            bool owned = OwnsBehaviorJob(pawn.CurJob) || pawn.CurJob?.jobGiver is JobGiver_SSCSlaveBreakdownWander;
            try
            {
                if (owned && pawn.jobs?.curDriver is { ended: false })
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
            }
            finally
            {
                if (HasBehaviorJob) CompleteBehaviorJob(false);
            }
        }

        private void ExposeBehavior()
        {
            Scribe_Values.Look(ref openingPhase, "sscBreakdownOpeningPhase", OpeningPhase.Wander);
            Scribe_Values.Look(ref behaviorJobId, "sscBreakdownBehaviorJob", -1);
            Scribe_Values.Look(ref behaviorIssuedAt, "sscBreakdownBehaviorIssuedAt", -1);
            Scribe_References.Look(ref openingPartner, "sscBreakdownOpeningPartner");
            Scribe_Values.Look(ref originalPartnerJobId, "sscBreakdownPartnerOriginalJob", -1);
            Scribe_Values.Look(ref receiverJobId, "sscBreakdownReceiverJob", -1);
            // 引用解析完成即可恢复运行期凭据，防止首次运行前发生替换后再认领新的目标人格。
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                partnerPersonality = SSCPersonalityRuntimeUtility.Capture(openingPartner);
        }
    }

    // 原版在驱动 Cleanup 返回后才清空当前 Job。跟踪整个任务结束流程，避免回收仍被跟踪器访问的挂起任务。
    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
    internal static class Patch_SSCSlaveBreakdownBehaviorCleanup
    {
        internal static void Prefix(Pawn_JobTracker __instance, Pawn ___pawn, JobCondition condition,
            out (MentalState_SSCSlaveBreakdown State, int Id, bool Succeeded) __state)
        {
            __state = default;
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(___pawn);
            Job job = __instance.curJob;
            if (state?.OwnsBehaviorJob(job) != true) return;
            // 配对任务同时需要原版完成结果和 SSC 的真实 Start 凭据。
            bool succeeded = condition == JobCondition.Succeeded &&
                (state.Phase != MentalState_SSCSlaveBreakdown.OpeningPhase.Pair ||
                 __instance.curDriver is JobDriver_Sex sex && SSCRestrictionJobGuard.HasStartedScene(sex, SSCInteractionKind.Consensual));
            __state = (state, job.loadID, succeeded);
        }

        internal static void Finalizer(Pawn_JobTracker __instance, Exception __exception,
            (MentalState_SSCSlaveBreakdown State, int Id, bool Succeeded) __state)
        {
            // 进入回调时冻结编号，原版可能已经归池旧 Job，或者回调已经撤销本场。
            if (__state.State?.OwnsBehaviorJobId(__state.Id) == true && __instance.curJob?.loadID != __state.Id)
                __state.State.CompleteBehaviorJob(__state.Succeeded && __exception == null);
        }
    }
}
