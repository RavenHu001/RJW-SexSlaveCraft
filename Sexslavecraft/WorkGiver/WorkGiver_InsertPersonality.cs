using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

// EN: This file exposes the personality reinsertion job.
// EN: It finds empty-shell pawns with valid gel assignments and reachable targets.
// CN: 这个文件把人格重新植入流程暴露为 WorkGiver。
// CN: 它查找带有有效 gel 分配且目标可达的空壳 Pawn。
namespace SexSlaveCraft
{
    /// <summary>
    /// WorkGiver：自动扫描地图上有分配的空壳Pawn，让殖民者拿凝胶去塞入。
    /// 也支持右键手动指派（directOrderable=true）。
    /// 扫描目标是空壳Pawn（TargetThing），Job中 TargetA=凝胶, TargetB=空壳Pawn。
    /// </summary>
    public class WorkGiver_InsertPersonality : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        // ==========================================================
        // 自动扫描：返回所有有分配的空壳Pawn
        // ==========================================================
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Pawn p in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (!WorkGiverTargetUtility.TryGetInsertPersonalityJobTarget(pawn, p, false, out _)) continue;
                yield return p;
            }
        }

        // ==========================================================
        // 核心逻辑：为空壳Pawn生成搬运凝胶的Job
        // ==========================================================
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn hollow = t as Pawn;
            if (!WorkGiverTargetUtility.TryGetInsertPersonalityJobTarget(pawn, hollow, forced, out Thing gel)) return null;

            Job job = JobMaker.MakeJob(SSCDefOf.SSC_Job_InsertPersonality, gel, hollow);
            job.count = 1;
            return job;
        }
    }
}
