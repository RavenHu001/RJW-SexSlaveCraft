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
            // 整场中断时只把主持者交给逐人入口，不遍历参与者背包。
            ApplyToPawn(ritual, ritual.PawnWithRole(SSCCosmeticUtility.HostRole));
        }

        public override void ApplyToPawn(LordJob_Ritual ritual, Pawn pawn)
        {
            // 离场回调也会传入其他角色；筛选后复用 XML 中原版 DropThing 的数量和落地处理。
            if (pawn != null && pawn.Spawned && pawn == ritual.PawnWithRole(SSCCosmeticUtility.HostRole))
                base.ApplyToPawn(ritual, pawn);
        }
    }
}
