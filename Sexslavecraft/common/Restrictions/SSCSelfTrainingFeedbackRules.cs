namespace SexSlaveCraft
{
    /// <summary>自我调教反馈的数值与索引规则；不读取游戏状态。</summary>
    public static class SSCSelfTrainingFeedbackRules
    {
        public const int NoImaginedPawn = 0;
        public const int NegativeOpinion = 1;
        public const int NeutralOpinion = 2;
        public const int PositiveOpinion = 3;

        public static int NormalizeStage(int stage) => stage < 1 ? 1 : stage > 4 ? 4 : stage;

        public static int OpinionCategory(bool hadImaginedPawn, int rawOpinion)
        {
            if (!hadImaginedPawn) return NoImaginedPawn;
            if (rawOpinion <= -30) return NegativeOpinion;
            return rawOpinion >= 30 ? PositiveOpinion : NeutralOpinion;
        }

        public static int CompletionMood(int stage, bool masochist, int opinionCategory)
        {
            int baseMood = 2 * NormalizeStage(stage) - 6;
            int opinionMood = opinionCategory == NegativeOpinion ? -3 :
                opinionCategory == PositiveOpinion ? 2 : 0;
            return baseMood + (masochist ? 2 : 0) + opinionMood;
        }

        public static int PermissionStageIndex(int stage, bool masochist)
            => (masochist ? 4 : 0) + NormalizeStage(stage) - 1;

        public static int PermissionMood(bool selfTrainingAllowed, int stage, bool masochist)
        {
            int normalized = NormalizeStage(stage);
            if (selfTrainingAllowed) return normalized - 3 + (masochist ? 2 : 0);
            return -4 - normalized + (masochist ? 2 * normalized : 0);
        }

        public static bool TryPermissionKind(SSCRestrictionReason masturbationReason,
            SSCRestrictionReason trainingReason, out bool selfTrainingAllowed)
        {
            selfTrainingAllowed = trainingReason == SSCRestrictionReason.Allowed;
            return masturbationReason == SSCRestrictionReason.RuleDenied &&
                (trainingReason == SSCRestrictionReason.Allowed ||
                 trainingReason == SSCRestrictionReason.RuleDenied);
        }
    }
}
