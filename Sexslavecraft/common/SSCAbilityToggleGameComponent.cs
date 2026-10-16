// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCAbilityToggleGameComponent : GameComponent
    {
        public SSCAbilityToggleGameComponent(Game game) { }
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // 完整加载后统一修复地图/世界角色；不在 ExposeData 半成品阶段写健康状态，也不轮询 Tick。
            foreach (Pawn pawn in SSCRestrictionGameComponent.AllPawns()) SSCAbilityToggleUtility.SyncMarkers(pawn);
        }
    }
}
