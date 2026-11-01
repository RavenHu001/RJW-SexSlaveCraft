// AI 辅助生成：保留普通工作的原生排程优先级，仅在选下一份工作时先尝试补药。
using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed class JobGiver_SSCPnaMaintenanceOrWork : JobGiver_Work
    {
        public override float GetPriority(Pawn pawn)
        {
            float priority = base.GetPriority(pawn);
            if (SSCPnaMaintenanceUtility.IsMaintenance(pawn.CurJob)) return Math.Max(priority, 1f);
            return priority > 0f ? priority : SSCPnaMaintenanceUtility.CanStart(pawn) ? 1f : 0f;
        }

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            // 沿用原Work的首个拒绝，不能让提前生成补药绕过临产处理。
            if (pawn.RaceProps.Humanlike && pawn.health.hediffSet.InLabor()) return ThinkResult.NoJob;
            // 保持同一个Job引用；原生ShouldStartJobFromThinkTree据此避免普通工作抢断自己的服药。
            if (SSCPnaMaintenanceUtility.IsMaintenance(pawn.CurJob))
                return new ThinkResult(pawn.CurJob, this, JobTag.SatisfyingNeeds);
            Job supply = SSCPnaMaintenanceUtility.TryMakeJob(pawn);
            if (supply != null) return new ThinkResult(supply, this, JobTag.SatisfyingNeeds);
            return pawn.workSettings?.EverWork == true ? base.TryIssueJobPackage(pawn, jobParams) : ThinkResult.NoJob;
        }
    }
}
