using System;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    internal enum SSCSelfTrainingAutoChoice { None, Masturbation, SelfTraining }

    /// <summary>只在 RJW 已生成的自动自慰候选上选择用途；不创建独立的触发节奏。</summary>
    internal static class SSCSelfTrainingAutoJob
    {
        internal static SSCSelfTrainingAutoChoice Select(bool masturbationAvailable, bool selfTrainingAvailable,
            float selfTrainingChance, Func<float> nextRoll)
        {
            if (!selfTrainingAvailable)
                return masturbationAvailable ? SSCSelfTrainingAutoChoice.Masturbation : SSCSelfTrainingAutoChoice.None;
            if (!masturbationAvailable) return SSCSelfTrainingAutoChoice.SelfTraining;
            return nextRoll() < selfTrainingChance
                ? SSCSelfTrainingAutoChoice.SelfTraining : SSCSelfTrainingAutoChoice.Masturbation;
        }

        /// <summary>在通用自慰许可守卫前分流；结果仍经过同一守卫核对新任务用途。</summary>
        internal static void Divert(ThinkNode_JobGiver giver, Pawn pawn, ref ThinkResult result)
        {
            Job candidate = result.Job;
            if (!(giver is JobGiver_Masturbate) || result.FromQueue || pawn == null ||
                candidate?.def != xxx.Masturbate || candidate.targetA.Pawn != pawn) return;

            bool masturbationAvailable = SSCRestrictionPolicy.Evaluate(
                new SSCRestrictionRequest(pawn, pawn, SSCInteractionKind.Masturbation, true)).Allowed;
            bool selfTrainingAvailable = JobDriver_SelfTraining.CanStart(pawn, candidate.targetC.Cell, out _);
            SSCSelfTrainingAutoChoice choice = Select(masturbationAvailable, selfTrainingAvailable,
                SSCSelfTrainingUtility.GetAutoSelectionChance(pawn), () => Rand.Value);
            // 普通候选或两者均不可用时交给原有守卫；后者会回收被拒绝的候选。
            if (choice != SSCSelfTrainingAutoChoice.SelfTraining) return;

            Job replacement = JobMaker.MakeJob(SSCDefOf.SelfTraining,
                candidate.targetA, candidate.targetB, candidate.targetC);
            pawn.ClearReservationsForJob(candidate);
            JobMaker.ReturnToPool(candidate);
            result = new ThinkResult(replacement, result.SourceNode, result.Tag, result.FromQueue);
        }
    }
}
