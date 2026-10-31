// AI 辅助生成。训练页和概览共用显式指派菜单；构造与悬停只读，选择后才提交既有事务。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCTrainerAssignmentMenu
    {
        internal static List<FloatMenuOption> Options(Pawn source, Action changed = null, Func<bool> current = null)
        {
            var options = new List<FloatMenuOption>();
            Game game = Current.Game;
            CompSexSlaveTraining comp = source?.TryGetComp<CompSexSlaveTraining>();
            Pawn previous = comp?.selectedTrainer;
            Pawn owner = SSCBondUtility.GetBoundMaster(source);
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            var clearRequest = new SSCTrainerWorkUtility.Request(source, null, current: current);
            if (previous != null)
            {
                SSCTrainerWorkState state = SSCTrainerWorkUtility.GetState(previous, source);
                if (state != SSCTrainerWorkState.Active)
                    options.Add(new FloatMenuOption(TrainerAssignmentUtility.GetAssignedTrainerLabel(source), null));
                FloatMenuOption enable = EnableOption(source, changed, current);
                if (enable != null) options.Add(enable);
            }
#endif
            // 候选仅在打开菜单时物化一次；沿用现有自由殖民者、工作、身份和授权规则。
            var candidates = TrainerAssignmentUtility.GetTrainerCandidates(source).ToList();
            if (candidates.Count == 0) options.Add(new FloatMenuOption("SSC_ITab_NoTrainerAvailable".Translate(), null));
            foreach (Pawn candidate in candidates)
            {
                string label = candidate.LabelShort;
                if (owner != null && candidate == owner) label += "SSC_ITab_TrainerMasterSuffix".Translate();
                else if (owner != null && comp?.restrictionConfig?.IsValid() == true && !comp.restrictionConfig.rules.receiveTraining)
                    label += "SSC_Restrictions_AssignAuthorizesSuffix".Translate();
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
                SSCTrainerWorkState state = SSCTrainerWorkUtility.GetState(candidate, source);
                bool available = state == SSCTrainerWorkState.Active || state == SSCTrainerWorkState.Off;
                if (state == SSCTrainerWorkState.Off) label += "SSC_TrainerWork_AssignAndEnable".Translate();
                else if (!available) label += "SSC_TrainerWork_StatusSuffix".Translate(SSCTrainerWorkUtility.Reason(state));
                var request = new SSCTrainerWorkUtility.Request(source, candidate, current: current);
                options.Add(new FloatMenuOption(label, available ? (Action)(() => Submit(request, changed)) : null)
                { tooltip = available ? "SSC_TrainerWork_CategoryTip".Translate() : SSCTrainerWorkUtility.Reason(state, candidate) });
#else
                options.Add(new FloatMenuOption(label, () => Assign(candidate)));
#endif
            }
            options.Add(new FloatMenuOption("SSC_ITab_ClearTrainer".Translate(),
                SSCRestrictionTrainerAssignment.CanAssign(source, null) ? (Action)(() => Assign(null)) : null));
            return options;

            void Assign(Pawn trainer)
            {
                // 菜单可能迟到：跨游戏、换来源、换组件、改绑或外部改指派后，拒绝覆盖新状态。
                // 最终资格、强制限制和非主人调教授权仍由 TryAssignTrainer 原子校验与提交。
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
                // 清空也捕获原人格；不因菜单迟到重新授予新载体的权限。
                Submit(clearRequest, changed);
#else
                if (game == null || !ReferenceEquals(game, Current.Game) || Current.ProgramState != ProgramState.Playing ||
                    Scribe.mode != LoadSaveMode.Inactive || current?.Invoke() == false || source == null || source.Destroyed || source.Discarded ||
                    comp == null || source.TryGetComp<CompSexSlaveTraining>() != comp || comp.selectedTrainer != previous ||
                    SSCBondUtility.GetBoundMaster(source) != owner || trainer != null && !SSCIdentityUtility.IsSexSlave(source) ||
                    !SSCBondUtility.TryAssignTrainer(source, trainer))
                {
                    Messages.Message("SSC_Restrictions_AssignFailed".Translate(), source, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                changed?.Invoke();
#endif
            }
        }

#if SSC_RW17 || SSC_TRAINER_WORK_TEST
        /// <summary>训练页按钮与概览菜单复用；只打开原指定者的工作，不重新提交指派或授权。</summary>
        internal static FloatMenuOption EnableOption(Pawn source, Action changed = null, Func<bool> current = null)
        {
            Pawn trainer = source?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            if (trainer == source || SSCTrainerWorkUtility.GetState(trainer, source) != SSCTrainerWorkState.Off) return null;
            var request = new SSCTrainerWorkUtility.Request(source, trainer, true, current);
            return new FloatMenuOption("SSC_TrainerWork_EnableAssigned".Translate(trainer.LabelShort), () => Submit(request, changed))
            { tooltip = "SSC_TrainerWork_CategoryTip".Translate() };
        }

        private static void Submit(SSCTrainerWorkUtility.Request request, Action changed)
        {
            if (!request.TrySubmit(out string reason))
            {
                Messages.Message(reason, request.Source, MessageTypeDefOf.RejectInput, false);
                return;
            }
            changed?.Invoke();
        }
#endif
    }
}
