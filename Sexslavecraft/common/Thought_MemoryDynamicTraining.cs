using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class Thought_MemoryDynamicTraining : Thought_MemorySocial
    {
        private enum DisplayLevel
        {
            Resentment,
            Wavering,
            Adaptation,
            Submission
        }

        private static DisplayLevel GetLevel(float offset)
        {
            if (offset < 0f) return DisplayLevel.Resentment;
            if (offset <= 5f) return DisplayLevel.Wavering;
            if (offset <= 10f) return DisplayLevel.Adaptation;
            return DisplayLevel.Submission;
        }

        private DisplayLevel Level => GetLevel(opinionOffset);

        public static int GetDisplayStage(float opinionOffset)
            => (int)GetLevel(opinionOffset);

        public override string LabelCap
            => ("SSC_TrainingOpinionMemory_" + Level)
                .Translate()
                .CapitalizeFirst();

        public override string LabelCapSocial => LabelCap;

        public override string Description
            => ("SSC_TrainingOpinionMemory_" + Level + "Desc")
                .Translate(otherPawn?.LabelShort ?? "???", opinionOffset.ToStringWithSign());

        public override bool GroupsWith(Thought other)
        {
            if (!(other is Thought_MemoryDynamicTraining trainingMemory)) return false;

            return base.GroupsWith(other)
                && Level == trainingMemory.Level;
        }
    }
}
