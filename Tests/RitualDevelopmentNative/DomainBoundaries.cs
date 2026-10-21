// AI 辅助生成
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    // 本专项链接真实仪式处理器，领域后果故意不可执行；整场 Apply 由 RitualLifecycle 验证。
    // 这些边界只满足处理器其余方法的编译，不替代被测的原版文化类型、曲线或点数算法。
    public static class SSCMod { public static SSCSettings settings = new SSCSettings(); }
    public static class SSCLog { public static void Important(string text) { } }
    public static class Strings
    {
        public static string Ritual_RepeatPenalty => "repeat";
        public static string Ritual_FlawedLoversCreated(string a, string b) => throw new NotSupportedException();
    }
    public static class BindingRitualStateUtility
    {
        public static bool TryClaimOutcome(LordJob_Ritual job) => throw new NotSupportedException("Use RitualLifecycle for whole ritual Apply.");
        public static bool CanContinueOutcome(LordJob_Ritual job, Pawn master, Pawn slave) => throw new NotSupportedException();
    }
    public static class TrainingJobUtility
    {
        public sealed class JobContext
        {
            public JobDriver Driver;
            public bool IsCurrent => throw new NotSupportedException();
            public LocalTargetInfo Target => throw new NotSupportedException();
            public JobContext(JobDriver driver) { Driver = driver; }
        }
    }
    public sealed class JobDriver_RitualTraining : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => throw new NotSupportedException();
        protected override IEnumerable<Toil> MakeNewToils() => throw new NotSupportedException();
    }
    public static class ConditioningUtility
    {
        public static string ExecuteRitualOutcome(Pawn a, Pawn b, float quality, ThoughtDef memory) => throw new NotSupportedException();
    }
    public static class SpecializationTrainingProgressUtility
    {
        public static float RitualScore(float quality) => throw new NotSupportedException();
        public static void NotifyTrainingCompleted(Pawn a, Pawn b, float score) => throw new NotSupportedException();
    }
    public static class TrainerSpecializationProgressUtility
    {
        public static void NotifyProvidedTrainingCompleted(Pawn a, Pawn b) => throw new NotSupportedException();
    }
    public static class TrainerOfficerFeedback
    {
        public static void NotifyTrainingCompleted(Pawn a, Pawn b) => throw new NotSupportedException();
    }
    public static class SSCMasterProgressionUtility
    {
        public const float BindingRitualExperience = 3f;
        public static void TryAddExperience(Pawn pawn, float value) => throw new NotSupportedException();
    }
    public static class SSCBondRelationUtility
    {
        public static bool TryCreateAfterRitual(Pawn a, Pawn b, Func<bool> canContinue) => throw new NotSupportedException();
    }
}
