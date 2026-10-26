// AI 辅助生成
using System;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>只有 Pending/Claimed 可继续；Executing 表示已占用一次调用，不能因读档或异常重放。</summary>
    public enum SSCRomanceCommandStatus { Pending, Claimed, Executing, Completed, Cancelled, Failed, Indeterminate }
    /// <summary>区分未互动、目标拒绝、关系成功及无法确认的外部结算，不用 Job 成功冒充接受。</summary>
    public enum SSCRomanceCommandOutcome { None, NotOccurred, Rejected, RelationshipChanged, Unconfirmed }

    /// <summary>本存档独有的任务记录；人格、凝胶与克隆快照均不持有此对象。</summary>
    public sealed class SSCRomanceCommandRecord : IExposable
    {
        // 诊断关联及输出去重仅属当前进程，不保存、不参与任务授权。
        internal long diagnosticId;
        internal bool diagnosticTerminalReported;
        internal int version = 1, jobId = -1;
        internal string requestId, reason;
        internal SSCRomanceCommandStamp stamp;
        internal SSCRomanceCommandStatus status;
        internal SSCRomanceCommandOutcome outcome;
        internal bool effectsStarted, workerEntered, workerCompleted, decisionKnown, accepted, feedbackSent;
        // Job.Clone 会复制 loadID；运行期必须额外比较原实例，加载后仅能从唯一合法 Job 重建。
        internal Job runtimeJob;
        internal JobDriver_SSCRomanceCommand runtimeDriver;
        internal SSCRomanceAuthorization authorization;
        internal object targetToken;
        internal bool scheduling = true;
        public string RequestId => requestId;
        public int JobId => jobId;
        public SSCRomanceCommandStamp Stamp => stamp;
        public SSCRomanceCommandStatus Status => status;
        public SSCRomanceCommandOutcome Outcome => outcome;
        public string Reason => reason;
        public bool EffectsStarted => effectsStarted;
        public bool Terminal => status >= SSCRomanceCommandStatus.Completed;

        /// <summary>Scribe 构造入口；空记录不能获得任务授权。</summary>
        public SSCRomanceCommandRecord() { }
        /// <summary>在调度前固定原请求、Job 与授权，避免同步启动时找不到登记。</summary>
        internal SSCRomanceCommandRecord(Job job, SSCRomanceAuthorization proof)
        {
            requestId = Guid.NewGuid().ToString("N"); jobId = job.loadID;
            runtimeJob = job; stamp = proof.Stamp; authorization = proof;
            targetToken = SSCPersonalityRuntimeUtility.Capture(stamp.Target);
        }
        /// <summary>只保存持久状态；驱动、原 Job 实例及人格 token 必须在引用恢复后重新认领。</summary>
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) { diagnosticId = 0; diagnosticTerminalReported = false; }
            Scribe_Values.Look(ref version, "version", 0);
            Scribe_Values.Look(ref requestId, "requestId"); Scribe_Values.Look(ref jobId, "jobId", -1);
            Scribe_Deep.Look(ref stamp, "stamp");
            Scribe_Values.Look(ref status, "status", SSCRomanceCommandStatus.Indeterminate);
            Scribe_Values.Look(ref outcome, "outcome", SSCRomanceCommandOutcome.None);
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref effectsStarted, "effectsStarted");
            Scribe_Values.Look(ref workerEntered, "workerEntered"); Scribe_Values.Look(ref workerCompleted, "workerCompleted");
            Scribe_Values.Look(ref decisionKnown, "decisionKnown"); Scribe_Values.Look(ref accepted, "accepted");
            Scribe_Values.Look(ref feedbackSent, "feedbackSent");
        }
    }
}
