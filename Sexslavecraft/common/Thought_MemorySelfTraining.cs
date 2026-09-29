using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存完成当时的角色感受，避免读档后按新的阶段、特质或关系重写旧记忆。</summary>
    public sealed class Thought_MemorySelfTraining : Thought_Memory
    {
        public int StageAtCompletion = 1;
        public bool MasochistAtCompletion;
        public int OpinionCategory;
        public string ImaginedPawnLabel;

        public override string LabelCap
            => ("SSC_SelfTraining_MemoryStage" + StageAtCompletion).Translate().CapitalizeFirst();

        public override string Description
        {
            get
            {
                string stage = ("SSC_SelfTraining_MemoryStage" + StageAtCompletion + "Desc").Translate();
                string trait = (MasochistAtCompletion ? "SSC_SelfTraining_MemoryMasochist" :
                    "SSC_SelfTraining_MemoryNoMasochist").Translate();
                string relation = ("SSC_SelfTraining_MemoryOpinion" + OpinionCategory)
                    .Translate(ImaginedPawnLabel ?? string.Empty);
                return stage + " " + trait + " " + relation;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref StageAtCompletion, "sscSelfTrainingMemoryStage", 1);
            Scribe_Values.Look(ref MasochistAtCompletion, "sscSelfTrainingMemoryMasochist", false);
            Scribe_Values.Look(ref OpinionCategory, "sscSelfTrainingMemoryOpinionCategory", 0);
            Scribe_Values.Look(ref ImaginedPawnLabel, "sscSelfTrainingMemoryImaginedLabel");
        }
    }
}
