// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>所有存档引用恢复后清理历史错误关系，涵盖地图、世界、商队及临时容器角色。</summary>
    public sealed class SSCBondRelationGameComponent : GameComponent
    {
        public SSCBondRelationGameComponent(Game game) { }
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // 等所有 Pawn 引用和 Hediff 完整恢复后，再按最终锁链清理旧档。
            // 复用现有世界快照覆盖离图角色；每次加载维护一次，不增加逐 Tick 扫描或自动补发奖励。
            foreach (Pawn pawn in SSCRestrictionGameComponent.AllPawns())
                SSCBondRelationUtility.RemoveInvalidRelations(pawn);
        }
    }
}
