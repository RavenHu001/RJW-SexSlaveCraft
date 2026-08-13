using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

// EN: This file exposes personality excretion as a manual workgiver.
// EN: It only creates jobs when PE-specific checks confirm the correct target state.
// CN: 这个文件把人格排泄暴露为手动 WorkGiver。
// CN: 它只会在 PE 专属判定确认目标状态正确时创建 Job。
namespace SexSlaveCraft
{
    public class WorkGiver_PE : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        // 禁止自动扫描，只允许右键
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            yield break;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn target = t as Pawn;
            if (!WorkGiverTargetUtility.IsValidPETarget(pawn, target, forced)) return null;
            return JobMaker.MakeJob(SSCDefOf.SSC_Job_PE, target);
        }
    }
}
