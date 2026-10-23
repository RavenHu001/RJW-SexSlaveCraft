// AI 辅助生成
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>保留原版到场检查，主持者还须备齐材料并返回当前仪式站位。</summary>
    public class StageEndTrigger_CosmeticMaterialsReady : StageEndTrigger_RolesArrived
    {
        protected override bool ArrivedCheck(string roleId, LordJob_Ritual ritual)
        {
            // 先保留原版角色到场标记；受塑者直接沿用，只对领取材料的主持者追加条件。
            if (!base.ArrivedCheck(roleId, ritual)) return false;
            if (roleId != SSCCosmeticUtility.HostRole) return true;
            // 到场标记可能在离位取料后残留，须确认背包齐备且人已回到指定格，才允许推进扣料。
            var host = ritual.PawnWithRole(roleId);
            return RitualStageAction_ConsumeCosmeticMaterials.MaterialsReady(host) && host.Spawned &&
                host.mindState?.duty != null && host.Position == host.mindState.duty.focus.Cell;
        }
    }
}
