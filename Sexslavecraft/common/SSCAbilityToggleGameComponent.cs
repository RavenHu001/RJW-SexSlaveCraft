// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCAbilityToggleGameComponent : GameComponent
    {
        /// <summary>由游戏创建协调器，不在构造期读取半加载的身体或写入默认值。</summary>
        public SSCAbilityToggleGameComponent(Game game) { }
        /// <summary>完整加载后一次性核对现存角色的派生标记，不触碰身体／关系字段或生产状态。</summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // 完整加载后统一修复地图/世界角色；不在 ExposeData 半成品阶段写健康状态，也不轮询 Tick。
            foreach (Pawn pawn in SSCRestrictionGameComponent.AllPawns()) SSCAbilityToggleUtility.SyncMarkers(pawn);
        }
    }
}
