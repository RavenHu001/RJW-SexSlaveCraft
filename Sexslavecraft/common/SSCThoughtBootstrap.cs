using RimWorld;
using Verse;

// EN: This bootstrap adjusts SSC thought defs after they are loaded.
// EN: It tweaks opinion offsets for training-memory thoughts and also provides the custom `与主人同床` thought worker used by the shared-bed system.
// CN: 这个启动器会在 SSC 想法 Def 载入后做二次调整。
// CN: 它会微调“调教记忆”的好感度偏移，并提供共享床位系统使用的“与主人同床”想法 worker。
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

    public class ThoughtWorker_SSC_SharedBedWithMaster : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || !p.RaceProps.Humanlike) return ThoughtState.Inactive;
            // EN: Use SSCSharedBedUtility to ask whether this pawn actually slept in the master's bed and which stage applies.
            // CN: 这里通过 SSCSharedBedUtility 判断这个 Pawn 是否真的和主人同床，以及该触发哪一档想法。
            if (!SSCSharedBedUtility.TryGetSharedBedContext(p, out Pawn master, out Pawn bondedPawn, out Building_Bed bed)) return ThoughtState.Inactive;
            if (bondedPawn != p) return ThoughtState.Inactive;

            int stage = SSCSharedBedUtility.GetSharedBedThoughtStage(bondedPawn, master);
            if (stage < 0) return ThoughtState.Inactive;

            return ThoughtState.ActiveAtStage(stage);
        }
    }
}
