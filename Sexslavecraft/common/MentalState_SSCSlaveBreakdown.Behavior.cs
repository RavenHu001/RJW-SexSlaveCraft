// AI 辅助生成：开场和游荡任务的归属随当前精神状态保存。
using HarmonyLib;
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
        internal bool HasBehaviorJob => behaviorJobId >= 0;
        /// <summary>只有当前、已确认且尚未到期的发作才可派发行为任务。</summary>
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
            if (behaviorJobId >= 0 && !OwnsBehaviorJob(pawn.CurJob) && SSCSlaveBreakdown.Now > behaviorIssuedAt)
                CompleteBehaviorJob();
        }

        internal void CompleteBehaviorJob()
        {
            int jobId = behaviorJobId;
            // 原版任务结束、被打断及派发未安装都汇到这里。先撤销认领，再处理可能触发回调的队列。
            FinishOpening();
            behaviorJobId = behaviorIssuedAt = -1;
            // 原版可先把当前工作挂回队列，再清理驱动。只取消本场任务，保留玩家和其他模块的队列。
            if (jobId >= 0)
                pawn.jobs?.jobQueue.RemoveAll(pawn, queued => queued?.loadID == jobId && queued != pawn.CurJob);
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
                if (HasBehaviorJob) CompleteBehaviorJob();
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

    // 原版清掉 CurJob 前还会访问挂起任务。前缀只记录归属，等 Finalizer 确认该任务退场才移除它的队列项。
    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
    internal static class Patch_SSCSlaveBreakdownBehaviorCleanup
    {
        internal static void Prefix(Pawn_JobTracker __instance, Pawn ___pawn,
            out (MentalState_SSCSlaveBreakdown State, int Id) __state)
        {
            __state = default;
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(___pawn);
            Job job = __instance.curJob;
            if (state?.OwnsBehaviorJob(job) != true) return;
            __state = (state, job.loadID);
        }

        internal static void Finalizer(Pawn_JobTracker __instance, (MentalState_SSCSlaveBreakdown State, int Id) __state)
        {
            // 进入回调时冻结编号，原版可能已经归池旧 Job，或者回调已经撤销本场。
            if (__state.State?.OwnsBehaviorJobId(__state.Id) == true && __instance.curJob?.loadID != __state.Id)
                __state.State.CompleteBehaviorJob();
        }
    }
}
