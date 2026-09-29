using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>完成记忆和许可状态心情的游戏侧入口。</summary>
    public static class SSCSelfTrainingFeedback
    {
        public static void OnCompleted(Pawn pawn, SSCSelfTrainingSnapshot snapshot)
        {
            int stage = SSCSelfTrainingFeedbackRules.NormalizeStage(SSCIdentityUtility.GetSexSlaveStage(pawn));
            bool masochist = xxx.is_masochist(pawn);
            bool hadImaginedPawn = snapshot.HadImaginedPawnAtStart || snapshot.ImaginedPawn != null;
            int opinionCategory = SSCSelfTrainingFeedbackRules.OpinionCategory(
                hadImaginedPawn, snapshot.RawOpinionAtStart);
            ThoughtDef def = SSCDefOf.SSC_SelfTraining_Completed;
            MemoryThoughtHandler memories = pawn.needs?.mood?.thoughts?.memories;
            if (def != null && memories != null)
            {
                int mood = SSCSelfTrainingFeedbackRules.CompletionMood(stage, masochist, opinionCategory);
                var memory = (Thought_MemorySelfTraining)ThoughtMaker.MakeThought(def, mood + 7);
                memory.StageAtCompletion = stage;
                memory.MasochistAtCompletion = masochist;
                memory.OpinionCategory = opinionCategory;
                memory.ImaginedPawnLabel = snapshot.ImaginedPawnLabelAtStart ??
                    snapshot.ImaginedPawn?.LabelShortCap ?? string.Empty;
                // 一天内重复完成时更新心情、文案及持续时间。
                memories.RemoveMemoriesOfDef(def);
                memories.TryGainMemory(memory);
            }

            Messages.Message("SSC_SelfTraining_Outcome".Translate(
                pawn.LabelShortCap, snapshot.Score.ToString("F1"), snapshot.CorruptionGain.ToString("P1")),
                pawn, MessageTypeDefOf.NeutralEvent, false);
        }

        public static void NotifyStatusChanged(Pawn pawn)
        {
            if (pawn != null && !pawn.Dead)
                pawn.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
        }

        public static bool TryGetPermissionState(Pawn pawn, out bool selfTrainingAllowed, out int stageIndex)
        {
            selfTrainingAllowed = false;
            stageIndex = 0;
            if (pawn == null || pawn.Dead || !(SSCMod.settings?.enableSexSlaveProtectionRules ?? true) ||
                !SSCSelfTrainingEligibility.IsEligible(pawn)) return false;
            SSCRestrictionConfig config = pawn.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            if (config?.IsValid() != true) return false;

            var masturbation = SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(
                pawn, pawn, SSCInteractionKind.Masturbation, true));
            var training = SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(
                pawn, pawn, SSCInteractionKind.SelfTraining, true));
            if (!SSCSelfTrainingFeedbackRules.TryPermissionKind(
                masturbation.Reason, training.Reason, out selfTrainingAllowed))
                return false;

            stageIndex = SSCSelfTrainingFeedbackRules.PermissionStageIndex(
                SSCIdentityUtility.GetSexSlaveStage(pawn), xxx.is_masochist(pawn));
            return true;
        }
    }

    public sealed class ThoughtWorker_SelfTrainingBothDenied : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
            => SSCSelfTrainingFeedback.TryGetPermissionState(p, out bool allowed, out int stage) && !allowed
                ? ThoughtState.ActiveAtStage(stage) : ThoughtState.Inactive;
    }

    public sealed class ThoughtWorker_SelfTrainingOnly : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
            => SSCSelfTrainingFeedback.TryGetPermissionState(p, out bool allowed, out int stage) && allowed
                ? ThoughtState.ActiveAtStage(stage) : ThoughtState.Inactive;
    }
}
