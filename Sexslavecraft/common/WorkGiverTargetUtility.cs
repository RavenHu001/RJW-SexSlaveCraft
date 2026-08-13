using RimWorld;
using Verse;
using Verse.AI;

// EN: This file centralizes non-training workgiver target checks.
// EN: It keeps PE and personality-insertion validation in one place.
// CN: 这个文件集中处理非训练类 WorkGiver 的目标判定。
// CN: 它把 PE 和人格植入的目标校验统一收口。
namespace SexSlaveCraft
{
    public static class WorkGiverTargetUtility
    {
        public static bool IsValidPETarget(Pawn actor, Pawn target, bool forced)
        {
            if (actor == null || target == null) return false;
            if (RabbitCloneUtility.IsRabbitClone(target))
            {
                if (forced)
                {
                    JobFailReason.Is("兔分身不能进行人格排泄。");
                }
                return false;
            }
            if (!TrainingJobUtility.TryValidateTarget(target, forced, "SSC_PE")) return false;
            if (!target.RaceProps.Humanlike || target == actor) return false;
            if (!target.IsColonist && !target.IsPrisonerOfColony) return false;

            Hediff hediff = target.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreting);
            if (hediff == null || hediff.Severity < 1.0f) return false;

            CompSexSlaveTraining trainingComp = target.TryGetComp<CompSexSlaveTraining>();
            if (trainingComp?.IsWaitingAfterFailedValidation == true) return false;

            return actor.CanReserve(target, 1, -1, null, forced);
        }

        public static bool TryGetInsertPersonalityJobTarget(Pawn actor, Pawn hollow, bool forced, out Thing gel)
        {
            gel = null;
            if (actor == null || hollow == null || hollow == actor) return false;
            if (!hollow.RaceProps.Humanlike || hollow.Dead) return false;

            LifeForceConflictUtility.TryRemoveLifeForceGeneIfConflicting(hollow);
            if (!hollow.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done)) return false;

            // Assignment lives on the map component, so keep the workgiver-facing lookup here instead of duplicating it.
            MapComponent_PersonalityAssignment mapComp = actor.Map?.GetComponent<MapComponent_PersonalityAssignment>();
            if (mapComp == null) return false;

            gel = mapComp.GetAssignedGel(hollow);
            if (gel == null || gel.Destroyed || !gel.Spawned) return false;
            if (gel.IsForbidden(actor)) return false;
            if (!actor.CanReserve(gel, 1, -1, null, forced)) return false;
            if (!actor.CanReserve(hollow, 1, -1, null, forced)) return false;
            if (!actor.CanReach(gel, PathEndMode.ClosestTouch, Danger.Deadly)) return false;
            if (!actor.CanReach(hollow, PathEndMode.Touch, Danger.Deadly)) return false;

            return true;
        }
    }
}
