// AI 辅助生成：生育开关保持身体 Hediff 保存，全部方向性规则委托原工具。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仅桥接当前子宫开发记录；完成度、妊娠、配置与健康通知均由原生育工具维护。</summary>
    internal sealed class SSCUterusAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCUterusAbilityToggleBackend Instance = new SSCUterusAbilityToggleBackend();
        /// <summary>保持原 UI 与生育消费者的有效选择定义，未完成时仍关闭。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCUterusDevelopmentUtility.Find(pawn)?.FertilityEnabled ?? false;
        /// <summary>只判断身体记录存在，不复制完成度、配置或妊娠检查。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCUterusDevelopmentUtility.Find(pawn) == null ? "SSC_BodyManagement_NotStarted".Translate().ToString() : null;
        /// <summary>开启／关闭分别使用原权威检查，包含未完成、妊娠和配置缺失的具体原因。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCUterusDevelopmentUtility.CanSetFertilityEnabled(pawn, enabled, out string reason) ? null : reason;
        /// <summary>捕获当前身体的子宫开发实例，不向人格快照搬运其开关。</summary>
        public override object CaptureOwner(Pawn pawn) => SSCUterusDevelopmentUtility.Find(pawn);
        /// <summary>开发记录被删除／替换时拒绝原请求。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(SSCUterusDevelopmentUtility.Find(pawn), owner);
        /// <summary>兼容基类写入签名；公共提交使用下方带失败原因的权威路径。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, enabled, out _);
        /// <summary>提交时重新核对妊娠等变化；原失败原因直接回传，同值幂等、进度和健康通知均不在后端重复实现。</summary>
        public override bool TrySetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled, out string reason)
            => SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, enabled, out reason);
    }
}
