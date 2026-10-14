// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>完整引用恢复后规范化身体负荷与依赖；每次读档幂等，不扫描每 tick 或推断历史暴露。</summary>
    public sealed class SSCPnaGameComponent : GameComponent
    {
        public SSCPnaGameComponent(Game game) { }

        public override void FinalizeInit()
        {
            // 初始化阶段遍历现有角色，涵盖地图、离图与没有调教组件的身体。
            base.FinalizeInit();
            foreach (Pawn pawn in SSCRestrictionGameComponent.AllPawns()) NormalizePawn(pawn);
        }

        public static void NormalizePawn(Pawn pawn)
        {
            // 先按有效链确定负荷形态并清除受保护身体的普通依赖，再清理旧依赖值。
            SSCPnaExposureUtility.RefreshBodyState(pawn, true);
            SSCPnaExposureUtility.NormalizeAddiction(pawn);
            // 满旧记录不补新依赖；已有合法进度和满足度不重置。
        }
    }
}
