using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    internal static class DebugActions_SSCSelfTraining
    {
        /// <summary>阶段 2 的受控入口；正式右键指派和自动分流分别在后续阶段接入。</summary>
        [DebugAction("SSC", "Self-training: choose spot (stage 2)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartSelected()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null)
            {
                Messages.Message("Select one pawn first. / 请先选中一个角色。", MessageTypeDefOf.RejectInput);
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
                Messages.Message("Self-training job was rejected. / 自我调教任务未被接受。", pawn,
                    MessageTypeDefOf.RejectInput, false);
        }
    }
}
