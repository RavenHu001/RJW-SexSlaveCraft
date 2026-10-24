// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 个人偏好随人格流转；健康标记及 PNA 身体状态不进入这个存档容器。
        public SSCAbilityToggleState abilityToggles = new SSCAbilityToggleState();
        /// <summary>沿原 sscAbilityToggles 节点保存人格字典；不纳入泌乳、关系或身体后端字段。</summary>
        public void ExposeAbilityToggles()
            => Scribe_Deep.Look(ref abilityToggles, "sscAbilityToggles");
    }

    public partial class CompPersonalityStore
    {
        public SSCAbilityToggleState abilityToggles = new SSCAbilityToggleState();
        /// <summary>凝胶独立保存原人格字典节点，旧档缺节点由查询／植入按既有空快照解释。</summary>
        public void ExposeAbilityToggleSnapshot()
            => Scribe_Deep.Look(ref abilityToggles, "sscAbilityToggles");

        // 采集与加工均复制原始偏好，不能用当前身份下的 IsActive 丢掉暂停的开启值。
        /// <summary>在清源前捕获原始偏好副本；退出身份、空壳或暂停的开启值也不丢失。</summary>
        public void CaptureAbilityToggles(Pawn pawn)
            => abilityToggles = pawn?.TryGetComp<CompSexSlaveTraining>()?.abilityToggles?.Copy() ?? new SSCAbilityToggleState();

        /// <summary>加工凝胶复制独立字典，保留未知键及明确 false；不复制任何其他后端。</summary>
        public void CopyAbilityToggleSnapshot(CompPersonalityStore other)
            => abilityToggles = other?.abilityToggles?.Copy() ?? new SSCAbilityToggleState();
    }
}
