using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>记录一个行为请求拥有的准备任务；只负责归属、保存和撤销，不判断行为许可。</summary>
    internal sealed class SSCRestrictionJobPreparation
    {
        // 接收角色与任务编号共同确定归属，不能仅按任务类型清空其他请求或玩家的排队命令。
        private Pawn target;
        private List<int> jobs = new List<int>();

        /// <summary>快照已核对的移动和等待任务，用回调前后的差异识别本请求新建的工作。</summary>
        public static HashSet<int> Snapshot(Pawn pawn)
        {
            var result = new HashSet<int>();
            if (pawn?.jobs == null) return result;
            if (IsPreparationJob(pawn.CurJob)) result.Add(pawn.CurJob.loadID);
            foreach (QueuedJob queued in pawn.jobs.jobQueue)
                if (IsPreparationJob(queued.job)) result.Add(queued.job.loadID);
            return result;
        }

        /// <summary>仅接受已核对的准备类型，未知第三方任务和行为接收任务均由其自身生命周期处理。</summary>
        private static bool IsPreparationJob(Job job) => job != null &&
            (job.def == JobDefOf.Goto || job.def == JobDefOf.Wait || job.def == JobDefOf.GotoMindControlled);

        /// <summary>登记原生准备回调新增的任务；回调之前存在的队列和当前工作不属于本请求。</summary>
        public void RecordNew(Pawn pawn, HashSet<int> before)
        {
            foreach (int id in Snapshot(pawn))
                if (!before.Contains(id)) Register(pawn, id);
        }

        /// <summary>登记事件显式创建的等待或差异捕获的任务；同一个请求保持唯一准备对象。</summary>
        public void Register(Pawn pawn, int jobId)
        {
            if (pawn == null) return;
            // 参与者被外部代码更换时不能把旧编号套用到新对象，旧归属只清理自己的编号。
            if (target != null && target != pawn) Cleanup();
            target = pawn;
            if (!jobs.Contains(jobId)) jobs.Add(jobId);
        }

        /// <summary>沿用已发布存档键，加载空列表时恢复可写容器；不要求升级或重建已有任务。</summary>
        public void ExposeData()
        {
            Scribe_References.Look(ref target, "sscRestrictionPreparedTarget");
            Scribe_Collections.Look(ref jobs, "sscRestrictionPreparedJobs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && jobs == null) jobs = new List<int>();
        }

        /// <summary>只清理登记过的准备任务编号；同类型的新任务、无关队列及其他参与者保持不变。</summary>
        public void Cleanup()
        {
            if (target?.jobs == null || jobs == null || jobs.Count == 0) return;
            target.jobs.jobQueue.RemoveAll(target, job => IsPreparationJob(job) && jobs.Contains(job.loadID));
            if (IsPreparationJob(target.CurJob) && jobs.Contains(target.CurJob.loadID))
                target.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
            jobs.Clear();
        }
    }
}
