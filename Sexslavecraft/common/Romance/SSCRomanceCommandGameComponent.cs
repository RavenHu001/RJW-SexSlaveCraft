// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>命令记录唯一保存源；只遍历本模块请求，不增加全世界 Tick 扫描，不跨存档共享。</summary>
    public sealed class SSCRomanceCommandGameComponent : GameComponent
    {
        private List<SSCRomanceCommandRecord> records = new List<SSCRomanceCommandRecord>();
        public bool Ready { get; private set; }
        public IReadOnlyList<SSCRomanceCommandRecord> Records => records;
        public static SSCRomanceCommandGameComponent Current => Verse.Current.Game?.GetComponent<SSCRomanceCommandGameComponent>();
        /// <summary>游戏为每个存档创建组件；初始化不扫描角色或签发授权。</summary>
        public SSCRomanceCommandGameComponent(Game game) { }
        /// <summary>深存任务记录，旧档缺节点为空；LoadingVars 先关闭准入，等已有完整恢复出口对账。</summary>
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) Ready = false;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 中断回调可能立即存档，此时 ended 已设置而 Finish 尚未运行；不能把已结束任务保存成可续行。
                // 仅核对记录对应的原当前 Job，不触碰合法排队任务，不在保存期间发送反馈或执行互动。
                foreach (var record in records)
                {
                    var jobs = record?.Stamp?.Actor?.jobs;
                    if (record != null && !record.Terminal && record.runtimeJob != null &&
                        jobs?.curJob == record.runtimeJob && jobs.curDriver?.job == record.runtimeJob && jobs.curDriver.ended)
                        Cancel(record, "JobEnded");
                }
            }
            Scribe_Collections.Look(ref records, "sscRomanceCommands", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && records == null) records = new List<SSCRomanceCommandRecord>();
        }
        /// <summary>只读枚举当前和排队任务；去重原实例，但同 loadID 不同实例仍保留以便发现 Clone。</summary>
        internal static List<Job> Jobs(Pawn actor)
        {
            var result = new List<Job>();
            if (actor?.jobs?.curJob != null) result.Add(actor.jobs.curJob);
            if (actor?.jobs?.jobQueue != null)
                foreach (var queued in actor.jobs.jobQueue)
                    if (queued.job != null && !result.Contains(queued.job)) result.Add(queued.job);
            return result;
        }
        /// <summary>完整读档恢复后的唯一认领：重复请求/编号、执行中状态和旧授权均不重放。</summary>
        public void ReconcileAfterLoad()
        {
            if (Scribe.mode != LoadSaveMode.Inactive) return;
            Ready = false;
            records.RemoveAll(r => r == null);
            var duplicateIds = records.GroupBy(r => r.RequestId).Where(g => g.Count() != 1).Select(g => g.Key).ToHashSet();
            foreach (var record in records)
            {
                record.runtimeDriver = null; record.runtimeJob = null; record.authorization = null;
                record.scheduling = false;
                // 终态也必须验证格式/枚举，不能因数值大于 Completed 就信任未知版本或缺失结果。
                bool invalid = record.version != 1 || record.Status < SSCRomanceCommandStatus.Pending || record.Status > SSCRomanceCommandStatus.Indeterminate ||
                    string.IsNullOrEmpty(record.RequestId) || duplicateIds.Contains(record.RequestId) ||
                    record.JobId < 0 || record.Stamp == null || records.Count(r => r.JobId == record.JobId) != 1;
                bool invalidResult = record.Terminal &&
                    (record.Outcome == SSCRomanceCommandOutcome.None || record.Outcome < SSCRomanceCommandOutcome.None || record.Outcome > SSCRomanceCommandOutcome.Unconfirmed ||
                    (record.Status == SSCRomanceCommandStatus.Completed && (!record.effectsStarted || !record.workerCompleted ||
                        (record.Outcome != SSCRomanceCommandOutcome.Rejected && record.Outcome != SSCRomanceCommandOutcome.RelationshipChanged))));
                if (invalid || invalidResult)
                {
                    record.status = SSCRomanceCommandStatus.Indeterminate;
                    record.outcome = SSCRomanceCommandOutcome.Unconfirmed;
                    record.reason = "InvalidRecord";
                }
                if (record.Terminal)
                {
                    // 终态只重建清理归属，不重建授权；保留尚在 Job 中的结果和一次性反馈。
                    var finishedJobs = Jobs(record.Stamp?.Actor).Where(j => j.loadID == record.JobId).ToList();
                    if (finishedJobs.Count == 1 && record.Stamp != null && Matches(record, finishedJobs[0])) record.runtimeJob = finishedJobs[0];
                    continue;
                }
                if (record.Status == SSCRomanceCommandStatus.Executing || record.effectsStarted)
                { record.status = SSCRomanceCommandStatus.Indeterminate; record.outcome = SSCRomanceCommandOutcome.Unconfirmed; continue; }
                var matches = Jobs(record.Stamp.Actor).Where(j => j.loadID == record.JobId).ToList();
                if (matches.Count != 1 || !Matches(record, matches[0]) ||
                    !SSCRomanceLifecycle.TryResume(record.Stamp, out var proof))
                { Cancel(record, "InvalidAuthorization"); continue; }
                record.runtimeJob = matches[0]; record.authorization = proof;
                record.targetToken = SSCPersonalityRuntimeUtility.Capture(record.Stamp.Target);
                record.status = SSCRomanceCommandStatus.Pending;
            }
            Ready = true;
            Prune();
        }
        /// <summary>比较命令的真实 Def、目标及行为；替换 Job 字段不能借用原记录。</summary>
        internal static bool Matches(SSCRomanceCommandRecord record, Job job)
            => job != null && job.def == SSCRomanceDefOf.SSC_RomanceCommand && job.loadID == record.JobId &&
                job.targetA.Pawn == record.Stamp?.Target && SSCRomanceInteractionUtility.TryGetAction(job.interaction, out var action) && action == record.Stamp.Action;
        /// <summary>先清除无 Job 的旧记录，再拒绝同一执行者、目标、行为的重复在途请求。</summary>
        internal SSCRomanceCommandRecord Register(Job job, SSCRomanceAuthorization proof)
        {
            Prune();
            // 原版 GetNextJobID 从 0 起；缺失编号使用 -1，不能误拒绝新存档第一个 Job。
            if (!Ready || job == null || job.loadID < 0 || proof == null || records.Any(r => r.JobId == job.loadID ||
                (!r.Terminal && r.Stamp.Actor == proof.Stamp.Actor && r.Stamp.Target == proof.Stamp.Target && r.Stamp.Action == proof.Stamp.Action))) return null;
            var record = new SSCRomanceCommandRecord(job, proof); records.Add(record); return record;
        }
        /// <summary>只有当前原 Job 与未结束的当前原驱动可以领取；同编号克隆和另一个驱动不得接管运行时归属。</summary>
        public SSCRomanceCommandRecord Claim(JobDriver_SSCRomanceCommand driver)
        {
            var record = FindOwned(driver);
            if (!Ready || record == null || driver.ended || record.Terminal || record.Status == SSCRomanceCommandStatus.Executing ||
                record.runtimeJob != driver.job || driver.pawn?.CurJob != driver.job || driver.pawn.jobs.curDriver != driver ||
                record.Stamp.Actor != driver.pawn || !Matches(record, driver.job) ||
                (record.runtimeDriver != null && record.runtimeDriver != driver) || !IsCurrent(record)) return null;
            record.runtimeDriver = driver; record.status = SSCRomanceCommandStatus.Claimed; return record;
        }
        /// <summary>只查询原 Job 的记录，包括已取消记录供驱动解释/收尾；此查询不授予执行权限。</summary>
        internal SSCRomanceCommandRecord FindOwned(JobDriver_SSCRomanceCommand driver)
        {
            var matches = records.Where(r => r.JobId == driver?.job?.loadID).ToList();
            var record = matches.Count == 1 ? matches[0] : null;
            return record?.runtimeJob == driver?.job && record?.Stamp?.Actor == driver?.pawn ? record : null;
        }
        /// <summary>运行中复查原人格及绑定；目标偏好/主人变化实时重读，不把被动拒绝误当任务失效。</summary>
        internal static bool IsCurrent(SSCRomanceCommandRecord record)
            => record.authorization?.IsCurrent(record.Stamp.Actor, record.Stamp.Target, record.Stamp.Action) == true &&
                SSCPersonalityRuntimeUtility.IsCurrent(record.Stamp.Target, record.targetToken) &&
                !SSCPersonalityRuntimeUtility.IsMutating(record.Stamp.Target);
        /// <summary>绑定事件撤销执行者授权；目标人格替换才取消以该目标为对象的命令。</summary>
        public void Invalidate(Pawn pawn, bool targetPersonality = false)
        {
            foreach (var record in records)
                if (record != null && !record.Terminal && (record.Stamp?.Actor == pawn || (targetPersonality && record.Stamp?.Target == pawn)))
                    Cancel(record, "InvalidAuthorization");
        }
        /// <summary>取消不回滚已经开始的原生副作用，留不确定终态阻止再次结算。</summary>
        internal static void Cancel(SSCRomanceCommandRecord record, string reason)
        {
            if (record == null || record.Terminal) return;
            record.reason = reason;
            record.status = record.effectsStarted ? SSCRomanceCommandStatus.Indeterminate : SSCRomanceCommandStatus.Cancelled;
            record.outcome = record.effectsStarted ? SSCRomanceCommandOutcome.Unconfirmed : SSCRomanceCommandOutcome.NotOccurred;
            SSCRomanceExecutionScope.TraceCommandTerminal(record);
        }
        /// <summary>结束/丢弃记录只处理本模块内存；原 Job 尚在当前/队列时保留终态防重放。</summary>
        internal void Prune()
            => records.RemoveAll(r => r == null || (!r.scheduling && (r.Stamp == null || !Jobs(r.Stamp.Actor).Any(j => j.loadID == r.JobId))));
        /// <summary>只发一条命令结果，原版记忆与信件由原 worker 自己处理，清理回调不补结算。</summary>
        internal static void Feedback(SSCRomanceCommandRecord record)
        {
            if (record == null || !record.Terminal || record.feedbackSent) return;
            record.feedbackSent = true;
            string text = ("SSC_Romance_Result_" + record.Outcome).Translate(record.Stamp.Actor.LabelShort, record.Stamp.Target?.LabelShort ?? "?");
            if (record.reason != null) text += " " + SSCRomanceCommandUtility.Explain(record.reason);
            Messages.Message(text,
                record.Stamp.Actor, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
