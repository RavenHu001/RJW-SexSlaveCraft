// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仅将原版 Multi/DropThing 应用于取料的主持者，避免动到观众自带的物资。</summary>
    public class RitualStageAction_DropCosmeticMaterials : RitualStageAction_Multi
    {
        public override void Apply(LordJob_Ritual ritual)
        {
            ApplyToPawn(ritual, ritual.PawnWithRole(SSCCosmeticUtility.HostRole));
        }

        public override void ApplyToPawn(LordJob_Ritual ritual, Pawn pawn)
        {
            if (pawn != null && pawn.Spawned && pawn == ritual.PawnWithRole(SSCCosmeticUtility.HostRole))
                base.ApplyToPawn(ritual, pawn);
        }
    }
}
