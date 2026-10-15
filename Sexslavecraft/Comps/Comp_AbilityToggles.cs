// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 个人偏好随人格流转；健康标记及 PNA 身体状态不进入这个存档容器。
        public SSCAbilityToggleState abilityToggles = new SSCAbilityToggleState();
        public void ExposeAbilityToggles()
            => Scribe_Deep.Look(ref abilityToggles, "sscAbilityToggles");
    }

    public partial class CompPersonalityStore
    {
        public SSCAbilityToggleState abilityToggles = new SSCAbilityToggleState();
        public void ExposeAbilityToggleSnapshot()
            => Scribe_Deep.Look(ref abilityToggles, "sscAbilityToggles");

        // 采集与加工均复制原始偏好，不能用当前身份下的 IsActive 丢掉暂停的开启值。
        public void CaptureAbilityToggles(Pawn pawn)
            => abilityToggles = pawn?.TryGetComp<CompSexSlaveTraining>()?.abilityToggles?.Copy() ?? new SSCAbilityToggleState();

        public void CopyAbilityToggleSnapshot(CompPersonalityStore other)
            => abilityToggles = other?.abilityToggles?.Copy() ?? new SSCAbilityToggleState();
    }
}
