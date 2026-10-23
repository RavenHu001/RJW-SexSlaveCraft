// AI 辅助生成
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>保留原版到场检查，主持者还须备齐材料并返回当前仪式站位。</summary>
    public class StageEndTrigger_CosmeticMaterialsReady : StageEndTrigger_RolesArrived
    {
        protected override bool ArrivedCheck(string roleId, LordJob_Ritual ritual)
        {
            if (!base.ArrivedCheck(roleId, ritual)) return false;
            if (roleId != SSCCosmeticUtility.HostRole) return true;
            var host = ritual.PawnWithRole(roleId);
            return RitualStageAction_ConsumeCosmeticMaterials.MaterialsReady(host) && host.Spawned &&
                host.mindState?.duty != null && host.Position == host.mindState.duty.focus.Cell;
        }
    }
}
