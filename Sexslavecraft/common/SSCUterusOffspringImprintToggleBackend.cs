// AI 辅助生成：后代初始印记选择沿子宫身体Hediff保存，不保存到人格能力字典。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>适配原布尔能力框架；保存选择、当前准入与孕中编辑锁使用各自查询。</summary>
    internal sealed class SSCUterusOffspringImprintToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCUterusOffspringImprintToggleBackend Instance = new SSCUterusOffspringImprintToggleBackend();
        /// <summary>读取原始身体选择；未解锁、生育关闭或失格不覆盖保存值。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCUterusDevelopmentUtility.Find(pawn)?.OffspringImprintEnabled ?? false;
        /// <summary>只描述当前通用资格，不使用未孕编辑资格撤销已采用方案。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCFertilityPreconceptionUtility.ImprintActivationFailure(pawn);
        /// <summary>开关两个方向都交给原孕前设置服务检查。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCFertilityPreconceptionUtility.ImprintChangeFailure(pawn);
        /// <summary>捕获唯一保存源，不创建身体记录。</summary>
        public override object CaptureOwner(Pawn pawn) => SSCUterusDevelopmentUtility.Find(pawn);
        /// <summary>只接受同一有效子宫实例，不以同名定义放宽。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(SSCUterusDevelopmentUtility.Find(pawn), owner);
        /// <summary>兼容框架写入签名，实际提交仍复查当前准入。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => TrySetEnabled(pawn, definition, enabled, out _);
        /// <summary>在可能执行扩展规则前保留原owner，最终setter拒绝替代实例。</summary>
        public override bool TrySetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled, out string reason)
        {
            var owner = SSCUterusDevelopmentUtility.Find(pawn);
            return SSCFertilityPreconceptionUtility.TrySetImprint(pawn, owner, enabled, out reason);
        }
    }
}
