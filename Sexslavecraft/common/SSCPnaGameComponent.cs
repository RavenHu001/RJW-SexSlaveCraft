// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>完整引用恢复后规范化旧身体负荷；每次读档幂等，不扫描每 tick 或推断历史暴露。</summary>
    public sealed class SSCPnaGameComponent : GameComponent
    {
        public SSCPnaGameComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (Pawn pawn in SSCRestrictionGameComponent.AllPawns()) NormalizePawn(pawn);
        }

        public static void NormalizePawn(Pawn pawn)
        {
            SSCPnaExposureUtility.RefreshBodyState(pawn, true);
            // 满旧记录不补新依赖；普通旧依赖和满足度本批不重写。
        }
    }
}
