using System.Collections.Generic;
using RimWorld;
using Verse.AI;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>把日常调教接入原版工作扫描；候选枚举、完整校验与任务创建各有独立入口。</summary>
    public class WorkGiver_Training : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        /// <summary>基础调教研究未完成时跳过整个工作类别，之后沿用原版扫描器条件。</summary>
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining) || base.ShouldSkip(pawn, forced);
        }

        /// <summary>只读筛选可能的自动工作对象，不为枚举恢复状态、移除冲突基因或分配任务。</summary>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // 没有有效调教员身份时不遍历全图目标；身份判断是只读的，
            // 状态恢复与最终许可仍由随后真正准备工作时的入口处理。
            if (pawn?.Map == null || !SSCIdentityUtility.IsTrainer(pawn)) yield break;
            foreach (Pawn potentialTarget in pawn.Map.mapPawns.AllPawns)
            {
                if (TrainerAssignmentUtility.IsPotentialTrainingTarget(potentialTarget, pawn))
                    yield return potentialTarget;
            }
        }

        /// <summary>完整检查当前目标并设置右键失败提示；查询结果不会创建随后被丢弃的临时 Job。</summary>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return TryPrepareTarget(pawn, t as Pawn, forced);
        }

        /// <summary>创建前重新验证当前状态，避免候选查询后设置、预约或指派变化造成过期许可。</summary>
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn target = t as Pawn;
            return TryPrepareTarget(pawn, target, forced)
                ? JobMaker.MakeJob(SSCDefOf.TrainingSexSlave, target)
                : null;
        }

        /// <summary>查询和创建共用完整准备入口；兼容修复在此明确发生，强制命令保留具体失败原因。</summary>
        private static bool TryPrepareTarget(Pawn trainer, Pawn target, bool forced)
        {
            if (TrainerAssignmentUtility.TryPrepareTrainingTarget(target, trainer, forced, out string reason)) return true;
            if (forced && !string.IsNullOrEmpty(reason)) JobFailReason.Is(reason);
            return false;
        }
    }
}
