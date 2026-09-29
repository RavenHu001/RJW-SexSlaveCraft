using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 右键选项在 JobDriver 创建前产生；按 Job 编号保存，连续指派不会覆盖旧任务。
        private Dictionary<int, InteractionDef> pendingSelfTrainingInteractions = new Dictionary<int, InteractionDef>();

        internal void RegisterManualSelfTraining(Job job, InteractionDef interaction)
        {
            if (job?.loadID > 0 && interaction != null)
                pendingSelfTrainingInteractions[job.loadID] = interaction;
        }

        internal InteractionDef TakeManualSelfTraining(Job job)
        {
            if (job == null || !pendingSelfTrainingInteractions.TryGetValue(job.loadID, out InteractionDef interaction))
                return null;
            pendingSelfTrainingInteractions.Remove(job.loadID);
            return interaction;
        }

        internal void ClearManualSelfTraining(Job job)
        {
            if (job != null) pendingSelfTrainingInteractions.Remove(job.loadID);
        }

        private void ExposeManualSelfTraining()
        {
            Scribe_Collections.Look(ref pendingSelfTrainingInteractions, "sscManualSelfTrainingInteractions",
                LookMode.Value, LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingSelfTrainingInteractions == null)
                pendingSelfTrainingInteractions = new Dictionary<int, InteractionDef>();
        }

        private void ReconcileManualSelfTraining(Pawn pawn)
        {
            if (pendingSelfTrainingInteractions.Count == 0 || pawn?.jobs == null) return;
            var active = new HashSet<int>();
            if (pawn.CurJob != null) active.Add(pawn.CurJob.loadID);
            foreach (QueuedJob queued in pawn.jobs.jobQueue)
                if (queued.job != null) active.Add(queued.job.loadID);
            var stale = new List<int>();
            foreach (int id in pendingSelfTrainingInteractions.Keys)
                if (!active.Contains(id)) stale.Add(id);
            foreach (int id in stale) pendingSelfTrainingInteractions.Remove(id);
        }
    }
}
