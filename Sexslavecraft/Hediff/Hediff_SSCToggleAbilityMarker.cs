// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只展示个人能力的当前生效状态；保存偏好始终来自人格组件。</summary>
    public sealed class Hediff_SSCToggleAbilityMarker : Hediff
    {
        // 可见性再查当前生效条件，健康页与外部上下文不会把失效的残留实例当成开启。
        public override bool Visible => SSCAbilityToggleUtility.IsActive(pawn,
            SSCAbilityToggleUtility.FindByMarker(def)?.Id) && base.Visible;
        // 生命周期协调器管理增删；标记没有疾病衰减或百分比进度，不自行决定偏好。
        public override bool ShouldRemove => false;
        public override string SeverityLabel => null;
    }
}
