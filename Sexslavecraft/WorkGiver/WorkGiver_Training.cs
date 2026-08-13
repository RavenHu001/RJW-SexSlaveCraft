using System.Collections.Generic;
using RimWorld;
using Verse.AI;
using Verse;

// EN: This file exposes ordinary training as a workgiver.
// EN: It relies on shared trainer and target utilities for consistent rules.
// CN: 这个文件把普通调教暴露为 WorkGiver。
// CN: 它依赖共享的 trainer 和 target 工具来统一规则。
namespace SexSlaveCraft
{
    public class WorkGiver_Training : WorkGiver_Scanner
    {


        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        // ==========================================================
        // 1. ShouldSkip 检查：看是不是连大门都没让进
        // ==========================================================
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {


            // 科技检查
            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining))
            {

                return true;
            }

            bool baseSkip = base.ShouldSkip(pawn, forced);


            return baseSkip;
        }

        // PotentialWorkThingsGlobal 主要用于自动寻找工作(非强制指派)，这里我们保持原样，不加Log防刷屏
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (var potentialTarget in pawn.Map.mapPawns.AllPawns)
            {
                if (!TrainerAssignmentUtility.IsTrainingTargetAvailable(potentialTarget, pawn, false)) continue;

                yield return potentialTarget;
            }
        }

        // ==========================================================
        // 2. HasJobOnThing：右键菜单生成的第一道关卡
        // ==========================================================
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {


            bool hasJob = JobOnThing(pawn, t, forced) != null;


            return hasJob;
        }

        // ==========================================================
        // 3. JobOnThing：核心逻辑，一层一层扒开看是在哪断的
        // ==========================================================
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {


            Pawn targetPawn = t as Pawn;
            if (!TrainerAssignmentUtility.TryGetTrainingTargetFailureReason(targetPawn, pawn, forced, out string reason))
            {
                if (forced && !string.IsNullOrEmpty(reason))
                {
                    JobFailReason.Is(reason);
                }
                return null;
            }


            return JobMaker.MakeJob(SSCDefOf.TrainingSexSlave, targetPawn);
        }
    }
}
