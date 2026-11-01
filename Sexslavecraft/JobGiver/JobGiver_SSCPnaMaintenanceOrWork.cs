// AI 辅助生成：保留普通工作的原生排程优先级，仅在选下一份工作时先尝试补药。
using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>普通工作的薄包装：在任务交界先尝试补药，缺药或不适用时交还原版工作选择。</summary>
    public sealed class JobGiver_SSCPnaMaintenanceOrWork : JobGiver_Work
    {
        /// <summary>继承原版排程优先级；本模块服药或无工作能力者需要补药时保留最低正优先级。</summary>
        public override float GetPriority(Pawn pawn)
        {
            float priority = base.GetPriority(pawn);
            // 零优先级节点不会被原版排序器尝试；只为确需补药者补足入口，不提升其他人的工作优先级。
            if (SSCPnaMaintenanceUtility.IsMaintenance(pawn.CurJob)) return Math.Max(priority, 1f);
            return priority > 0f ? priority : SSCPnaMaintenanceUtility.CanStart(pawn) ? 1f : 0f;
        }

        /// <summary>保留临产拒绝及自有任务续用，再尝试单份补给；普通工作仍由父类生成。</summary>
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            // 沿用原Work的首个拒绝，不能让提前生成补药绕过临产处理。
            if (pawn.RaceProps.Humanlike && pawn.health.hediffSet.InLabor()) return ThinkResult.NoJob;
            // 保持同一个Job引用；原生ShouldStartJobFromThinkTree据此避免普通工作抢断自己的服药。
            if (SSCPnaMaintenanceUtility.IsMaintenance(pawn.CurJob))
                return new ThinkResult(pawn.CurJob, this, JobTag.SatisfyingNeeds);
            Job supply = SSCPnaMaintenanceUtility.TryMakeJob(pawn);
            if (supply != null) return new ThinkResult(supply, this, JobTag.SatisfyingNeeds);
            // 原版工作选取会读取工作配置；没有工作能力时直接交还思考树，避免调用不适用的父类路径。
            return pawn.workSettings?.EverWork == true ? base.TryIssueJobPackage(pawn, jobParams) : ThinkResult.NoJob;
        }
    }
}
