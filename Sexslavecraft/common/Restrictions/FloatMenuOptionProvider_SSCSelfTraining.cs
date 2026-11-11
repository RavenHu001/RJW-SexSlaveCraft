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

        /// <summary>只为选中角色本人预检菜单项，禁用行显示本次不可用原因。</summary>
        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || clickedPawn != actor || !SSCIdentityUtility.IsSexSlave(actor)) yield break;
            string label = "SSC_SelfTraining_Assign".Translate();
            if (!JobDriver_SelfTraining.CanStart(actor, actor.Position, out string reason))
            {
                yield return new FloatMenuOption(label + " — " + reason, null, MenuOptionPriority.High);
                yield break;
            }
            yield return new FloatMenuOption(label, () => ShowInteractions(actor),
                MenuOptionPriority.High);
        }

        /// <summary>打开动作菜单前复查资格，避免用已经失效的右键选项继续操作。</summary>
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

        /// <summary>重新验证选中的动作，提交失败时仅在命令入口未反馈的情况下补发提示。</summary>
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
                bool reported = SSCRestrictionJobGuard.WasOrderedRejectionReported(job, actor);
                comp.ClearManualSelfTraining(job);
                if (!reported) Reject(actor, "SSC_SelfTraining_JobRejected".Translate());
            }
        }

        /// <summary>只在手动入口发送一条拒绝消息。</summary>
        private static void Reject(Pawn pawn, string reason)
            => Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
    }
}
