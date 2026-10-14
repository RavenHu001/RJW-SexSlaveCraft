// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>引用恢复后一次迁移地图、世界、运输和容器角色，不新增周期扫描。</summary>
    public sealed class SSCMasterProgressionGameComponent : GameComponent
    {
        public SSCMasterProgressionGameComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // 复用全存档角色快照，覆盖地图、世界、商队、运输和临时容器；
            // 只在游戏初始化完成后处理，不增加 Tick 扫描，也不依赖玩家打开面板。
            foreach (Pawn pawn in SSCRestrictionGameComponent.AllPawns())
            {
                SSCMasterProgressionMigration.MigratePawn(pawn);
                // 现代角色不再补建，但已有记录仍按读回的身份重算加成和健康缓存。
                SSCMasterProgressionUtility.RefreshActivation(pawn);
            }
        }
    }
}
