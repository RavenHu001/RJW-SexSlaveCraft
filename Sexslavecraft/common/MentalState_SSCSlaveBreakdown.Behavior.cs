// AI 辅助生成：开场和游荡任务的归属随当前精神状态保存。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed partial class MentalState_SSCSlaveBreakdown
    {
        // Solo 表示尚未检查候选，或唯一一次开场已认领且等待任务结束。两者用行为任务编号区分。
        // Wander 表示开场机会已用完，后续单人行为只通过游荡期的日常候选。
        internal enum OpeningPhase { Wander, Solo }

        // 新发作由 PostStart 标为 Solo。完成或用尽开场机会后进入 Wander。
        private OpeningPhase openingPhase;
        // Job 会归池，读档也会重建实例。用 loadID 跟踪本次派发，不能用引用或同名 JobDef 判定归属。
        private int behaviorJobId = -1;
        // 返回候选与原版安装任务之间存在同步窗口，当轮看到 CurJob 为空还不能认定派发失败。
        private int behaviorIssuedAt = -1;
        // 只在候选调用栈内设置。回调若触发重新思考，外层让原查询完成，由它负责消费开场机会。
        internal bool SelectingOpeningJob { get; set; }

        internal OpeningPhase Phase => openingPhase;
        // 求用可在尚无发起 Job 编号时先占住邀约槽，防止读档或 AI 再取一份候选。
        internal bool HasBehaviorJob => behaviorJobId >= 0 || invitation != null;
        /// <summary>当前发作须已确认、效果仍挂载且尚未到期。外部移除后立即关闭派发和既有任务的授权。</summary>
        internal bool CanIssueBehavior
        {
            get
            {
                if (!initialized || Scribe.mode != LoadSaveMode.Inactive || RemainingTicks <= 0) return false;
                CaptureCarrier();
                return HasEffect() && CanInitializeOrContinue() && SSCIdentityUtility.IsSexSlave(pawn);
            }
        }

        internal bool OwnsBehaviorJob(Job job) => behaviorJobId >= 0 && job?.loadID == behaviorJobId;

        internal bool OwnsBehaviorJobId(int id) => behaviorJobId >= 0 && behaviorJobId == id;

        /// <summary>认领已经通过 RJW 和 SSC 处理的最终候选。预约失败或任务覆写同样消耗本轮尝试。</summary>
        internal void SetBehaviorJob(Job job)
        {
            behaviorJobId = job.loadID;
            behaviorIssuedAt = SSCSlaveBreakdown.Now;
        }

        internal void FinishOpening() => openingPhase = OpeningPhase.Wander;

        internal void RetireMissingBehaviorJob()
        {
            // 候选在预约或覆写处被拒时可能没有结束回调。下一轮发现当前任务不匹配，才释放该派发。
            if (behaviorJobId >= 0 && !OwnsBehaviorJob(BehaviorJobOwner.CurJob) && SSCSlaveBreakdown.Now > behaviorIssuedAt)
                CompleteBehaviorJob();
        }

        internal void CompleteBehaviorJob(bool invitationSucceeded = false)
        {
            int jobId = behaviorJobId;
            // 求用的发起追踪器在另一名小人身上，必须在清空邀约之前取出原主人。
            Pawn owner = BehaviorJobOwner;
            SSCBreakdownInvitation completedInvitation = invitation;
            // 原版任务结束、被打断及派发未安装都汇到这里。先撤销认领，再处理可能触发回调的队列。
            invitation = null;
            FinishOpening();
            behaviorJobId = behaviorIssuedAt = -1;
            // 原版可先把当前工作挂回队列，再清理驱动。只取消本场任务，保留玩家和其他模块的队列。
            try
            {
                if (jobId >= 0)
                    owner.jobs?.jobQueue.RemoveAll(owner, queued => queued?.loadID == jobId && queued != owner.CurJob);
            }
            finally { completedInvitation?.Release(pawn, invitationSucceeded); }
        }

        private void StopBehaviorJob()
        {
            // 常规恢复和硬重置都只结束本场互动或由专用游荡节点派发的任务。
            try
            {
                if (invitation != null) CancelInvitation();
                bool owned = OwnsBehaviorJob(pawn.CurJob) || pawn.CurJob?.jobGiver is JobGiver_SSCSlaveBreakdownWander;
                if (owned && pawn.jobs?.curDriver is { ended: false })
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
            }
            finally
            {
                if (HasBehaviorJob || invitation != null) CompleteBehaviorJob();
            }
        }

        private void ExposeBehavior()
        {
            // 保存阶段和已认领任务。读档后凭当前 Job 的编号接续，未安装的候选仍按缺失任务收尾。
            // 正在查询候选是同步过程，SelectingOpeningJob 不随存档恢复。
            Scribe_Values.Look(ref openingPhase, "sscBreakdownOpeningPhase", OpeningPhase.Wander);
            Scribe_Values.Look(ref behaviorJobId, "sscBreakdownBehaviorJob", -1);
            Scribe_Values.Look(ref behaviorIssuedAt, "sscBreakdownBehaviorIssuedAt", -1);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class Patch_SSCBreakdownInvitationWaitInstallation
    {
        // 记下候选编号，再等原版完成安装。双方相邻时，反向派发可立即把等待换成正式接收任务。
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(Pawn_JobTracker __instance, Pawn ___pawn, Job newJob,
            out (MentalState_SSCSlaveBreakdown State, SSCBreakdownInvitation Invitation, int Id) __state)
        {
            __state = default;
            if (!SSCBreakdownInvitation.IsWait(newJob)) return true;
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(___pawn);
            var invitation = state?.WaitingInvitationFor(newJob);
            if (invitation == null)
            {
                if (newJob != __instance.curJob && !__instance.jobQueue.Contains(newJob))
                    JobMaker.ReturnToPool(newJob);
                return false;
            }
            __state = (state, invitation, newJob.loadID);
            return true;
        }

        internal static void Postfix(
            (MentalState_SSCSlaveBreakdown State, SSCBreakdownInvitation Invitation, int Id) __state)
            => __state.State?.FinishInvitedUseWaitInstallation(__state.Invitation, __state.Id);

        internal static void Finalizer(System.Exception __exception,
            (MentalState_SSCSlaveBreakdown State, SSCBreakdownInvitation Invitation, int Id) __state)
        {
            // 等待安装和后续预约都可能抛错。原异常继续交给调用方，同时撤销已经认领的本场任务。
            if (__exception != null) __state.State?.CancelIfCurrentInvitation(__state.Invitation);
        }
    }

    // 原版 stopsJobs 同时控制入场和恢复。只拦下 SSC 恢复时的 StopAll，其余通知和入场行为照原版。
    [HarmonyPatch(typeof(MentalState), nameof(MentalState.RecoverFromState))]
    internal static class Patch_SSCSlaveBreakdownRecoveryJobs
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var stopsJobs = AccessTools.Field(typeof(MentalStateDef), nameof(MentalStateDef.stopsJobs));
            int flag = codes.FindIndex(code => code.LoadsField(stopsJobs));
            if (flag < 0 || flag != codes.FindLastIndex(code => code.LoadsField(stopsJobs)))
                throw new InvalidOperationException("Expected one recovery job-stop guard in MentalState.RecoverFromState.");

            // 保留原判断的其余条件。SSC 的精准清理已在状态清除回调中处理任务及队列。
            codes.InsertRange(flag + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(Patch_SSCSlaveBreakdownRecoveryJobs), nameof(ShouldStopJobs)))
            });
            return codes;
        }

        private static bool ShouldStopJobs(bool stopJobs, MentalState state)
            => stopJobs && state is not MentalState_SSCSlaveBreakdown;
    }

    // 原版清掉 CurJob 前还会访问挂起任务。前缀只记录归属，等 Finalizer 确认该任务退场才移除它的队列项。
    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
    internal static class Patch_SSCSlaveBreakdownBehaviorCleanup
    {
        internal static void Prefix(Pawn_JobTracker __instance, Pawn ___pawn, JobCondition condition,
            out (MentalState_SSCSlaveBreakdown State, int Id, bool InvitationSucceeded, SSCBreakdownInvitation Receiver) __state)
        {
            __state = default;
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(___pawn);
            Job job = __instance.curJob;
            if (state?.InvitedUseReceiverFor(job) is { } receiver)
            {
                // 受方单独退场时记录原邀请实例。保留同一编号的新状态和无关任务，晚到的回调只认领本场。
                __state = (state, job.loadID, false, receiver);
                return;
            }
            if (JobDriver_SSCBreakdownInvitedUse.IsInvitedJob(job))
                // 实际发起者完成求用时，沿目标 A 找回保存预约和接收任务的发作。
                state = MentalState_SSCSlaveBreakdown.CurrentFor(job.targetA.Pawn);
            if (state?.OwnsBehaviorJob(job) != true) return;
            // 发起端正常完成后，原 RJW 接收端本来也会返回 Succeeded。只有真实开场过的成功可保留这一结果。
            bool invitationSucceeded = condition == JobCondition.Succeeded &&
                __instance.curDriver is JobDriver_SexBaseInitiator driver && state.InvitationFor(job)?.HasStarted(driver) == true;
            __state = (state, job.loadID, invitationSucceeded, null);
        }

        internal static void Finalizer(Pawn_JobTracker __instance, System.Exception __exception,
            (MentalState_SSCSlaveBreakdown State, int Id, bool InvitationSucceeded, SSCBreakdownInvitation Receiver) __state)
        {
            // 进入回调时冻结编号，原版可能已经归池旧 Job，或者回调已经撤销本场。
            if (__state.State == null || __instance.curJob?.loadID == __state.Id) return;
            if (__state.Receiver != null)
                __state.State.CancelIfCurrentInvitation(__state.Receiver);
            else if (__state.State.OwnsBehaviorJobId(__state.Id))
                __state.State.CompleteBehaviorJob(__state.InvitationSucceeded && __exception == null);
        }
    }
}
