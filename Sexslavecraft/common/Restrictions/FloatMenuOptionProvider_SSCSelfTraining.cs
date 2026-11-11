using System.Collections.Generic;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>独立于 RJW 的英雄模式菜单，按 SSC 自我调教许可显示本人指派入口。</summary>
    public class FloatMenuOptionProvider_SSCSelfTraining : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool CanSelfTarget => true;
        protected override bool RequiresManipulation => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || clickedPawn != actor) yield break;
            SSCRestrictionDecision permission = SSCRestrictionPolicy.Evaluate(
                new SSCRestrictionRequest(actor, actor, SSCInteractionKind.SelfTraining, true));
            if (!permission.Allowed) yield break;
            yield return new FloatMenuOption("SSC_SelfTraining_Assign".Translate(), () => ShowInteractions(actor),
                MenuOptionPriority.High);
        }

        private static void ShowInteractions(Pawn actor)
        {
            if (!JobDriver_SelfTraining.CanStart(actor, actor.Position, out string reason))
            {
                Reject(actor, reason);
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (InteractionDef def in SSCSelfTrainingInteractions.Available(actor))
            {
                InteractionDef chosen = def;
                var interaction = new SexInteraction(chosen);
                string label = interaction.Extension.RMBLabel;
                if (string.IsNullOrEmpty(label)) label = chosen.label ?? chosen.defName;
                label = label.CapitalizeFirst();
                if (RJWSettings.DevMode) label += " (" + chosen.defName + ")";
                options.Add(new FloatMenuOption(label, () => StartSelected(actor, chosen), MenuOptionPriority.High));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("SSC_SelfTraining_NoInteraction".Translate(), null));
            FloatMenuUtility.MakeMenu(options, option => option.Label, option => option.action);
        }

        private static void StartSelected(Pawn actor, InteractionDef interaction)
        {
            if (!JobDriver_SelfTraining.CanStart(actor, actor.Position, out string reason))
            {
                Reject(actor, reason);
                return;
            }
            if (!SSCSelfTrainingInteractions.TryBuild(actor, interaction, out _))
            {
                Reject(actor, "SSC_SelfTraining_InteractionChanged".Translate());
                return;
            }
            CompSexSlaveTraining comp = actor.TryGetComp<CompSexSlaveTraining>();
            Job job = JobMaker.MakeJob(SSCDefOf.SelfTraining, actor, null, actor.Position);
            if (comp == null || job.loadID <= 0)
            {
                Reject(actor, "SSC_SelfTraining_JobRejected".Translate());
                return;
            }
            comp.RegisterManualSelfTraining(job, interaction);
            if (!actor.jobs.TryTakeOrderedJob(job))
            {
                comp.ClearManualSelfTraining(job);
                Reject(actor, "SSC_SelfTraining_JobRejected".Translate());
            }
        }

        private static void Reject(Pawn pawn, string reason)
            => Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
    }
}
