using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    internal static class DebugActions_SSCSelfTraining
    {
        /// <summary>供开发模式手动指定自我调教地点。</summary>
        [DebugAction("SSC", "Self-training: choose spot", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartSelected()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null)
            {
                Messages.Message("SSC_SelfTraining_SelectPawn".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetLocations = true,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = target => !target.HasThing
            }, target => StartAt(pawn, target.Cell));
        }

        private static void StartAt(Pawn pawn, IntVec3 destination)
        {
            if (!JobDriver_SelfTraining.CanStart(pawn, destination, out string reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Job job = JobMaker.MakeJob(SSCDefOf.SelfTraining, pawn, null, destination);
            if (!pawn.jobs.TryTakeOrderedJob(job))
                Messages.Message("SSC_SelfTraining_JobRejected".Translate(), pawn,
                    MessageTypeDefOf.RejectInput, false);
        }
    }
}
