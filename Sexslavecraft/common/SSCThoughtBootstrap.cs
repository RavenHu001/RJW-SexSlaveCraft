using RimWorld;
using Verse;

// EN: This bootstrap adjusts SSC thought defs after they are loaded.
// EN: It tweaks opinion offsets for training-memory thoughts.
// CN: 这个启动器会在 SSC 想法 Def 载入后做二次调整。
// CN: 它会微调“调教记忆”的好感度偏移。同床效果由睡眠结束时生成的记忆处理。
namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class SSCThoughtBootstrap
    {
        static SSCThoughtBootstrap()
        {
            AdjustExistingThoughtDefs();
        }

        private static void AdjustExistingThoughtDefs()
        {
            // EN: SSC's social training memories need stronger opinion offsets than the XML default values.
            // CN: SSC 的“调教记忆”社交想法需要比 XML 默认值更强一点的好感度偏移。
            SetOpinionOffset(SSCDefOf.SSC_Training_Social_Lvl2, 5);
            SetOpinionOffset(SSCDefOf.SSC_Training_Social_Lvl3, 15);
        }

        private static void SetOpinionOffset(ThoughtDef def, int opinionOffset)
        {
            if (def?.stages == null || def.stages.Count == 0 || def.stages[0] == null) return;
            def.stages[0].baseOpinionOffset = opinionOffset;
        }
    }

}
